# RadiusTemplate Generator

A small Windows console program that turns a radius into a **radius template (radius gauge) as a DXF file**, ready for laser cutting:

- a convex radius arc at the bottom (the measuring edge);
- two straight "ears" of 25 mm, perpendicular to the arc;
- a flat top edge;
- a hole in the top-right corner (inner contour);
- engraving text `R<radius>` as text outlines.

One DXF file is written per radius: `RadiusTemplate_R<radius>.dxf`.

The engraving uses the outlines of the Arial font (via GDI+), so the program runs on Windows. Everything else is plain geometry.

## Quick start

Download `RadiusMal.exe` from the [Releases](../../releases) page (self-contained, no .NET install needed), then:

```text
RadiusMal.exe 317.5
```

or double-click it, type the radius and press Enter.

Several radii at once:

```text
RadiusMal.exe 25 50 100 200 300 500 -o C:\Out
```

A decimal comma works as input too: `RadiusMal 317,5`.

## Arc angle

The arc angle is chosen automatically:

| Radius        | Arc  |
|---------------|------|
| R ≤ 100 mm    | 90°  |
| R ≤ 200 mm    | 60°  |
| R ≤ 300 mm    | 45°  |
| R > 300 mm    | 30°  |

Override it with `--angle`, e.g. `RadiusMal 317.5 --angle 60`.

## Fixed dimensions and small radii

Above R100 the default dimensions are:

| Feature          | Size |
|------------------|------|
| Ear length       | 25 mm |
| Hole             | Ø10 mm, 15 mm from top edge and side edge |
| Engraving text   | 12.7 mm cap height |

Below R100 these dimensions scale with `R / 100` so the ear, hole and text always stay inside the template. All of them can be overridden with the options below.

The engraving text is automatically shifted or reduced if it would otherwise touch the hole (for example at R50).

## Options

```text
-o, --out <folder>       Output folder (default: current folder)
-a, --angle <degrees>    Force the arc angle
-e, --ear <mm>           Ear length (default 25)
    --hole-dia <mm>      Hole diameter (default 10)
    --hole-inset <mm>    Distance from hole centre to top/side edge (default 15)
    --text-height <mm>   Engraving text height (default 12.7)
    --text <text>        Custom engraving text (default "R<radius>")
    --no-text            No engraving text
    --no-hole            No hole
    --both-holes         Hole in both top corners
    --comma              Decimal comma in the engraving text (R317,5)
    --pause              Wait for Enter when finished
-h, --help               Help
```

## DXF layers

| Layer             | Content                      | Colour |
|-------------------|------------------------------|--------|
| `DECOUPE_EXTERNE` | outer contour (cut outside)  | white  |
| `DECOUPE_INTERNE` | corner hole (cut inside)     | white  |
| `GRAVURE`         | engraving text               | yellow |

The (French) layer names are kept because laser software maps operations to them.

The file is DXF R12 (AC1009) in millimetres, with a complete header, tables and blocks so that strict readers such as AutoCAD and Inventor accept it. Header variables `$INSUNITS = 4` and `$MEASUREMENT = 1` mark it as millimetres/metric. The outer contour is three `LINE` entities and one true `ARC` (not a polyline approximation); the engraving is made of short `LINE` segments.

## How the shape is built

A circular arc with radius `R` over the chosen angle `θ`, symmetric at the bottom. The ends of the arc are:

```text
P± = (±R·sin(θ/2), −R·cos(θ/2))
```

From each end an ear of length `E` runs radially inward. The top edge is the straight line between the two inner ear ends. The hole sits on the bisector of the top-right corner at the configured distance from the top and side edge. The engraving text is centred horizontally with its centre on the chord through the arc ends.

## Building from source

Requires the .NET 9 SDK.

```powershell
dotnet build -c Release
dotnet run -c Release -- 317.5 --angle 60
```

Or create the standalone Windows exe:

```powershell
.\build.ps1
```

This produces `dist\RadiusMal.exe`: one self-contained file (~10.5 MB, trimmed) that needs no .NET installation.

## Files

```text
RadiusMal.csproj     project
Program.cs           command line / console
RadiusTemplate.cs    template geometry
TextEngraver.cs      text -> engraving lines (Arial outlines)
DxfWriter.cs         DXF R12 writer
skeleton_r12.dxf     R12 header/tables/blocks (embedded resource)
RadiusMal.ico        application icon
build.ps1            publish script
Examples/            sample DXF files
```
