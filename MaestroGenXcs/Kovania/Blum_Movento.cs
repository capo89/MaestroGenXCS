using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Kovania;

/// <summary>
/// Blum Movento – montážne vŕtanie na plochu <c>Top</c> boku L/P skrinky.
/// </summary>
public static class Blum_Movento
{
    public const string Nazov = "Blum Movento";

    public const PartFace VrtaciaPlocha = PartFace.Top;

    public const double PriemerMm = 3.0;
    public const double HlbkaMm = 3.0;

    /// <summary>Špicatý vrták – hodnota <c>kindOfHole</c> pre Maestro XCS.</summary>
    public const string SpicatyVrtakKindOfHole = "S";

    public const string DefaultTool = "P001";
    public const int DefaultDischargerStep = 3;

    /// <summary>
    /// Fixný prídavok k výške sekcie na <c>Top</c> (mm) – <c>YStart = VyskaMm + 38,5</c>.
    /// </summary>
    public const double HranaOdSpoduMm = 38.5;

    /// <summary>Prvý pár dier – vzdialenosť od prednej hrany (os Y, mm).</summary>
    public const double PrvyParOdPrednejHranyMm = 37.0;

    /// <summary>Druhá skupina dier – základ od prednej hrany (os Y, mm).</summary>
    public const double DruhyParOdPrednejHranyMm = 261.0;

    /// <summary>Rozteč dvoch dier v páre pozdĺž osi Y (mm) – druhá diera = základ + 32.</summary>
    public const int PocetDierVParu = 2;

    public const double ParRoztecOsYMm = 32.0;

    /// <summary>Bok L → refpos 2; Bok P → refpos 0 (plocha Top).</summary>
    public static int ResolveRefPos(PartKind bok) => bok switch
    {
        PartKind.BokL => 2,
        PartKind.BokP => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(bok), bok, "Movento sa vŕta len na Bok L alebo Bok P."),
    };

    /// <summary>Vráti vzdialenosti od prednej hrany pre jednotlivé vŕtacie skupiny.</summary>
    public static IReadOnlyList<MoventoVrtaciaSkupina> ResolveVrtacieSkupiny(MoventoDlzkaKovania dlzkaKovania)
    {
        var skupiny = new List<MoventoVrtaciaSkupina>
        {
            new(PrvyParOdPrednejHranyMm, JePar: true),
        };

        // 270–600: druhý pár Y=261 a Y=261+32; 250–270: iba jedna diera Y=261.
        skupiny.Add(new(DruhyParOdPrednejHranyMm, JePar: dlzkaKovania == MoventoDlzkaKovania.Dlha270_600));

        return skupiny;
    }

    /// <summary>
    /// Vŕtacie operácie pre jeden bok – <c>Top</c> + príslušný <c>refpos</c>.
    /// Pár: <c>CreatePattern(2,1,32,32)</c> + <c>CreateDrill</c>;
    /// jedna diera: iba <c>CreateDrill</c> (bez patternu).
    /// </summary>
    public static IReadOnlyList<MoventoVrtaciPattern> ResolveVrtaciePatternyPreBok(
        PartKind bok,
        double sekciaVyskaMm,
        double sekciaOdsadenieOdPreduMm,
        MoventoDlzkaKovania dlzkaKovania)
    {
        var refPos = ResolveRefPos(bok);
        var yStart = sekciaVyskaMm + HranaOdSpoduMm;
        var patterny = new List<MoventoVrtaciPattern>();

        foreach (var skupina in ResolveVrtacieSkupiny(dlzkaKovania))
        {
            patterny.Add(new(
                Bok: bok,
                Face: VrtaciaPlocha,
                RefPos: refPos,
                PouzivaPattern: skupina.JePar,
                CountX: skupina.JePar ? PocetDierVParu : 1,
                CountY: 1,
                PitchX: ParRoztecOsYMm,
                PitchY: ParRoztecOsYMm,
                XStart: sekciaOdsadenieOdPreduMm + skupina.OdPrednejHranyMm,
                YStart: yStart));
        }

        return patterny;
    }

    /// <summary>Patterny pre Bok L aj Bok P v jednej sekcii.</summary>
    public static IReadOnlyList<MoventoVrtaciPattern> ResolveVrtaciePatternyObochBokov(
        double sekciaVyskaMm,
        double sekciaOdsadenieOdPreduMm,
        MoventoDlzkaKovania dlzkaKovania)
    {
        var patterny = new List<MoventoVrtaciPattern>();
        patterny.AddRange(ResolveVrtaciePatternyPreBok(PartKind.BokL, sekciaVyskaMm, sekciaOdsadenieOdPreduMm, dlzkaKovania));
        patterny.AddRange(ResolveVrtaciePatternyPreBok(PartKind.BokP, sekciaVyskaMm, sekciaOdsadenieOdPreduMm, dlzkaKovania));
        return patterny;
    }

    /// <summary>Rozvinuté body z patternov oboch bokov – pre 2D/3D náhľad.</summary>
    public static IReadOnlyList<MoventoVrtaciBod> ResolveVrtacieBody(
        double sekciaVyskaMm,
        double sekciaOdsadenieOdPreduMm,
        MoventoDlzkaKovania dlzkaKovania)
    {
        var body = new List<MoventoVrtaciBod>();
        foreach (var pattern in ResolveVrtaciePatternyObochBokov(sekciaVyskaMm, sekciaOdsadenieOdPreduMm, dlzkaKovania))
            body.AddRange(pattern.EnumerateBody());
        return body;
    }
}

