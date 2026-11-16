using System.Globalization;

namespace RadiusMal;

/// <summary>Point in the XY plane (mm).</summary>
public readonly record struct Pt(double X, double Y)
{
    public static Pt operator +(Pt a, Pt b) => new(a.X + b.X, a.Y + b.Y);
    public static Pt operator -(Pt a, Pt b) => new(a.X - b.X, a.Y - b.Y);
    public static Pt operator *(Pt a, double s) => new(a.X * s, a.Y * s);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public Pt Normalized()
    {
        var len = Length;
        return len < 1e-12 ? new Pt(0, 0) : new Pt(X / len, Y / len);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X:0.###}, {Y:0.###})");
}

public sealed record LineEnt(string Layer, Pt A, Pt B);
public sealed record ArcEnt(string Layer, Pt Center, double Radius, double StartDeg, double EndDeg);
public sealed record CircleEnt(string Layer, Pt Center, double Radius);

/// <summary>Options for building a radius template.</summary>
public sealed class TemplateOptions
{
    /// <summary>Force the arc angle (degrees). null = automatic according to the radius rule.</summary>
    public double? AngleDeg { get; init; }

    /// <summary>Ear length in mm. null = 25 mm (scales down below R100).</summary>
    public double? EarLength { get; init; }

    /// <summary>Hole diameter in mm. null = 10 mm (scales down below R100).</summary>
    public double? HoleDiameter { get; init; }

    /// <summary>Distance from the hole centre to the top edge and the side edge, in mm.</summary>
    public double? HoleInset { get; init; }

    /// <summary>Cap height of the engraving text in mm.</summary>
    public double? TextHeight { get; init; }

    /// <summary>Custom engraving text. null = "R" + radius.</summary>
    public string? Label { get; init; }

    public bool NoText { get; init; }
    public bool NoHole { get; init; }

    /// <summary>Hole in both top corners instead of only the right one.</summary>
    public bool HoleBothCorners { get; init; }

    /// <summary>Decimal comma in the engraving text instead of a point.</summary>
    public bool DecimalComma { get; init; }
}

public sealed class TemplateResult
{
    public required double Radius { get; init; }
    public required double AngleDeg { get; init; }
    public required double EarLength { get; init; }
    public required double HoleDiameter { get; init; }
    public required double HoleInset { get; init; }
    public required double TextHeight { get; init; }
    public required string Label { get; init; }

    public required double Width { get; init; }
    public required double Height { get; init; }

    public required List<LineEnt> OuterLines { get; init; }
    public required List<ArcEnt> OuterArcs { get; init; }
    public required List<CircleEnt> Holes { get; init; }
    public required List<LineEnt> Engraving { get; init; }

    public required List<string> Warnings { get; init; }

    /// <summary>True when something does not fit well (shown in the console output).</summary>
    public bool HasWarnings => Warnings.Count > 0;
}

/// <summary>
/// Builds the radius template: a circular arc over the chosen angle,
/// two straight "ears" of 25 mm, a flat top, a hole and engraving text.
/// </summary>
public static class RadiusTemplate
{
    public const double DefaultEar = 25.0;
    public const double DefaultHoleDiameter = 10.0;
    public const double DefaultHoleInset = 15.0;
    public const double DefaultTextHeight = 12.7;

    /// <summary>Below this radius the fixed dimensions are scaled down proportionally.</summary>
    public const double FeatureScaleRadius = 100.0;

    /// <summary>Automatic angle according to the client specification.</summary>
    public static double AngleForRadius(double radius) => radius switch
    {
        <= 100.0 => 90.0,
        <= 200.0 => 60.0,
        <= 300.0 => 45.0,
        _ => 30.0,
    };

    public static string FormatRadius(double radius, bool decimalComma)
    {
        var s = radius.ToString("0.###", CultureInfo.InvariantCulture);
        return decimalComma ? s.Replace('.', ',') : s;
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(List<LineEnt> lines)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var line in lines)
        {
            minX = Math.Min(minX, Math.Min(line.A.X, line.B.X));
            maxX = Math.Max(maxX, Math.Max(line.A.X, line.B.X));
            minY = Math.Min(minY, Math.Min(line.A.Y, line.B.Y));
            maxY = Math.Max(maxY, Math.Max(line.A.Y, line.B.Y));
        }

        return (minX, minY, maxX, maxY);
    }

    public static TemplateResult Build(double radius, TemplateOptions options)
    {
        if (!double.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "The radius must be greater than 0.");

        var angle = options.AngleDeg ?? AngleForRadius(radius);
        if (!double.IsFinite(angle) || angle < 5 || angle > 170)
            throw new ArgumentOutOfRangeException(nameof(options), "The angle must be between 5 and 170 degrees.");

        // For small radii the ear, hole and text would fall outside the template,
        // so they scale down below R100; from R100 upwards the fixed sizes are used.
        var featureScale = Math.Min(1.0, radius / FeatureScaleRadius);

        var ear = options.EarLength ?? DefaultEar * featureScale;
        var holeDia = options.HoleDiameter ?? DefaultHoleDiameter * featureScale;
        var holeInset = options.HoleInset ?? DefaultHoleInset * featureScale;
        var textHeight = options.TextHeight ?? DefaultTextHeight * featureScale;
        var label = options.Label ?? "R" + FormatRadius(radius, options.DecimalComma);

        if (ear <= 0) throw new ArgumentOutOfRangeException(nameof(options), "The ear length must be greater than 0.");
        if (ear >= radius * 0.95)
            throw new ArgumentOutOfRangeException(nameof(options),
                $"The ear length ({ear:0.###} mm) is too large for R{radius:0.###}. " +
                "Use a smaller --ear or a larger radius.");

        if (holeDia < 0) throw new ArgumentOutOfRangeException(nameof(options), "The hole diameter cannot be negative.");
        if (holeInset <= 0) throw new ArgumentOutOfRangeException(nameof(options), "The hole inset must be greater than 0.");
        if (textHeight < 0) throw new ArgumentOutOfRangeException(nameof(options), "The text height cannot be negative.");

        var half = angle * Math.PI / 360.0; // half angle in radians
        var sin = Math.Sin(half);
        var cos = Math.Cos(half);

        // Local frame: arc centre at (0,0), arc pointing downwards.
        var arcStart = 270.0 - angle / 2.0;
        var arcEnd = 270.0 + angle / 2.0;

        var pRight = new Pt(radius * sin, -radius * cos);     // end of arc, right
        var pLeft = new Pt(-radius * sin, -radius * cos);     // end of arc, left

        var innerRadius = radius - ear;
        var qRight = new Pt(innerRadius * sin, -innerRadius * cos);   // inner end of ear, right
        var qLeft = new Pt(-innerRadius * sin, -innerRadius * cos);

        var yTop = -innerRadius * cos;
        var yBottom = -radius;
        var halfWidth = radius * sin;

        // Shift everything so the bottom-left corner of the template is at (0,0).
        var shift = new Pt(halfWidth, radius);
        Pt T(Pt p) => p + shift;

        var outerLines = new List<LineEnt>
        {
            new("DECOUPE_EXTERNE", T(pLeft), T(qLeft)),
            new("DECOUPE_EXTERNE", T(qLeft), T(qRight)),
            new("DECOUPE_EXTERNE", T(qRight), T(pRight)),
        };
        var outerArcs = new List<ArcEnt>
        {
            new("DECOUPE_EXTERNE", T(new Pt(0, 0)), radius, arcStart, arcEnd),
        };

        var holes = new List<CircleEnt>();
        var warnings = new List<string>();

        if (!options.NoHole && holeDia > 0)
        {
            var holeRadius = holeDia / 2.0;

            // Top-right corner: intersection of the flat top edge and the ear.
            // The hole sits on the bisector, equally far from both edges.
            var dirTop = (qLeft - qRight).Normalized();
            var dirSide = (pRight - qRight).Normalized();
            var bisector = (dirTop + dirSide).Normalized();
            var cornerAngle = Math.Acos(Math.Clamp(dirTop.X * dirSide.X + dirTop.Y * dirSide.Y, -1.0, 1.0));
            var bisectorDistance = holeInset / Math.Sin(cornerAngle / 2.0);

            var holeRightLocal = qRight + bisector * bisectorDistance;
            var localHoles = new List<Pt> { holeRightLocal };
            if (options.HoleBothCorners)
                localHoles.Add(new Pt(-holeRightLocal.X, holeRightLocal.Y));

            foreach (var holeLocal in localHoles)
            {
                var holeCenter = T(holeLocal);
                holes.Add(new CircleEnt("DECOUPE_INTERNE", holeCenter, holeRadius));

                // Check that the hole sits well inside the template.
                var arcDistance = radius - holeLocal.Length;
                if (holeInset - holeRadius < 0.25)
                    warnings.Add($"Hole Ø{holeDia:0.###} almost touches the top/side edge (inset {holeInset:0.###} mm).");
                if (arcDistance - holeRadius < 0.25)
                    warnings.Add($"Hole Ø{holeDia:0.###} almost touches the radius arc.");
            }
        }

        var engraving = new List<LineEnt>();
        if (!options.NoText && textHeight > 0 && !string.IsNullOrWhiteSpace(label))
        {
            // Text is centred horizontally on the template, with its centre on the chord
            // (the imaginary line between the two ends of the arc).
            var textCenter = T(new Pt(0, -radius * cos));
            engraving = TextEngraver.Build(label, "GRAVURE", textHeight, textCenter);

            if (engraving.Count > 0)
            {
                var textBounds = Bounds(engraving);
                var textWidth = textBounds.MaxX - textBounds.MinX;
                var clearance = 2.0 * featureScale;
                var availableLeft = 2.0;
                var availableRight = 2 * halfWidth - 2.0;

                // Keep the text clear of the hole(s) when they are in the way horizontally.
                foreach (var hole in holes)
                {
                    var overlapsVertically =
                        hole.Center.Y + hole.Radius + clearance >= textBounds.MinY &&
                        hole.Center.Y - hole.Radius - clearance <= textBounds.MaxY;
                    if (!overlapsVertically) continue;

                    if (hole.Center.X >= halfWidth)
                        availableRight = Math.Min(availableRight, hole.Center.X - hole.Radius - clearance);
                    else
                        availableLeft = Math.Max(availableLeft, hole.Center.X + hole.Radius + clearance);
                }

                var availableWidth = Math.Max(0, availableRight - availableLeft);
                var finalHeight = textHeight;
                if (textWidth > availableWidth && availableWidth > 1.0)
                    finalHeight = textHeight * availableWidth / textWidth;

                var resized = Math.Abs(finalHeight - textHeight) > 1e-9;
                double centerX;
                if (resized)
                {
                    centerX = (availableLeft + availableRight) / 2;
                }
                else
                {
                    centerX = halfWidth;
                    if (centerX + textWidth / 2 > availableRight)
                        centerX = availableRight - textWidth / 2;
                    if (centerX - textWidth / 2 < availableLeft)
                        centerX = availableLeft + textWidth / 2;
                }

                var moved = Math.Abs(centerX - halfWidth) > 1e-9;
                var shrunkenALot = resized && finalHeight < textHeight * 0.75;
                if (moved || resized)
                {
                    engraving = TextEngraver.Build(label, "GRAVURE", finalHeight,
                        new Pt(centerX, textCenter.Y));
                    textHeight = finalHeight;
                }

                if (shrunkenALot)
                    warnings.Add("The engraving text was reduced to stay clear of the hole.");

                var minX = engraving.Min(l => Math.Min(l.A.X, l.B.X));
                var maxX = engraving.Max(l => Math.Max(l.A.X, l.B.X));
                if (maxX - minX > 2 * halfWidth * 0.94)
                    warnings.Add("The engraving text is almost wider than the template; use a smaller --text-height or shorter text.");
            }
        }

        return new TemplateResult
        {
            Radius = radius,
            AngleDeg = angle,
            EarLength = ear,
            HoleDiameter = holeDia,
            HoleInset = holeInset,
            TextHeight = textHeight,
            Label = label,
            Width = 2 * halfWidth,
            Height = yTop - yBottom,
            OuterLines = outerLines,
            OuterArcs = outerArcs,
            Holes = holes,
            Engraving = engraving,
            Warnings = warnings,
        };
    }
}
