﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Squared.Game;
using Squared.PRGUI.Decorations;
using Squared.PRGUI.Layout;
using Squared.Render;
using Squared.Render.Convenience;
using Squared.Render.Text;
using Squared.Util;
using Squared.Util.Text;

namespace Squared.PRGUI {
    public abstract class Control {
        public IDecorator CustomDecorations;
        public Margins Margins, Padding;
        public ControlFlags LayoutFlags = ControlFlags.Layout_Fill_Row;
        public float? FixedWidth, FixedHeight;
        public float? MinimumWidth, MinimumHeight;
        public float? MaximumWidth, MaximumHeight;
        public Color? BackgroundColor;

        // Accumulates scroll offset(s) from parent controls
        private Vector2 _AbsoluteDisplayOffset;

        public ControlStates State;

        internal ControlKey LayoutKey;

        public bool Visible { get; set; } = true;
        public bool Enabled { get; set; } = true;
        public bool AcceptsCapture { get; protected set; }
        public bool AcceptsFocus { get; protected set; }
        public bool AcceptsTextInput { get; protected set; }
        protected virtual bool HasNestedContent => false;
        protected virtual bool ShouldClipContent => false;

        protected WeakReference<Control> WeakParent = null;

        public Vector2 AbsoluteDisplayOffset {
            get {
                return _AbsoluteDisplayOffset;
            }
            set {
                if (value == _AbsoluteDisplayOffset)
                    return;
                _AbsoluteDisplayOffset = value;
                OnDisplayOffsetChanged();
            }
        }

        protected virtual void OnDisplayOffsetChanged () {
        }

        internal bool HandleEvent (string name) {
            return OnEvent(name);
        }

        internal bool HandleEvent<T> (string name, T args) {
            return OnEvent(name, args);
        }

        protected virtual bool OnEvent (string name) {
            return false;
        }

        protected virtual bool OnEvent<T> (string name, T args) {
            return false;
        }

        public void GenerateLayoutTree (UIOperationContext context, ControlKey parent) {
            LayoutKey = OnGenerateLayoutTree(context, parent);
        }

        protected Vector2 GetFixedInteriorSpace () {
            return new Vector2(
                FixedWidth.HasValue
                    ? Math.Max(0, FixedWidth.Value - Margins.X)
                    : LayoutItem.NoValue,
                FixedHeight.HasValue
                    ? Math.Max(0, FixedHeight.Value - Margins.Y)
                    : LayoutItem.NoValue
            );
        }

        protected virtual bool OnHitTest (LayoutContext context, RectF box, Vector2 position, bool acceptsCaptureOnly, bool acceptsFocusOnly, ref Control result) {
            if (!AcceptsCapture && acceptsCaptureOnly)
                return false;
            if (!AcceptsFocus && acceptsFocusOnly)
                return false;
            if ((acceptsFocusOnly || acceptsCaptureOnly) && !Enabled)
                return false;

            if (box.Contains(position)) {
                result = this;
                return true;
            }

            return false;
        }

        public RectF GetRect (LayoutContext context, bool includeOffset = true, bool contentRect = false) {
            var result = contentRect 
                ? context.GetContentRect(LayoutKey) 
                : context.GetRect(LayoutKey);
            result.Left += _AbsoluteDisplayOffset.X;
            result.Top += _AbsoluteDisplayOffset.Y;

            // HACK
            if (FixedWidth.HasValue)
                result.Width = FixedWidth.Value;
            if (FixedHeight.HasValue)
                result.Height = FixedHeight.Value;

            if (MinimumWidth.HasValue)
                result.Width = Math.Max(MinimumWidth.Value, result.Width);
            if (MinimumHeight.HasValue)
                result.Height = Math.Max(MinimumHeight.Value, result.Height);
            
            return result;
        }

        public Control HitTest (LayoutContext context, Vector2 position, bool acceptsCaptureOnly, bool acceptsFocusOnly) {
            if (!Visible)
                return null;

            var result = this;
            var box = GetRect(context);
            if (OnHitTest(context, box, position, acceptsCaptureOnly, acceptsFocusOnly, ref result))
                return result;

            return null;
        }

        protected virtual Margins ComputeMargins (UIOperationContext context, IDecorator decorations) {
            var result = Margins;
            if (decorations != null)
                result += decorations.Margins;
            return result;
        }

        protected virtual Margins ComputePadding (UIOperationContext context, IDecorator decorations) {
            var result = Padding;
            if (decorations != null)
                result += decorations.Padding;
            return result;
        }

        protected virtual void ComputeFixedSize (out float? fixedWidth, out float? fixedHeight) {
            fixedWidth = FixedWidth;
            fixedHeight = FixedHeight;
        }

        protected virtual void ComputeSizeConstraints (
            out float? minimumWidth, out float? minimumHeight,
            out float? maximumWidth, out float? maximumHeight
        ) {
            minimumWidth = MinimumWidth;
            minimumHeight = MinimumHeight;
            maximumWidth = MaximumWidth;
            maximumHeight = MaximumHeight;
        }

