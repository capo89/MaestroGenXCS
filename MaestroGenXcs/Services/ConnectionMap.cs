using System.Collections.Generic;
using System.Linq;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Services;

/// <summary>
/// Pevná tabuľka partnerstiev pre <b>kolíky</b> (<see cref="OperationPropagator"/>).
/// Každý riadok = presná dvojica plôch + refpos na oboch stranách.
/// </summary>
public static class ConnectionMap
{
    public sealed record Rule(
        PartKind FromKind,
        PartFace FromFace,
        int FromRefpos,
        PartKind ToKind,
        PartFace ToFace,
        int ToRefpos,
        ConnectionType Type = ConnectionType.Kolikovy,
        string? Note = null,
        /// <summary>True = pri Kolíkoch sa tento spoj použije (inak len záznam v mape).</summary>
        bool PropagateOnUserDrill = true,
        /// <summary>True = x/y bez výmeny osí (Top↔Top výška police medzi bokmi).</summary>
        bool IdentityCoordinates = false,
        bool RequiresOppositeBokOptIn = false,
        /// <summary>null = platí v oboch režimoch korpusu; inak len pre daný režim (dno/vrch ↔ bok).</summary>
        AssemblyCorpusMode? CorpusModeOnly = null);

    private static readonly IReadOnlyList<Rule> Rules = BuildRules();

    private static Rule[] BuildRules() =>
    [
        // ── Dno ↔ Boky (vložené mapa – korpus Bok vložený) ─────────────────────
        new(PartKind.BokL, PartFace.Right, 0, PartKind.Dno, PartFace.Top, 0,
            Note: "vložené", CorpusModeOnly: AssemblyCorpusMode.BokVlozeny),
        new(PartKind.BokP, PartFace.Left,  2, PartKind.Dno, PartFace.Top, 2,
            Note: "vložené", CorpusModeOnly: AssemblyCorpusMode.BokVlozeny),

        // ── Vrch ↔ Boky (vložené) ───────────────────────────────────────────────
        new(PartKind.BokL, PartFace.Left,  2, PartKind.Vrch, PartFace.Top, 2,
            Note: "vložené", CorpusModeOnly: AssemblyCorpusMode.BokVlozeny),
        new(PartKind.BokP, PartFace.Right, 0, PartKind.Vrch, PartFace.Top, 0,
            Note: "vložené", CorpusModeOnly: AssemblyCorpusMode.BokVlozeny),

        // ── Dno/Vrch ↔ Boky (naložené mapa – korpus Bok nalozený) ───────────────
        new(PartKind.BokL, PartFace.Top, 2, PartKind.Dno, PartFace.Left,  2,
            Note: "naložené", CorpusModeOnly: AssemblyCorpusMode.BokNalozeny),
        new(PartKind.BokP, PartFace.Top, 0, PartKind.Dno, PartFace.Right, 0,
            Note: "naložené", CorpusModeOnly: AssemblyCorpusMode.BokNalozeny),
        new(PartKind.BokL, PartFace.Top, 0, PartKind.Vrch, PartFace.Right, 0,
            Note: "naložené", CorpusModeOnly: AssemblyCorpusMode.BokNalozeny),
        new(PartKind.BokP, PartFace.Top, 2, PartKind.Vrch, PartFace.Left,  2,
            Note: "naložené", CorpusModeOnly: AssemblyCorpusMode.BokNalozeny),

        // ── Polica / priečka (Top boku – oba režimy) ────────────────────────────
        new(PartKind.BokL, PartFace.Top, 2, PartKind.Priecka, PartFace.Left,  2,
            Note: "naložené"),
        new(PartKind.BokP, PartFace.Top, 0, PartKind.Priecka, PartFace.Right, 0,
            Note: "naložené"),
        new(PartKind.BokL, PartFace.Top, 2, PartKind.Polica, PartFace.Left,  2,
            Note: "pevná polica"),
        new(PartKind.BokP, PartFace.Top, 0, PartKind.Polica, PartFace.Right, 0,
            Note: "pevná polica"),

        // ── Bok L ↔ Bok P – výška (Top, rovnaké x, refpos z mapy) ───────────────
        new(PartKind.BokL, PartFace.Top, 2, PartKind.BokP, PartFace.Top, 0,
            Note: "výška polica", IdentityCoordinates: true),

        // ── Bok L ↔ Bok P (hrany) – len ak „Preniesť na druhý bok" ─────────────
        new(PartKind.BokL, PartFace.Right, 0, PartKind.BokP, PartFace.Left,  2,
            Note: "bok↔bok", RequiresOppositeBokOptIn: true),
        new(PartKind.BokL, PartFace.Left,  2, PartKind.BokP, PartFace.Right, 0,
            Note: "bok↔bok", RequiresOppositeBokOptIn: true),
    ];

