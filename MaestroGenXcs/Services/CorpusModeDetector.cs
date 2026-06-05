using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Services;

/// <summary>
/// Odvodí <see cref="AssemblyCorpusMode"/> z používateľských kolíkov na dne/vrchu a bokoch.
/// Pri zmiešanom stave (hrany + Top na dne) majú prioritu hrany – Top bez partnera boku nie je pre boky.
/// </summary>
public static class CorpusModeDetector
{
    public static AssemblyCorpusMode? Infer(IEnumerable<Part> partsInZostava)
    {
        var parts = partsInZostava.ToList();
        var panels = parts.Where(p => p.Kind is PartKind.Dno or PartKind.Vrch).ToList();
        var boky = parts.Where(p => p.Kind is PartKind.BokL or PartKind.BokP).ToList();

        if (panels.Any(HasEdgeUserDrill))
            return AssemblyCorpusMode.BokNalozeny;

        if (panels.Any(HasTopUserDrillWithBokPartner))
            return AssemblyCorpusMode.BokVlozeny;

        if (boky.Any(HasEdgeUserDrill))
            return AssemblyCorpusMode.BokVlozeny;

        return null;
    }

    private static bool HasEdgeUserDrill(Part part) =>
        part.Operations.OfType<DrillOperation>().Any(IsUserDrillOnEdge);

    private static bool HasTopUserDrillWithBokPartner(Part part) =>
        part.Operations.OfType<DrillOperation>().Any(o =>
            IsUserDrill(o)
            && o.Face == PartFace.Top
            && o.KolikPartnerBok is PartKind.BokL or PartKind.BokP);

    private static bool IsUserDrillOnEdge(DrillOperation op) =>
        IsUserDrill(op) && op.Face is PartFace.Left or PartFace.Right;

    private static bool IsUserDrill(DrillOperation op) =>
        op.IsEnabled && !op.IsPropagated && !op.IsTraverzaMaster && !op.IsPolicaMaster;
}
