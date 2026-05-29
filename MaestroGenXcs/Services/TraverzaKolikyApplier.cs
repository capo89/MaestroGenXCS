using System.Globalization;
using System.Linq;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Services;

/// <summary>
/// Kolíky traverzy (Left/Right) + zrkadlené operácie na Bok L / Bok P Top.
/// Logika podľa starého <c>Priecka.PripravKolikyDoBokov</c> – nie cez globálny propagátor.
/// </summary>
public static class TraverzaKolikyApplier
{
    public const string SettingsTemplate = "Traverza kolíky";

    /// <summary>Pevné plochy – pri nastavení traverzy sa vytvoria vždy všetky štyri.</summary>
    public static IReadOnlyList<string> RequiredPlosky { get; } =
    [
        "Traverza · Left · refpos 2",
        "Traverza · Right · refpos 0",
        "Bok L · Top · refpos 2",
        "Bok P · Top · refpos 0",
    ];

    public sealed record Request(
        string Name,
        int Pocet,
        double Roztec,
        double PrvyKolikTraverza,
        double BokXStart,
        double BokYStart,
        bool PatternAlongX,
        double DepthEdge,
        string Tool,
        int DischargerStep);

    public static bool IsTraverzaPart(Part part) =>
        part.Kind == PartKind.Traverza
        || part.Name.Contains("traverz", StringComparison.OrdinalIgnoreCase);

    public static DrillOperation? FindTraverzaMaster(Part traverza) =>
        traverza.Operations.OfType<DrillOperation>().FirstOrDefault(o => o.IsTraverzaMaster);

    public static Request RequestFromMaster(DrillOperation master)
    {
        var pocet = master.TraverzaPatternAlongX
            ? Math.Max(1, master.CountX)
            : Math.Max(1, master.CountY);
        var roztec = master.TraverzaPatternAlongX ? master.PitchX : master.PitchY;

        return new Request(
            StripTraverzaSuffix(master.Name),
            pocet,
            roztec,
            master.XStart,
            master.TraverzaBokXStart,
            master.TraverzaBokYStart,
            master.TraverzaPatternAlongX,
            master.Depth,
            master.Tool,
            master.DischargerStep);
    }

    public static Request DefaultsFromStore(TemplateStore store)
    {
        var bag = store.GetAll(SettingsTemplate);
        return new Request(
            Name: "Kolik_traverza",
            Pocet: ReadInt(bag, "pocetKolikov", 2),
            Roztec: ReadDouble(bag, "roztecKolikov", 32),
            PrvyKolikTraverza: ReadDouble(bag, "prvyKolikTraverza", 37),
            BokXStart: ReadDouble(bag, "bokPrvyKolikX", 900),
            BokYStart: ReadDouble(bag, "bokPrvyKolikY", 37),
            PatternAlongX: ReadBool(bag, "patternPozdlzX", false),
            DepthEdge: ReadDouble(bag, "depthHrana", 23),
            Tool: ReadString(bag, "tool", "E071"),
            DischargerStep: ReadInt(bag, "dischargeSteps", 3));
    }

    /// <summary>Odstráni predchádzajúci súbor kolíkov traverzy (ak existuje).</summary>
    public static void RemoveExistingBundle(PartsStore store, Part traverza)
    {
        var masters = traverza.Operations.OfType<DrillOperation>()
            .Where(o => o.IsTraverzaMaster)
            .ToList();

        foreach (var master in masters)
        {
            foreach (var part in store.Parts.ToList())
            {
                var toRemove = part.Operations
                    .Where(op => op == master || op.SourceOperationId == master.Id)
                    .ToList();
                foreach (var op in toRemove)
                    part.Operations.Remove(op);
            }
        }
    }

