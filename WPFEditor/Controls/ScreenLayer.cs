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
    public abstract class ScreenLayer : Canvas
    {
        private ScreenDocument _screen;

        public ScreenDocument Screen
        {
            get
            {
                return _screen;
            }
            set
            {
                if (_screen != null)
                {
                    UnbindScreen(_screen);
                }

                _screen = value;

                if (_screen != null)
                {
                    BindScreen(_screen);
                }

                Update();
            }
        }

        protected abstract void UnbindScreen(ScreenDocument oldScreen);
        protected abstract void BindScreen(ScreenDocument newScreen);

        protected abstract void Update();

        public ScreenLayer()
        {
            ((App)App.Current).Tick += ScreenLayer_Tick;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            return new Size(_screen.PixelWidth, _screen.PixelHeight);
        }

        private void ScreenLayer_Tick()
        {
            InvalidateVisual();
        }

        static ScreenLayer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ScreenLayer), new FrameworkPropertyMetadata(typeof(ScreenLayer)));
        }
    }
}
