using System.Text;
using System.Text.RegularExpressions;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Services;
using MaestroGenXcs.Sufle;
using MaestroGenXcs.Xcs;

// Automatické testy: XCS vzor, assembly solver, store, export.

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

// --- 6. Boky skrinky z kusovníka (bok (1), bok 2) ---
{
    var parts = new List<Part>
    {
        new("bok (1)", 2000, 320, 18) { Zostava = "1", Poradie = 3 },
        new("bok 2", 2000, 320, 18) { Zostava = "1", Poradie = 5 },
        new("bok sufel 1", 400, 300, 18) { Zostava = "1", PocetKs = 8 },
    };
    CabinetBokClassifier.Apply(parts);
    var bokL = parts.FirstOrDefault(p => p.Kind == PartKind.BokL);
    var bokP = parts.FirstOrDefault(p => p.Kind == PartKind.BokP);
    var sufelBok = parts.First(p => p.Name.StartsWith("bok sufel"));
    if (bokL?.Name == "bok (1)" && bokP?.Name == "bok 2" && sufelBok.Kind == PartKind.Generic)
        Pass("CabinetBok – bok (1)/bok 2 vs bok sufel");
    else
        Fail("CabinetBok – bok (1)/bok 2", $"L={bokL?.Name}/{bokL?.Kind}, P={bokP?.Name}/{bokP?.Kind}, sufel={sufelBok.Kind}");

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

// --- 8. Pomocný bok nie je referencia ---
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

// --- 9. VzoryXcs súbor na disku ---
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
