﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MegaMan.Editor.Bll;

namespace MegaMan.Editor.Controls
{
    public class TileScreenLayer : ScreenLayer
    {
        private bool _grayscale;

        static TileScreenLayer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TileScreenLayer), new FrameworkPropertyMetadata(typeof(TileScreenLayer)));
        }

        public void RenderColor()
        {
            _grayscale = false;
        }

        public void RenderGrayscale()
        {
            _grayscale = true;
        }

        protected override void UnbindScreen(ScreenDocument oldScreen)
        {
            oldScreen.TileChanged -= Update;
        }

        protected override void BindScreen(ScreenDocument newScreen)
        {
            newScreen.TileChanged += Update;
        }

        protected override void Update()
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var bitmap = new WriteableBitmap(Screen.PixelWidth, Screen.PixelHeight, 96, 96, PixelFormats.Pbgra32, null);

            base.OnRender(dc);

            var size = Screen.Tileset.TileSize;

            for (int y = 0; y < Screen.Height; y++)
            {
                for (int x = 0; x < Screen.Width; x++)
                {
                    var tile = Screen.TileAt(x, y);
                    var location = tile.Sprite.CurrentFrame.SheetLocation;

                    if (_grayscale)
                    {
                        var image = SpriteBitmapCache.GetOrLoadFrameGrayscale(Screen.Tileset.SheetRelativePath.Absolute, location);
                        bitmap.Blit(new Rect(x * size, y * size, size, size), image, new Rect(0, 0, image.PixelWidth, image.PixelHeight));
                    }
                    else
                    {
                        var image = SpriteBitmapCache.GetOrLoadFrame(Screen.Tileset.SheetRelativePath.Absolute, location);
                        bitmap.Blit(new Rect(x * size, y * size, size, size), image, new Rect(0, 0, image.PixelWidth, image.PixelHeight));
                    }
                }
            }

            dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        }
    }
}
