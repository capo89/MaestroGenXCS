using MaestroGenXcs.Domain;
using MaestroGenXcs.Kovania;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Services;

/// <summary>
/// Po importe / rozpoznaní šuflí zosúladí <see cref="AssemblyContext.MoventoSekcie"/> s <see cref="AssemblyContext.SufelSkupiny"/>.
/// </summary>
public static class MoventoSekcieSynchronizer
{
    public static void Sync(AssemblyContext ctx)
    {
        var kept = ctx.MoventoSekcie
            .Where(s => s.SufelSkupinaId.HasValue)
            .ToDictionary(s => s.SufelSkupinaId!.Value);

        ctx.MoventoSekcie.Clear();

        foreach (var sk in ctx.SufelSkupiny
                     .OrderBy(s => s.Pozicia.SortOrder())
                     .ThenBy(s => s.PoradieOdSpodu))
        {
            if (kept.TryGetValue(sk.Id, out var sekcia))
            {
                sekcia.Nazov = sk.Nazov;
                ctx.MoventoSekcie.Add(sekcia);
                continue;
            }

            ctx.MoventoSekcie.Add(new SufelMoventoSekcia
            {
                SufelSkupinaId = sk.Id,
                Nazov = sk.Nazov,
                PocetKovani = 1,
                DlzkaKovania = InferDlzkaKovania(sk),
            });
        }
    }

    public static void SyncAll(AssemblyStore store)
    {
        foreach (var ctx in store.Contexts.Values)
            Sync(ctx);
    }

    private static MoventoDlzkaKovania InferDlzkaKovania(SufelSkupina sk)
    {
        var depth = sk.CeloPart?.Dy ?? sk.BokPart?.Dy ?? sk.ZadPart?.Dy ?? 0;
        if (depth > 0 && depth < 270)
            return MoventoDlzkaKovania.Kratka250_270;
        return MoventoDlzkaKovania.Dlha270_600;
    }
}
