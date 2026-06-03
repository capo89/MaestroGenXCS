using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Services;

/// <summary>
/// Mapovanie <see cref="DrillOperation"/> na partnerskú plochu podľa konkrétneho spoja.
/// <para>
/// Predvolene: hrana ↔ Top môže vymeniť osi (podľa pravidla v <see cref="ConnectionMap"/>).
/// Výnimka: spoj s <c>IdentityCoordinates</c> (napr. Bok L Top ↔ Bok P Top pri výške police)
/// – <c>x/y</c> ostávajú rovnaké, líši sa len plocha a <c>refpos</c> z mapy.
/// </para>
/// </summary>
public static class WorkplaneMapper
{
    public static (double X, double Y) MapPoint(PartFace src, PartFace dst, double x, double y, bool identityCoordinates)
    {
        if (identityCoordinates || src == dst)
            return (x, y);

        if (NeedsAxisSwap(src, dst))
            return (y, x);

        return (x, y);
    }

    public static bool NeedsAxisSwap(PartFace src, PartFace dst)
    {
        var edge = (PartFace f) => f is PartFace.Right or PartFace.Left;

        if (edge(src) && dst == PartFace.Top) return true;
        if (src == PartFace.Top && edge(dst)) return true;
        return false;
    }

    public static DrillOperation MapDrillPattern(
        DrillOperation src,
        PartFace dstFace,
        int dstRefpos,
        Guid connectionId,
        bool identityCoordinates,
        double targetPartDz)
    {
        var swap = !identityCoordinates && NeedsAxisSwap(src.Face, dstFace);

        var dst = new DrillOperation
        {
            Name = string.IsNullOrWhiteSpace(src.Name) ? "Drill" : src.Name,
            Face = dstFace,
            RefPos = dstRefpos,
            Diameter = src.Diameter,
            Depth = src.Depth,
            Tool = src.Tool,
            KindOfHole = src.KindOfHole,
            DischargerStep = src.DischargerStep,
            TemplateLabel = src.TemplateLabel,
            Description = src.Description,
            IsEnabled = src.IsEnabled,

            IsPropagated = true,
            // Všetky zrkadlá ukazujú na koreňovú (užívateľskú) operáciu – umožní kaskádu Bok L → Bok P → Dno.
            SourceOperationId = src.SourceOperationId ?? src.Id,
            SourceConnectionId = connectionId,
            PreniestNaDruhyBok = src.PreniestNaDruhyBok,
        };

        if (swap)
        {
            dst.CountX = src.CountY;
            dst.CountY = src.CountX;
            dst.PitchX = src.PitchY;
            dst.PitchY = src.PitchX;
            dst.XStart = src.YStart;
            dst.YStart = src.XStart;
        }
        else
        {
            dst.CountX = src.CountX;
            dst.CountY = src.CountY;
            dst.PitchX = src.PitchX;
            dst.PitchY = src.PitchY;
            dst.XStart = src.XStart;
            dst.YStart = src.YStart;
        }

        if (DrillOperation.IsEdgeFace(dstFace))
            dst.YStart = DrillOperation.CenterThicknessMm(targetPartDz);

        return dst;
    }
}
