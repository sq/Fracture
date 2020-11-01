﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Squared.Game;
using Squared.PRGUI.Decorations;
using Squared.PRGUI.Layout;
using Squared.Render;
using Squared.Render.Convenience;
using Squared.Render.Text;
using Squared.Util;

namespace Squared.PRGUI.Controls {
    public class Window : Container {
        private Vector2 _ScreenAlignment = Vector2.One * 0.5f;
        public Vector2 ScreenAlignment {
            get => _ScreenAlignment;
            set {
                _ScreenAlignment = value;
                NeedsAlignment = true;
            }
        }

        public Vector2 Position {
            get {
                return new Vector2(Margins.Left, Margins.Top);
            }
            set {
                NeedsAlignment = false;
                Margins.Left = value.X;
                Margins.Top = value.Y;
            }
        }

        public string Title;
        public bool AllowDrag = true;
        public bool AllowMaximize = true;

        private bool NeedsAlignment = true;
        private bool Dragging, DragStartedMaximized;
        private Vector2 DragStartMousePosition, DragStartWindowPosition;
        private RectF MostRecentTitleBox, MostRecentUnmaximizedRect;

        protected DynamicStringLayout TitleLayout = new DynamicStringLayout {
            LineLimit = 1
        };

        public bool Maximized {
            get => LayoutFlags.IsFlagged(ControlFlags.Layout_Fill);
            set {
                if (!AllowMaximize)
                    value = false;
                var flags = LayoutFlags & ~ControlFlags.Layout_Fill;
                if (value)
                    flags |= ControlFlags.Layout_Fill;
                Context.Log($"Window layout flags {LayoutFlags} -> {flags}");
                LayoutFlags = flags;
            }
        }

        public Window ()
            : base () {
            AcceptsMouseInput = true;
            ContainerFlags |= ControlFlags.Container_Constrain_Size;
            LayoutFlags = ControlFlags.Layout_Floating;
        }

        protected IDecorator UpdateTitle (UIOperationContext context, DecorationSettings settings, out Material material, ref pSRGBColor? color) {
            var decorations = context.DecorationProvider?.WindowTitle;
            if (decorations == null) {
                material = null;
                return null;
            }
            decorations.GetTextSettings(context, settings.State, out material, out IGlyphSource font, ref color);
            TitleLayout.Text = Title;
            TitleLayout.GlyphSource = font;
            TitleLayout.DefaultColor = color?.ToColor() ?? Color.White;
            TitleLayout.LineBreakAtX = settings.ContentBox.Width;
            return decorations;
        }

        protected override Margins ComputePadding (UIOperationContext context, IDecorator decorations) {
            var result = base.ComputePadding(context, decorations);
            var titleDecorations = context.DecorationProvider?.WindowTitle;
            if (titleDecorations == null)
                return result;

            pSRGBColor? color = null;
            titleDecorations.GetTextSettings(context, default(ControlStates), out Material temp, out IGlyphSource font, ref color);
            result.Top += titleDecorations.Margins.Bottom;
            result.Top += titleDecorations.Padding.Top;
            result.Top += titleDecorations.Padding.Bottom;
            result.Top += font.LineSpacing;
            return result;
        }

        protected override void OnLayoutComplete (UIOperationContext context, ref bool relayoutRequested) {
            base.OnLayoutComplete(context, ref relayoutRequested);

            var rect = GetRect(context.Layout, includeOffset: false);

            // Handle the corner case where the canvas size has changed since we were last moved and ensure we are still on screen
            if (!Maximized)
                MostRecentUnmaximizedRect = rect;

            var availableSpace = (context.UIContext.CanvasSize - rect.Size);

            if (!NeedsAlignment) {
                if (UpdatePosition(Position, context.UIContext, rect))
                    relayoutRequested = true;

                _ScreenAlignment = Position / availableSpace;
            } else {
                Position = availableSpace * _ScreenAlignment;
                NeedsAlignment = false;
                relayoutRequested = true;
            }
        }