        protected virtual ControlKey OnGenerateLayoutTree (UIOperationContext context, ControlKey parent) {
            var result = context.Layout.CreateItem();

            var decorations = GetDecorations(context);
            var computedMargins = ComputeMargins(context, decorations);
            var computedPadding = ComputePadding(context, decorations);

            ComputeFixedSize(out float? fixedWidth, out float? fixedHeight);
            var actualLayoutFlags = ComputeLayoutFlags(fixedWidth.HasValue, fixedHeight.HasValue);

            context.Layout.SetLayoutFlags(result, actualLayoutFlags);
            context.Layout.SetMargins(result, computedMargins);
            context.Layout.SetPadding(result, computedPadding);
            context.Layout.SetFixedSize(result, fixedWidth ?? LayoutItem.NoValue, fixedHeight ?? LayoutItem.NoValue);

            ComputeSizeConstraints(
                out float? minimumWidth, out float? minimumHeight,
                out float? maximumWidth, out float? maximumHeight
            );
            context.Layout.SetSizeConstraints(
                result, 
                minimumWidth, minimumHeight, 
                maximumWidth, maximumHeight
            );

            if (!parent.IsInvalid)
                context.Layout.InsertAtEnd(parent, result);

            return result;
        }

        protected ControlFlags ComputeLayoutFlags (bool hasFixedWidth, bool hasFixedHeight) {
            var result = LayoutFlags;
            // FIXME: If we do this, fixed-size elements extremely are not fixed size
            if (hasFixedWidth && result.IsFlagged(ControlFlags.Layout_Fill_Row))
                result &= ~ControlFlags.Layout_Fill_Row;
            if (hasFixedHeight && result.IsFlagged(ControlFlags.Layout_Fill_Column))
                result &= ~ControlFlags.Layout_Fill_Column;
            return result;
        }

        protected virtual IDecorator GetDefaultDecorations (UIOperationContext context) {
            return null;
        }

        protected IDecorator GetDecorations (UIOperationContext context) {
            return CustomDecorations ?? GetDefaultDecorations(context);
        }

        protected ControlStates GetCurrentState (UIOperationContext context) {
            var result = State;

            if (!Enabled) {
                result |= ControlStates.Disabled;
            } else {
                if (context.UIContext.Hovering == this)
                    result |= ControlStates.Hovering;
                if (context.UIContext.Focused == this)
                    result |= ControlStates.Focused;
            }

            if (context.UIContext.MouseCaptured == this)
                result |= ControlStates.Pressed;

            return result;
        }

        protected virtual void OnRasterize (UIOperationContext context, DecorationSettings settings, IDecorator decorations) {
            decorations?.Rasterize(context, settings);
        }

        protected virtual void ApplyClipMargins (UIOperationContext context, ref RectF box) {
        }

        protected virtual DecorationSettings MakeDecorationSettings (ref RectF box, ref RectF contentBox, ControlStates state) {
            return new DecorationSettings {
                Box = box,
                ContentBox = contentBox,
                State = state,
                BackgroundColor = BackgroundColor
            };
        }

        public void Rasterize (UIOperationContext context) {
            if (!Visible)
                return;

            var box = GetRect(context.Layout);
            var contentBox = GetRect(context.Layout, contentRect: true);
            var decorations = GetDecorations(context);
            var state = GetCurrentState(context);

            var contentContext = context;
            var hasNestedContext = (context.Pass == RasterizePasses.Content) && (ShouldClipContent || HasNestedContent);

            // For clipping we need to create a separate batch group that contains all the rasterization work
            //  for our children. At the start of it we'll generate the stencil mask that will be used for our
            //  rendering operation(s).
            if (hasNestedContext) {
                context.Renderer.Layer += 1;
                contentContext = context.Clone();
                contentContext.Renderer = context.Renderer.MakeSubgroup();
                contentContext.Renderer.Layer = 0;

                if (ShouldClipContent)
                    contentContext.Renderer.DepthStencilState = RenderStates.StencilTest;
            }

            var settings = MakeDecorationSettings(ref box, ref contentBox, state);
            OnRasterize(contentContext, settings, decorations);

            if (hasNestedContext) {
                // GROSS OPTIMIZATION HACK: Detect that any rendering operation(s) occurred inside the
                //  group and if so, set up the stencil mask so that they will be clipped.
                if (ShouldClipContent && !contentContext.Renderer.Container.IsEmpty) {
                    contentContext.Renderer.DepthStencilState = RenderStates.StencilWrite;

                    // FIXME: Because we're doing Write here and clearing first, nested clips won't work right.
                    // The solution is probably a combination of test-and-increment when entering the clip,
                    //  and then a test-and-decrement when exiting to restore the previous clip region.
                    contentContext.Renderer.Clear(stencil: 0, layer: -9999);

                    // FIXME: Separate context?
                    contentContext.Pass = RasterizePasses.ContentClip;

                    ApplyClipMargins(context, ref box);

                    contentContext.Renderer.Layer = -999;
                    settings.State = default(ControlStates);
                    decorations.Rasterize(contentContext, settings);
                }

                context.Renderer.Layer += 1;
            }
        }

        public bool TryGetParent (out Control parent) {
            if (WeakParent == null) {
                parent = null;
                return false;
            }

            return WeakParent.TryGetTarget(out parent);
        }

