using MathNet.Numerics;
using mcore.Utils;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System;
using System.Diagnostics;
using System.Numerics;

namespace MauiAppTest
{
    public partial class MainPage : ContentPage
    {
        int iterations = 10;
        float zoom = 100f;
        SKBitmap current, preview, fullView;

        SKRect ComplexPlane = new(-2,-2,2,2);
        SKRect ViewPlane = new(0, 0, 300, 300);

        Func<Complex, Complex, Complex> fractal;
        bool showAxis = false;
        SKPoint ComplexToView(float r, float i)
        {
            var p = new SKPoint(r, i);
            var vp = ComplexPlane.Project(p, ref ViewPlane);
            return vp;
        }
        public MainPage()
        {
            InitializeComponent();

            fractal = (z, z0) => z * z + z0;

            canvasView.EnableTouchEvents = true;

            fullView = new SKBitmap(256, 256);
            preview = new SKBitmap(128, 128);
        }
        bool validatePlanes()
        {
            if (canvasView.CanvasSize.Width == 0)
                return false;

            var x = ComplexPlane.MidX;
            var y = ComplexPlane.MidY;

            var w = canvasView.CanvasSize.Width / zoom / 2;
            var h = canvasView.CanvasSize.Height / zoom / 2;
            ComplexPlane = new SKRect(
                -w + x, -h + y,
                 w + x, h + y);

            ViewPlane = new(0, 0, canvasView.CanvasSize.Width, canvasView.CanvasSize.Height);

            return true;
        }
        private void canvasView_SizeChanged(object sender, EventArgs e)
        {
            if (!validatePlanes())
                return;

            CreateCurrentFractal(preview, 0);
        }
        SKPoint dragStartComplex;
        private void CanvasView_Touch(object? sender, SKTouchEventArgs e)
        {
            if (!e.InContact)
            {
                switch(e.ActionType)
                {
                    case SKTouchAction.WheelChanged:
                        zoom += e.WheelDelta * 0.001f * zoom;
                        CreateCurrentFractal(preview, 0);
                        break;
                }
                return;
            }

            if (!validatePlanes())
                return;

            var s0 = ViewPlane.Project(e.Location, ref ComplexPlane);

            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    dragStartComplex = s0;
                    break;
                case SKTouchAction.Moved:
                    if (SeedRadio.IsChecked)
                    {
                        var seed = new Complex(s0.X, s0.Y);
                        fractal = (z, z0) => z.Power(2) + seed;
                    }
                    else if (MoveRadio.IsChecked)
                    {
                        var c = new SKPoint(dragStartComplex.X - s0.X, dragStartComplex.Y - s0.Y);
                        ComplexPlane.Location += c;
                    }
                    break;
            }

            e.Handled = true;
            CreateCurrentFractal(preview, 0);
        }
        private void Render_Clicked(object sender, EventArgs e)
        {
            if (!validatePlanes())
                return;
            if (canvasView.Width != fullView.Width)
            {
                fullView = new SKBitmap((int)canvasView.Width, (int)canvasView.Height);
            }
            Task.Run(() => CreateCurrentFractal(fullView, 300));
        }
        private void Clear_Clicked(object sender, EventArgs e)
        {
            fractal = (z, z0) => z * z + z0;
            ComplexPlane = new SKRect(-2,-2,2,2);
            CreateCurrentFractal(preview, 0);
        }
        bool isRendering = false;
        bool cancelRenderRequested = false;
        void CreateCurrentFractal(SKBitmap target, int invalidateCurrentMillis)
        {
            if (!validatePlanes())
                return;

            cancelRenderRequested = true;
            while (isRendering)
            {
                Thread.Sleep(100);
            }
            cancelRenderRequested = false;

            current = target;

            isRendering = true;

            var pixels = new SKColor[current.Width * current.Height];
            var depth = new int[current.Width * current.Height];
            var trace = new int[current.Width * current.Height];
            var maxDepth = 0;
            var maxTrace = 0;

            for (int x = 0; x < current.Width; ++x)
                for (int y = 0; y < current.Height; ++y)
                {
                    pixels[x + y * current.Width] = SKColors.White;
                    trace[x + y * current.Width] = 0;
                    depth[x + y * current.Width] = 0;
                }

            var fractalPlane = new SKRect(0, 0, current.Width, current.Height);
            var w = Stopwatch.StartNew();

            for (int x = 0; x < current.Width; ++x)
            {
                for (int y = 0; y < current.Height; ++y)
                {
                    var c0 = fractalPlane.Project(new SKPoint(x, y), ref ComplexPlane);
                    var z = new Complex(c0.X, c0.Y);
                    var abs = z;
                    int i = 0;
                    while (true)
                    {
                        if (cancelRenderRequested)
                        {
                            isRendering = false;
                            return;
                        }

                        //var z0 = z;
                        z = fractal(z, abs);
                        //if ((z - z0).MagnitudeSquared() < 0.00000001)
                        //{
                        //    i = iterations;
                        //    break;
                        //}
                        //if (!ComplexPlane.Contains((float)z.Real, (float)z.Imaginary))
                        //    return i;
                        if (z.MagnitudeSquared() > 4)
                            break;

                        if (++i == iterations)
                            break;

                        var p0 = ComplexPlane.Project(new SKPoint(
                            (float)z.Real,
                            (float)z.Imaginary), ref fractalPlane);
                        if (fractalPlane.Contains(p0))
                        {
                            var index = (int)p0.X + (int)p0.Y * current.Width;
                            trace[index]++;
                            if (trace[index] > maxTrace)
                                maxTrace = trace[index];
                        }
                    }
                    if (invalidateCurrentMillis > 0 && w.Elapsed(invalidateCurrentMillis))
                    {
                        current.Pixels = pixels;
                        canvasView.InvalidateSurface();
                    }
                    if (i > maxDepth)
                        maxDepth = i;
                    var k = x + y * current.Width;
                    depth[k] = i;
                    pixels[k] = SKColors.White.Interpolate(SKColors.Black, (double)i / iterations);
                }
            }
            if (maxDepth == 0) maxDepth++;
            if (maxTrace == 0) maxTrace++;
            for (int x = 0; x < current.Width; ++x)
                for (int y = 0; y < current.Height; ++y)
                {
                    var i = x + y * current.Width;

                    var pd = depth[i] / (double)maxDepth;
                    var d = Depth1Color_sld.ThumbColor.ToSKColor();
                    pixels[i] = SKColors.Black.Interpolate(d, pd);
                }
            current.Pixels = pixels;
            canvasView.InvalidateSurface();

            isRendering = false;
        }
        private void CanvasView_OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            if (!validatePlanes())
                return;