        protected override void OnRasterize (UIOperationContext context, ref ImperativeRenderer renderer, DecorationSettings settings, IDecorator decorations) {
            base.OnRasterize(context, ref renderer, settings, decorations);

            if (context.Pass != RasterizePasses.Below)
                return;

            IDecorator titleDecorator;
            pSRGBColor? titleColor = null;
            if (
                (titleDecorator = UpdateTitle(context, settings, out Material titleMaterial, ref titleColor)) != null
            ) {
                var layout = TitleLayout.Get();
                var titleBox = settings.Box;
                titleBox.Height = titleDecorator.Padding.Top + titleDecorator.Padding.Bottom + TitleLayout.GlyphSource.LineSpacing;
                // FIXME: Compute this somewhere else, like in OnLayoutComplete
                MostRecentHeaderHeight = titleBox.Height;
                MostRecentTitleBox = titleBox;

                var titleContentBox = titleBox;
                titleContentBox.Left += titleDecorator.Padding.Left;
                titleContentBox.Top += titleDecorator.Padding.Top;
                titleContentBox.Width -= titleDecorator.Padding.X;

                var offsetX = (titleContentBox.Width - layout.Size.X) / 2f;

                var subSettings = settings;
                subSettings.Box = titleBox;
                subSettings.ContentBox = titleContentBox;

                renderer.Layer += 1;
                titleDecorator.Rasterize(context, ref renderer, subSettings);

                renderer.Layer += 1;
                renderer.DrawMultiple(
                    layout.DrawCalls, new Vector2(titleContentBox.Left + offsetX, titleContentBox.Top),
                    samplerState: RenderStates.Text, multiplyColor: titleColor?.ToColor()
                );
            }
        }

        protected override bool OnEvent<T> (string name, T args) {
            if (args is MouseEventArgs)
                return OnMouseEvent(name, (MouseEventArgs)(object)args);

            return base.OnEvent<T>(name, args);
        }

        private bool UpdatePosition (Vector2 newPosition, UIContext context, RectF box) {
            var availableSpaceX = Math.Max(0, context.CanvasSize.X - box.Width);
            var availableSpaceY = Math.Max(0, context.CanvasSize.Y - box.Height);
            newPosition = new Vector2(
                Arithmetic.Saturate(newPosition.X, availableSpaceX),
                Arithmetic.Saturate(newPosition.Y, availableSpaceY)
            ).Floor();

            if (Position == newPosition)
                return false;

            // context.Log($"Window position {Position} -> {newPosition}");
            Position = newPosition;
            return true;
        }

        private bool OnMouseEvent (string name, MouseEventArgs args) {
            if (name == UIEvents.MouseDown) {
                Context.TrySetFocus(this, false);

                if (MostRecentTitleBox.Contains(args.RelativeGlobalPosition) && AllowDrag) {
                    Context.CaptureMouse(this);
                    Dragging = true;
                    DragStartedMaximized = Maximized;
                    DragStartMousePosition = args.GlobalPosition;
                    DragStartWindowPosition = Position;
                    return true;
                } else {
                    Dragging = false;
                    return base.OnEvent(name, args);
                }
            } else if (
                (name == UIEvents.MouseMove) ||
                (name == UIEvents.MouseUp)
            ) {
                if (!Dragging)
                    return base.OnEvent(name, args);

                if (name == UIEvents.MouseUp)
                    Dragging = false;

                var delta = args.GlobalPosition - DragStartMousePosition;
                var newPosition = DragStartedMaximized
                    ? new Vector2(args.GlobalPosition.X - (MostRecentUnmaximizedRect.Width / 2f), args.GlobalPosition.Y - 4)
                    : (DragStartWindowPosition + delta);
                var shouldMaximize = (newPosition.Y < -5) && !Maximized && AllowMaximize;
                var shouldUnmaximize = (((delta.Y > 4) || (newPosition.Y > 4)) || !AllowMaximize) && Maximized;
                if (shouldUnmaximize) {
                    // FIXME: Scale the mouse anchor based on the new size vs the old maximized size
                    Maximized = false;
                    UpdatePosition(newPosition, args.Context, MostRecentUnmaximizedRect);
                } else if (shouldMaximize || Maximized) {
                    Maximized = true;
                } else {
                    UpdatePosition(newPosition, args.Context, args.Box);
                }

                FireEvent(UIEvents.Moved);

                return true;
            } else
                return base.OnEvent(name, args);
        }

        public override string ToString () {
            return $"{GetType().Name} #{GetHashCode():X8} '{Title}'";
        }
    }
}