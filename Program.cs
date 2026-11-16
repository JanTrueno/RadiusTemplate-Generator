using System.Globalization;
using System.Text;

namespace RadiusMal;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            if (args.Any(a => a is "-h" or "--help" or "/?"))
            {
                PrintHelp();
                return 0;
            }

            var parsed = ParseArgs(args);
            var radii = new List<double>(parsed.Radii);
            var interactive = radii.Count == 0;

            if (interactive)
            {
                if (Console.IsInputRedirected)
                {
                    PrintHelp();
                    return 1;
                }

                PrintBanner();
                Console.Write("Radius(es) in mm (several allowed, separated by spaces): ");
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("No radius entered.");
                    Pause();
                    return 1;
                }

                foreach (var token in input.Split(new[] { ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!TryParseNumber(token, out var value))
                    {
                        Console.Error.WriteLine($"Invalid radius: '{token}'");
                        Pause();
                        return 1;
                    }
                    radii.Add(value);
                }
            }
            else
            {
                PrintBanner();
            }

            var success = true;
            foreach (var radius in radii)
            {
                try
                {
                    success &= Generate(radius, parsed.Options, parsed.OutputDirectory);
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"R{radius:0.###}: {ex.Message}");
                    success = false;
                }
            }

            if (interactive || parsed.Pause)
                Pause();

            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }

    private static bool Generate(double radius, TemplateOptions options, string? outputDirectory)
    {
        var template = RadiusTemplate.Build(radius, options);

        var writer = new DxfWriter();
        writer.Add(template);

        var fileName = $"RadiusTemplate_R{RadiusTemplate.FormatRadius(radius, decimalComma: false)}.dxf";
        var directory = outputDirectory ?? Directory.GetCurrentDirectory();
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        writer.Save(path);

        Console.WriteLine();
        Console.WriteLine($"R{Format(template.Radius)}  ->  {Format(template.AngleDeg)}° arc");
        Console.WriteLine($"  template   : {Format(template.Width)} x {Format(template.Height)} mm");
        Console.WriteLine($"  ear length : {Format(template.EarLength)} mm");
        if (template.Holes.Count > 0)
            Console.WriteLine($"  hole       : {template.Holes.Count} x Ø{Format(template.HoleDiameter)} mm, inset {Format(template.HoleInset)} mm");
        else
            Console.WriteLine("  hole       : none");
        if (template.Engraving.Count > 0)
            Console.WriteLine($"  engraving  : \"{template.Label}\", {template.Engraving.Count} lines, height {Format(template.TextHeight)} mm");
        else
            Console.WriteLine("  engraving  : none");
        Console.WriteLine($"  file       : {path}");

        foreach (var warning in template.Warnings)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  WARNING: " + warning);
            Console.ForegroundColor = oldColor;
        }

        Console.WriteLine($"  done       : {template.OuterLines.Count} cut lines, {template.OuterArcs.Count} arc, " +
                          $"{template.Holes.Count} hole, {template.Engraving.Count} engraving lines");
        return true;
    }

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.CurrentCulture);

    private sealed class ParsedArgs
    {
        public List<double> Radii { get; } = new();
        public string? OutputDirectory { get; set; }
        public bool Pause { get; set; }
        public TemplateOptions Options { get; set; } = new();
    }

    private static ParsedArgs ParseArgs(string[] args)
    {
        var result = new ParsedArgs();

        double? angle = null;
        double? ear = null;
        double? holeDia = null;
        double? holeInset = null;
        double? textHeight = null;
        string? label = null;
        var noText = false;
        var noHole = false;
        var bothHoles = false;
        var comma = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-o":
                case "--out":
                    result.OutputDirectory = NextValue(args, ref i, arg);
                    break;
                case "-a":
                case "--angle":
                    angle = NextNumber(args, ref i, arg);
                    break;
                case "-e":
                case "--ear":
                    ear = NextNumber(args, ref i, arg);
                    break;
                case "--hole-dia":
                    holeDia = NextNumber(args, ref i, arg);
                    break;
                case "--hole-inset":
                    holeInset = NextNumber(args, ref i, arg);
                    break;
                case "--text-height":
                    textHeight = NextNumber(args, ref i, arg);
                    break;
                case "--text":
                    label = NextValue(args, ref i, arg);
                    break;
                case "--no-text":
                    noText = true;
                    break;
                case "--no-hole":
                    noHole = true;
                    break;
                case "--both-holes":
                    bothHoles = true;
                    break;
                case "--comma":
                    comma = true;
                    break;
                case "--pause":
                    result.Pause = true;
                    break;
                default:
                    if (arg.StartsWith('-') && !TryParseNumber(arg, out _))
                        throw new ArgumentException($"Unknown option: {arg}. Use --help for usage.");
                    if (!TryParseNumber(arg, out var radius))
                        throw new ArgumentException($"Invalid radius: '{arg}'.");
                    result.Radii.Add(radius);
                    break;
            }
        }

        result.Options = new TemplateOptions
        {
            AngleDeg = angle,
            EarLength = ear,
            HoleDiameter = holeDia,
            HoleInset = holeInset,
            TextHeight = textHeight,
            Label = label,
            NoText = noText,
            NoHole = noHole,
            HoleBothCorners = bothHoles,
            DecimalComma = comma,
        };

        return result;
    }

    private static string NextValue(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Option {option} needs a value.");
        return args[++i];
    }

    private static double NextNumber(string[] args, ref int i, string option)
    {
        var value = NextValue(args, ref i, option);
        if (!TryParseNumber(value, out var number))
            throw new ArgumentException($"Invalid value for {option}: '{value}'.");
        return number;
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(
            text.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value) && double.IsFinite(value);

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter to close...");
        Console.ReadLine();
    }

    private static void PrintBanner()
    {
        Console.WriteLine("RadiusMal 1.0  -  radius template to DXF");
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        PrintBanner();
        Console.WriteLine("""
Usage:
  RadiusMal 317.5
  RadiusMal 50 100 200 300 500 -o C:\Out
  RadiusMal 150 --angle 45 --text-height 10

Without arguments the program asks for a radius.
One DXF file is written per radius: RadiusTemplate_R<radius>.dxf

Options:
  -o, --out <folder>      Output folder (default: current folder)
  -a, --angle <degrees>   Force the arc angle. Default is automatic:
                            R <= 100  -> 90 degrees
                            R <= 200  -> 60 degrees
                            R <= 300  -> 45 degrees
                            R >  300  -> 30 degrees
  -e, --ear <mm>          Ear length (default 25 mm, scales down below R100)
      --hole-dia <mm>     Hole diameter (default 10 mm)
      --hole-inset <mm>   Distance from hole centre to top and side edge (default 15 mm)
      --text-height <mm>  Engraving text height (default 12.7 mm)
      --text <text>       Custom engraving text (default "R<radius>")
      --no-text           No engraving text
      --no-hole           No hole
      --both-holes        Hole in both top corners
      --comma             Decimal comma in the engraving text (R317,5 instead of R317.5)
      --pause             Wait for Enter when finished
  -h, --help              This help

Layers in the DXF:
  DECOUPE_EXTERNE   outer contour (cut outside)
  DECOUPE_INTERNE   corner hole (cut inside)
  GRAVURE           engraving text
""");
    }
}
