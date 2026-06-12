using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Chrbaty;

/// <summary>Dva panelové rozmery a hrúbka chrbátu – nezávisle od <see cref="Part.Dx"/>/<see cref="Part.Dy"/>.</summary>
public sealed record ChrbatPanelRozmery(double RozmerA, double RozmerB, double Hrubka)
{
    public static ChrbatPanelRozmery FromPart(Part part)
    {
        var dims = new[] { part.Dx, part.Dy, part.Dz }.OrderBy(d => d).ToArray();
        return new ChrbatPanelRozmery(dims[1], dims[2], dims[0]);
    }

    public bool MatchesVyskaSirka(double vyska, double sirka, double toleranceMm)
    {
        var okDirect = DiffersAtMost(RozmerA, vyska, toleranceMm) && DiffersAtMost(RozmerB, sirka, toleranceMm);
        var okSwap = DiffersAtMost(RozmerA, sirka, toleranceMm) && DiffersAtMost(RozmerB, vyska, toleranceMm);
        return okDirect || okSwap;
    }

    /// <summary>Jeden z panelových rozmerov sedí so <paramref name="sirka"/>.</summary>
    public bool MatchesSirka(double sirka, double toleranceMm) =>
        DiffersAtMost(RozmerA, sirka, toleranceMm) || DiffersAtMost(RozmerB, sirka, toleranceMm);

    /// <summary>Druhý panelový rozmer (Vyska z dielca) pri známej Sirke.</summary>
    public double? ResolveVyskaZDielca(double sirka, double toleranceMm)
    {
        if (DiffersAtMost(RozmerA, sirka, toleranceMm))
            return RozmerB;
        if (DiffersAtMost(RozmerB, sirka, toleranceMm))
            return RozmerA;
        return null;
    }

    private static bool DiffersAtMost(double value, double expected, double toleranceMm) =>
        Math.Abs(value - expected) <= toleranceMm;
}
