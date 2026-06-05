using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Rendering;

/// <summary>
/// 3D skladanie – referenčný Bok L (pohľad Top) + dielce (dno/vrch na stojaka, pri „bok vložený“ s korekciou polohy).
/// </summary>
public sealed class AssemblyScene3DBuilder
{
    private static readonly Scene3DBuilder BoardBuilder = new();

    private static readonly Brush ReferenceBrush =
        new SolidColorBrush(Color.FromRgb(0xC4, 0xA4, 0x6A));
    private static readonly Brush PlacedBrush =
        new SolidColorBrush(Color.FromRgb(0x6B, 0x9E, 0xC4));
    private static readonly Brush SelectedBrush =
        new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));

    public IEnumerable<Visual3D> BuildLayout(
        AssemblyContext? ctx,
        double guideX,
        double guideY,
        Part? selectedPart)
    {
        if (ctx?.ReferenceBok is not { } refBok)
            yield break;

        var refDz = refBok.Dz;
        var dx = refBok.Dx;
        var dy = refBok.Dy;
        var refKind = refBok.Kind;

        foreach (var v in BuildPart(refBok, null, ReferenceBrush))
            yield return v;
        foreach (var v in BuildOperationVisuals(refBok, null))
            yield return v;

        foreach (var placement in ctx.Placements.Where(p => p.IsPlacedInScene))
        {
            var brush = ReferenceEquals(placement.Part, selectedPart) ? SelectedBrush : PlacedBrush;
            var transform = AssemblyPartLayout.BuildPlacementTransform(placement, refBok, ctx.CorpusMode);
            foreach (var v in BuildPart(placement.Part, transform, brush))
                yield return v;
            foreach (var v in BuildOperationVisuals(placement.Part, transform))
                yield return v;

        }

        foreach (var guide in BuildGuideLines(dx, dy, refDz + 0.8, guideX, guideY))
            yield return guide;
    }

    public static (double Dx, double Dy, double Dz) GetSceneExtents(AssemblyContext? ctx)
    {
        var refBok = ctx?.ReferenceBok;
        return (
            Math.Max(1, refBok?.Dx ?? 600),
            Math.Max(1, refBok?.Dy ?? 320),
            Math.Max(1, refBok?.Dz ?? 18));
    }

    private static IEnumerable<Visual3D> BuildPart(Part part, Transform3D? transform, Brush fill)
    {
        var board = BoardBuilder.BuildBoardVisualForPart(part, fill);
        yield return Wrap(board, transform);
    }

    private static IEnumerable<Visual3D> BuildOperationVisuals(Part part, Transform3D? transform)
    {
        var dx = Math.Max(1, part.Dx);
        var dy = Math.Max(1, part.Dy);
        var dz = Math.Max(1, part.Dz);

        foreach (var op in part.Operations)
        {
            if (!op.IsEnabled)
                continue;

            foreach (var hint in op.BuildVisualHints(dx, dy, dz))
            {
                var visual = Scene3DBuilder.CreateVisualFromHint(hint, dx, dy, dz);
                if (visual != null)
                    yield return Wrap(visual, transform);
            }
        }
    }

    private static IEnumerable<Visual3D> BuildGuideLines(
        double dx, double dy, double z, double guideX, double guideY)
    {
        // Pravítko X sa zadáva od pravej hrany boku.
        var gx = Math.Clamp(dx - guideX, 0, dx);
        var gy = Math.Clamp(guideY, 0, dy);

        yield return DashedLine(
            new Point3D(gx, 0, z),
            new Point3D(gx, dy, z),
            Color.FromRgb(0xFF, 0xA5, 0x00));

        yield return DashedLine(
            new Point3D(0, gy, z),
            new Point3D(dx, gy, z),
            Color.FromRgb(0xFF, 0xA5, 0x00));
    }

    private static LinesVisual3D DashedLine(Point3D a, Point3D b, Color color)
    {
        const int segments = 24;
        var lines = new LinesVisual3D { Color = color, Thickness = 1.8 };
        var dir = b - a;
        for (var i = 0; i < segments; i += 2)
        {
            var t0 = (double)i / segments;
            var t1 = Math.Min(1.0, (double)(i + 1) / segments);
            lines.Points.Add(a + dir * t0);
            lines.Points.Add(a + dir * t1);
        }
        return lines;
    }

    private static Visual3D Wrap(Visual3D visual, Transform3D? transform)
    {
        if (transform == null)
            return visual;

        var container = new ModelVisual3D { Transform = transform };
        container.Children.Add(visual);
        return container;
    }
}