/// <summary>Rozsah dĺžky kovania Movento (výber v combo boxe).</summary>
public enum MoventoDlzkaKovania
{
    Kratka250_270 = 0,
    Dlha270_600 = 1,
}

/// <summary>Položka pre combo box dĺžky kovania.</summary>
public sealed record MoventoDlzkaKovaniaChoice(MoventoDlzkaKovania Hodnota, string Label)
{
    public static IReadOnlyList<MoventoDlzkaKovaniaChoice> Vsetky { get; } =
    [
        new(MoventoDlzkaKovania.Kratka250_270, "250–270"),
        new(MoventoDlzkaKovania.Dlha270_600, "270–600"),
    ];
}

/// <summary>Jedna montážna skupina – pár alebo jedna diera.</summary>
public readonly record struct MoventoVrtaciaSkupina(double OdPrednejHranyMm, bool JePar);

/// <summary>
/// Jedna vŕtacia operácia na <c>Top</c>.
/// Pár: <c>CreatePattern(2,1,32,32)</c> + <c>CreateDrill</c>;
/// jedna diera: iba <c>CreateDrill</c> (<see cref="PouzivaPattern"/> = false).
/// <see cref="XStart"/>/<see cref="PitchX"/> = Maestro os po hĺbke; <see cref="YStart"/> = výška (pevná).
/// </summary>
public readonly record struct MoventoVrtaciPattern(
    PartKind Bok,
    PartFace Face,
    int RefPos,
    bool PouzivaPattern,
    int CountX,
    int CountY,
    double PitchX,
    double PitchY,
    double XStart,
    double YStart)
{
    /// <summary>Fyzické súradnice: Xmm = výška, Ymm = hĺbka od prednej hrany.</summary>
    public IEnumerable<MoventoVrtaciBod> EnumerateBody()
    {
        if (!PouzivaPattern)
        {
            yield return new(YStart, XStart);
            yield break;
        }

        for (var ix = 0; ix < CountX; ix++)
        for (var iy = 0; iy < CountY; iy++)
            yield return new(YStart + iy * PitchY, XStart + ix * PitchX);
    }
}

/// <summary>Bod vŕtania na boku – Xmm = výška, Ymm = hĺbka od prednej hrany.</summary>
public readonly record struct MoventoVrtaciBod(double Xmm, double Ymm);
