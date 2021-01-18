﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Squared.Game;
using Squared.PRGUI.Decorations;
using Squared.PRGUI.Imperative;
using Squared.PRGUI.Layout;
using Squared.Render.Convenience;
using Squared.Util;

namespace Squared.PRGUI.Controls {
    public abstract class ContainerBase : Control, IControlContainer {
        protected ControlCollection _Children;
        public ControlCollection Children {
            get {
                if (DynamicContentIsInvalid)
                    GenerateDynamicContent(false || DynamicContentIsInvalid);
                return _Children;
            }
        }

        protected Vector2 AbsoluteDisplayOffsetOfChildren;

        /// <summary>
        /// If set, children will only be rendered within the volume of this container
        /// </summary>
        protected bool ClipChildren { get; set; } = false;

        bool IControlContainer.ClipChildren {
            get => ClipChildren;
            set => ClipChildren = value;
        }

        public ControlFlags ContainerFlags { get; set; } =
            ControlFlags.Container_Align_Start | ControlFlags.Container_Row | 
            ControlFlags.Container_Wrap;

        protected ContainerBuilder DynamicBuilder;
        protected ContainerContentsDelegate _DynamicContents;
        /// <summary>
        /// If set, every update this delegate will be invoked to reconstruct the container's children
        /// </summary>        
        public ContainerContentsDelegate DynamicContents {
            get => _DynamicContents;
            set {
                if (_DynamicContents == value)
                    return;
                _DynamicContents = value;
                DynamicContentIsInvalid = true;
            }
        }
        /// <summary>
        /// If true, dynamic contents will only be updated when this container is invalidated
        /// </summary>
        public bool CacheDynamicContent;

        protected bool DynamicContentIsInvalid = true;
        protected bool FreezeDynamicContent = false;
        protected bool SuppressChildLayout = false;

        public ContainerBase () 
            : base () {
            _Children = new ControlCollection(this);
        }

        public void InvalidateDynamicContent () {
            DynamicContentIsInvalid = true;
        }

        internal override void InvalidateLayout () {
            base.InvalidateLayout();
            foreach (var ch in _Children)
                ch.InvalidateLayout();
        }

        private bool IsGeneratingDynamicContent = false;

        internal void EnsureDynamicBuilderInitialized (out ContainerBuilder result) {
            if (
                (DynamicContents == null) && 
                (DynamicBuilder.Container == this)
            ) {
                DynamicBuilder.PreviousRemovedControls.EnsureList();
                DynamicBuilder.CurrentRemovedControls.EnsureList();
                result = DynamicBuilder;
            } else {
                result = DynamicBuilder = new ContainerBuilder(this);
            }
        }

        protected void GenerateDynamicContent (bool force) {
            DynamicContentIsInvalid = false;

            if (DynamicContents == null)
                return;

            if ((FreezeDynamicContent || CacheDynamicContent) && !force)
                return;

            if (IsGeneratingDynamicContent)
                return;

            IsGeneratingDynamicContent = true;
            try {
                if (DynamicBuilder.Container != this)
                    DynamicBuilder = new ContainerBuilder(this);
                DynamicBuilder.Reset();
                DynamicContents(ref DynamicBuilder);
                DynamicBuilder.Finish();
                // FIXME: Is this right?
                DynamicContentIsInvalid = false;
            } finally {
                IsGeneratingDynamicContent = false;
            }
        }
        
        protected override ControlKey OnGenerateLayoutTree (UIOperationContext context, ControlKey parent, ControlKey? existingKey) {
            var result = base.OnGenerateLayoutTree(context, parent, existingKey);
            if (result.IsInvalid) {
                foreach (var item in _Children)
                    item.InvalidateLayout();

                return result;
            }

            context.Layout.SetContainerFlags(result, ContainerFlags);

            if (SuppressChildLayout) {
                // FIXME: We need to also lock our minimum width in this case
                // HACK
                foreach (var item in _Children)
                    item.InvalidateLayout();

                return result;
            } else {
                GenerateDynamicContent(false || DynamicContentIsInvalid);

                foreach (var item in _Children) {
                    item.AbsoluteDisplayOffset = AbsoluteDisplayOffsetOfChildren;

                    // If we're performing layout again on an existing layout item, attempt to do the same
                    //  for our children
                    var childExistingKey = (ControlKey?)null;
                    if ((existingKey.HasValue) && !item.LayoutKey.IsInvalid)
                        childExistingKey = item.LayoutKey;

                    item.GenerateLayoutTree(ref context, result, childExistingKey);
                }
                return result;
            }
        }