            var canvas = e.Surface.Canvas;

            if (current != null)
                canvas.DrawBitmap(current, ViewPlane);

            if (showAxis)
            {
                var paint = new SKPaint()
                {
                    Color = new SKColor(0,0,0),
                    IsStroke = true
                };
                var x0 = ComplexPlane.Left;
                var x1 = ComplexPlane.Right;
                var y0 = ComplexPlane.Top;
                var y1 = ComplexPlane.Bottom;

                var rn1 = ComplexToView(-1, 0);
                canvas.DrawText("-1", (float)rn1.X, (float)rn1.Y, paint);
                var rp1 = ComplexToView(1, 0);
                canvas.DrawText("1", (float)rp1.X, (float)rp1.Y, paint);
                canvas.DrawLine(ComplexToView(x0, 0), ComplexToView(x1, 0), paint);


                var in1 = ComplexToView(0, -1);
                canvas.DrawText("-1", (float)in1.X, (float)in1.Y, paint);
                var ip1 = ComplexToView(0, 1);
                canvas.DrawText("1", (float)ip1.X, (float)ip1.Y, paint);
                canvas.DrawLine(ComplexToView(0, y0), ComplexToView(0, y1), paint);
            }
        }
        void updateDepth1Sliders()
        {
            if (Depth1Color_sld == null || Depth1Factor_sld == null)
                return;
            var h = (float)Depth1Color_sld.Value;
            var sv = (float)Depth1Factor_sld.Value;
            var s = Math.Clamp(sv * 2, 0, 1);
            var v = 1 - Math.Clamp(sv * 2 - 1, 0, 1);
            if (h == 0)
            {
                s = 0;
                v = sv;
            }
            var c = Color.FromHsv(h, s, v);
            Depth1Factor_sld.ThumbColor = c;
            Depth1Color_sld.ThumbColor = c;
        }
        private void Depth1ColorChanged(object sender, ValueChangedEventArgs e)
            => updateDepth1Sliders();
        private void Depth1FactorChanged(object sender, ValueChangedEventArgs e)
            => updateDepth1Sliders();
        private void IterationsChanged(object sender, ValueChangedEventArgs e)
        {
            iterations = (int)e.NewValue;
            Iterations_lbl.Text = $"Iterations ({e.NewValue:F0}):";

            CreateCurrentFractal(preview, 0);
        }
    }
    public static class Helpers
    {
        public static SKPoint Project(this ref readonly SKRect from, SKPoint o, ref SKRect to)
        {
            var x = (o.X - from.Left) / from.Width * to.Width + to.Left;
            var y = (o.Y - from.Bottom) / from.Height * to.Height + to.Bottom;
            return new SKPoint(x, y);
        }
        public static SKRect Project(this ref readonly SKRect from, ref SKRect o, ref SKRect to)
        {
            var min = from.Project(o.Location, ref to);
            var max = from.Project(new SKPoint(o.Right, o.Bottom), ref to);
            return new SKRect(min.X, min.Y, max.X - min.X, max.Y - max.Y);
        }
        public static IEnumerable<Rect> Walk(this Rect r, double size)
        {
            for (var x = r.X; x < r.Right; x += size)
                for (var y = r.Y; y < r.Bottom; y += size)
                {
                    yield return new Rect(x, y, size, size);
                }
        }
        public static double Interpolate(double from, double to, double factor)
        {
            return from * factor + to * (1 - factor);
        }
        public static SKColor Interpolate(this ref readonly SKColor self, SKColor target, double f)
        {
            var r = (byte)Interpolate(self.Red, target.Red, f);
            var g = (byte)Interpolate(self.Green, target.Green, f);
            var b = (byte)Interpolate(self.Blue, target.Blue, f);
            return new SKColor(r, g, b);
        }
        public static SKColor Brighten(this ref readonly SKColor self, int d)
        {
            var r = self.Red +d;
            var g = self.Green +d;
            var b = self.Blue +d;

            return new SKColor(
                (byte)Math.Clamp(r, 0, 255),
                (byte)Math.Clamp(g, 0, 255),
                (byte)Math.Clamp(b, 0, 255));
        }
    }
}
