using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace RadiusMal;

/// <summary>
/// Converts text into engraving lines using the outlines of a TrueType
/// font (Arial by default).
/// </summary>
public static class TextEngraver
{
    private const float EmSize = 1000f;
    private const float FlattenTolerance = 0.5f;

    public static List<LineEnt> Build(string text, string layer, double capHeight, Pt center)
    {
        var result = new List<LineEnt>();
        if (string.IsNullOrWhiteSpace(text) || capHeight <= 0)
            return result;

        using var family = ResolveFontFamily();
        using var path = new GraphicsPath();

        path.AddString(
            text,
            family,
            (int)FontStyle.Regular,
            EmSize,
            PointF.Empty,
            StringFormat.GenericTypographic);

        if (path.PointCount == 0)
            return result;

        var bounds = path.GetBounds();
        if (bounds.Height <= 0 || bounds.Width <= 0)
            return result;

        var scale = capHeight / bounds.Height;
        var centerX = bounds.Left + bounds.Width / 2.0;
        var centerY = bounds.Top + bounds.Height / 2.0;

        using var flattened = (GraphicsPath)path.Clone();
        flattened.Flatten(null, FlattenTolerance);

        var points = flattened.PathPoints;
        var types = flattened.PathTypes;

        var i = 0;
        while (i < points.Length)
        {
            var start = i;
            var end = i + 1;
            while (end < points.Length && (types[end] & 0x07) != 0)
                end++;

            for (var k = start; k < end - 1; k++)
                AddSegment(result, layer, points[k], points[k + 1], scale, centerX, centerY, center);

            // Closed contour (the last point carries the close flag).
            if (end - 1 > start && (types[end - 1] & 0x80) != 0)
                AddSegment(result, layer, points[end - 1], points[start], scale, centerX, centerY, center);

            i = end;
        }

        return result;
    }

    private static void AddSegment(
        List<LineEnt> target,
        string layer,
        PointF a,
        PointF b,
        double scale,
        double centerX,
        double centerY,
        Pt center)
    {
        var ax = center.X + (a.X - centerX) * scale;
        var ay = center.Y - (a.Y - centerY) * scale; // DXF Y points up
        var bx = center.X + (b.X - centerX) * scale;
        var by = center.Y - (b.Y - centerY) * scale;

        var dx = bx - ax;
        var dy = by - ay;
        if (dx * dx + dy * dy < 1e-12)
            return;

        target.Add(new LineEnt(layer, new Pt(ax, ay), new Pt(bx, by)));
    }

    private static FontFamily ResolveFontFamily()
    {
        foreach (var name in new[] { "Arial", "Helvetica", "Segoe UI", "Tahoma" })
        {
            try
            {
                return new FontFamily(name);
            }
            catch (ArgumentException)
            {
                // Font not installed; try the next one.
            }
        }

        return new FontFamily(GenericFontFamilies.SansSerif);
    }
}