        internal void SetParent (Control parent) {
            Control actualParent;
            if ((WeakParent != null) && WeakParent.TryGetTarget(out actualParent)) {
                if (actualParent != parent)
                    throw new Exception("This control already has a parent");
                else
                    return;
            }

            WeakParent = new WeakReference<Control>(parent, false);
        }

        internal void UnsetParent (Control oldParent) {
            if (WeakParent == null)
                return;

            Control actualParent;
            if (!WeakParent.TryGetTarget(out actualParent))
                return;

            if (actualParent != oldParent)
                throw new Exception("Parent mismatch");

            WeakParent = null;
        }

        public override string ToString () {
            return $"{GetType().Name} #{GetHashCode():X8}";
        }
    }

    public class StaticText : Control {
        public const bool DiagnosticText = false;

        public Color? TextColor = null;
        public Material TextMaterial = null;
        public DynamicStringLayout Content = new DynamicStringLayout();
        public bool AutoSizeWidth = true, AutoSizeHeight = true;

        private float? AutoSizeComputedWidth, AutoSizeComputedHeight;

        public StaticText ()
            : base () {
            Content.LineLimit = 1;
        }

        public bool Multiline {
            get {
                return Content.LineLimit > 1;
            }
            set {
                Content.LineLimit = value ? int.MaxValue : 1;
            }
        }

        public bool AutoSize {
            set {
                AutoSizeWidth = AutoSizeHeight = value;
            }
        }

        public HorizontalAlignment TextAlignment {
            get {
                return Content.Alignment;
            }
            set {
                Content.Alignment = value;
            }
        }

        public AbstractString Text {
            get {
                return Content.Text;
            }
            set {
                Content.Text = value;
            }
        }

        protected override void ComputeFixedSize (out float? fixedWidth, out float? fixedHeight) {
            base.ComputeFixedSize(out fixedWidth, out fixedHeight);
            if (AutoSizeWidth && !FixedWidth.HasValue)
                fixedWidth = AutoSizeComputedWidth ?? fixedWidth;
            if (AutoSizeHeight && !FixedHeight.HasValue)
                fixedHeight = AutoSizeComputedHeight ?? fixedHeight;
        }

        private void ComputeAutoSize (UIOperationContext context) {
            AutoSizeComputedHeight = AutoSizeComputedWidth = null;
            if (!AutoSizeWidth && !AutoSizeHeight)
                return;

            var interiorSpace = GetFixedInteriorSpace();
            if (interiorSpace.X > 0)
                Content.LineBreakAtX = interiorSpace.X;
            else
                Content.LineBreakAtX = null;

            var decorations = GetDecorations(context);
            UpdateFont(context, decorations);

            var computedPadding = ComputePadding(context, decorations);
            var layoutSize = Content.Get().Size;
            var computedSize = layoutSize + computedPadding.Size;

            if (AutoSizeWidth)
                AutoSizeComputedWidth = computedSize.X;
            if (AutoSizeHeight)
                AutoSizeComputedHeight = computedSize.Y;
        }

        protected override ControlKey OnGenerateLayoutTree (UIOperationContext context, ControlKey parent) {
            ComputeAutoSize(context);
            var result = base.OnGenerateLayoutTree(context, parent);
            return result;
        }

        protected override IDecorator GetDefaultDecorations (UIOperationContext context) {
            return context.DecorationProvider?.StaticText;
        }

        protected override void OnRasterize (UIOperationContext context, DecorationSettings settings, IDecorator decorations) {
            base.OnRasterize(context, settings, decorations);

            if (context.Pass != RasterizePasses.Content)
                return;

            Content.LineBreakAtX = settings.Box.Width;

            Color? overrideColor = TextColor;
            Material material;
            GetTextSettings(context, decorations, settings.State, out material, ref overrideColor);

            settings.Box.SnapAndInset(out Vector2 a, out Vector2 b);

            var computedPadding = ComputePadding(context, decorations);
            var textOffset = a + new Vector2(computedPadding.Left, computedPadding.Top);
            if (settings.State.HasFlag(ControlStates.Pressed))
                textOffset += decorations.PressedInset;

            var layout = Content.Get();
            var xSpace = (b.X - a.X) - layout.Size.X - computedPadding.X;
            switch (Content.Alignment) {
                case HorizontalAlignment.Left:
                    break;
                case HorizontalAlignment.Center:
                    textOffset.X += (xSpace / 2f);
                    break;
                case HorizontalAlignment.Right:
                    textOffset.X += xSpace;
                    break;
            }

            context.Renderer.DrawMultiple(
                layout.DrawCalls, offset: textOffset.Floor(),
                material: material, samplerState: RenderStates.Text, multiplyColor: overrideColor
            );
        }

        protected void UpdateFont (UIOperationContext context, IDecorator decorations) {
            Color? temp2 = null;
            GetTextSettings(context, decorations, default(ControlStates), out Material temp, ref temp2);
        }

        protected void GetTextSettings (UIOperationContext context, IDecorator decorations, ControlStates state, out Material material, ref Color? color) {
            decorations.GetTextSettings(context, state, out material, out IGlyphSource font, ref color);
            if (Content.GlyphSource == null)
                Content.GlyphSource = font;
            if (TextMaterial != null)
                material = TextMaterial;
        }