        protected override bool ShouldClipContent => ClipChildren && (_Children.Count > 0);
        // FIXME: Always true?
        protected override bool HasChildren => (Children.Count > 0);

        protected virtual bool HideChildren => false;

        protected override void OnRasterizeChildren (UIOperationContext context, ref RasterizePassSet passSet, DecorationSettings settings) {
            if (HideChildren)
                return;

            // FIXME
            int layer1 = passSet.Below.Layer,
                layer2 = passSet.Content.Layer,
                layer3 = passSet.Above.Layer,
                maxLayer1 = layer1,
                maxLayer2 = layer2,
                maxLayer3 = layer3;

            RasterizeChildrenInOrder(
                ref context, ref passSet, 
                layer1, layer2, layer3,
                ref maxLayer1, ref maxLayer2, ref maxLayer3
            );

            passSet.Below.Layer = maxLayer1;
            passSet.Content.Layer = maxLayer2;
            passSet.Above.Layer = maxLayer3;
        }

        protected virtual void RasterizeChildrenInOrder (
            ref UIOperationContext context, ref RasterizePassSet passSet, 
            int layer1, int layer2, int layer3, 
            ref int maxLayer1, ref int maxLayer2, ref int maxLayer3
        ) {
            var sequence = Children.InDisplayOrder(Context.FrameIndex);
            foreach (var item in sequence)
                RasterizeChild(ref context, item, ref passSet, layer1, layer2, layer3, ref maxLayer1, ref maxLayer2, ref maxLayer3);
        }

        /// <summary>
        /// Intelligently rasterize children starting from an automatically selected
        ///  midpoint, instead of rasterizing all of our children.
        /// May draw an unnecessarily large number of children in some cases, but will
        ///  typically only draw slightly more than the number of children currently
        ///  in view.
        /// </summary>
        /// <returns>The number of child controls rasterization was attempted for</returns>
        public static int RasterizeChildrenFromCenter (
            ref UIOperationContext context, ref RasterizePassSet passSet, 
            RectF box, ControlCollection children, Control selectedItem,
            int layer1, int layer2, int layer3, 
            ref int maxLayer1, ref int maxLayer2, ref int maxLayer3,
            ref int lastOffset1, ref int lastOffset2
        ) {
            if (children.Count <= 0)
                return 0;

            RectF childRect =
                (selectedItem != null)
                    ? selectedItem.GetRect()
                    : default(RectF);

            int count = children.Count, 
                selectedIndex = children.IndexOf(selectedItem), 
                startOffset = (
                    (selectedIndex >= 0) &&
                    box.Intersects(ref childRect)
                )
                    // If we have a selected item and the selected item is visible, begin painting
                    //  from its position
                    ? selectedIndex
                    : (
                        (
                            (lastOffset1 >= 0) &&
                            (lastOffset2 >= 0) &&
                            (lastOffset2 < count)
                        )
                            // Otherwise, start painting from the midpoint of the last paint region
                            ? (lastOffset1 + lastOffset2) / 2
                            // And if we don't have a last paint region, start from our midpoint
                            : count / 2
                    );
            bool hasRenderedAny = false;

            int itemsAttempted = 0;
            for (int i = startOffset, j = startOffset; (i >= 0) || (j < count); i--, j++) {
                if (i >= 0) {
                    itemsAttempted++;
                    // Stop searching upward once an item fails to render
                    var item1 = children[i];
                    var ok = RasterizeChild(
                        ref context, item1, ref passSet,
                        layer1, layer2, layer3,
                        ref maxLayer1, ref maxLayer2, ref maxLayer3
                    );
                    if (item1.IsTransparent) {
                        ;
                    } else if (!ok && hasRenderedAny) {
                        lastOffset1 = i;
                        i = -1;
                    } else if (ok) {
                        hasRenderedAny = true;
                    }
                }

                if (j < count) {
                    itemsAttempted++;
                    var item2 = children[j];
                    var ok = RasterizeChild(
                        ref context, item2, ref passSet,
                        layer1, layer2, layer3,
                        ref maxLayer1, ref maxLayer2, ref maxLayer3
                    );
                    if (item2.IsTransparent) {
                        ;
                    } else if (!ok && hasRenderedAny) {
                        lastOffset2 = j;
                        j = count;
                    } else if (ok) {
                        hasRenderedAny = true;
                    }
                }
            }

            return itemsAttempted;
        }

