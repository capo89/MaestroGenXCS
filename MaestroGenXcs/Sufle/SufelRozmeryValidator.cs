using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Sufle;

/// <summary>
/// Kontrola súladu rozmerov šufľa: smerodajný je <c>sufel bok</c> (Dy),
/// čelo a zad musia mať Dy = bok − (15 + hrúbka dna) − 2 mm.
/// </summary>
public static class SufelRozmeryValidator
{
    public const double CeloZadMedzeraMm = 2.0;

    /// <summary>Fixná zložka odsadenia (25 mm pri dne 10 mm, 33 mm pri dne 18 mm).</summary>
    public const double OdsadenieZakladMm = 15.0;

    public const double ToleranceMm = 0.5;

    /// <summary>Očakávané Dy čela/zadu podľa Dy boku a hrúbky dna.</summary>
    public static double ResolveExpectedCeloZadDyMm(double bokDyMm, double dnoThicknessMm) =>
        bokDyMm - dnoThicknessMm - OdsadenieZakladMm - CeloZadMedzeraMm;

    public static void ValidateSkupina(string zostava, SufelSkupina sk, ICollection<string> warnings)
    {
        if (sk.BokPart == null)
            return;

        var bokDy = sk.BokPart.Dy;
        if (bokDy <= 0)
            return;

        if (sk.DnoPart == null)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {sk.Nazov}: chýba dno, nie je možná kontrola Dy čela/zad.");
            return;
        }

        var dnoHr = SufelBokMacroParams.ResolveDnoThicknessMm(sk.DnoPart);
        if (dnoHr <= 0)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {sk.Nazov}: neplatná hrúbka dna ({dnoHr:0.##} mm).");
            return;
        }

        var expected = ResolveExpectedCeloZadDyMm(bokDy, dnoHr);
        ValidatePartDy(zostava, sk.Nazov, "čelo", sk.CeloPart, expected, dnoHr, bokDy, warnings);
        ValidatePartDy(zostava, sk.Nazov, "zad", sk.ZadPart, expected, dnoHr, bokDy, warnings);
    }

    private static void ValidatePartDy(
        string zostava,
        string skupinaNazov,
        string rolaLabel,
        Part? part,
        double expectedDy,
        double dnoHr,
        double bokDy,
        ICollection<string> warnings)
    {
        if (part == null)
            return;

        if (Math.Abs(part.Dy - expectedDy) <= ToleranceMm)
            return;

        warnings.Add(
            $"Zostava „{zostava}“ – {skupinaNazov}: sufel {rolaLabel} Dy={part.Dy:0.##} mm, " +
            $"očakávané {expectedDy:0.##} mm (bok Dy={bokDy:0.##}, dno {dnoHr:0.##} mm).");
    }
}
