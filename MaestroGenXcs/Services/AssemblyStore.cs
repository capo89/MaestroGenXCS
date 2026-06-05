using MaestroGenXcs.Domain;
using MaestroGenXcs.Rendering;

namespace MaestroGenXcs.Services;

/// <summary>Udržiava <see cref="AssemblyContext"/> pre každú zostavu.</summary>
public sealed class AssemblyStore
{
    private readonly Dictionary<string, AssemblyContext> _byZostava = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, AssemblyContext> Contexts => _byZostava;

    public AssemblyContext? GetContext(string? zostava)
    {
        if (string.IsNullOrWhiteSpace(zostava))
            return null;
        return _byZostava.GetValueOrDefault(zostava);
    }

    public AssemblyCorpusMode GetCorpusMode(string? zostava) =>
        GetContext(zostava)?.CorpusMode ?? AssemblyCorpusMode.BokVlozeny;

    /// <summary>Po zmene <see cref="AssemblyContext.CorpusMode"/> – kontaktné plochy a default offsety.</summary>
    public void ApplyCorpusModeToPlacements(AssemblyContext ctx)
    {
        if (ctx.ReferenceBok == null)
            return;

        foreach (var placement in ctx.Placements)
        {
            placement.AnchorFace = ConnectionMap.GetLayoutContactFace(
                ctx.ReferenceBok.Kind, placement.Part.Kind, ctx.CorpusMode);
            placement.OffsetY = DefaultOffsetX(placement.Part, ctx.ReferenceBok, ctx.CorpusMode);
        }
    }

    /// <summary>Po <see cref="PartsStore.ReplaceParts"/> – doplní kontexty a default umiestnenia.</summary>
    public void SyncFromParts(IEnumerable<Part> parts)
    {
        var groups = parts.GroupBy(p => p.Zostava ?? "Bez zostavy");
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var grp in groups)
        {
            seen.Add(grp.Key);
            var members = grp.ToList();
            if (!_byZostava.TryGetValue(grp.Key, out var ctx))
            {
                ctx = new AssemblyContext { Zostava = grp.Key };
                _byZostava[grp.Key] = ctx;
            }

            ctx.ReferenceBok = members.FirstOrDefault(p => p.Kind == PartKind.BokL)
                ?? members.FirstOrDefault(p => p.Kind == PartKind.BokP);

            var existing = new Dictionary<Part, AssemblyPlacement>();
            foreach (var p in ctx.Placements)
                existing[p.Part] = p;
            ctx.Placements.Clear();

            foreach (var part in members)
            {
                if (ReferenceEquals(part, ctx.ReferenceBok))
                    continue;

                if (part.Kind == PartKind.Generic)
                    continue;

                if (existing.TryGetValue(part, out var prev))
                {
                    if (ctx.ReferenceBok != null)
                    {
                        prev.AnchorFace = ConnectionMap.GetLayoutContactFace(
                            ctx.ReferenceBok.Kind, part.Kind, ctx.CorpusMode);
                    }
                    ctx.Placements.Add(prev);
                    continue;
                }

                var anchorFace = ctx.ReferenceBok != null
                    ? ConnectionMap.GetLayoutContactFace(
                        ctx.ReferenceBok.Kind, part.Kind, ctx.CorpusMode)
                    : PartFace.Left;

                ctx.Placements.Add(new AssemblyPlacement
                {
                    Part = part,
                    AnchorFace = anchorFace,
                    OffsetY = DefaultOffsetX(part, ctx.ReferenceBok, ctx.CorpusMode),
                    OffsetDepthMm = DefaultOffsetDepth(part),
                    IsPlacedInScene = false
                });
            }
        }

        foreach (var key in _byZostava.Keys.Where(k => !seen.Contains(k)).ToList())
            _byZostava.Remove(key);
    }

    private static double DefaultOffsetDepth(Part part) =>
        part.Kind is PartKind.BokP ? part.Dy : 0;

    private static double DefaultOffsetX(Part part, Part? referenceBok, AssemblyCorpusMode corpusMode)
    {
        return part.Kind switch
        {
            PartKind.Polica => part.PolicaSerie.Count > 0
                ? part.PolicaSerie[0].OdSpoduMm
                : part.PolicaDefaultVyskaOdSpoduMm,
            PartKind.Traverza => TraverzaKolikyApplier.FindTraverzaMaster(part)?.TraverzaBokYStart ?? 37,
            PartKind.Dno when ConnectionMap.UsesVlozenyDnoVrchPlacement(PartKind.Dno, corpusMode) =>
                referenceBok != null
                    ? AssemblyPartLayout.VlozenyDefaultOffsetX(part, referenceBok)
                    : 0,
            PartKind.Dno => 0,
            PartKind.Vrch when ConnectionMap.UsesVlozenyDnoVrchPlacement(PartKind.Vrch, corpusMode) =>
                referenceBok != null
                    ? AssemblyPartLayout.VlozenyDefaultOffsetX(part, referenceBok)
                    : 0,
            PartKind.Vrch => referenceBok != null
                ? Math.Max(0, referenceBok.Dx - part.Dz)
                : 0,
            _ => 0
        };
    }
}
