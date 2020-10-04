﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Squared.PRGUI.Decorations;
using Squared.PRGUI.Layout;
using Squared.Render.Convenience;
using Squared.Util;

namespace Squared.PRGUI.Controls {
    public class Menu : Container {
        public const float MenuShowSpeed = 0.1f;
        public const float MenuHideSpeed = 0.25f;

        private Control _SelectedItem;

        public Control SelectedItem {
            get {
                return _SelectedItem;
            }
            set {
                if (_SelectedItem == value)
                    return;
                OnSelectionChange(_SelectedItem, value);
                _SelectedItem = value;
            }
        }

        public Vector2 Position {
            get {
                return new Vector2(Margins.Left, Margins.Top);
            }
            set {
                Margins.Left = value.X;
                Margins.Top = value.Y;
            }
        }

        private bool IsActive = false;

        public Menu ()
            : base () {
            AcceptsMouseInput = true;
            AcceptsFocus = true;
            ContainerFlags = ControlFlags.Container_Row | ControlFlags.Container_Wrap | ControlFlags.Container_Align_Start 
                | ControlFlags.Container_Constrain_Size;
            LayoutFlags |= ControlFlags.Layout_Floating;
            PaintOrder = 9900;
        }

        protected override IDecorator GetDefaultDecorations (IDecorationProvider provider) {
            return provider?.Menu;
        }

        protected override ControlKey OnGenerateLayoutTree (UIOperationContext context, ControlKey parent, ControlKey? existingKey) {
            var result = base.OnGenerateLayoutTree(context, parent, existingKey);
            foreach (var child in Children) {
                var lk = child.LayoutKey;
                var cf = context.Layout.GetLayoutFlags(lk);
                context.Layout.SetLayoutFlags(lk, cf | ControlFlags.Layout_ForceBreak);
                var m = context.Layout.GetMargins(lk);
                m.Top = m.Bottom = 0;
                context.Layout.SetMargins(lk, m);
            }
            return result;
        }

        protected override bool OnHitTest (LayoutContext context, RectF box, Vector2 position, bool acceptsMouseInputOnly, bool acceptsFocusOnly, ref Control result) {
            var ok = base.OnHitTest(context, box, position, acceptsMouseInputOnly, acceptsFocusOnly, ref result);
            // HACK: Ensure that hit-test does not pass through to our individual items. We want to handle all events for them
            if (ok)
                result = this;
            return ok;
        }

        private void OnSelectionChange (Control previous, Control newControl) {
            foreach (var child in Children)
                child.CustomTextDecorations = (child == newControl)
                    ? Context.Decorations.Selection 
                    : null;

            FireEvent(UIEvents.SelectionChanged, newControl);
        }

        private Control ChildFromGlobalPosition (LayoutContext context, Vector2 globalPosition) {
            foreach (var child in Children)
                if (child.HitTest(context, globalPosition, false, false) == child)
                    return child;

            return null;
        }

        private bool OnMouseEvent (string name, MouseEventArgs args) {
            // Console.WriteLine($"menu.{name}");

            if (name == UIEvents.MouseDown) {
                if (HitTest(Context.Layout, args.GlobalPosition, false, false) != this) {
                    Context.ReleaseCapture(this);
                    Close();
                    return true;
                }
            }

            var virtualGlobalPosition = args.GlobalPosition + ScrollOffset;
            if (args.Box.Contains(virtualGlobalPosition))
                // HACK: Shift the coordinates in a little to deal with items that are not full width
                virtualGlobalPosition.X = args.ContentBox.Left + 4;
            var item = ChildFromGlobalPosition(Context.Layout, virtualGlobalPosition);

            if ((Context.MouseOver != this) && (Context.MouseCaptured != this)) {
                SelectedItem = null;
            } else {
                if (item != null)
                    SelectedItem = item;
            }

            if (name == UIEvents.MouseUp) {
                // This indicates that the mouse is in our padding zone
                if (!args.ContentBox.Contains(virtualGlobalPosition))
                    ;
                else if (item != null)
                    ItemChosen(item);
                else
                    Close();
            }

            return true;
        }