    /// <summary>
    /// Vytvorí kolíky na traverze (Left refpos 2 + Right refpos 0) a na oboch bokoch Top.
    /// Volať z <see cref="OperationPropagator.ExecuteWithoutPropagation"/>.
    /// </summary>
    public static DrillOperation Apply(PartsStore store, Part traverza, Request req)
    {
        RemoveExistingBundle(store, traverza);

        var bokL = store.Parts.FirstOrDefault(p => p.Kind == PartKind.BokL && p.Zostava == traverza.Zostava);
        var bokP = store.Parts.FirstOrDefault(p => p.Kind == PartKind.BokP && p.Zostava == traverza.Zostava);
        if (bokL == null || bokP == null)
            throw new InvalidOperationException("V zostave chýba Bok L alebo Bok P – kolíky traverzy sa nedajú preniesť.");

        var pocet = Math.Max(1, req.Pocet);
        var roztec = Math.Max(1, req.Roztec);
        var yEdge = Math.Round(traverza.Dz / 2.0, 3);
        var prvy = Math.Max(0, req.PrvyKolikTraverza);

        var master = BuildTraverzaEdge(PartFace.Left, req, pocet, roztec, prvy, yEdge, suffix: "_L");
        master.IsTraverzaMaster = true;
        master.TraverzaBokXStart = req.BokXStart;
        master.TraverzaBokYStart = req.BokYStart;
        master.TraverzaPatternAlongX = req.PatternAlongX;

        var rightOp = BuildTraverzaEdge(PartFace.Right, req, pocet, roztec, prvy, yEdge, suffix: "_P");
        LinkMirror(rightOp, master);

        traverza.Operations.Add(master);
        traverza.Operations.Add(rightOp);

        var bokLOp = BuildBokTop(bokL, traverza.Name, pocet, roztec, req, refpos: 2);
        var bokPOp = BuildBokTop(bokP, traverza.Name, pocet, roztec, req, refpos: 0);
        LinkMirror(bokLOp, master);
        LinkMirror(bokPOp, master);

        bokL.Operations.Add(bokLOp);
        bokP.Operations.Add(bokPOp);

        return master;
    }

    public static void RefreshMirrors(PartsStore store, DrillOperation master)
    {
        if (!master.IsTraverzaMaster) return;

        var traverza = store.Parts.FirstOrDefault(p => p.Operations.Contains(master));
        if (traverza == null) return;

        var pocet = master.TraverzaPatternAlongX
            ? Math.Max(1, master.CountX)
            : Math.Max(1, master.CountY);
        var roztec = master.TraverzaPatternAlongX ? master.PitchX : master.PitchY;

        var baseName = StripTraverzaSuffix(master.Name);
        var req = new Request(
            baseName,
            pocet,
            roztec,
            master.XStart,
            master.TraverzaBokXStart,
            master.TraverzaBokYStart,
            master.TraverzaPatternAlongX,
            master.Depth,
            master.Tool,
            master.DischargerStep);

        pocet = Math.Max(1, req.Pocet);
        roztec = Math.Max(1, req.Roztec);
        var yEdge = Math.Round(traverza.Dz / 2.0, 3);
        var prvy = master.XStart;

        CopyEdgeState(BuildTraverzaEdge(PartFace.Left, req, pocet, roztec, prvy, yEdge, suffix: "_L"), master);

        var rightOnTraverza = traverza.Operations.OfType<DrillOperation>()
            .FirstOrDefault(o => o.SourceOperationId == master.Id && o.Face == PartFace.Right);
        if (rightOnTraverza != null)
            CopyDrillState(BuildTraverzaEdge(PartFace.Right, req, pocet, roztec, prvy, yEdge, suffix: "_P"), rightOnTraverza);

        foreach (var part in store.Parts)
        {
            foreach (var child in part.Operations.OfType<DrillOperation>()
                         .Where(o => o.SourceOperationId == master.Id).ToList())
            {
                var refpos = part.Kind == PartKind.BokL ? 2 : 0;
                if (part.Kind is not (PartKind.BokL or PartKind.BokP)) continue;

                var fresh = BuildBokTop(part, traverza.Name, pocet, roztec, req, refpos);
                LinkMirror(fresh, master);
                CopyDrillState(fresh, child);
            }
        }
    }

    private static string StripTraverzaSuffix(string name)
    {
        if (name.EndsWith("_L", StringComparison.Ordinal)) return name[..^2];
        if (name.EndsWith("_P", StringComparison.Ordinal)) return name[..^2];
        return name;
    }

