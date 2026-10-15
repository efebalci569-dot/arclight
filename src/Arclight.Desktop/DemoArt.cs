using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Arclight.Desktop;

/// <summary>
/// Paints original, abstract key art for the fictional sample library so screenshots look like a
/// real collection without using anyone's artwork. Only used by <see cref="DemoData"/>.
/// </summary>
internal static class DemoArt
{
    public enum Motif { Sun, Mountains, Rings, Hills, Shards, Waves }

    public static void Render(string path, int width, int height, string title, Motif motif, Color deep, Color mid, Color glow, bool withTitle)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, width, height);
            dc.DrawRectangle(new LinearGradientBrush(mid, deep, new Point(0.5, 0), new Point(0.5, 1)), null, bounds);

            switch (motif)
            {
                case Motif.Sun: DrawSun(dc, width, height, glow, deep); break;
                case Motif.Mountains: DrawMountains(dc, width, height, glow, deep, mid); break;
                case Motif.Rings: DrawRings(dc, width, height, glow); break;
                case Motif.Hills: DrawHills(dc, width, height, glow, deep, mid); break;
                case Motif.Shards: DrawShards(dc, width, height, glow, deep); break;
                case Motif.Waves: DrawWaves(dc, width, height, glow, deep); break;
            }

            // Soft vignette keeps the focus in the middle.
            var vignette = new RadialGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(150, 0, 0, 0)) { RadiusX = 0.8, RadiusY = 0.8 };
            dc.DrawRectangle(vignette, null, bounds);

            if (withTitle)
            {
                DrawTitle(dc, title, width, height);
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void DrawSun(DrawingContext dc, double w, double h, Color glow, Color deep)
    {
        var center = new Point(w / 2, h * 0.52);
        var radius = Math.Min(w, h) * 0.3;
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(120, glow.R, glow.G, glow.B), Color.FromArgb(0, glow.R, glow.G, glow.B)), null, center, radius * 2.2, radius * 2.2);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, center.Y + (radius * 0.15))));
        dc.DrawEllipse(new LinearGradientBrush(Lighten(glow, 0.45), glow, 90), null, center, radius, radius);
        dc.Pop();
        for (var i = 0; i < 6; i++)
        {
            var y = center.Y - (radius * 0.55) + (i * radius * 0.13);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(255, deep.R, deep.G, deep.B)), null, new Rect(center.X - radius, y, radius * 2, 2 + (i * 1.4)));
        }

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(110, glow.R, glow.G, glow.B)), 1.5);
        var horizon = center.Y + (radius * 0.15);
        dc.DrawLine(new Pen(new SolidColorBrush(glow), 2), new Point(0, horizon), new Point(w, horizon));
        for (var i = 1; i < 12; i++)
        {
            var y = horizon + (Math.Pow(i / 11.0, 1.8) * (h - horizon));
            dc.DrawLine(pen, new Point(0, y), new Point(w, y));
        }

        for (var i = -10; i <= 10; i++)
        {
            dc.DrawLine(pen, new Point(center.X + (i * w * 0.02), horizon), new Point(center.X + (i * w * 0.16), h));
        }
    }

    private static void DrawMountains(DrawingContext dc, double w, double h, Color glow, Color deep, Color mid)
    {
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(230, glow.R, glow.G, glow.B)), null, new Point(w * 0.68, h * 0.3), w * 0.09, w * 0.09);
        for (var layer = 0; layer < 4; layer++)
        {
            var baseY = h * (0.5 + (layer * 0.12));
            var color = Mix(mid, deep, 0.25 + (layer * 0.25));
            var figure = new PathFigure { StartPoint = new Point(0, h), IsClosed = true };
            var x = -w * 0.1;
            var up = layer % 2 == 0;
            var k = 0;
            while (x < w * 1.1)
            {
                var step = w * (0.12 + ((((layer * 7) + k++) % 5) * 0.03));
                figure.Segments.Add(new LineSegment(new Point(x, up ? baseY - (h * (0.18 - (layer * 0.03))) : baseY), true));
                up = !up;
                x += step;
            }

            figure.Segments.Add(new LineSegment(new Point(w * 1.1, h), true));
            dc.DrawGeometry(new SolidColorBrush(color), null, new PathGeometry([figure]));
        }
    }

    private static void DrawRings(DrawingContext dc, double w, double h, Color glow)
    {
        var center = new Point(w * 0.55, h * 0.42);
        for (var i = 0; i < 9; i++)
        {
            var r = Math.Min(w, h) * (0.08 + (i * 0.07));
            var alpha = (byte)Math.Max(30, 200 - (i * 20));
            dc.PushTransform(new RotateTransform(-18, center.X, center.Y));
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(alpha, glow.R, glow.G, glow.B)), i == 3 ? 4 : 1.5), center, r * 1.6, r * 0.6);
            dc.Pop();
        }

        dc.DrawEllipse(new RadialGradientBrush(Lighten(glow, 0.6), glow), null, center, Math.Min(w, h) * 0.07, Math.Min(w, h) * 0.07);
        var random = new Random(w.GetHashCode());
        for (var i = 0; i < 90; i++)
        {
            var size = random.NextDouble() * 2.2;
            dc.DrawEllipse(Brushes.White, null, new Point(random.NextDouble() * w, random.NextDouble() * h), size, size);
        }
    }

    private static void DrawHills(DrawingContext dc, double w, double h, Color glow, Color deep, Color mid)
    {
        dc.DrawEllipse(new SolidColorBrush(Lighten(glow, 0.3)), null, new Point(w * 0.25, h * 0.26), w * 0.07, w * 0.07);
        for (var i = 0; i < 4; i++)
        {
            var color = Mix(Lighten(mid, 0.2), deep, i * 0.28);
            var cy = h * (0.62 + (i * 0.11));
            dc.DrawEllipse(new SolidColorBrush(color), null, new Point(w * (i % 2 == 0 ? 0.25 : 0.8), cy + (h * 0.35)), w * 0.9, h * 0.4);
        }
    }

    private static void DrawShards(DrawingContext dc, double w, double h, Color glow, Color deep)
    {
        var random = new Random((int)(w + h));
        for (var i = 0; i < 14; i++)
        {
            var x = random.NextDouble() * w;
            var top = random.NextDouble() * h * 0.4;
            var width = w * (0.02 + (random.NextDouble() * 0.08));
            var figure = new PathFigure { StartPoint = new Point(x, top), IsClosed = true };
            figure.Segments.Add(new LineSegment(new Point(x + width, top + (h * 0.1)), true));
            figure.Segments.Add(new LineSegment(new Point(x + (width * 0.6), h), true));
            figure.Segments.Add(new LineSegment(new Point(x - (width * 0.4), h), true));
            var alpha = (byte)(60 + random.Next(0, 150));
            dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(alpha, glow.R, glow.G, glow.B), Color.FromArgb(0, deep.R, deep.G, deep.B), 90), null, new PathGeometry([figure]));
        }
    }

    private static void DrawWaves(DrawingContext dc, double w, double h, Color glow, Color deep)
    {
        for (var i = 0; i < 9; i++)
        {
            var y0 = h * (0.35 + (i * 0.075));
            var figure = new PathFigure { StartPoint = new Point(0, y0) };
            for (var x = 0.0; x <= w; x += w / 40)
            {
                figure.Segments.Add(new LineSegment(new Point(x, y0 + (Math.Sin((x / w * Math.PI * 3) + i) * h * 0.03)), true));
            }

            var alpha = (byte)(220 - (i * 18));
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(alpha, glow.R, glow.G, glow.B)), 2 + (i * 0.4)), new PathGeometry([figure]));
        }

        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, glow.R, glow.G, glow.B)), null, new Point(w * 0.72, h * 0.2), w * 0.05, w * 0.05);
    }

    private static void DrawTitle(DrawingContext dc, string title, double w, double h)
    {
        var face = new Typeface(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.Bold, FontStretches.SemiCondensed);
        var text = new FormattedText(title.ToUpperInvariant(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, w * 0.12, Brushes.White, 1.0)
        {
            MaxTextWidth = w * 0.84,
            TextAlignment = TextAlignment.Left,
            LineHeight = w * 0.12,
        };
        var origin = new Point(w * 0.08, h - text.Height - (h * 0.08));
        var shadow = text.BuildGeometry(new Point(origin.X, origin.Y + 3));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), null, shadow);
        dc.DrawText(text, origin);
    }

    private static Color Lighten(Color c, double amount) => Mix(c, Colors.White, amount);

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + ((b.R - a.R) * t)),
        (byte)(a.G + ((b.G - a.G) * t)),
        (byte)(a.B + ((b.B - a.B) * t)));
}
