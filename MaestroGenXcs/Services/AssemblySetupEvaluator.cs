using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Services;

/// <summary>
/// Určuje, či je dielec / zostava „použitá“ (vložená v 3D) a „nastavená“ (má kolíky / balík operácií).
/// </summary>
public static class AssemblySetupEvaluator
{
    public static bool IsPartConfigured(Part part, AssemblyContext? ctx)
    {
        if (!HasMachiningConfigured(part))
            return false;

        if (ctx?.ReferenceBok != null && ReferenceEquals(part, ctx.ReferenceBok))
            return true;

        var placement = ctx?.Placements.FirstOrDefault(p => ReferenceEquals(p.Part, part));
        return placement != null && placement.IsPlacedInScene;
    }

    public static bool IsZostavaConfigured(string? zostava, AssemblyContext? ctx, IEnumerable<Part> partsInZostava)
    {
        if (string.IsNullOrWhiteSpace(zostava) || ctx == null)
            return false;

        var members = partsInZostava
            .Where(p => string.Equals(p.Zostava, zostava, StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Kind != PartKind.Generic)
            .ToList();

        return members.Count > 0 && members.All(p => IsPartConfigured(p, ctx));
    }

    private static bool HasMachiningConfigured(Part part)
    {
        if (TraverzaKolikyApplier.IsTraverzaPart(part))
            return TraverzaKolikyApplier.FindTraverzaMaster(part) != null;

        if (PolicaKolikyApplier.IsPolicaPart(part))
            return part.Operations.OfType<DrillOperation>()
                .Any(o => o.IsEnabled && o.IsPolicaMaster);

        return part.Operations.OfType<DrillOperation>().Any(o => o.IsEnabled);
    }
}