        /// <summary>
        /// Rasterizes a child control and updates the pass layer data
        /// </summary>
        /// <returns>Whether the child was successfully rasterized</returns>
        public static bool RasterizeChild (
            ref UIOperationContext context, Control item, ref RasterizePassSet passSet, 
            int layer1, int layer2, int layer3, ref int maxLayer1, 
            ref int maxLayer2, ref int maxLayer3
        ) {
            passSet.Below.Layer = layer1;
            passSet.Content.Layer = layer2;
            passSet.Above.Layer = layer3;

            var result = item.Rasterize(ref context, ref passSet);

            maxLayer1 = Math.Max(maxLayer1, passSet.Below.Layer);
            maxLayer2 = Math.Max(maxLayer2, passSet.Content.Layer);
            maxLayer3 = Math.Max(maxLayer3, passSet.Above.Layer);

            return result;
        }

        protected override void OnVisibilityChange (bool newValue) {
            base.OnVisibilityChange(newValue);

            DynamicContentIsInvalid = true;
            if (newValue)
                return;

            ReleaseChildFocus();
        }

        protected void ReleaseChildFocus () {
            if (IsEqualOrAncestor(Context?.Focused, this))
                Context.NotifyControlBecomingInvalidFocusTarget(Context.Focused, false);
        }

        protected bool HitTestShell (RectF box, Vector2 position, bool acceptsMouseInputOnly, bool acceptsFocusOnly, ref Control result) {
            return base.OnHitTest(box, position, acceptsMouseInputOnly, acceptsFocusOnly, ref result);
        }

        protected bool HitTestInterior (RectF box, Vector2 position, bool acceptsMouseInputOnly, bool acceptsFocusOnly, ref Control result) {
            var ipic = this as IPartiallyIntangibleControl;
            return (AcceptsMouseInput && (ipic?.IsIntangibleAtPosition(position) != true)) || 
                !acceptsMouseInputOnly;
        }

        protected bool HitTestChildren (Vector2 position, bool acceptsMouseInputOnly, bool acceptsFocusOnly, ref Control result) {
            // FIXME: Should we only perform the hit test if the position is within our boundaries?
            // This doesn't produce the right outcome when a container's computed size is zero
            var sorted = Children.InDisplayOrder(Context.FrameIndex);
            for (int i = sorted.Count - 1; i >= 0; i--) {
                var item = sorted[i];
                var newResult = item.HitTest(position, acceptsMouseInputOnly, acceptsFocusOnly);
                if (newResult != null) {
                    result = newResult;
                    return true;
                }
            }

            return false;
        }

        protected override bool OnHitTest (RectF box, Vector2 position, bool acceptsMouseInputOnly, bool acceptsFocusOnly, ref Control result) {
            if (!HitTestShell(box, position, false, false, ref result))
                return false;

            if (!HitTestInterior(box, position, acceptsMouseInputOnly, acceptsFocusOnly, ref result))
                return false;

            return HitTestChildren(position, acceptsMouseInputOnly, acceptsFocusOnly, ref result);
        }

        public T Child<T> (Func<T, bool> predicate)
            where T : Control {

            foreach (var child in Children) {
                if (!(child is T))
                    continue;
                var t = (T)child;
                if (predicate(t))
                    return t;
            }

            return null;
        }

        protected virtual void OnDescendantReceivedFocus (Control control, bool isUserInitiated) {
        }

        void IControlContainer.DescendantReceivedFocus (Control descendant, bool isUserInitiated) {
            OnDescendantReceivedFocus(descendant, isUserInitiated);
        }
    }
}