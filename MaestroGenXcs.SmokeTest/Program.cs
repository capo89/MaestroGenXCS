using System.Text;
using System.Text.RegularExpressions;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Services;
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

// --- 5. VzoryXcs súbor na disku ---
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
