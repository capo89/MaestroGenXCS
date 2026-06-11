using MaestroGenXcs.Domain;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Services;

/// <summary>
/// Po importe zoskupí dielce šuflí v zostave a rozdelí spoločné dno medzi šufle.
/// Jedna šufľa = 2× bok, 1× čelo, 1× zad, 1× dno.
/// </summary>
public static class SufelAssemblyResolver
{
    public sealed record Result(int SkupinyCount, IReadOnlyList<string> Warnings);

    public static Result Resolve(PartsStore store, AssemblyStore assemblyStore)
    {
        var warnings = new List<string>();
        var skupinyCount = 0;

        foreach (var grp in store.Parts.GroupBy(p => p.Zostava ?? "Bez zostavy"))
        {
            var ctx = assemblyStore.GetContext(grp.Key);
            if (ctx == null)
                continue;

            ctx.SufelSkupiny.Clear();
            var local = ResolveZostava(grp.Key, grp.ToList(), ctx, store, warnings);
            skupinyCount += local;
        }

        return new Result(skupinyCount, warnings);
    }

    private static int ResolveZostava(
        string zostava,
        List<Part> parts,
        AssemblyContext ctx,
        PartsStore store,
        List<string> warnings)
    {
        var sufelParts = CollectSufelParts(parts);
        if (sufelParts.Count == 0)
            return 0;

        ClassifyParts(sufelParts);

        var byPozicia = BuildPositionBuckets(sufelParts);
        var poolDna = sufelParts
            .Where(p => p.Kind == PartKind.SufelDno && (p.SufelPozicia is null or SufelPozicia.Nezadana))
            .ToList();

        var pozicie = byPozicia.Keys
            .Where(p => p != SufelPozicia.Nezadana)
            .OrderBy(p => p.SortOrder())
            .ToList();

        if (pozicie.Count == 0)
        {
            var drawerCount = InferDrawerCount(sufelParts, warnings, zostava);
            if (drawerCount <= 0)
                return 0;

            var byCislo = BuildNumberBuckets(sufelParts);
            var cisla = byCislo.Keys.Where(c => c > 0).OrderBy(c => c).ToList();
            var useNumbered = cisla.Count == drawerCount
                              && cisla.All(c => IsCompleteDrawerBucket(byCislo[c]));

            if (useNumbered)
            {
                foreach (var cislo in cisla)
                    AddSkupinaFromBucket(ctx, byCislo[cislo], SufelPozicia.Nezadana, cislo, zostava, warnings);
            }
            else
            {
                for (var i = 0; i < drawerCount; i++)
                {
                    ctx.SufelSkupiny.Add(new SufelSkupina
                    {
                        Pozicia = SufelPozicia.Nezadana,
                        PoradieOdSpodu = i + 1,
                    });
                }

                DistributeAggregated(store, ctx.SufelSkupiny, sufelParts, warnings);
            }
        }
        else
        {
            foreach (var poz in pozicie)
            {
                var bucket = byPozicia[poz];
                AddSkupinaFromBucket(ctx, bucket, poz, poz.SortOrder() + 1, zostava, warnings);
            }
        }

        var bezDna = ctx.SufelSkupiny.Where(s => s.DnoPart == null).ToList();
        if (bezDna.Count > 0 && poolDna.Count > 0)
            DistributePoolDna(zostava, bezDna, poolDna, store, warnings);

        foreach (var sk in ctx.SufelSkupiny)
        {
            BindSkupina(sk);
            if (!sk.JeKompletna)
                warnings.Add($"Zostava „{zostava}“ – {sk.Nazov}: nekompletná šufľa.");
        }

        return ctx.SufelSkupiny.Count;
    }

    private static List<Part> CollectSufelParts(List<Part> parts)
    {
        var list = new List<Part>();
        foreach (var part in parts)
        {
            var parsed = SufelNameParser.Parse(part.Name);
            if (parsed.JeSufel || SufelNameParser.IsSufelKind(part.Kind))
                list.Add(part);
        }

        return list;
    }

    private static void ClassifyParts(IEnumerable<Part> sufelParts)
    {
        foreach (var part in sufelParts)
        {
            var parsed = SufelNameParser.Parse(part.Name);
            if (!parsed.JeSufel)
                continue;

            var kind = SufelNameParser.ToPartKind(parsed.Rola);
            if (kind != PartKind.Generic)
                part.Kind = kind;

            if (parsed.Pozicia != SufelPozicia.Nezadana)
                part.SufelPozicia = parsed.Pozicia;
        }
    }

