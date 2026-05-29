using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Xcs;

namespace MaestroGenXcs.Operations;

/// <summary>
/// Vŕtacia operácia – mriežka kolíkov cez <c>CreatePattern</c> + <c>CreateDrill</c>.
/// Mapuje sa 1:1 na to, čo robí <see cref="MaestroXcsBuilder.DrillPattern"/>.
/// </summary>
public sealed partial class DrillOperation : CncOperation
{
    [ObservableProperty]
    private int _countX = 1;

    [ObservableProperty]
    private int _countY = 1;

    [ObservableProperty]
    private double _pitchX = 32;

    [ObservableProperty]
    private double _pitchY = 32;

    [ObservableProperty]
    private double _xStart;

    [ObservableProperty]
    private double _yStart;

    [ObservableProperty]
    private double _diameter = 8;

    [ObservableProperty]
    private double _depth = 13;

    [ObservableProperty]
    private string _tool = "E071";

    [ObservableProperty]
    private string? _kindOfHole;

    [ObservableProperty]
    private int _dischargerStep = 3;

    [ObservableProperty]
    private string? _description;

    /// <summary>
    /// Pre UI – krátky popis typu („Kolík plocha" / „Kolík hrana"), aby sa to
    /// dalo v zozname operácií rozlíšiť.
    /// </summary>
    [ObservableProperty]
    private string _templateLabel = "";

    /// <summary>Bok L/P Right/Left: či propagovať aj na druhý bok (ak je spoj v mape).</summary>
    [ObservableProperty]
    private bool _preniestNaDruhyBok = true;

    /// <summary>Majster operácia pre traverzu – z nej sa generujú kolíky na bokoch.</summary>
    [ObservableProperty]
    private bool _isTraverzaMaster;

    /// <summary>Majster pevnej police – z nej sa obnoví celý balík kolíkov (polica + boky).</summary>
    [ObservableProperty]
    private bool _isPolicaMaster;

    [ObservableProperty]
    private double _traverzaBokXStart;

    [ObservableProperty]
    private double _traverzaBokYStart;

    /// <summary>True = pattern na hrane traverzy zľava–vpravo (X); false = zpredu–vzadu (Y).</summary>
    [ObservableProperty]
    private bool _traverzaPatternAlongX;

    public override string TypeLabel => string.IsNullOrWhiteSpace(TemplateLabel)
        ? "Vŕtanie"
        : $"Vŕtanie · {TemplateLabel}";

    public override string ToXcs(MaestroContext ctx)
    {
        _ = ctx;
        return MaestroXcsBuilder.DrillPattern(
            countX: CountX,
            countY: CountY,
            xStart: XStart,
            yStart: YStart,
            pitchX: PitchX,
            pitchY: PitchY,
            diameter: Diameter,
            depth: Depth,
            name: string.IsNullOrWhiteSpace(Name) ? "Drill" : Name,
            tool: Tool,
            workplane: Face.ToWorkplane(),
            referencePosition: RefPos,
            kindOfHole: KindOfHole,
            description: Description,
            dischargerStep: DischargerStep);
    }

    /// <summary>
    /// Body mriežky pre 2D/3D náhľad – vychádza zo zadanej <c>refpos</c>,
    /// počtu kolíkov a rozteče, identicky ako produkuje <c>CreatePattern</c>.
    /// Súradnice sú v lokálnej rovine dielca v ramci aktuálnej plochy.
    /// </summary>
    public IEnumerable<(double X, double Y)> EnumerateGridPoints()
    {
        for (var iy = 0; iy < Math.Max(1, CountY); iy++)
        for (var ix = 0; ix < Math.Max(1, CountX); ix++)
            yield return (XStart + ix * PitchX, YStart + iy * PitchY);
    }

    public override IEnumerable<VisualHint> BuildVisualHints(double dx, double dy, double dz)
    {
        if (Face is PartFace.Left or PartFace.Right or PartFace.Front or PartFace.Back)
        {
            foreach (var (wx, wy) in EnumerateEdgeTopViewPoints(dx, dy, dz))
            {
                yield return new HoleHint(
                    X: wx,
                    Y: wy,
                    Z: 0,
                    Face: Face,
                    Diameter: Diameter,
                    Depth: Depth,
                    Tag: "drill");
            }
            yield break;
        }

        // Top / Bottom – klasická projekcia s refpos v rohoch plášťa.
        foreach (var (gx, gy) in EnumerateGridPoints())
        {
            var (wx, wy) = TransformByRefpos(gx, gy, dx, dy);
            yield return new HoleHint(
                X: wx,
                Y: wy,
                Z: 0,
                Face: Face,
                Diameter: Diameter,
                Depth: Depth,
                Tag: "drill");
        }
    }

    /// <summary>
    /// Náhľad kolíkov v hrane – rovnaká logika ako starý
    /// <c>PreviewEdgeMarkers</c> + <c>Priecka.PripravKolikyDoBokov</c>.
    /// CreatePattern(…, layoutAngle=90) dáva rozostup do CountY; v 2D musí ísť
    /// pozdĺž hrany (Dy pre Ľavá/Pravá), nie do hrúbky materiálu.
    /// </summary>
    private IEnumerable<(double X, double Y)> EnumerateEdgeTopViewPoints(double dx, double dy, double dz)
    {
        _ = dz;
        var alongCount = CountY > 1 && CountX == 1 ? CountY : CountX;
        var alongPitch = CountY > 1 && CountX == 1 ? PitchY : PitchX;

        for (var i = 0; i < Math.Max(1, alongCount); i++)
        {
            var along = XStart + i * alongPitch;
            var fromPredna = RefPos is 1 or 3 ? dy - along : along;

            yield return Face switch
            {
                PartFace.Left or PartFace.Right => (fromPredna, YStart),
                PartFace.Front => (RefPos is 2 or 3 ? dx - along : along, YStart),
                PartFace.Back => (RefPos is 1 or 3 ? dx - along : along, YStart),
                _ => (fromPredna, YStart)
            };
        }
    }

    private (double X, double Y) TransformByRefpos(double localX, double localY, double dx, double dy)
    {
        // Pre Top plochu (a podobne pre hrany) interpretujeme refpos rovnako
        // ako v REFPOS_PATTERN_MAP zo starého projektu: pôvod je v zvolenom rohu,
        // os X ide pozdĺž šírky a os Y ide do vnútra dielca.
        return RefPos switch
        {
            0 => (localX,               localY),               // L-predný roh
            1 => (localX,               dy - localY),          // L-zadný roh
            2 => (dx - localX,          localY),               // P-predný roh
            3 => (dx - localX,          dy - localY),          // P-zadný roh
            _ => (localX,               localY)
        };
    }

    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture,
            "DrillOperation[{0} {1}×{2} @{3:0.#},{4:0.#} pitch {5:0.#}×{6:0.#} ø{7:0.#} d{8:0.#}]",
            Name, CountX, CountY, XStart, YStart, PitchX, PitchY, Diameter, Depth);
}