        private string GetTrimmedText () {
            var s = Text.ToString() ?? "";
            if (s.Length > 16)
                return s.Substring(0, 16) + "...";
            else
                return s;
        }

        public override string ToString () {
            return $"{GetType().Name} #{GetHashCode():X8} '{GetTrimmedText()}'";
        }
    }

    public class EditableText : Control {
        // FIXME
        private bool Multiline = false;

        private Pair<int> _Selection;

        public Pair<int> Selection {
            get => _Selection;
            set {
                if (_Selection == value)
                    return;
                value.First = Arithmetic.Clamp(value.First, 0, Builder.Length);
                value.Second = Arithmetic.Clamp(value.Second, value.First, Builder.Length);

                if (value.First == value.Second) {
                    if ((value.First < Builder.Length) && char.IsLowSurrogate(Builder[value.First])) {
                        value.First--;
                        // FIXME: Bump this forward?
                        value.Second--;
                    }
                } else {
                    // Expand selection outward if it rests in the middle of a surrogate pair
                    if ((value.First > 0) && char.IsLowSurrogate(Builder[value.First]))
                        value.First--;
                    /*
                    if ((value.Second < (Builder.Length - 1)) && char.IsHighSurrogate(Builder[value.Second]))
                        value.Second++;
                    */
                }

                if (_Selection == value)
                    return;
                _Selection = value;
                Console.WriteLine("New selection is {0}", value);
                Invalidate();
            }
        }

        protected DynamicStringLayout DynamicLayout = new DynamicStringLayout();
        protected StringBuilder Builder = new StringBuilder();
        protected Margins CachedPadding;

        private Vector2? ClickStartPosition = null;

        protected override bool ShouldClipContent => true;

        public EditableText ()
            : base () {
            DynamicLayout.Text = Builder;
            AcceptsCapture = true;
            AcceptsFocus = true;
            AcceptsTextInput = true;
        }

        public string Text {
            get {
                return Builder.ToString();
            }
            set {
                // FIXME: Optimize the 'value hasn't changed' case
                Builder.Clear();
                Builder.Append(value);
                Invalidate();
            }
        }

        public void Invalidate () {
            DynamicLayout.ResetMarkersAndHitTests();
            UpdateLayoutSettings();
            DynamicLayout.Invalidate();
        }

        private void UpdateLayoutSettings () {
            DynamicLayout.LineLimit = Multiline ? int.MaxValue : 1;
            MarkSelection();
        }

        protected StringLayout UpdateLayout (
            UIOperationContext context, DecorationSettings settings, IDecorator decorations, out Material material
        ) {
            // HACK: Avoid accumulating too many extra hit tests from previous mouse positions
            // This will invalidate the layout periodically as the mouse moves, but whatever
            if (DynamicLayout.HitTests.Count > 8)
                DynamicLayout.ResetMarkersAndHitTests();

            UpdateLayoutSettings();

            Color? color = null;
            decorations.GetTextSettings(context, settings.State, out material, out IGlyphSource font, ref color);
            CachedPadding = ComputePadding(context, decorations);

            DynamicLayout.GlyphSource = font;
            DynamicLayout.Color = color ?? Color.White;

            return DynamicLayout.Get();
        }

        protected override IDecorator GetDefaultDecorations (UIOperationContext context) {
            return context.DecorationProvider?.EditableText;
        }

        protected override void ComputeSizeConstraints (out float? minimumWidth, out float? minimumHeight, out float? maximumWidth, out float? maximumHeight) {
            base.ComputeSizeConstraints(out minimumWidth, out minimumHeight, out maximumWidth, out maximumHeight);
            if (DynamicLayout.GlyphSource == null)
                return;

            var lineHeight = DynamicLayout.GlyphSource.LineSpacing;
            var contentMinimumHeight = lineHeight * (Multiline ? 2 : 1) + CachedPadding.Y; // FIXME: Include padding
            minimumHeight = Math.Max(minimumHeight ?? 0, contentMinimumHeight);
        }

        protected override ControlKey OnGenerateLayoutTree (UIOperationContext context, ControlKey parent) {
            // HACK: Populate various fields that we will use to compute minimum size
            UpdateLayout(context, new DecorationSettings(), context.DecorationProvider.EditableText, out Material temp);
            return base.OnGenerateLayoutTree(context, parent);
        }

        protected LayoutMarker? MarkSelection () {
            return DynamicLayout.Mark(Selection.First, Math.Max(Selection.Second - 1, Selection.First));
        }

        private LayoutHitTest? ImmediateHitTest (Vector2 position) {
            DynamicLayout.HitTest(position);
            DynamicLayout.Get();
            return DynamicLayout.HitTest(position);
        }

        private int? MapPositionToCharacterIndex (Vector2 position, bool? leanOverride) {
            var result = ImmediateHitTest(position);
            if (position.X < 0)
                return 0;
            else if (position.X > DynamicLayout.Get().Size.X)
                return Builder.Length;

            if (result.HasValue) {
                var rv = result.Value;
                var lean = leanOverride ?? rv.LeaningRight;
                var newIndex =
                    rv.FirstCharacterIndex.HasValue
                        ? (
                            lean
                                ? rv.LastCharacterIndex.Value + 1
                                : rv.FirstCharacterIndex.Value
                        )
                        : Builder.Length;
                return newIndex;
            }

            return null;
        }