    private sealed class RoleBucket
    {
        public List<Part> Boky { get; } = new();
        public List<Part> Cela { get; } = new();
        public List<Part> Zady { get; } = new();
        public List<Part> Dna { get; } = new();
    }

    private static void AddSkupinaFromBucket(
        AssemblyContext ctx,
        RoleBucket bucket,
        SufelPozicia pozicia,
        int poradieOdSpodu,
        string zostava,
        List<string> warnings)
    {
        var sk = new SufelSkupina
        {
            Pozicia = pozicia,
            PoradieOdSpodu = poradieOdSpodu,
        };

        sk.BokPart = PickPrimary(bucket.Boky);
        sk.CeloPart = PickPrimary(bucket.Cela);
        sk.ZadPart = PickPrimary(bucket.Zady);
        sk.DnoPart = PickPrimary(bucket.Dna);

        ValidateRoleKs(zostava, sk, warnings);
        ctx.SufelSkupiny.Add(sk);
    }

    private static Dictionary<int, RoleBucket> BuildNumberBuckets(IEnumerable<Part> sufelParts)
    {
        var map = new Dictionary<int, RoleBucket>();

        foreach (var part in sufelParts)
        {
            var cislo = SufelNameParser.Parse(part.Name).Cislo;
            if (cislo is not > 0)
                continue;

            if (!map.TryGetValue(cislo.Value, out var bucket))
            {
                bucket = new RoleBucket();
                map[cislo.Value] = bucket;
            }

            switch (part.Kind)
            {
                case PartKind.SufelBok:
                    bucket.Boky.Add(part);
                    break;
                case PartKind.SufelCelo:
                    bucket.Cela.Add(part);
                    break;
                case PartKind.SufelZad:
                    bucket.Zady.Add(part);
                    break;
                case PartKind.SufelDno:
                    bucket.Dna.Add(part);
                    break;
            }
        }

        return map;
    }

    private static Dictionary<SufelPozicia, RoleBucket> BuildPositionBuckets(IEnumerable<Part> sufelParts)
    {
        var map = new Dictionary<SufelPozicia, RoleBucket>();

        foreach (var part in sufelParts)
        {
            var poz = part.SufelPozicia ?? SufelPozicia.Nezadana;
            if (!map.TryGetValue(poz, out var bucket))
            {
                bucket = new RoleBucket();
                map[poz] = bucket;
            }

            switch (part.Kind)
            {
                case PartKind.SufelBok:
                    bucket.Boky.Add(part);
                    break;
                case PartKind.SufelCelo:
                    bucket.Cela.Add(part);
                    break;
                case PartKind.SufelZad:
                    bucket.Zady.Add(part);
                    break;
                case PartKind.SufelDno:
                    bucket.Dna.Add(part);
                    break;
            }
        }

        return map;
    }

    private static bool IsCompleteDrawerBucket(RoleBucket bucket)
    {
        var bokKs = bucket.Boky.Sum(p => p.PocetKs ?? 1);
        var celoKs = bucket.Cela.Sum(p => p.PocetKs ?? 1);
        var zadKs = bucket.Zady.Sum(p => p.PocetKs ?? 1);
        var dnoKs = bucket.Dna.Sum(p => p.PocetKs ?? 1);
        return bokKs == 2 && celoKs == 1 && zadKs == 1 && dnoKs is 0 or 1;
    }

    /// <summary>
    /// Rozdelí súhrnné riadky (bok 8 ks, čelo 4 ks…) medzi N šuflí – každá dostane 2× bok, 1× čelo, zad, dno.
    /// </summary>
    private static void DistributeAggregated(
        PartsStore store,
        IReadOnlyList<SufelSkupina> skupiny,
        List<Part> sufelParts,
        List<string> warnings)
    {
        var bokQueue = BuildUnitQueue(sufelParts.Where(p => p.Kind == PartKind.SufelBok));
        var celoQueue = BuildUnitQueue(sufelParts.Where(p => p.Kind == PartKind.SufelCelo));
        var zadQueue = BuildUnitQueue(sufelParts.Where(p => p.Kind == PartKind.SufelZad));
        var dnoQueue = BuildUnitQueue(sufelParts.Where(p => p.Kind == PartKind.SufelDno));
        var consumed = new Dictionary<Part, int>();

        foreach (var sk in skupiny)
        {
            sk.BokPart = TakeDrawerBok(bokQueue, sk, store, consumed, warnings);
            sk.CeloPart = TakeDrawerUnit(celoQueue, sk, store, consumed);
            sk.ZadPart = TakeDrawerUnit(zadQueue, sk, store, consumed);
            sk.DnoPart = TakeDrawerUnit(dnoQueue, sk, store, consumed);
        }

        FinalizeConsumedSources(store, consumed, warnings);
    }

