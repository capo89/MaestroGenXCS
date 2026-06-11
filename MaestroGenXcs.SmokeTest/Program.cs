using System.Text;
using System.Text.RegularExpressions;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Import;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Services;
using MaestroGenXcs.Sufle;
using MaestroGenXcs.Xcs;

// Automatické testy: XCS vzor, assembly solver, store, export.
// Voliteľne: dotnet run --project MaestroGenXcs.SmokeTest -- --dump-sufel-xcs

if (args.Contains("--dump-sufel-xcs", StringComparer.Ordinal))
{
    DumpSufelXcsSample();
    return;
}

var failures = new List<string>();
var passed = 0;

void Pass(string name)
{
    passed++;
    Console.WriteLine($"  OK   {name}");
}

void Fail(string name, string detail)
{
    failures.Add($"{name}: {detail}");
    Console.WriteLine($"  FAIL {name}");
    Console.WriteLine($"       {detail}");
}

void AssertCore(string name, string xcs, HashSet<string> expected)
{
    var err = CoreMatchError(xcs, expected);
    if (err == null)
        Pass(name);
    else
        Fail(name, err);
}

static string BuildXcs(Part part)
{
    var exporter = new XcsExporter();
    return exporter.BuildContent(part, XcsExporter.ContextFromPart(part));
}

static HashSet<string> CoreMachiningLines(string xcs)
{
    var set = new HashSet<string>(StringComparer.Ordinal);
    foreach (var raw in xcs.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        var line = raw.Trim();
        if (!line.StartsWith("SelectWorkplane", StringComparison.Ordinal)
            && !line.StartsWith("SetReferencePosition", StringComparison.Ordinal)
            && !line.StartsWith("CreatePattern", StringComparison.Ordinal)
            && !line.StartsWith("CreateDrill(", StringComparison.Ordinal))
            continue;

        if (line.StartsWith("CreateDrill(", StringComparison.Ordinal))
            line = NormalizeDrillLine(line);

        set.Add(line);
    }
    return set;
}

/// <summary>Normalizuje CreateDrill – ignoruje názov vrtania aj popis (porovnanie s VzoryXcs).</summary>
static string NormalizeDrillLine(string createDrillLine)
{
    var m = Regex.Match(createDrillLine,
        @"^CreateDrill\(""[^""]*""\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,",
        RegexOptions.CultureInvariant);
    if (!m.Success)
        return createDrillLine;

    return FormattableString.Invariant(
        $@"CreateDrill(""*"",{m.Groups[1].Value},{m.Groups[2].Value},{m.Groups[3].Value},{m.Groups[4].Value},"""",TypeOfProcess.Drilling,""E071"",""-1"",3,-1,-1,""-1"");");
}

static (PartsStore Store, OperationPropagator Propagator, Part BokL, Part BokP, Part Polica) CreateVzorZostava(bool applyKoliky = true)
{
    var store = new PartsStore();
    var propagator = new OperationPropagator(store);
    var bokL = new Part("Bok L", 1800, 320, 18) { Zostava = "1", Kind = PartKind.BokL };
    var bokP = new Part("Bok P", 1800, 320, 18) { Zostava = "1", Kind = PartKind.BokP };
    var polica = new Part("Polica-pevna", 564, 320, 18)
    {
        Zostava = "1",
        Kind = PartKind.Polica,
        PolicaJePevna = true,
        PolicaFrontOffsetMm = 0,
    };
    polica.EnsurePolicaSerie();
    polica.PolicaSerie[0].OdSpoduMm = 200;
    polica.PolicaSerie[0].OdPrednejHranyMm = 30;
    polica.PolicaSerie[0].RoztecMm = 128;
    polica.PolicaSerie[0].PocetKolikov = 4;

    store.ReplaceParts([bokL, bokP, polica]);
    if (applyKoliky)
    {
        propagator.ExecuteWithoutPropagation(() =>
            PolicaKolikyApplier.Apply(store, polica));
    }

    return (store, propagator, bokL, bokP, polica);
}

static HashSet<string> ExpectedPolicaCore() => new HashSet<string>(new[]
{
    @"SelectWorkplane(""Right"");",
    @"SetReferencePosition(0);",
    @"CreatePattern(1,4,32.00,128.00,0,90);",
    @"CreateDrill(""*"",30.00,9.00,23.00,8.00,"""",TypeOfProcess.Drilling,""E071"",""-1"",3,-1,-1,""-1"");",
    @"SelectWorkplane(""Left"");",
    @"SetReferencePosition(2);",
    @"CreatePattern(1,4,32.00,128.00,0,90);",
    @"CreateDrill(""*"",30.00,9.00,23.00,8.00,"""",TypeOfProcess.Drilling,""E071"",""-1"",3,-1,-1,""-1"");",
}, StringComparer.Ordinal);

static HashSet<string> ExpectedBokLCore() => new HashSet<string>(new[]
{
    @"SelectWorkplane(""Top"");",
    @"SetReferencePosition(2);",
    @"CreatePattern(4,1,128.00,32.00,0,90);",
    @"CreateDrill(""*"",200.00,30.00,12.00,8.00,"""",TypeOfProcess.Drilling,""E071"",""-1"",3,-1,-1,""-1"");",
}, StringComparer.Ordinal);

