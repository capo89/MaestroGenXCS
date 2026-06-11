using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Sufle;

/// <summary>
/// ABS z Excelu (stĺpec L = x1 = predná hrana v kusovníku skrinky) pre šufle
/// mapuje na zadnú hranu dielca – šufľa sa v Maestre orientuje opačne.
/// </summary>
public static class SufelAbsMapper
{
    public readonly record struct AbsSides(int? Predna, int? Lava, int? Zadna, int? Prava);

    /// <summary>
    /// Excel: L/M/N/O → predná/ľavá/zadná/pravá. Pri šufli vymení predná ↔ zadná.
    /// </summary>
    public static AbsSides MapFromExcel(
        string partName,
        PartKind kind,
        int? excelPredna,
        int? excelLava,
        int? excelZadna,
        int? excelPrava)
    {
        if (!IsSufelPart(partName, kind))
            return new AbsSides(excelPredna, excelLava, excelZadna, excelPrava);

        return new AbsSides(excelZadna, excelLava, excelPredna, excelPrava);
    }

    public static bool IsSufelPart(string partName, PartKind kind) =>
        IsSufelKind(kind) || Parse(partName).JeSufel;

    private static bool IsSufelKind(PartKind kind) => SufelNameParser.IsSufelKind(kind);

    private static SufelNameParser.Parsed Parse(string partName) => SufelNameParser.Parse(partName);
}