        private bool OnMouseEvent (string name, MouseEventArgs args) {
            var position = new Vector2(
                Arithmetic.Clamp(args.LocalPosition.X, 0, args.ContentBox.Width - 1),
                Arithmetic.Clamp(args.LocalPosition.Y, 0, args.ContentBox.Height - 1)
            );

            if (name == UIContext.Events.MouseDown) {
                ClickStartPosition = position;
                var currentCharacter = MapPositionToCharacterIndex(position, null);
                // If we're double-clicking inside the selection don't update it yet
                if (currentCharacter.HasValue && !args.DoubleClicking)
                    Selection = new Pair<int>(currentCharacter.Value, currentCharacter.Value);
                return true;
            } else if (
                (name == UIContext.Events.MouseDrag) ||
                (name == UIContext.Events.MouseUp)
            ) {
                // FIXME: Ideally we would just clamp the mouse coordinates into our rectangle instead of rejecting
                //  coordinates outside our rect. Maybe UIContext should do this?
                if (ClickStartPosition.HasValue) {
                    // If the user is drag-selecting multiple characters, we want to expand the selection
                    //  to cover all the character hitboxes touched by the mouse drag instead of just picking
                    //  the character(s) the positions were leaning towards. For clicks that just place the
                    //  caret on one side of a character, we honor the leaning value
                    var csp = ClickStartPosition.Value;
                    var deltaBigEnough = Math.Abs(position.X - csp.X) >= 4;
                    bool? leanA = deltaBigEnough ? (position.X < csp.X) : (bool?)null,
                        leanB = deltaBigEnough ? (position.X >= csp.X) : (bool?)null;
                    // FIXME: This -1 shouldn't be needed
                    var a = MapPositionToCharacterIndex(csp, leanA) ?? -1;
                    var b = MapPositionToCharacterIndex(position, leanB) ?? -1;
                    
                    Selection = new Pair<int>(Math.Min(a, b), Math.Max(a, b));
                }

                return true;
            } else
                return false;
        }

        protected override bool OnEvent<T> (string name, T args) {
            if (args is MouseEventArgs)
                return OnMouseEvent(name, (MouseEventArgs)(object)args);
            else if (name == UIContext.Events.Click)
                return OnClick(Convert.ToInt32(args));
            else if (name == UIContext.Events.KeyPress)
                return OnKeyPress((KeyEventArgs)(object)args);
            return false;
        }

        protected bool OnKeyPress (KeyEventArgs evt) {
            Console.WriteLine("{0:X4} '{1}' {2}", (int)(evt.Char ?? '\0'), new String(evt.Char ?? '\0', 1), evt.Key);

            if (evt.Char.HasValue) {
                if (Selection.Second != Selection.First)
                    Builder.Remove(Selection.First, Selection.Second - Selection.First);
                Builder.Insert(Selection.First, evt.Char);
                Selection = new Pair<int>(Selection.First + 1, Selection.First + 1);
                Invalidate();
            } else if (evt.Key.HasValue) {
                switch (evt.Key.Value) {
                    case Keys.Back:
                        if (Selection.Second != Selection.First) {
                            Builder.Remove(Selection.First, Selection.Second - Selection.First);
                            Selection = new Pair<int>(Selection.First, Selection.First);
                        } else if (Selection.First > 0) {
                            int pos = Selection.First - 1, count = 1;
                            if (char.IsLowSurrogate(Builder[pos])) {
                                pos--; count++;
                            }
                            Builder.Remove(pos, count);
                            Selection = new Pair<int>(Selection.First - count, Selection.First - count);
                        }
                        Invalidate();
                        break;

                    case Keys.Left:
                    case Keys.Right:
                        HandleSelectionShift(evt.Key == Keys.Left ? -1 : 1);
                        break;

                    case Keys.Home:
                    case Keys.End:
                        HandleSelectionShift(evt.Key == Keys.Home ? -99999 : 99999);
                        break;
                }
            }

            return true;
        }

        private void HandleSelectionShift (int direction) {
            if (direction < 0)
                Selection = new Pair<int>(Selection.First + direction, Selection.First + direction);
            else {
                var newOffset = Selection.Second + direction;
                if ((newOffset < Builder.Length) && char.IsLowSurrogate(Builder[newOffset]))
                    newOffset++;
                Selection = new Pair<int>(newOffset, newOffset);
            }
        }

        protected bool OnClick (int clickCount) {
            // FIXME: Select current word, then entire textbox on triple click
            if (clickCount == 3) {
                Selection = new Pair<int>(0, Builder.Length);
                return true;
            } else if (clickCount == 2) {
                if (!ClickStartPosition.HasValue)
                    return false;

                var centerIndex = MapPositionToCharacterIndex(ClickStartPosition.Value, null);
                if (!centerIndex.HasValue)
                    return false;

                var boundary = Unicode.FindWordBoundary(Builder, centerIndex.Value);
                Selection = boundary;
                return true;
            }

            return false;
        }