    private static Queue<Part> BuildUnitQueue(IEnumerable<Part> parts)
    {
        var queue = new Queue<Part>();
        foreach (var part in parts)
        {
            var ks = part.PocetKs ?? 1;
            for (var i = 0; i < ks; i++)
                queue.Enqueue(part);
        }

        return queue;
    }

    private static Part? TakeDrawerBok(
        Queue<Part> queue,
        SufelSkupina sk,
        PartsStore store,
        Dictionary<Part, int> consumed,
        List<string> warnings)
    {
        if (queue.Count < 2)
            return null;

        var first = queue.Dequeue();
        var second = queue.Dequeue();
        if (!ReferenceEquals(first, second))
        {
            warnings.Add($"{sk.Nazov}: bok šufle z viacerých riadkov – použitý prvý ({first.Name}).");
            queue.Enqueue(second);
        }

        return TakeDrawerSlice(first, 2, sk, store, consumed);
    }

    private static Part? TakeDrawerUnit(
        Queue<Part> queue,
        SufelSkupina sk,
        PartsStore store,
        Dictionary<Part, int> consumed)
    {
        if (!queue.TryDequeue(out var source))
            return null;

        return TakeDrawerSlice(source, 1, sk, store, consumed);
    }

    private static Part TakeDrawerSlice(
        Part source,
        int pocetKs,
        SufelSkupina sk,
        PartsStore store,
        Dictionary<Part, int> consumed)
    {
        consumed.TryGetValue(source, out var taken);
        consumed[source] = taken + pocetKs;

        var clone = ClonePart(source);
        clone.PocetKs = pocetKs;
        clone.SufelSkupinaId = sk.Id;

        var insertAt = store.Parts.IndexOf(source);
        if (insertAt < 0)
            insertAt = store.Parts.Count;
        store.Parts.Insert(insertAt, clone);
        return clone;
    }

    private static void FinalizeConsumedSources(
        PartsStore store,
        Dictionary<Part, int> consumed,
        List<string> warnings)
    {
        foreach (var (source, taken) in consumed)
        {
            var total = source.PocetKs ?? 1;
            var remaining = total - taken;
            if (remaining < 0)
                warnings.Add($"Diel „{source.Name}“: pri rozdelení šuflí chýba {-remaining} ks.");
            else if (remaining == 0)
                store.Parts.Remove(source);
            else
                source.PocetKs = remaining;
        }
    }

    private static Part? PickPrimary(IReadOnlyList<Part> parts)
    {
        if (parts.Count == 0)
            return null;
        if (parts.Count == 1)
            return parts[0];
        return parts.OrderByDescending(p => p.PocetKs ?? 1).First();
    }

    private static void ValidateRoleKs(string zostava, SufelSkupina sk, List<string> warnings)
    {
        if (sk.BokPart != null && sk.BokKsSkutocne != sk.BokKsOcekavane)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {sk.Nazov}: sufel bok má {sk.BokKsSkutocne} ks (očakávané {sk.BokKsOcekavane}).");
        }

