using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Services;

/// <summary>Aplikuje <see cref="AssemblyPlacement"/> na operácie (polica, traverza, …).</summary>
public static class AssemblySolverApplier
{
    public static void Apply(PartsStore store, AssemblyContext ctx, OperationPropagator propagator)
    {
        if (ctx.ReferenceBok == null)
            throw new InvalidOperationException($"Zostava „{ctx.Zostava}“: chýba Bok L (referenčný bok).");

        store.RegenerateConnections();

        foreach (var placement in ctx.Placements)
            ApplyPlacement(store, placement, propagator);
    }

    private static void ApplyPlacement(PartsStore store, AssemblyPlacement placement, OperationPropagator propagator)
    {
        var part = placement.Part;
        switch (part.Kind)
        {
            case PartKind.Polica:
                ApplyPolica(store, part, placement.OffsetY, propagator);
                break;
            case PartKind.Traverza:
                ApplyTraverza(store, part, placement.OffsetY, propagator);
                break;
            case PartKind.Dno:
            case PartKind.Vrch:
                break;
        }
    }

    private static void ApplyPolica(PartsStore store, Part polica, double offsetY, OperationPropagator propagator)
    {
        polica.EnsurePolicaSerie();
        if (polica.PolicaSerie.Count > 0)
            polica.PolicaSerie[0].OdSpoduMm = Math.Max(0, offsetY);

        propagator.ExecuteWithoutPropagation(() =>
            PolicaKolikyApplier.Apply(store, polica));
    }

    private static void ApplyTraverza(PartsStore store, Part traverza, double offsetY, OperationPropagator propagator)
    {
        var master = TraverzaKolikyApplier.FindTraverzaMaster(traverza);
        TraverzaKolikyApplier.Request req;
        if (master != null)
        {
            req = TraverzaKolikyApplier.RequestFromMaster(master) with { BokYStart = Math.Max(0, offsetY) };
        }
        else
        {
            req = TraverzaKolikyApplier.DefaultsFromStore(TemplateStore.Instance) with
            {
                BokYStart = Math.Max(0, offsetY)
            };
        }

        propagator.ExecuteWithoutPropagation(() =>
            TraverzaKolikyApplier.Apply(store, traverza, req));
    }
}