    private static DrillOperation BuildTraverzaEdge(
        PartFace face, Request req, int pocet, double roztec, double prvy, double yEdge, string suffix)
    {
        int countX, countY;
        double pitchX, pitchY;
        if (req.PatternAlongX)
        {
            countX = pocet;
            countY = 1;
            pitchX = roztec;
            pitchY = 0;
        }
        else
        {
            countX = 1;
            countY = pocet;
            pitchX = 32;
            pitchY = roztec;
        }

        var refpos = face == PartFace.Left ? 2 : 0;

        return new DrillOperation
        {
            Name = req.Name + suffix,
            Face = face,
            RefPos = refpos,
            CountX = countX,
            CountY = countY,
            PitchX = pitchX,
            PitchY = pitchY,
            XStart = prvy,
            YStart = yEdge,
            Depth = req.DepthEdge,
            Diameter = 8,
            Tool = req.Tool,
            DischargerStep = req.DischargerStep,
            TemplateLabel = SettingsTemplate,
        };
    }

    private static DrillOperation BuildBokTop(
        Part bok, string traverzaName, int pocet, double roztec, Request req, int refpos)
    {
        int countX, countY;
        double pitchX, pitchY;
        if (req.PatternAlongX)
        {
            countX = 1;
            countY = pocet;
            pitchX = 32;
            pitchY = roztec;
        }
        else
        {
            countX = pocet;
            countY = 1;
            pitchX = roztec;
            pitchY = 32;
        }

        return new DrillOperation
        {
            Name = $"{bok.Name}_traverza_{traverzaName}",
            Face = PartFace.Top,
            RefPos = refpos,
            CountX = countX,
            CountY = countY,
            PitchX = pitchX,
            PitchY = pitchY,
            XStart = req.BokXStart,
            YStart = req.BokYStart,
            Depth = 13,
            Diameter = 8,
            Tool = req.Tool,
            DischargerStep = req.DischargerStep,
            TemplateLabel = $"{SettingsTemplate} · bok",
            IsPropagated = true,
        };
    }

    private static void LinkMirror(DrillOperation mirror, DrillOperation master)
    {
        mirror.SourceOperationId = master.Id;
        mirror.IsPropagated = true;
    }

    private static void CopyDrillState(DrillOperation from, DrillOperation to)
    {
        to.Name = from.Name;
        to.Face = from.Face;
        to.RefPos = from.RefPos;
        to.CountX = from.CountX;
        to.CountY = from.CountY;
        to.PitchX = from.PitchX;
        to.PitchY = from.PitchY;
        to.XStart = from.XStart;
        to.YStart = from.YStart;
        to.Diameter = from.Diameter;
        to.Depth = from.Depth;
        to.Tool = from.Tool;
        to.DischargerStep = from.DischargerStep;
        to.TemplateLabel = from.TemplateLabel;
        to.IsEnabled = from.IsEnabled;
    }

    private static void CopyEdgeState(DrillOperation from, DrillOperation to)
    {
        CopyDrillState(from, to);
        to.TraverzaBokXStart = from.TraverzaBokXStart;
        to.TraverzaBokYStart = from.TraverzaBokYStart;
        to.TraverzaPatternAlongX = from.TraverzaPatternAlongX;
        to.IsTraverzaMaster = true;
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> bag, string key, int fb) =>
        int.TryParse(ReadString(bag, key, fb.ToString(CultureInfo.InvariantCulture)),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fb;

    private static double ReadDouble(IReadOnlyDictionary<string, string> bag, string key, double fb) =>
        double.TryParse(ReadString(bag, key, fb.ToString(CultureInfo.InvariantCulture)).Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fb;

    private static bool ReadBool(IReadOnlyDictionary<string, string> bag, string key, bool fb)
    {
        var s = ReadString(bag, key, fb ? "true" : "false");
        return bool.TryParse(s, out var b) ? b : fb;
    }

    private static string ReadString(IReadOnlyDictionary<string, string> bag, string key, string fb) =>
        bag.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fb;
}
