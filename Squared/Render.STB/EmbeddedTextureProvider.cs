﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace Squared.Render {
    public class TextureLoadOptions {
        public bool Premultiply = true;
        public bool FloatingPoint;
        public bool GenerateMips;
    }

    public class EmbeddedTexture2DProvider : EmbeddedResourceProvider<Texture2D> {
        new public TextureLoadOptions DefaultOptions {
            get {
                return (TextureLoadOptions)base.DefaultOptions;
            }
            set {
                base.DefaultOptions = value;
            }
        }

        public EmbeddedTexture2DProvider (Assembly assembly, RenderCoordinator coordinator) 
            : base(assembly, coordinator) {
        }

        public EmbeddedTexture2DProvider (RenderCoordinator coordinator) 
            : base(Assembly.GetCallingAssembly(), coordinator) {
        }

        public Texture2D Load (string name, TextureLoadOptions options) {
            return base.Load(name, options);
        }

        protected override Texture2D CreateInstance (Stream stream, object data) {
            var options = (TextureLoadOptions)data ?? DefaultOptions ?? new TextureLoadOptions();
            using (var img = new STB.Image(stream, false, options.Premultiply, options.FloatingPoint))
            lock (Coordinator.CreateResourceLock)
                return img.CreateTexture(Coordinator, options.GenerateMips);
        }
    }
}