        if (sk.CeloPart != null && (sk.CeloPart.PocetKs ?? 1) != 1)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {sk.Nazov}: sufel čelo má {sk.CeloPart.PocetKs} ks (očakávané 1).");
        }

        if (sk.ZadPart != null && (sk.ZadPart.PocetKs ?? 1) != 1)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {sk.Nazov}: sufel zad má {sk.ZadPart.PocetKs} ks (očakávané 1).");
        }
    }

    private static void DistributePoolDna(
        string zostava,
        IReadOnlyList<SufelSkupina> skupiny,
        List<Part> poolDna,
        PartsStore store,
        List<string> warnings)
    {
        var needed = skupiny.Count;
        var available = poolDna.Sum(p => p.PocetKs ?? 1);

        if (available < needed)
        {
            warnings.Add(
                $"Zostava „{zostava}“: sufel dno má {available} ks, chýba pre {needed} šuflí.");
            return;
        }

        if (available > needed)
        {
            warnings.Add(
                $"Zostava „{zostava}“: sufel dno má {available} ks, ale len {needed} šuflí – priradené prvých {needed}.");
        }

        var queue = new Queue<Part>();
        foreach (var p in poolDna)
        {
            var ks = p.PocetKs ?? 1;
            for (var i = 0; i < ks; i++)
                queue.Enqueue(p);
        }

        var consumedFrom = new Dictionary<Part, int>();

        for (var i = 0; i < needed; i++)
        {
            if (!queue.TryDequeue(out var source))
                break;

            var sk = skupiny[i];
            var assigned = TakeOneDno(source, sk, store, consumedFrom);
            sk.DnoPart = assigned;
        }

        foreach (var pool in poolDna)
        {
            if (!consumedFrom.TryGetValue(pool, out var taken))
                continue;

            var total = pool.PocetKs ?? 1;
            var remaining = total - taken;
            if (remaining <= 0)
                store.Parts.Remove(pool);
            else
                pool.PocetKs = remaining;
        }
    }

    private static Part TakeOneDno(
        Part source,
        SufelSkupina sk,
        PartsStore store,
        Dictionary<Part, int> consumedFrom)
    {
        consumedFrom.TryGetValue(source, out var taken);
        consumedFrom[source] = taken + 1;

        var clone = ClonePart(source);
        clone.Name = SufelNameParser.AppendPoziciaToName(
            StripPoziciaSuffix(source.Name),
            sk.Pozicia);
        clone.SufelPozicia = sk.Pozicia;
        clone.PocetKs = 1;
        clone.SufelSkupinaId = sk.Id;

        var insertAt = store.Parts.IndexOf(source);
        if (insertAt < 0)
            insertAt = store.Parts.Count;
        store.Parts.Insert(insertAt, clone);
        return clone;
    }

    private static string StripPoziciaSuffix(string name)
    {
        var parsed = SufelNameParser.Parse(name);
        if (!parsed.JeSufel || parsed.Pozicia == SufelPozicia.Nezadana)
            return name.Trim();

        foreach (var label in new[] { "vrchný", "vrchny", "stredný", "stredny", "spodný", "spodny" })
        {
            var idx = name.LastIndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return name[..idx].Trim();
        }

        return name.Trim();
    }

    private static void BindSkupina(SufelSkupina sk)
    {
        if (sk.BokPart != null)
            sk.BokPart.SufelSkupinaId = sk.Id;
        if (sk.CeloPart != null)
            sk.CeloPart.SufelSkupinaId = sk.Id;
        if (sk.ZadPart != null)
            sk.ZadPart.SufelSkupinaId = sk.Id;
        if (sk.DnoPart != null)
            sk.DnoPart.SufelSkupinaId = sk.Id;

        sk.BokMacro.SyncDno18FromDno(sk.DnoPart);
        SufelMacroSynchronizer.SyncAll(sk);
        SufelMacroApplier.Apply(sk);
    }

    private static int InferDrawerCount(IReadOnlyList<Part> sufelParts, List<string> warnings, string zostava)
    {
        var bokKs = sufelParts.Where(p => p.Kind == PartKind.SufelBok).Sum(p => p.PocetKs ?? 1);
        var celoKs = sufelParts.Where(p => p.Kind == PartKind.SufelCelo).Sum(p => p.PocetKs ?? 1);
        var zadKs = sufelParts.Where(p => p.Kind == PartKind.SufelZad).Sum(p => p.PocetKs ?? 1);
        var dnoKs = sufelParts.Where(p => p.Kind == PartKind.SufelDno).Sum(p => p.PocetKs ?? 1);

        if (bokKs == 0)
            return 0;

        var fromBok = bokKs / 2;
        var candidates = new[] { fromBok, celoKs, zadKs, dnoKs }.Where(n => n > 0).ToList();
        if (candidates.Count == 0)
            return fromBok;

        var count = candidates.Min();
        if (fromBok != count || celoKs != count || zadKs != count || dnoKs != count)
        {
            warnings.Add(
                $"Zostava „{zostava}“: nekonzistentné počty šuflí (bok/2={fromBok}, čelo={celoKs}, zad={zadKs}, dno={dnoKs}).");
        }

        return count;
    }

    private static Part ClonePart(Part source) => new(source.Name, source.Dx, source.Dy, source.Dz)
    {
        Poradie = source.Poradie,
        PocetKs = source.PocetKs,
        Zostava = source.Zostava,
        Kind = source.Kind,
        AbsPredna = source.AbsPredna,
        AbsZadna = source.AbsZadna,
        AbsLava = source.AbsLava,
        AbsPrava = source.AbsPrava,
        PoznamkaPreExport = source.PoznamkaPreExport,
        PrepisatAbsJednaNaNulaOsemPriExporte = source.PrepisatAbsJednaNaNulaOsemPriExporte,
        SufelPozicia = source.SufelPozicia,
    };
}
