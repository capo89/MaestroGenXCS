using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Services;

/// <summary>
/// Dno/vrch Top pri <see cref="AssemblyCorpusMode.BokVlozeny"/> – dva samostatné patterny (Bok L / Bok P).
/// </summary>
public static class DnoVrchTopKolikyHelper
{
    public const double DefaultOffsetFromEdgeMm = 9.0;
    public const double DefaultYStartMm = 30.0;
    public const int DefaultPinCount = 3;
    public const double DefaultPitchAlongDepthMm = 128.0;

    /// <summary>Mriežka Top: 1 stĺpec (pri ľavej/pravej hrane), rozostup po Y (predná → zadná).</summary>
    public static void ApplyStandardTopPattern(
        DrillOperation op,
        Part panel,
        PartKind partner,
        int? countAlongDepth = null,
        double? pitchAlongDepth = null,
        double? xStart = null,
        double? yStart = null)
    {
        op.Face = PartFace.Top;
        op.RefPos = GetRefPosOnPanel(panel.Kind, partner);
        op.KolikPartnerBok = partner;
        op.CountX = 1;
        op.CountY = Math.Max(1, countAlongDepth ?? DefaultPinCount);
        op.PitchX = 32;
        op.PitchY = pitchAlongDepth is > 0 ? pitchAlongDepth.Value : DefaultPitchAlongDepthMm;
        op.XStart = ResolveLocalXStart(xStart ?? DefaultOffsetFromEdgeMm, op.RefPos, panel.Dx);
        op.YStart = yStart ?? DefaultYStartMm;
    }

    /// <summary>
    /// <see cref="DrillOperation.XStart"/> je vždy odsadenie od rohu daného <c>refpos</c>,
    /// nie svetová X. Pri refpos 2/3 nesmie byť uložené „Dx − 9“ (inak po zrkadlení spadne na 9 ako Bok L).
    /// </summary>
    public static double ResolveLocalXStart(double xStart, int refPos, double panelDx)
    {
        if (refPos is 2 or 3 && xStart > panelDx * 0.5)
            return Math.Max(DefaultOffsetFromEdgeMm, panelDx - xStart);
        return xStart;
    }

    public static double ResolveLocalXStart(DrillOperation op, double panelDx) =>
        ResolveLocalXStart(op.XStart, op.RefPos, panelDx);

    /// <summary>Staré uložené patterny „po X“ pre Top dno/vrch – pre náhľad a export.</summary>
    public static (int CountX, int CountY, double PitchX, double PitchY) GetTopGridForPreview(DrillOperation op)
    {
        if (op.Face is not (PartFace.Top or PartFace.Bottom)
            || op.KolikPartnerBok is null
            || op.CountX <= 1
            || op.CountY != 1)
        {
            return (op.CountX, op.CountY, op.PitchX, op.PitchY);
        }

        var alongPitch = op.PitchX > 0 ? op.PitchX : op.PitchY;
        return (1, op.CountX, 32, alongPitch);
    }

    public static bool UsesDualTopKoliky(Part part, PartFace face, AssemblyCorpusMode corpusMode) =>
        face == PartFace.Top
        && part.Kind is PartKind.Dno or PartKind.Vrch
        && corpusMode == AssemblyCorpusMode.BokVlozeny;

    public static string PartnerLabel(PartKind partner) =>
        partner switch
        {
            PartKind.BokL => "Bok L",
            PartKind.BokP => "Bok P",
            _ => partner.ToString()
        };

    public static int GetRefPosOnPanel(PartKind panelKind, PartKind partner) =>
        (panelKind, partner) switch
        {
            (PartKind.Dno, PartKind.BokL) => 0,
            (PartKind.Dno, PartKind.BokP) => 2,
            (PartKind.Vrch, PartKind.BokL) => 2,
            (PartKind.Vrch, PartKind.BokP) => 0,
            _ => 0
        };

    /// <summary>Odsadenie od referenčného rohu (refpos určí ľavý/pravý bok na ploche).</summary>
    public static double DefaultXStart(Part panel, PartKind partner) =>
        DefaultOffsetFromEdgeMm;
}