        private void ColorizeSelection (
            ArraySegment<BitmapDrawCall> drawCalls, LayoutMarker? selection,
            UIOperationContext context, ControlStates state, IBaseDecorator selectionDecorator
        ) {
            Color? selectedColor = DynamicLayout.Color;
            selectionDecorator.GetTextSettings(context, state, out Material temp, out IGlyphSource temp2, ref selectedColor);
            var noColorizing = (selection == null) || 
                (selection.Value.Bounds == null) || 
                (_Selection.First == _Selection.Second) ||
                (selection.Value.FirstCharacterIndex == selection.Value.LastCharacterIndex);
            for (int i = 0; i < drawCalls.Count; i++) {
                var color = noColorizing || ((i < selection.Value.FirstDrawCallIndex) || (i > selection.Value.LastDrawCallIndex))
                    ? DynamicLayout.Color
                    : Color.Black;
                drawCalls.Array[i + drawCalls.Offset].MultiplyColor = color;
            }
        }

        private Bounds? GetBoundsForSelection (LayoutMarker? selection, Vector2 textOffset) {
            if (selection == null)
                return null;
            else if (selection.Value.Bounds == null)
                return null;

            var sel = selection.Value.Bounds ?? default(Bounds);
            // If there's no text or something else bad happened, synthesize a selection rect
            if (sel.Size.Length() < 1)
                sel.BottomRight = sel.TopLeft + new Vector2(0, DynamicLayout.GlyphSource.LineSpacing);

            var hasRange = _Selection.First != _Selection.Second;

            // FIXME: Multiline
            sel = sel.Translate(textOffset);
            if (!hasRange) {
                if (_Selection.First >= Builder.Length)
                    sel.TopLeft.X = sel.BottomRight.X;
                else
                    sel.BottomRight.X = sel.TopLeft.X;
            }

            return sel;
        }

        protected override void OnRasterize (UIOperationContext context, DecorationSettings settings, IDecorator decorations) {
            base.OnRasterize(context, settings, decorations);

            MarkSelection();

            var textOffset = settings.ContentBox.Position.Floor();
            var selectionDecorator = context.DecorationProvider.Selection;
            var layout = UpdateLayout(context, settings, decorations, out Material textMaterial);
            var selection = MarkSelection();

            if (context.Pass != RasterizePasses.Content)
                return;

            var selBounds = GetBoundsForSelection(selection, textOffset);
            if (selBounds.HasValue && 
                (
                    settings.State.HasFlag(ControlStates.Focused) || 
                    (_Selection.First != _Selection.Second)
                )
            ) {
                var selSettings = new DecorationSettings {
                    BackgroundColor = settings.BackgroundColor,
                    State = settings.State,
                    Box = (RectF)selBounds.Value,
                    ContentBox = (RectF)selBounds.Value
                };
                selectionDecorator.Rasterize(context, selSettings);
                context.Renderer.Layer += 1;
            }

            ColorizeSelection(layout.DrawCalls, selection, context, settings.State, selectionDecorator);

            context.Renderer.DrawMultiple(
                layout.DrawCalls, offset: textOffset,
                material: textMaterial, samplerState: RenderStates.Text
            );
        }
    }

    public class Button : StaticText {
        public Button ()
            : base () {
            Content.Alignment = HorizontalAlignment.Center;
            AcceptsCapture = true;
            AcceptsFocus = true;
        }

        protected override IDecorator GetDefaultDecorations (UIOperationContext context) {
            return context.DecorationProvider?.Button;
        }

        /*
        protected override void OnRasterize (UIOperationContext context, DecorationSettings settings, IDecorator decorations) {
            base.OnRasterize(context, settings, decorations);
        }
        */
    }

    public class Container : Control {
        public readonly ControlCollection Children;

        /// <summary>
        /// If set, children will only be rendered within the volume of this container
        /// </summary>
        public bool ClipChildren = false;

        public bool Scrollable = false;
        public bool ShowVerticalScrollbar = true, ShowHorizontalScrollbar = true;

        private Vector2 _ScrollOffset;
        protected Vector2 AbsoluteDisplayOffsetOfChildren;

        public ControlFlags ContainerFlags = ControlFlags.Container_Row;

        protected ScrollbarState HScrollbar, VScrollbar;
        protected bool HasContentBounds;
        protected RectF ContentBounds;

        public Container () 
            : base () {
            Children = new ControlCollection(this);
            AcceptsCapture = true;

            HScrollbar = new ScrollbarState {
                DragInitialPosition = null,
                Horizontal = true
            };
            VScrollbar = new ScrollbarState {
                DragInitialPosition = null,
                Horizontal = false
            };
        }

        public Vector2 ScrollOffset {
            get {
                return _ScrollOffset;
            }
            set {
                if (value == _ScrollOffset)
                    return;

                _ScrollOffset = value;
                OnDisplayOffsetChanged();
            }
        }

        protected void OnScroll (float delta) {
            ScrollOffset = new Vector2(ScrollOffset.X, ScrollOffset.Y - delta);
        }

