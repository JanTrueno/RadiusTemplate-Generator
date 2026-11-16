using System.Globalization;
using System.Text;

namespace RadiusMal;

/// <summary>
/// Writes a DXF (R12 / AC1009) with the layers DECOUPE_EXTERNE, DECOUPE_INTERNE and GRAVURE.
/// </summary>
public sealed class DxfWriter
{
    private readonly StringBuilder _sb = new();
    private long _nextHandle = 0x1000;
    private readonly List<(double X, double Y)> _extents = new();

    public void Add(LineEnt line)
    {
        CheckPair(line.A, line.B);
        Begin("LINE", line.Layer);
        WritePoint(10, line.A);
        WritePoint(11, line.B);
    }

    public void Add(ArcEnt arc)
    {
        AddCircleExtents(arc.Center, arc.Radius);
        Begin("ARC", arc.Layer);
        WritePoint(10, arc.Center);
        _sb.AppendLine(" 40").AppendLine(F(arc.Radius));
        _sb.AppendLine(" 50").AppendLine(F(NormalizeAngle(arc.StartDeg)));
        _sb.AppendLine(" 51").AppendLine(F(NormalizeAngle(arc.EndDeg)));
    }

    public void Add(CircleEnt circle)
    {
        AddCircleExtents(circle.Center, circle.Radius);
        Begin("CIRCLE", circle.Layer);
        WritePoint(10, circle.Center);
        _sb.AppendLine(" 40").AppendLine(F(circle.Radius));
    }

    public void Add(TemplateResult template)
    {
        _extents.Add((0, 0));
        _extents.Add((template.Width, template.Height));
        foreach (var line in template.OuterLines) Add(line);
        foreach (var arc in template.OuterArcs) Add(arc);
        foreach (var hole in template.Holes) Add(hole);
        foreach (var line in template.Engraving) Add(line);
    }

    public void Save(string path)
    {
        var minX = _extents.Min(p => p.X);
        var minY = _extents.Min(p => p.Y);
        var maxX = _extents.Max(p => p.X);
        var maxY = _extents.Max(p => p.Y);

        var sb = new StringBuilder(LoadSkeleton());
        sb.Replace("{EXTMINX}", F(minX)).Replace("{EXTMINY}", F(minY))
          .Replace("{EXTMAXX}", F(maxX)).Replace("{EXTMAXY}", F(maxY))
          .Replace("{HANDSEED}", (_nextHandle + 16).ToString("X", CultureInfo.InvariantCulture));

        sb.AppendLine("  0").AppendLine("SECTION");
        sb.AppendLine("  2").AppendLine("ENTITIES");
        sb.Append(_sb);
        sb.AppendLine("  0").AppendLine("ENDSEC");
        sb.AppendLine("  0").AppendLine("EOF");

        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
    }

    /// <summary>
    /// Full R12 skeleton (header, tables, blocks) that AutoCAD and Inventor accept.
    /// A stripped-down header/tables set was rejected by Inventor.
    /// </summary>
    private static string LoadSkeleton()
    {
        using var stream = typeof(DxfWriter).Assembly.GetManifestResourceStream("RadiusMal.skeleton_r12.dxf")
            ?? throw new InvalidOperationException("skeleton_r12.dxf is missing as an embedded resource.");
        using var reader = new StreamReader(stream, Encoding.ASCII);
        return reader.ReadToEnd().Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    private void Begin(string type, string layer)
    {
        _sb.AppendLine("  0").AppendLine(type);
        _sb.AppendLine("  5").AppendLine(_nextHandle++.ToString("X", CultureInfo.InvariantCulture));
        _sb.AppendLine("  8").AppendLine(layer);
    }

    private void WritePoint(int codeBase, Pt p)
    {
        _sb.AppendLine(codeBase.ToString(CultureInfo.InvariantCulture).PadLeft(3)).AppendLine(F(p.X));
        _sb.AppendLine((codeBase + 10).ToString(CultureInfo.InvariantCulture).PadLeft(3)).AppendLine(F(p.Y));
        _sb.AppendLine((codeBase + 20).ToString(CultureInfo.InvariantCulture).PadLeft(3)).AppendLine("0.0");
    }

    private void AddCircleExtents(Pt center, double radius)
    {
        _extents.Add((center.X - radius, center.Y - radius));
        _extents.Add((center.X + radius, center.Y + radius));
    }

    private void CheckPair(Pt a, Pt b)
    {
        _extents.Add((a.X, a.Y));
        _extents.Add((b.X, b.Y));
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;
        if (angle < 0) angle += 360.0;
        return angle;
    }

    private static string F(double value) =>
        value.ToString("0.########", CultureInfo.InvariantCulture);
}