        protected override bool OnEvent<T> (string name, T args) {
            if (!IsActive)
                return false;

            if (name == UIEvents.MouseLeave)
                SelectedItem = null;
            else if (args is MouseEventArgs)
                return OnMouseEvent(name, (MouseEventArgs)(object)args);
            else if (name == UIEvents.LostFocus)
                Close();
            /*
            else if (name == UIEvents.KeyPress)
                return OnKeyPress((KeyEventArgs)(object)args);
            */
            return false;
        }

        protected override void OnRasterize (UIOperationContext context, ref ImperativeRenderer renderer, DecorationSettings settings, IDecorator decorations) {
            if ((SelectedItem != null) && (context.Pass == RasterizePasses.Below)) {
                var selectionBox = SelectedItem.GetRect(context.Layout, true, false);
                selectionBox.Left = settings.ContentBox.Left;
                selectionBox.Width = settings.ContentBox.Width;

                // HACK
                context.Pass = RasterizePasses.Content;
                var selectionSettings = new DecorationSettings {
                    Box = selectionBox,
                    ContentBox = selectionBox,
                    State = ControlStates.Hovering | ControlStates.Focused
                };
                context.DecorationProvider.Selection?.Rasterize(context, ref renderer, selectionSettings);
                context.Pass = RasterizePasses.Below;
            }

            base.OnRasterize(context, ref renderer, settings, decorations);
        }

        private void ItemChosen (Control item) {
            Context.FireEvent<int>(UIEvents.Click, item, 1);
            Close();
        }

        private void ShowInternalPrologue (UIContext context) {
            if (!context.Controls.Contains(this))
                context.Controls.Add(this);

            MaximumWidth = context.CanvasSize.X * 0.5f;
            MaximumHeight = context.CanvasSize.Y * 0.66f;
        }

        private void ShowInternal (UIContext context, Vector2 adjustedPosition) {
            SelectedItem = null;
            Position = adjustedPosition;
            Visible = true;
            Intangible = false;
            if (!IsActive)
                Opacity = Tween<float>.StartNow(0, 1, MenuShowSpeed, now: NowL);
            context.CaptureMouse(this);
            IsActive = true;
            Context.FireEvent(UIEvents.Shown, this);
        }

        private Vector2 AdjustPosition (UIContext context, Vector2 desiredPosition) {
            var margin = context.Decorations.Menu.Margins;
            desiredPosition.X -= margin.Left;
            desiredPosition.Y -= margin.Top;
            context.UpdateSubtreeLayout(this);
            var box = GetRect(context.Layout);
            desiredPosition.X = Arithmetic.Clamp(desiredPosition.X, 0, context.CanvasSize.X - box.Width - margin.Right);
            desiredPosition.Y = Arithmetic.Clamp(desiredPosition.Y, 0, context.CanvasSize.Y - box.Height - margin.Bottom);
            return desiredPosition;
        }

        public void Show (UIContext context, Vector2? position = null) {
            ShowInternalPrologue(context);

            // Align the top-left corner of the menu with the target position (compensating for margin),
            //  then shift the menu around if necessary to keep it on screen
            var adjustedPosition = AdjustPosition(context, (position ?? context.LastMousePosition));

            ShowInternal(context, adjustedPosition);
        }

        public void Show (UIContext context, Control anchor) {
            ShowInternalPrologue(context);

            // Align the top-left corner of the menu with the target position (compensating for margin),
            //  then shift the menu around if necessary to keep it on screen
            var anchorBox = anchor.GetRect(context.Layout);
            var adjustedPosition = AdjustPosition(
                context, new Vector2(anchorBox.Left, anchorBox.Top + anchorBox.Height)
            );

            ShowInternal(context, adjustedPosition);
        }

        public void Close () {
            if (!IsActive)
                return;
            IsActive = false;
            Intangible = true;
            Context.ReleaseCapture(this);
            var now = NowL;
            Opacity = Tween<float>.StartNow(Opacity.Get(now), 0, MenuHideSpeed, now: now);
            Context.FireEvent(UIEvents.Closed, this);
        }
    }
}