        protected override bool OnEvent<T> (string name, T args) {
            if (name == UIContext.Events.Scroll) {
                OnScroll(Convert.ToSingle(args));
                return true;
            }

            return false;
        }

        protected override void OnDisplayOffsetChanged () {
            AbsoluteDisplayOffsetOfChildren = AbsoluteDisplayOffset - _ScrollOffset;

            foreach (var child in Children)
                child.AbsoluteDisplayOffset = AbsoluteDisplayOffsetOfChildren;
        }

        protected override ControlKey OnGenerateLayoutTree (UIOperationContext context, ControlKey parent) {
            HasContentBounds = false;
            var result = base.OnGenerateLayoutTree(context, parent);
            context.Layout.SetContainerFlags(result, ContainerFlags);
            foreach (var item in Children) {
                item.AbsoluteDisplayOffset = AbsoluteDisplayOffsetOfChildren;
                item.GenerateLayoutTree(context, result);
            }
            return result;
        }

        protected override IDecorator GetDefaultDecorations (UIOperationContext context) {
            if (LayoutFlags.IsFlagged(ControlFlags.Layout_Floating))
                return context.DecorationProvider?.FloatingContainer ?? context.DecorationProvider?.Container;
            else
                return context.DecorationProvider?.Container;
        }

        protected override bool ShouldClipContent => ClipChildren && (Children.Count > 0);
        // FIXME: Always true?
        protected override bool HasNestedContent => (Children.Count > 0);

        private void RasterizeChildren (UIOperationContext context, RasterizePasses pass) {
            context.Pass = pass;
            // FIXME
            int layer = context.Renderer.Layer, maxLayer = layer;

            foreach (var item in Children) {
                context.Renderer.Layer = layer;
                item.Rasterize(context);
                maxLayer = Math.Max(maxLayer, context.Renderer.Layer);
            }

            context.Renderer.Layer = maxLayer;
        }

        protected override Margins ComputePadding (UIOperationContext context, IDecorator decorations) {
            var result = base.ComputePadding(context, decorations);
            if (!Scrollable)
                return result;
            var scrollbar = context.DecorationProvider?.Scrollbar;
            if (scrollbar == null)
                return result;
            result.Right += scrollbar.MinimumSize.X;
            result.Bottom += scrollbar.MinimumSize.Y;
            return result;
        }

        protected bool GetContentBounds (UIOperationContext context, out RectF contentBounds) {
            if (!HasContentBounds)
                HasContentBounds = context.Layout.TryMeasureContent(LayoutKey, out ContentBounds);

            contentBounds = ContentBounds;
            return HasContentBounds;
        }

        protected void UpdateScrollbars (UIOperationContext context, DecorationSettings settings) {
        }

        protected override void OnRasterize (UIOperationContext context, DecorationSettings settings, IDecorator decorations) {
            base.OnRasterize(context, settings, decorations);

            // FIXME: This should be done somewhere else
            if (Scrollable) {
                var box = settings.Box;
                var scrollbar = context.DecorationProvider?.Scrollbar;
                float viewportWidth = box.Width - (scrollbar?.MinimumSize.X ?? 0),
                    viewportHeight = box.Height - (scrollbar?.MinimumSize.Y ?? 0);

                GetContentBounds(context, out RectF contentBounds);

                if (HasContentBounds) {
                    float maxScrollX = ContentBounds.Width - viewportWidth, maxScrollY = ContentBounds.Height - viewportHeight;
                    maxScrollX = Math.Max(0, maxScrollX);
                    maxScrollY = Math.Max(0, maxScrollY);
                    ScrollOffset = new Vector2(
                        Arithmetic.Clamp(ScrollOffset.X, 0, maxScrollX),
                        Arithmetic.Clamp(ScrollOffset.Y, 0, maxScrollY)
                    );
                }

                HScrollbar.ContentSize = ContentBounds.Width;
                HScrollbar.ViewportSize = box.Width;
                HScrollbar.Position = ScrollOffset.X;
                VScrollbar.ContentSize = ContentBounds.Height;
                VScrollbar.ViewportSize = box.Height;
                VScrollbar.Position = ScrollOffset.Y;

                HScrollbar.HasCounterpart = VScrollbar.HasCounterpart = (ShowHorizontalScrollbar && ShowVerticalScrollbar);

                if (ShowHorizontalScrollbar)
                    scrollbar?.Rasterize(context, settings, ref HScrollbar);
                if (ShowVerticalScrollbar)
                    scrollbar?.Rasterize(context, settings, ref VScrollbar);
            } else {
                ScrollOffset = Vector2.Zero;
            }

            if (context.Pass != RasterizePasses.Content)
                return;

            if (Children.Count == 0)
                return;

            RasterizeChildren(context, RasterizePasses.Below);
            RasterizeChildren(context, RasterizePasses.Content);
            RasterizeChildren(context, RasterizePasses.Above);
        }

        protected override void ApplyClipMargins (UIOperationContext context, ref RectF box) {
            var scroll = context.DecorationProvider?.Scrollbar;
            if (scroll != null) {
                if (ShowHorizontalScrollbar)
                    box.Height -= scroll.MinimumSize.Y;
                if (ShowVerticalScrollbar)
                    box.Width -= scroll.MinimumSize.X;
            }
        }