static HashSet<string> ExpectedBokPCore() => new HashSet<string>(new[]
{
    @"SelectWorkplane(""Top"");",
    @"SetReferencePosition(0);",
    @"CreatePattern(4,1,128.00,32.00,0,90);",
    @"CreateDrill(""*"",200.00,30.00,12.00,8.00,"""",TypeOfProcess.Drilling,""E071"",""-1"",3,-1,-1,""-1"");",
}, StringComparer.Ordinal);

static string? CoreMatchError(string xcs, HashSet<string> expected)
{
    var actual = CoreMachiningLines(xcs);
    if (actual.SetEquals(expected))
        return null;

    var missing = expected.Except(actual).ToList();
    var extra = actual.Except(expected).ToList();
    var sb = new StringBuilder();
    if (missing.Count > 0)
        sb.AppendLine("  chýba: " + string.Join("; ", missing));
    if (extra.Count > 0)
        sb.AppendLine("  navyše: " + string.Join("; ", extra));
    return sb.ToString().TrimEnd();
}

Console.WriteLine("MaestroGenXcs – automatické testy");
Console.WriteLine(new string('=', 50));

// --- 1. XCS kolíky polica + boky (vzor VzoryXcs/Polica-pevna.txt) ---
{
    var (store, _, bokL, bokP, polica) = CreateVzorZostava();
    _ = store;

    AssertCore("XCS polica – jadro operácií", BuildXcs(polica), ExpectedPolicaCore());
    AssertCore("XCS Bok L – jadro operácií", BuildXcs(bokL), ExpectedBokLCore());
    AssertCore("XCS Bok P – jadro operácií", BuildXcs(bokP), ExpectedBokPCore());

    var policaXcs = BuildXcs(polica);
    if (policaXcs.Contains("CreateFinishedWorkpieceBox", StringComparison.Ordinal)
        && policaXcs.Contains("ResetRetractStrategy", StringComparison.Ordinal))
        Pass("XCS polica – hlavička a ukončenie");
    else
        Fail("XCS polica – hlavička a ukončenie", "chýba CreateFinishedWorkpieceBox alebo ResetRetractStrategy");
}

// --- 2. AssemblyStore ---
{
    var (store, _, _, _, polica) = CreateVzorZostava();
    var asm = new AssemblyStore();
    asm.SyncFromParts(store.Parts);
    var ctx = asm.GetContext("1");

    if (ctx?.ReferenceBok?.Kind == PartKind.BokL)
        Pass("AssemblyStore – referenčný Bok L");
    else
        Fail("AssemblyStore – referenčný Bok L", "ReferenceBok nie je Bok L");

    var pl = ctx?.Placements.FirstOrDefault(p => ReferenceEquals(p.Part, polica));
    if (pl != null && Math.Abs(pl.OffsetY - 200) < 0.01)
        Pass("AssemblyStore – default OffsetY polica 200");
    else
        Fail("AssemblyStore – default OffsetY polica 200", $"OffsetY={pl?.OffsetY}");
}

// --- 3. AssemblySolverApplier ---
{
    var (store, propagator, bokL, _, polica) = CreateVzorZostava(applyKoliky: false);
    var asm = new AssemblyStore();
    asm.SyncFromParts(store.Parts);
    var ctx = asm.GetContext("1")!;
    var pl = ctx.Placements.First(p => ReferenceEquals(p.Part, polica));
    pl.OffsetY = 416;

    AssemblySolverApplier.Apply(store, ctx, propagator);

    if (Math.Abs(polica.PolicaSerie[0].OdSpoduMm - 416) < 0.01)
        Pass("Solver – OdSpoduMm = OffsetY");
    else
        Fail("Solver – OdSpoduMm = OffsetY", $"OdSpoduMm={polica.PolicaSerie[0].OdSpoduMm}");

    var bokXcs = BuildXcs(bokL);
    if (bokXcs.Contains("416.00,30.00,12.00", StringComparison.Ordinal))
        Pass("Solver – vrtanie na boku pri 416 mm");
    else
        Fail("Solver – vrtanie na boku pri 416 mm", "CreateDrill s 416.00,30.00,12.00 nenájdené");
}

// --- 4. Export zostavy (dočasný priečinok) ---
{
    var (store, propagator, _, _, _) = CreateVzorZostava(applyKoliky: false);
    var asm = new AssemblyStore();
    asm.SyncFromParts(store.Parts);
    var ctx = asm.GetContext("1")!;
    AssemblySolverApplier.Apply(store, ctx, propagator);

    var dir = Path.Combine(Path.GetTempPath(), "MaestroGenXcs_smoke_" + Guid.NewGuid().ToString("N")[..8]);
    try
    {
        Directory.CreateDirectory(dir);
        var exporter = new XcsExporter();
        var written = 0;
        foreach (var part in store.Parts.Where(p => p.Zostava == "1"))
        {
            var path = Path.Combine(dir, part.Name + ".xcs");
            var content = exporter.BuildContent(part, XcsExporter.ContextFromPart(part));
            File.WriteAllText(path, content, Encoding.UTF8);
            if (new FileInfo(path).Length > 50)
                written++;
        }

        if (written == 3 && File.Exists(Path.Combine(dir, "Polica-pevna.xcs")))
            Pass("Export zostavy – 3 súbory");
        else
            Fail("Export zostavy – 3 súbory", $"written={written}, dir={dir}");
    }
    finally
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* temp */ }
    }
}

// --- 5. Šufle – parser a rozdelenie dna ---
{
    var pVrchny = SufelNameParser.Parse("sufel bok vrchny");
    if (pVrchny.JeSufel && pVrchny.Rola == SufelNameParser.SufelRola.Bok && pVrchny.Pozicia == SufelPozicia.Vrchny)
        Pass("Sufel parser – bok vrchny");
    else
        Fail("Sufel parser – bok vrchny", $"{pVrchny}");

    var pDno = SufelNameParser.Parse("sufel dno");
    if (pDno.JeSufel && pDno.Rola == SufelNameParser.SufelRola.Dno && pDno.Pozicia == SufelPozicia.Nezadana)
        Pass("Sufel parser – dno bez pozície");
    else
        Fail("Sufel parser – dno bez pozície", $"{pDno}");

    var pCislo = SufelNameParser.Parse("bok šufel 1");
    if (pCislo.JeSufel && pCislo.Rola == SufelNameParser.SufelRola.Bok && pCislo.Cislo == 1)
        Pass("Sufel parser – bok šufel 1");
    else
        Fail("Sufel parser – bok šufel 1", $"{pCislo}");

    var store = new PartsStore();
    var asm = new AssemblyStore();
    const string z = "7";
    void Add(string name, int ks) =>
        store.Parts.Add(new Part(name, 400, 300, 18) { Zostava = z, PocetKs = ks });

    Add("sufel bok vrchny", 2);
    Add("sufel celo vrchny", 1);
    Add("sufel zad vrchny", 1);
    Add("sufel bok stredny", 2);
    Add("sufel celo stredny", 1);
    Add("sufel zad stredny", 1);
    Add("sufel bok spodny", 2);
    Add("sufel celo spodny", 1);
    Add("sufel zad spodny", 1);
    Add("sufel dno", 3);

    asm.SyncFromParts(store.Parts);
    var result = SufelAssemblyResolver.Resolve(store, asm);
    var ctx = asm.GetContext(z)!;

    if (result.SkupinyCount == 3 && ctx.SufelSkupiny.Count == 3)
        Pass("Sufel resolver – 3 šufle");
    else
        Fail("Sufel resolver – 3 šufle", $"skupiny={result.SkupinyCount}");

    if (ctx.SufelSkupiny.All(s => s.JeKompletna))
        Pass("Sufel resolver – kompletné šufle");
    else
        Fail("Sufel resolver – kompletné šufle", string.Join("; ", ctx.SufelSkupiny.Select(s => s.Nazov)));

    var dna = store.Parts.Where(p => p.Kind == PartKind.SufelDno).ToList();
    if (dna.Count == 3 && dna.All(p => p.PocetKs == 1 && p.SufelPozicia != null))
        Pass("Sufel resolver – rozdelené dno (3×1 ks)");
    else
        Fail("Sufel resolver – rozdelené dno", $"dna={dna.Count}, ks=[{string.Join(",", dna.Select(p => p.PocetKs))}]");

    if (ctx.SufelSkupiny.Select(s => s.Nazov).SequenceEqual(new[] { "Šufel 1", "Šufel 2", "Šufel 3" }))
        Pass("Sufel resolver – názvy Šufel 1–3");
    else
        Fail("Sufel resolver – názvy", string.Join(", ", ctx.SufelSkupiny.Select(s => s.Nazov)));

    var storeNum = new PartsStore();
    var asmNum = new AssemblyStore();
    const string zn = "8";
    void AddNum(string name, int ks) =>
        storeNum.Parts.Add(new Part(name, 400, 300, 18) { Zostava = zn, PocetKs = ks });

    AddNum("bok šufel 1", 2);
    AddNum("čelo šufel 1", 1);
    AddNum("zad šufel 1", 1);
    AddNum("dno šufle 1", 1);
    AddNum("bok šufel 2", 2);
    AddNum("čelo šufel 2", 1);
    AddNum("zad šufel 2", 1);
    AddNum("dno šufle 2", 1);
    AddNum("bok šufel 3", 2);
    AddNum("čelo šufel 3", 1);
    AddNum("zad šufel 3", 1);
    AddNum("dno šufle 3", 1);

    asmNum.SyncFromParts(storeNum.Parts);
    SufelAssemblyResolver.Resolve(storeNum, asmNum);
    var ctxNum = asmNum.GetContext(zn)!;
    if (ctxNum.SufelSkupiny.Count == 3
        && ctxNum.SufelSkupiny.All(s => s.JeKompletna)
        && ctxNum.SufelSkupiny.All(s => s.EnumeratePartsInDisplayOrder().Any()))
        Pass("Sufel resolver – číslované názvy (3× kompletné)");
    else
        Fail("Sufel resolver – číslované názvy", string.Join("; ", ctxNum.SufelSkupiny.Select(s => $"{s.Nazov}:{s.JeKompletna}")));

    var storeAgg = new PartsStore();
    var asmAgg = new AssemblyStore();
    const string za = "9";
    void AddAgg(string name, int ks) =>
        storeAgg.Parts.Add(new Part(name, 400, 300, 18) { Zostava = za, PocetKs = ks });

    AddAgg("bok sufel", 8);
    AddAgg("celo sufel", 4);
    AddAgg("zad sufel", 4);
    AddAgg("dno sufel", 4);

    asmAgg.SyncFromParts(storeAgg.Parts);
    SufelAssemblyResolver.Resolve(storeAgg, asmAgg);
    var ctxAgg = asmAgg.GetContext(za)!;
    if (ctxAgg.SufelSkupiny.Count == 4
        && ctxAgg.SufelSkupiny.All(s => s.JeKompletna)
        && ctxAgg.SufelSkupiny.Select(s => s.Nazov).SequenceEqual(new[] { "Šufel 1", "Šufel 2", "Šufel 3", "Šufel 4" })
        && ctxAgg.SufelSkupiny.All(s => s.BokPart?.PocetKs == 2 && s.CeloPart?.PocetKs == 1))
        Pass("Sufel resolver – súhrnné riadky (8+4+4+4 ks → 4 šufle)");
    else
        Fail("Sufel resolver – súhrnné riadky", string.Join("; ", ctxAgg.SufelSkupiny.Select(s => $"{s.Nazov} bok={s.BokPart?.PocetKs}")));
}

// --- 6. Boky skrinky – L/P z (1)/(2), nie z čísla zostavy ---
{
    var parts = new List<Part>
    {
        new("bok (1)", 2000, 320, 18) { Zostava = "1", Poradie = 3 },
        new("bok (2)", 2000, 320, 18) { Zostava = "1", Poradie = 5 },
        new("bok sufel 1", 400, 300, 18) { Zostava = "1", PocetKs = 8 },
    };
    CabinetBokClassifier.Apply(parts);
    var bokL = parts.FirstOrDefault(p => p.Kind == PartKind.BokL);
    var bokP = parts.FirstOrDefault(p => p.Kind == PartKind.BokP);
    var sufelBok = parts.First(p => p.Name.StartsWith("bok sufel"));
    if (bokL?.Name == "bok (1)" && bokP?.Name == "bok (2)" && sufelBok.Kind == PartKind.Generic)
        Pass("CabinetBok – bok (1)/bok (2) vs bok sufel");
    else
        Fail("CabinetBok – bok (1)/bok (2)", $"L={bokL?.Name}/{bokL?.Kind}, P={bokP?.Name}/{bokP?.Kind}, sufel={sufelBok.Kind}");

    var store = new PartsStore();
    foreach (var p in parts)
        store.Parts.Add(p);
    var asm = new AssemblyStore();
    asm.SyncFromParts(store.Parts);
    if (asm.GetContext("1")?.ReferenceBok?.Name == "bok (1)")
        Pass("CabinetBok – ReferenceBok v 3D kontexte");
    else
        Fail("CabinetBok – ReferenceBok", asm.GetContext("1")?.ReferenceBok?.Name ?? "null");
}

// --- 7. Krycí bok nie je referencia ---
{
    var parts = new List<Part>
    {
        new("bok 1,2 kryci", 2130, 560, 18) { Zostava = "1", Poradie = 4, Kind = PartKind.BokL },
        new("bok 1", 2130, 560, 18) { Zostava = "1", Poradie = 3 },
        new("bok 2", 2130, 560, 18) { Zostava = "1", Poradie = 5 },
    };
    CabinetBokClassifier.Apply(parts);
    var store = new PartsStore();
    foreach (var p in parts)
        store.Parts.Add(p);
    var asm = new AssemblyStore();
    asm.SyncFromParts(store.Parts);
    var refName = asm.GetContext("1")?.ReferenceBok?.Name;
    if (refName == "bok 1" && parts.First(p => p.Name.StartsWith("bok 1,2")).Kind == PartKind.Generic)
        Pass("CabinetBok – krycí bok nie je referencia");
    else
        Fail("CabinetBok – krycí bok", $"ref={refName}, kryciKind={parts[0].Kind}");
}

// --- 8. „bok N“ = zostava N, nie L/P ---
{
    var parts = new List<Part>
    {
        new("bok 2", 2130, 560, 18) { Zostava = "2", Poradie = 3 },
    };
    CabinetBokClassifier.Apply(parts);
    if (parts[0].Kind == PartKind.BokL)
        Pass("CabinetBok – bok 2 v zostave 2 nie je BokP");
    else
        Fail("CabinetBok – bok 2 nie je BokP", $"kind={parts[0].Kind}");
}

// --- 9. Pomocný bok nie je referencia ---
{
    var parts = new List<Part>
    {
        new("pomocna bok 3 cista miera", 2130, 560, 18) { Zostava = "3", Poradie = 2, Kind = PartKind.BokL },
        new("bok 3", 2130, 560, 18) { Zostava = "3", Poradie = 3 },
    };
    CabinetBokClassifier.Apply(parts);
    var store = new PartsStore();
    foreach (var p in parts)
        store.Parts.Add(p);
    var asm = new AssemblyStore();
    asm.SyncFromParts(store.Parts);
    var refName = asm.GetContext("3")?.ReferenceBok?.Name;
    if (refName == "bok 3" && parts[0].Kind == PartKind.Generic)
        Pass("CabinetBok – pomocný bok nie je referencia");
    else
        Fail("CabinetBok – pomocný bok", $"ref={refName}, pomocnaKind={parts[0].Kind}");
}

// --- 10. Bok kusovník – rozdelenie ks a L/P ---
{
    var expander = new BokKusovnikExpander();

    static string Fmt(IEnumerable<Part> parts) =>
        string.Join("; ", parts.Select(p => $"{p.Name} z={p.Zostava} ks={p.PocetKs} {p.Kind}"));

    if (expander.TryExpand("1_bok 4", 2, out var v1))
    {
        var parts = v1.Select(v => new Part(v.Name, 100, 100, 18)
        {
            Zostava = v.Zostava, PocetKs = v.PocetKs, Kind = v.Kind
        }).ToList();
        var ok = parts.Count == 2
            && parts.Any(p => p.Name == "1_bok 4 L" && p.Zostava == "4" && p.PocetKs == 1 && p.Kind == PartKind.BokL)
            && parts.Any(p => p.Name == "1_bok 4 P" && p.Zostava == "4" && p.PocetKs == 1 && p.Kind == PartKind.BokP);
        if (ok) Pass("BokKusovnik – 1_bok 4 → L/P 1ks");
        else Fail("BokKusovnik – 1_bok 4", Fmt(parts));
    }
    else Fail("BokKusovnik – 1_bok 4", "TryExpand false");

    if (expander.TryExpand("3_bok 8,9,11", 6, out var v2))
    {
        var parts = v2.Select(v => new Part(v.Name, 100, 100, 18)
        {
            Zostava = v.Zostava, PocetKs = v.PocetKs, Kind = v.Kind
        }).ToList();
        var expected = new[]
        {
            ("3_bok 8 L", "8", 1, PartKind.BokL),
            ("3_bok 8 P", "8", 1, PartKind.BokP),
            ("3_bok 9 L", "9", 1, PartKind.BokL),
            ("3_bok 9 P", "9", 1, PartKind.BokP),
            ("3_bok 11 L", "11", 1, PartKind.BokL),
            ("3_bok 11 P", "11", 1, PartKind.BokP),
        };
        var ok = parts.Count == 6 && expected.All(e => parts.Any(p =>
            p.Name == e.Item1 && p.Zostava == e.Item2 && p.PocetKs == e.Item3 && p.Kind == e.Item4));
        if (ok) Pass("BokKusovnik – 3_bok 8,9,11 → 6×1ks L/P");
        else Fail("BokKusovnik – 3_bok 8,9,11", Fmt(parts));
    }
    else Fail("BokKusovnik – 3_bok 8,9,11", "TryExpand false");

    if (expander.TryExpand("8_bok L", 1, out var v3))
    {
        if (v3.Count != 1)
            Fail("BokKusovnik – 8_bok L", $"očakávaný 1 variant, je {v3.Count}: {string.Join("; ", v3.Select(x => x.Name))}");
        else if (v3[0].Name == "8_bok L" && v3[0].Zostava == "8" && v3[0].PocetKs == 1 && v3[0].Kind == PartKind.BokL)
            Pass("BokKusovnik – 8_bok L bez ďalšieho rozdelenia");
        else
            Fail("BokKusovnik – 8_bok L", $"{v3[0].Name} z={v3[0].Zostava} ks={v3[0].PocetKs} {v3[0].Kind}");
    }
    else Fail("BokKusovnik – 8_bok L", "TryExpand false");

    if (expander.TryExpand("14_bok L 8,9", 2, out var v4))
    {
        var parts = v4.Select(v => new Part(v.Name, 100, 100, 18)
        {
            Zostava = v.Zostava, PocetKs = v.PocetKs, Kind = v.Kind
        }).ToList();
        var ok = parts.Count == 2
            && parts.Any(p => p.Name == "14_bok 8 L" && p.Zostava == "8" && p.PocetKs == 1)
            && parts.Any(p => p.Name == "14_bok 9 L" && p.Zostava == "9" && p.PocetKs == 1);
        if (ok) Pass("BokKusovnik – 14_bok L 8,9 → 8 L + 9 L");
        else Fail("BokKusovnik – 14_bok L 8,9", Fmt(parts));
    }
    else Fail("BokKusovnik – 14_bok L 8,9", "TryExpand false");

    var bokRows = new List<Part>();
    foreach (var row in new[] { ("8_bok L", 1), ("8_bok P", 1) })
    {
        if (!expander.TryExpand(row.Item1, row.Item2, out var vv))
        {
            Fail("BokKusovnik – 8_bok L+P v zostave", $"TryExpand false pre {row.Item1}");
            bokRows.Clear();
            break;
        }
        foreach (var v in vv)
            bokRows.Add(new Part(v.Name, 2000, 560, 18) { Zostava = v.Zostava, PocetKs = v.PocetKs, Kind = v.Kind });
    }
    if (bokRows.Count == 2)
    {
        var store = new PartsStore();
        foreach (var p in bokRows) store.Parts.Add(p);
        var asm = new AssemblyStore();
        asm.SyncFromParts(store.Parts);
        var ctx = asm.GetContext("8");
        if (ctx?.ReferenceBok?.Kind == PartKind.BokL
            && bokRows.Any(p => p.Kind == PartKind.BokP && p.Zostava == "8"))
            Pass("BokKusovnik – 8_bok L + 8_bok P v jednej zostave");
        else
            Fail("BokKusovnik – 8_bok L+P v zostave", $"ref={ctx?.ReferenceBok?.Name}, P={bokRows.FirstOrDefault(p => p.Kind == PartKind.BokP)?.Name}");
    }
}

// --- 11. Sufel bok – dno18 z hrúbky dna ---
{
    var sk = new SufelSkupina();
    sk.DnoPart = new Part("sufel dno", 400, 300, 18);
    if (sk.BokMacro.Dno18)
        Pass("SufelBok – dno18 true pri dne 18 mm");
    else
        Fail("SufelBok – dno18 pri 18 mm", $"Dno18={sk.BokMacro.Dno18}");

    sk.DnoPart = new Part("sufel dno", 400, 300, 16);
    if (!sk.BokMacro.Dno18)
        Pass("SufelBok – dno18 false pri dne 16 mm");
    else
        Fail("SufelBok – dno18 pri 16 mm", $"Dno18={sk.BokMacro.Dno18}");

    sk.DnoPart = new Part("sufel dno", 500, 18, 300);
    if (sk.BokMacro.Dno18)
        Pass("SufelBok – dno18 true pri hrúbke v Dy (nie Dz)");
    else
        Fail("SufelBok – dno18 hrúbka v Dy", $"Dno18={sk.BokMacro.Dno18}, min={SufelBokMacroParams.ResolveDnoThicknessMm(sk.DnoPart)}");
}

// --- 12. Šufel – ABS x1 (Excel predná) → zadná hrana ---
{
    var abs = SufelAbsMapper.MapFromExcel("sufel bok vrchny", PartKind.SufelBok, 1, null, null, null);
    if (abs.Zadna == 1 && abs.Predna == null)
        Pass("SufelAbs – x1 predná z Excelu → zadná hrana");
    else
        Fail("SufelAbs – x1 → zadná", $"pred={abs.Predna}, zad={abs.Zadna}");

    var cabinet = SufelAbsMapper.MapFromExcel("bok L", PartKind.BokL, 1, null, null, null);
    if (cabinet.Predna == 1 && cabinet.Zadna == null)
        Pass("SufelAbs – skrinka bez premapovania");
    else
        Fail("SufelAbs – skrinka", $"pred={cabinet.Predna}, zad={cabinet.Zadna}");
}

// --- 13. Šufel – sync bok ↔ čelo/zad ---
{
    var sk = new SufelSkupina
    {
        CeloPart = new Part("sufel celo", 400, 200, 18) { Kind = PartKind.SufelCelo },
        ZadPart = new Part("sufel zad", 400, 200, 18) { Kind = PartKind.SufelZad },
    };
    sk.BokMacro.PocetDier = 3;
    sk.BokMacro.RoztecDierMm = 40;
    sk.BokMacro.PolohaDieryYmm = 50;
    SufelMacroSynchronizer.SyncAll(sk);

    var ok = Math.Abs(sk.BokMacro.PolohaDieryXmm - 9) < 0.01
        && sk.CeloZadMacro.PocetDier == 3
        && Math.Abs(sk.CeloZadMacro.RoztecDierMm - 40) < 0.01
        && Math.Abs(sk.CeloZadMacro.PolohaDieryMm - 48) < 0.01;
    if (ok)
        Pass("SufelMacro – PolohaDieryX=9, PolohaDiery=48 pri čele 18 mm");
    else
        Fail("SufelMacro – sync bok↔čelo/zad",
            $"X={sk.BokMacro.PolohaDieryXmm}, Pocet={sk.CeloZadMacro.PocetDier}, Poloha={sk.CeloZadMacro.PolohaDieryMm}");

    sk.CeloPart = new Part("sufel celo", 400, 200, 16) { Kind = PartKind.SufelCelo };
    SufelMacroSynchronizer.SyncPolohaDieryXFromCeloZad(sk);
    if (Math.Abs(sk.BokMacro.PolohaDieryXmm - 8) < 0.01)
        Pass("SufelMacro – PolohaDieryX=8 pri čele 16 mm");
    else
        Fail("SufelMacro – hrúbka 16 mm", $"X={sk.BokMacro.PolohaDieryXmm}");
}

// --- 14. Šufel – export názov súboru ---
{
    var part = new Part("sufel bok", 400, 200, 18)
    {
        Kind = PartKind.SufelBok,
        Poradie = 14,
        Zostava = "4",
    };
    var name = SufelXcsExportNames.BuildFileName(part);
    if (name == "14_sufel bok 4.xcs")
        Pass("SufelXcsExport – 14_sufel bok 4.xcs");
    else
        Fail("SufelXcsExport – názov", name);
}

// --- 15. Sufel bok – SetMacroParam + CreateMacro SufelBok2_novy ---
{
    var sk = new SufelSkupina
    {
        BokPart = new Part("sufel bok", 564, 320, 18) { Kind = PartKind.SufelBok },
        CeloPart = new Part("sufel celo", 564, 150, 18) { Kind = PartKind.SufelCelo },
        DnoPart = new Part("sufel dno", 500, 300, 18) { Kind = PartKind.SufelDno },
    };
    sk.BokMacro.PolohaDieryYmm = 50;
    sk.BokMacro.PocetDier = 2;
    sk.BokMacro.RoztecDierMm = 32;
    sk.ZadPart = new Part("sufel zad", 564, 150, 18) { Kind = PartKind.SufelZad };
    SufelMacroApplier.Apply(sk);

    var xcs = BuildXcs(sk.BokPart!);
    var macroIdx = xcs.IndexOf("CreateMacro(\"sufelbok2_novy\"", StringComparison.Ordinal);
    var ok = macroIdx >= 0
        && !xcs.Contains("ExecMacro(\"sufelbok2_novy\"", StringComparison.Ordinal)
        && xcs.Contains("SetMacroParam(\"PocetDier\",2);", StringComparison.Ordinal)
        && xcs.Contains("SetMacroParam(\"RoztecDier\",32);", StringComparison.Ordinal)
        && xcs.Contains("SetMacroParam(\"dno18\",true);", StringComparison.Ordinal)
        && xcs.IndexOf("SetMacroParam(\"PocetDier\",2);", StringComparison.Ordinal) < macroIdx
        && !xcs.Contains("SetMacroParam(\"dx1\"", StringComparison.Ordinal)
        && !xcs.Contains("SetMacroParam(\"dy1\"", StringComparison.Ordinal)
        && !xcs.Contains("SetMacroParam(\"dz1\"", StringComparison.Ordinal)
        && !xcs.Contains("SetMacroParam(\"PolohaDieryX\",9);", StringComparison.Ordinal)
        && !xcs.Contains("SetMacroParam(\"HlbkaDier\",13);", StringComparison.Ordinal)
        && !xcs.Contains("SetRetractStrategy", StringComparison.Ordinal)
        && !xcs.Contains("Obeh_novy_DTD", StringComparison.Ordinal);
    if (ok)
        Pass("SufelBok – SetMacroParam + CreateMacro len zmenené");
    else
        Fail("SufelBok – SetMacroParam + CreateMacro", xcs.Replace('\n', ' ')[..Math.Min(500, xcs.Length)]);

    if (!xcs.Contains("Obeh_novy_DTD", StringComparison.Ordinal))
        Pass("SufelBok – export bez obehu");
    else
        Fail("SufelBok – export bez obehu", "nájdený Obeh_novy_DTD");

    var celoXcs = BuildXcs(sk.CeloPart!);
    var celoMacroIdx = celoXcs.IndexOf("CreateMacro(\"sufelcelozad2\"", StringComparison.Ordinal);
    var celoOk = celoMacroIdx >= 0
        && !celoXcs.Contains("ExecMacro(\"sufelcelozad2\"", StringComparison.Ordinal)
        && celoXcs.Contains("SetMacroParam(\"CeloZad\",true);", StringComparison.Ordinal)
        && celoXcs.Contains("SetMacroParam(\"PolohaDiery\",48);", StringComparison.Ordinal)
        && celoXcs.Contains("SetMacroParam(\"RoztecDier\",32);", StringComparison.Ordinal)
        && celoXcs.IndexOf("SetMacroParam(\"CeloZad\",true);", StringComparison.Ordinal) < celoMacroIdx
        && !celoXcs.Contains("SetMacroParam(\"dx1\"", StringComparison.Ordinal);
    if (celoOk)
        Pass("SufelCelo – CreateMacro + CeloZad=true");
    else
        Fail("SufelCelo – export", celoXcs.Replace('\n', ' ')[..Math.Min(400, celoXcs.Length)]);

    var zadXcs = BuildXcs(sk.ZadPart!);
    var zadOk = zadXcs.Contains("CreateMacro(\"sufelcelozad2\"", StringComparison.Ordinal)
        && !zadXcs.Contains("ExecMacro(\"sufelcelozad2\"", StringComparison.Ordinal)
        && zadXcs.Contains("SetMacroParam(\"PolohaDiery\",48);", StringComparison.Ordinal)
        && !zadXcs.Contains("SetMacroParam(\"CeloZad\"", StringComparison.Ordinal)
        && !zadXcs.Contains("SetMacroParam(\"dx1\"", StringComparison.Ordinal);
    if (zadOk)
        Pass("SufelZad – CreateMacro bez CeloZad");
    else
        Fail("SufelZad – export", zadXcs.Replace('\n', ' ')[..Math.Min(400, zadXcs.Length)]);
}

// --- 16. VzoryXcs súbor na disku ---
{
    var repoRoot = FindRepoRoot();
    var vzorPath = Path.Combine(repoRoot, "VzoryXcs", "Polica-pevna.txt");
    if (File.Exists(vzorPath) && File.ReadAllText(vzorPath).Contains("Od spodu = 200", StringComparison.Ordinal))
        Pass("VzoryXcs/Polica-pevna.txt prítomný");
    else
        Fail("VzoryXcs/Polica-pevna.txt prítomný", vzorPath);
}

Console.WriteLine(new string('=', 50));
Console.WriteLine($"Výsledok: {passed} OK, {failures.Count} zlyhaní");

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Zlyhania:");
    foreach (var f in failures)
        Console.WriteLine($"  • {f}");
    Environment.Exit(1);
}

Console.WriteLine("Všetky testy prešli.");
Environment.Exit(0);

static void DumpSufelXcsSample()
{
    var sk = new SufelSkupina
    {
        BokPart = new Part("sufel bok", 564, 320, 18)
        {
            Kind = PartKind.SufelBok, Poradie = 14, Zostava = "4", PocetKs = 2,
            AbsZadna = 1,
        },
        CeloPart = new Part("sufel celo", 564, 150, 18)
        {
            Kind = PartKind.SufelCelo, Poradie = 15, Zostava = "4", PocetKs = 1,
        },
        ZadPart = new Part("sufel zad", 564, 150, 18)
        {
            Kind = PartKind.SufelZad, Poradie = 16, Zostava = "4", PocetKs = 1,
        },
        DnoPart = new Part("sufel dno", 500, 300, 18)
        {
            Kind = PartKind.SufelDno, Poradie = 17, Zostava = "4", PocetKs = 1,
        },
    };
    sk.BokMacro.PolohaDieryYmm = 50;
    sk.BokMacro.PocetDier = 2;
    sk.BokMacro.RoztecDierMm = 32;
    sk.BokMacro.HlbkaDrazkyMm = 12;
    sk.BokMacro.PaskaMm = 0.8;
    SufelMacroApplier.Apply(sk);

    var exporter = new XcsExporter();
    var outDir = Path.Combine(FindRepoRoot(), ".tmp_xcs_export");
    Directory.CreateDirectory(outDir);

    foreach (var part in new[] { sk.BokPart!, sk.CeloPart!, sk.ZadPart! })
    {
        var fileName = SufelXcsExportNames.BuildFileName(part);
        var path = Path.Combine(outDir, fileName);
        var content = exporter.BuildContent(part, XcsExporter.ContextFromPart(part));
        File.WriteAllText(path, content, Encoding.UTF8);
        Console.WriteLine($"===== {fileName} =====");
        Console.WriteLine(content);
        Console.WriteLine($"→ uložené: {path}");
        Console.WriteLine();
    }
}

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 8; i++)
    {
        if (Directory.Exists(Path.Combine(dir, "VzoryXcs")))
            return dir;
        var parent = Directory.GetParent(dir);
        if (parent == null)
            break;
        dir = parent.FullName;
    }
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
