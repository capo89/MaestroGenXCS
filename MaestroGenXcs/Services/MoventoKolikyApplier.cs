using MaestroGenXcs.Domain;
using MaestroGenXcs.Kovania;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Services;

/// <summary>
/// Z <see cref="AssemblyContext.MoventoSekcie"/> vygeneruje vŕtanie Movento na Top bokoch L/P.
/// </summary>
public static class MoventoKolikyApplier
{
    public const string BundleTemplateLabel = "Movento";

    public static bool IsManagedMoventoOperation(DrillOperation op) =>
        op.TemplateLabel == BundleTemplateLabel;

    /// <summary>
    /// Zmaže operácie z dielca; pri Movento na Bok L/P zrkadlí výmaz na partnerský bok
    /// (rovnaká sekcia/skupina, suffix <c>_L</c> / <c>_P</c>).
    /// Ak na zdrojovom boku neostane žiadne Movento, zmaže všetky Movento na partnerovi.
    /// </summary>
    public static void RemoveWithPartnerMirror(PartsStore store, Part part, IEnumerable<CncOperation> toRemove)
    {
        var list = toRemove.ToList();
        if (list.Count == 0)
            return;

        var moventoRemoved = list.OfType<DrillOperation>().Where(IsManagedMoventoOperation).ToList();
        var other = list.Except(moventoRemoved).ToList();

        foreach (var op in other)
            part.Operations.Remove(op);

        foreach (var op in moventoRemoved)
            part.Operations.Remove(op);

        if (part.Kind is not (PartKind.BokL or PartKind.BokP))
            return;

        var partner = ResolvePartnerBok(store, part);
        if (partner == null)
            return;

        foreach (var op in moventoRemoved)
        {
            var partnerOp = FindPartnerMoventoOperation(partner, op, part.Kind);
            if (partnerOp != null)
                partner.Operations.Remove(partnerOp);
        }

        if (!part.Operations.OfType<DrillOperation>().Any(IsManagedMoventoOperation))
            RemoveAllMoventoFromPart(partner);
    }

    public static void Apply(PartsStore store, AssemblyContext ctx, string zostava)
    {
        var bokL = store.Parts.FirstOrDefault(p =>
            p.Zostava == zostava && p.Kind == PartKind.BokL);
        var bokP = store.Parts.FirstOrDefault(p =>
            p.Zostava == zostava && p.Kind == PartKind.BokP);

        RemoveAllMoventoFromPart(bokL);
        RemoveAllMoventoFromPart(bokP);

        for (var si = 0; si < ctx.MoventoSekcie.Count; si++)
        {
            var sekcia = ctx.MoventoSekcie[si];
            ApplySekcia(bokL, PartKind.BokL, sekcia, si);
            ApplySekcia(bokP, PartKind.BokP, sekcia, si);
        }
    }

    private static void ApplySekcia(Part? bok, PartKind kind, SufelMoventoSekcia sekcia, int sekciaIndex)
    {
        if (bok == null)
            return;

        var patterny = Sufel_Movento.ResolveVrtaciePatternyPreBok(sekcia, kind);
        for (var pi = 0; pi < patterny.Count; pi++)
            bok.Operations.Add(BuildDrill(patterny[pi], sekcia, sekciaIndex, pi, kind));
    }

    public static void RemoveAllMoventoFromPart(Part? bok)
    {
        if (bok == null)
            return;

        var toRemove = bok.Operations.OfType<DrillOperation>()
            .Where(IsManagedMoventoOperation)
            .ToList();

        foreach (var op in toRemove)
            bok.Operations.Remove(op);
    }

    private static Part? ResolvePartnerBok(PartsStore store, Part part)
    {
        var partnerKind = part.Kind == PartKind.BokL ? PartKind.BokP : PartKind.BokL;
        return store.Parts.FirstOrDefault(p =>
            p.Zostava == part.Zostava && p.Kind == partnerKind);
    }

    private static DrillOperation? FindPartnerMoventoOperation(Part partner, DrillOperation op, PartKind sourceKind)
    {
        var partnerName = SwapMoventoBokSuffix(op.Name, sourceKind);
        return partner.Operations.OfType<DrillOperation>()
            .FirstOrDefault(d => IsManagedMoventoOperation(d) && d.Name == partnerName);
    }

    private static string SwapMoventoBokSuffix(string name, PartKind sourceKind) =>
        sourceKind switch
        {
            PartKind.BokL when name.EndsWith("_L", StringComparison.Ordinal) => string.Concat(name.AsSpan(0, name.Length - 2), "_P"),
            PartKind.BokP when name.EndsWith("_P", StringComparison.Ordinal) => string.Concat(name.AsSpan(0, name.Length - 2), "_L"),
            _ => name,
        };

    private static DrillOperation BuildDrill(
        MoventoVrtaciPattern pattern,
        SufelMoventoSekcia sekcia,
        int sekciaIndex,
        int patternIndex,
        PartKind bok)
    {
        // Top v DrillOperation: XStart = výška, YStart = hĺbka; pár po hĺbke = CountY + PitchY.
        // MoventoVrtaciPattern drží Maestro osi (X = hĺbka, Y = výška) pre export (2,1,32,32).
        var suffix = bok == PartKind.BokL ? "L" : "P";
        var pairAlongDepth = pattern.PouzivaPattern;
        return new DrillOperation
        {
            Name = $"Movento_s{sekciaIndex + 1}_g{patternIndex + 1}_{suffix}",
            Face = pattern.Face,
            RefPos = pattern.RefPos,
            CountX = 1,
            CountY = pairAlongDepth ? pattern.CountX : 1,
            PitchX = pattern.PitchX,
            PitchY = pairAlongDepth ? pattern.PitchX : pattern.PitchY,
            XStart = pattern.YStart,
            YStart = pattern.XStart,
            Diameter = Blum_Movento.PriemerMm,
            Depth = Blum_Movento.HlbkaMm,
            Tool = Blum_Movento.DefaultTool,
            KindOfHole = Blum_Movento.SpicatyVrtakKindOfHole,
            DischargerStep = Blum_Movento.DefaultDischargerStep,
            TemplateLabel = BundleTemplateLabel,
            PreviewAsPredvrtavanie = true,
            Description = $"Movento · {sekcia.Nazov}",
        };
    }
}
