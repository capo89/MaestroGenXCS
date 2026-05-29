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
        bool RequiresOppositeBokOptIn = false);

    private static readonly IReadOnlyList<Rule> Rules = BuildRules();

    private static Rule[] BuildRules() =>
    [
        // ── Dno ↔ Boky (vložené) ───────────────────────────────────────────────
        new(PartKind.BokL, PartFace.Right, 0, PartKind.Dno, PartFace.Top, 0, Note: "vložené"),
        new(PartKind.BokP, PartFace.Left,  2, PartKind.Dno, PartFace.Top, 2, Note: "vložené"),

        // ── Vrch ↔ Boky (vložené) ───────────────────────────────────────────────
        new(PartKind.BokL, PartFace.Left,  2, PartKind.Vrch, PartFace.Top, 2, Note: "vložené"),
        new(PartKind.BokP, PartFace.Right, 0, PartKind.Vrch, PartFace.Top, 0, Note: "vložené"),

        // ── Naložené / polica / priečka – v mape, propagácia z Kolíkov zatiaľ vypnutá ─
        new(PartKind.BokL, PartFace.Top, 2, PartKind.Dno, PartFace.Left,  2,
            Note: "naložené", PropagateOnUserDrill: false),
        new(PartKind.BokP, PartFace.Top, 0, PartKind.Dno, PartFace.Right, 0,
            Note: "naložené", PropagateOnUserDrill: false),
        new(PartKind.BokL, PartFace.Top, 0, PartKind.Vrch, PartFace.Right, 0,
            Note: "naložené", PropagateOnUserDrill: false),
        new(PartKind.BokP, PartFace.Top, 2, PartKind.Vrch, PartFace.Left,  2,
            Note: "naložené", PropagateOnUserDrill: false),
        new(PartKind.BokL, PartFace.Top, 2, PartKind.Priecka, PartFace.Left,  2,
            PropagateOnUserDrill: false),
        new(PartKind.BokP, PartFace.Top, 0, PartKind.Priecka, PartFace.Right, 0,
            PropagateOnUserDrill: false),
        new(PartKind.BokL, PartFace.Top, 2, PartKind.Polica, PartFace.Left,  2,
            Note: "pevná polica", PropagateOnUserDrill: false),
        new(PartKind.BokP, PartFace.Top, 0, PartKind.Polica, PartFace.Right, 0,
            Note: "pevná polica", PropagateOnUserDrill: false),

        // ── Bok L ↔ Bok P – výška (Top, rovnaké x, refpos z mapy) ───────────────
        new(PartKind.BokL, PartFace.Top, 2, PartKind.BokP, PartFace.Top, 0,
            Note: "výška polica", IdentityCoordinates: true),

        // ── Bok L ↔ Bok P (hrany) – len ak „Preniesť na druhý bok" ─────────────
        new(PartKind.BokL, PartFace.Right, 0, PartKind.BokP, PartFace.Left,  2,
            Note: "bok↔bok", RequiresOppositeBokOptIn: true),
        new(PartKind.BokL, PartFace.Left,  2, PartKind.BokP, PartFace.Right, 0,
            Note: "bok↔bok", RequiresOppositeBokOptIn: true),
    ];

    /// <summary>
    /// Kontaktná plocha dielca voči <see cref="PartFace.Top"/> referenčného boku (pravidlá „naložené“ / polica).
    /// </summary>
    public static PartFace GetContactFaceToReferenceBokTop(PartKind referenceBokKind, PartKind partKind)
    {
        var rule = Rules.FirstOrDefault(r =>
            r.FromKind == referenceBokKind
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

    public static IEnumerable<Connection> GenerateConnections(IEnumerable<Part> parts)
    {
        var byZostava = parts.GroupBy(p => p.Zostava ?? "");
        foreach (var grp in byZostava)
        {
            var members = grp.ToList();
            foreach (var rule in Rules)
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