    private static bool RuleApplies(Rule rule, AssemblyCorpusMode mode) =>
        rule.CorpusModeOnly is null || rule.CorpusModeOnly == mode;

    /// <summary>
    /// Kontaktná plocha dielca voči <see cref="PartFace.Top"/> referenčného boku (režim bok nalozený).
    /// </summary>
    public static PartFace GetContactFaceToReferenceBokTop(
        PartKind referenceBokKind,
        PartKind partKind,
        AssemblyCorpusMode corpusMode)
    {
        var rule = Rules.FirstOrDefault(r =>
            RuleApplies(r, corpusMode)
            && r.FromKind == referenceBokKind
            && r.FromFace == PartFace.Top
            && r.ToKind == partKind);

        if (rule != null)
            return rule.ToFace;

        return referenceBokKind switch
        {
            PartKind.BokP => PartFace.Right,
            _ => PartFace.Left
        };
    }

    /// <summary>
    /// Plocha dielca pre 3D skladanie (stojaci panel dno/vrch pri boku vloženom).
    /// </summary>
    public static PartFace GetLayoutContactFace(
        PartKind referenceBokKind,
        PartKind partKind,
        AssemblyCorpusMode corpusMode)
    {
        if (UsesVlozenyDnoVrchPlacement(partKind, corpusMode))
        {
            return (referenceBokKind, partKind) switch
            {
                (PartKind.BokL, PartKind.Dno) => PartFace.Left,
                (PartKind.BokL, PartKind.Vrch) => PartFace.Right,
                (PartKind.BokP, PartKind.Dno) => PartFace.Right,
                (PartKind.BokP, PartKind.Vrch) => PartFace.Left,
                _ => PartFace.Left
            };
        }

        return GetContactFaceToReferenceBokTop(referenceBokKind, partKind, corpusMode);
    }

    public static bool UsesVlozenyDnoVrchPlacement(PartKind partKind, AssemblyCorpusMode corpusMode) =>
        corpusMode == AssemblyCorpusMode.BokVlozeny
        && partKind is PartKind.Dno or PartKind.Vrch;

    public static IEnumerable<Connection> GenerateConnections(
        IEnumerable<Part> parts,
        Func<string, AssemblyCorpusMode>? getCorpusMode = null)
    {
        getCorpusMode ??= _ => AssemblyCorpusMode.BokVlozeny;

        var byZostava = parts.GroupBy(p => p.Zostava ?? "");
        foreach (var grp in byZostava)
        {
            var members = grp.ToList();
            var mode = getCorpusMode(grp.Key);

            foreach (var rule in Rules.Where(r => RuleApplies(r, mode)))
            {
                var sources = members.Where(p => p.Kind == rule.FromKind).ToList();
                var targets = members.Where(p => p.Kind == rule.ToKind).ToList();
                if (sources.Count == 0 || targets.Count == 0)
                    continue;

                foreach (var src in sources)
                foreach (var tgt in targets)
                {
                    var note = string.IsNullOrWhiteSpace(rule.Note)
                        ? "auto · ConnectionMap"
                        : $"auto · {rule.Note}";

                    yield return new Connection
                    {
                        PartA                  = src,
                        FaceA                  = rule.FromFace,
                        RefposA                = rule.FromRefpos,
                        PartB                  = tgt,
                        FaceB                  = rule.ToFace,
                        RefposB                = rule.ToRefpos,
                        Type                   = rule.Type,
                        AutoPropagate          = true,
                        PropagateOnUserDrill   = rule.PropagateOnUserDrill,
                        IdentityCoordinates    = rule.IdentityCoordinates,
                        RequiresOppositeBokOptIn = rule.RequiresOppositeBokOptIn,
                        Poznamka               = note,
                    };
                }
            }
        }
    }
}
