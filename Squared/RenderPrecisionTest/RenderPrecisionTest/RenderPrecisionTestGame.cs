using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Squared.Render;
using Squared.Render.Convenience;

namespace RenderPrecisionTest {
    public class RenderPrecisionTestGame : MultithreadedGame {
        public static readonly Color ClearColor = new Color(24, 96, 220, 255);

        Texture2D TestTexture;

        DefaultMaterialSet Materials;
        GraphicsDeviceManager Graphics;

        public RenderPrecisionTestGame () {
            Graphics = new GraphicsDeviceManager(this);
            Graphics.PreferredBackBufferWidth = 820;
            Graphics.PreferredBackBufferHeight = 860;
        }

        protected override void Initialize () {
            base.Initialize();

            Materials = new DefaultMaterialSet(RenderCoordinator);
        }

        protected override void OnLoadContent (bool isReloading) {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("test.png"))
                TestTexture = Texture2D.FromStream(Graphics.GraphicsDevice, s);
        }

        protected override void Update (GameTime gameTime) {
            base.Update(gameTime);
        }

        public override void Draw (GameTime gameTime, Frame frame) {
            var ir = new ImperativeRenderer(frame, Materials);
            
            // Set to true to draw each quad in a separate batch... for no sensible reason
            if (false)
                ir.AutoIncrementLayer = true;

            ir.Clear(color: ClearColor);
            ir.Layer += 1;

            var rect1 = new Rectangle(0, 0, 32, 32);
            var rect2 = new Rectangle(1, 1, 30, 30);

            var drawSet = (Action<Rectangle, float>)((r, y) => {
                DrawRow(ref ir, 0f, y + 0f, SamplerState.PointClamp, r, false);
                DrawRow(ref ir, 0.5f, y + 1 + 64f, SamplerState.PointClamp, r, true);
                DrawRow(ref ir, 0f, (y + 3 + 128) + 0.5f, SamplerState.PointClamp, r, false);

                DrawRow(ref ir, 0f, y + 5 + 192f, SamplerState.LinearClamp, r, true);
                DrawRow(ref ir, 0.5f, y + 7 + 256f, SamplerState.LinearClamp, r, false);
                DrawRow(ref ir, 0f, (y + 9 + 320) + 0.5f, SamplerState.LinearClamp, r, true);
            });

            drawSet(rect1, 0f);

            drawSet(rect2, (70f*6));

            var cornerSamplers = SamplerState.LinearClamp;
            ir.Draw(TestTexture, new Vector2(Graphics.PreferredBackBufferWidth - 1, Graphics.PreferredBackBufferHeight - 1), origin: new Vector2(1, 1), samplerState: cornerSamplers);
            ir.Draw(TestTexture, new Vector2(0, Graphics.PreferredBackBufferHeight - 1), origin: new Vector2(0, 1), samplerState: cornerSamplers);
            ir.Draw(TestTexture, new Vector2(Graphics.PreferredBackBufferWidth - 1, 0), origin: new Vector2(1, 0), samplerState: cornerSamplers);
        }

        private void DrawRow (ref ImperativeRenderer ir, float x, float y, SamplerState samplerState, Rectangle sourceRect, bool mirror) {
            var tlState = samplerState;
            if ((x == 0) && (y == 0))
                tlState = SamplerState.LinearClamp;

            ir.Draw(TestTexture, x, y, sourceRect, samplerState: tlState, mirrorX: mirror);
            x += 64f;
            ir.Draw(TestTexture, x, y, sourceRect, scaleX: 2f, scaleY: 2f, samplerState: samplerState, mirrorX: mirror);
            x += 96f;

            for (float r = 0.1f; r < Math.PI / 2f; r += 0.2f) {
                ir.Draw(TestTexture, x, y, sourceRect, rotation: r, samplerState: samplerState, mirrorX: mirror);

                x += 64f;
            }
        }
    }
}