        protected override bool OnHitTest (LayoutContext context, RectF box, Vector2 position, bool acceptsCaptureOnly, bool acceptsFocusOnly, ref Control result) {
            if (!base.OnHitTest(context, box, position, false, false, ref result))
                return false;

            bool success = AcceptsCapture || !acceptsCaptureOnly;
            // FIXME: Should we only perform the hit test if the position is within our boundaries?
            // This doesn't produce the right outcome when a container's computed size is zero
            for (int i = Children.Count - 1; i >= 0; i--) {
                var item = Children[i];
                var newResult = item.HitTest(context, position, acceptsCaptureOnly, acceptsFocusOnly);
                if (newResult != null) {
                    result = newResult;
                    success = true;
                }
            }

            return success;
        }
    }

    public class Window : Container {
        public Vector2 Position {
            get {
                return new Vector2(Margins.Left, Margins.Top);
            }
            set {
                Margins.Left = value.X;
                Margins.Top = value.Y;
            }
        }

        public string Title;

        protected DynamicStringLayout TitleLayout = new DynamicStringLayout {
            LineLimit = 1
        };

        public Window ()
            : base () {
            ContainerFlags |= ControlFlags.Container_Constrain_Size;
            LayoutFlags |= ControlFlags.Layout_Floating;
        }

        protected IDecorator UpdateTitle (UIOperationContext context, DecorationSettings settings, out Material material, ref Color? color) {
            var decorations = context.DecorationProvider?.WindowTitle;
            if (decorations == null) {
                material = null;
                return null;
            }
            decorations.GetTextSettings(context, settings.State, out material, out IGlyphSource font, ref color);
            TitleLayout.Text = Title;
            TitleLayout.GlyphSource = font;
            TitleLayout.Color = color ?? Color.White;
            TitleLayout.LineBreakAtX = settings.ContentBox.Width;
            return decorations;
        }

        protected override Margins ComputePadding (UIOperationContext context, IDecorator decorations) {
            var result = base.ComputePadding(context, decorations);
            var titleDecorations = context.DecorationProvider?.WindowTitle;
            if (titleDecorations == null)
                return result;

            Color? color = null;
            titleDecorations.GetTextSettings(context, default(ControlStates), out Material temp, out IGlyphSource font, ref color);
            result.Top += titleDecorations.Margins.Bottom;
            result.Top += titleDecorations.Padding.Top;
            result.Top += titleDecorations.Padding.Bottom;
            result.Top += font.LineSpacing;
            return result;
        }

        protected override void OnRasterize (UIOperationContext context, DecorationSettings settings, IDecorator decorations) {
            base.OnRasterize(context, settings, decorations);

            IDecorator titleDecorator;
            Color? titleColor = null;
            if (
                (context.Pass == RasterizePasses.Below) && 
                (titleDecorator = UpdateTitle(context, settings, out Material titleMaterial, ref titleColor)) != null
            ) {
                var layout = TitleLayout.Get();
                var titleBox = settings.Box;
                titleBox.Height = titleDecorator.Padding.Top + titleDecorator.Padding.Bottom + TitleLayout.GlyphSource.LineSpacing;
                var titleContentBox = titleBox;
                titleContentBox.Left += titleDecorator.Padding.Left;
                titleContentBox.Top += titleDecorator.Padding.Top;
                titleContentBox.Width -= titleDecorator.Padding.X;

                var offsetX = (titleContentBox.Width - layout.Size.X) / 2f;

                var subSettings = settings;
                subSettings.Box = titleBox;
                subSettings.ContentBox = titleContentBox;

                titleDecorator.Rasterize(context, subSettings);
                context.Renderer.DrawMultiple(
                    layout.DrawCalls, new Vector2(titleContentBox.Left + offsetX, titleContentBox.Top),
                    samplerState: RenderStates.Text, multiplyColor: titleColor.Value
                );
            }
        }

        public override string ToString () {
            return $"{GetType().Name} #{GetHashCode():X8} '{Title}'";
        }
    }

    public class ControlCollection : IEnumerable<Control> {
        private List<Control> Items = new List<Control>();

        public int Count => Items.Count;
        public Control Parent { get; private set; }

        public ControlCollection (Control parent) {
            Parent = parent;
        }

        public void Add (Control control) {
            if (Items.Contains(control))
                throw new InvalidOperationException("Control already in collection");

            Items.Add(control);
            control.SetParent(Parent);
        }

        public void Remove (Control control) {
            control.UnsetParent(Parent);
            Items.Remove(control);
        }

        public void Clear () {
            foreach (var control in Items)
                control.UnsetParent(Parent);

            Items.Clear();
        }

        public List<Control>.Enumerator GetEnumerator () {
            return Items.GetEnumerator();
        }

        public Control this[int index] {
            get {
                return Items[index];
            }
            set {
                Items[index] = value;
            }
        }

        IEnumerator<Control> IEnumerable<Control>.GetEnumerator () {
            return ((IEnumerable<Control>)Items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator () {
            return ((IEnumerable)Items).GetEnumerator();
        }
    }
}
