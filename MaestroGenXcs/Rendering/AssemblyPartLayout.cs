using System.Windows.Media.Media3D;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;

namespace MaestroGenXcs.Rendering;

/// <summary>
/// Orientácia dielca v 3D zostave: referenčný bok = pohľad Top, ostatné = kontaktná plocha k Top boku.
/// </summary>
public static class AssemblyPartLayout
{
    /// <summary>
    /// Rozsah osi X (ľavý okraj stojaceho panelu voči boku 0…refDx) pre vložené dno/vrch.
    /// Bok L: vrch −dz…0, dno refDx…refDx+dz; pri Bok P zrkadlene.
    /// </summary>
    public static (double MinX, double MaxX) GetVlozenyOffsetXRange(
        PartKind partKind,
        Part referenceBok,
        PartKind referenceBokKind)
    {
        var dz = Math.Max(1, referenceBok.Dz);
        var refDx = referenceBok.Dx;

        return (partKind, referenceBokKind) switch
        {
            (PartKind.Vrch, PartKind.BokL) => (-dz, 0),
            (PartKind.Vrch, _) => (refDx, refDx + dz),
            (PartKind.Dno, PartKind.BokL) => (refDx, refDx + dz),
            (PartKind.Dno, _) => (-dz, 0),
            _ => (0, refDx)
        };
    }

    /// <summary>Min/max <see cref="AssemblyPlacement.OffsetY"/> a max. <see cref="AssemblyPlacement.OffsetDepthMm"/> pri ťahaní.</summary>
    public static (double MinX, double MaxX, double MaxY) GetPlacementOffsetLimits(
        Part part,
        Part referenceBok,
        PartKind referenceBokKind,
        AssemblyCorpusMode corpusMode)
    {
        var contact = ResolveContactFace(referenceBokKind, part.Kind, PartFace.Left, corpusMode);
        var (footX, footY) = GetFootprintOnBokTop(part, contact);

        double minX;
        double maxX;
        if (ConnectionMap.UsesVlozenyDnoVrchPlacement(part.Kind, corpusMode))
            (minX, maxX) = GetVlozenyOffsetXRange(part.Kind, referenceBok, referenceBokKind);
        else
        {
            minX = 0;
            maxX = Math.Max(0, referenceBok.Dx - footX);
        }

        var maxY = Math.Max(0, referenceBok.Dy - footY);
        return (minX, maxX, maxY);
    }

    /// <summary>Os X/Y pôdorysu na ploche Top boku (mm) pre umiestnenie a clamp.</summary>
    public static (double SizeX, double SizeY) GetFootprintOnBokTop(Part part, PartFace contactFace)
    {
        var dx = Math.Max(1, part.Dx);
        var dy = Math.Max(1, part.Dy);
        var dz = Math.Max(1, part.Dz);

        return contactFace switch
        {
            PartFace.Top or PartFace.Bottom => (dx, dy),
            PartFace.Left or PartFace.Right => (dz, dy),
            PartFace.Front or PartFace.Back => (dx, dz),
            _ => (dx, dy)
        };
    }

    public static PartFace ResolveContactFace(
        PartKind referenceBokKind,
        PartKind partKind,
        PartFace anchorFace,
        AssemblyCorpusMode corpusMode)
    {
        if (ConnectionMap.UsesVlozenyDnoVrchPlacement(partKind, corpusMode))
            return ConnectionMap.GetLayoutContactFace(referenceBokKind, partKind, corpusMode);

        return anchorFace is PartFace.Top or PartFace.Bottom
            ? ConnectionMap.GetContactFaceToReferenceBokTop(referenceBokKind, partKind, corpusMode)
            : anchorFace;
    }

    public static Transform3D BuildPlacementTransform(
        AssemblyPlacement placement,
        Part referenceBok,
        AssemblyCorpusMode corpusMode)
    {
        var part = placement.Part;
        if (ConnectionMap.UsesVlozenyDnoVrchPlacement(part.Kind, corpusMode))
            return BuildVlozenyPanelTransform(placement, referenceBok);

        var contact = ResolveContactFace(referenceBok.Kind, part.Kind, placement.AnchorFace, corpusMode);

        var group = new Transform3DGroup();
        group.Children.Add(CreateContactFaceRotation(contact));
        group.Children.Add(new TranslateTransform3D(AlignmentAfterRotation(part, contact)));

        var tx = placement.OffsetY;
        var ty = placement.OffsetDepthMm;
        var tz = referenceBok.Dz;

        group.Children.Add(new TranslateTransform3D(tx, ty, tz));
        return group;
    }

    /// <summary>
    /// Dno/vrch na stojaka. <see cref="AssemblyPlacement.OffsetY"/> = ľavý okraj panelu v osi X voči boku (mm).
    /// </summary>
    private static Transform3D BuildVlozenyPanelTransform(AssemblyPlacement placement, Part referenceBok)
    {
        var part = placement.Part;
        var dz = part.Dz;
        var (minX, maxX, _) = GetPlacementOffsetLimits(
            part, referenceBok, referenceBok.Kind, AssemblyCorpusMode.BokVlozeny);
        var leftX = Math.Clamp(placement.OffsetY, minX, maxX);
        var ty = placement.OffsetDepthMm;

        var group = new Transform3DGroup();

        if (part.Kind == PartKind.Dno)
        {
            group.Children.Add(CreateContactFaceRotation(PartFace.Left));
            group.Children.Add(new TranslateTransform3D(dz + leftX, ty, 0));
            return group;
        }

        group.Children.Add(CreateContactFaceRotation(PartFace.Right));
        group.Children.Add(new TranslateTransform3D(leftX, ty, part.Dx));
        return group;
    }

    /// <summary>Predvolená os X (OffsetY) – ľavý okraj v zóne previsu pri referenčnom boku.</summary>
    public static double VlozenyDefaultOffsetX(Part part, Part referenceBok)
    {
        var (minX, _) = GetVlozenyOffsetXRange(part.Kind, referenceBok, referenceBok.Kind);
        return minX;
    }

    /// <summary>Obrys kontaktnej plochy v lokálnych súradniciach dielca (rovnaká konvencia ako <see cref="Scene3DBuilder"/>).</summary>
    public static IReadOnlyList<Point3D> GetContactFaceOutline(Part part, PartFace contactFace)
    {
        var dx = Math.Max(1, part.Dx);
        var dy = Math.Max(1, part.Dy);
        var dz = Math.Max(1, part.Dz);
        const double offset = 0.4;

        return contactFace switch
        {
            PartFace.Top => new[]
            {
                new Point3D(0, 0, dz + offset),
                new Point3D(dx, 0, dz + offset),
                new Point3D(dx, dy, dz + offset),
                new Point3D(0, dy, dz + offset),
            },
            PartFace.Bottom => new[]
            {
                new Point3D(0, 0, -offset),
                new Point3D(dx, 0, -offset),
                new Point3D(dx, dy, -offset),
                new Point3D(0, dy, -offset),
            },
            PartFace.Left => new[]
            {
                new Point3D(-offset, 0, 0),
                new Point3D(-offset, dy, 0),
                new Point3D(-offset, dy, dz),
                new Point3D(-offset, 0, dz),
            },
            PartFace.Right => new[]
            {
                new Point3D(dx + offset, 0, 0),
                new Point3D(dx + offset, dy, 0),
                new Point3D(dx + offset, dy, dz),
                new Point3D(dx + offset, 0, dz),
            },
            PartFace.Front => new[]
            {
                new Point3D(0, -offset, 0),
                new Point3D(dx, -offset, 0),
                new Point3D(dx, -offset, dz),
                new Point3D(0, -offset, dz),
            },
            PartFace.Back => new[]
            {
                new Point3D(0, dy + offset, 0),
                new Point3D(dx, dy + offset, 0),
                new Point3D(dx, dy + offset, dz),
                new Point3D(0, dy + offset, dz),
            },
            _ => Array.Empty<Point3D>()
        };
    }

    private static RotateTransform3D CreateContactFaceRotation(PartFace contactFace)
    {
        var axis = new Vector3D(0, 1, 0);
        var angle = contactFace switch
        {
            PartFace.Top => 0.0,
            PartFace.Left => -90.0,
            PartFace.Right => 90.0,
            PartFace.Front => 90.0,
            PartFace.Back => -90.0,
            _ => 0.0
        };

        if (contactFace is PartFace.Front or PartFace.Back)
            axis = new Vector3D(1, 0, 0);

        return new RotateTransform3D(new AxisAngleRotation3D(axis, angle));
    }

    private static Vector3D AlignmentAfterRotation(Part part, PartFace contactFace)
    {
        var dx = Math.Max(1, part.Dx);
        var dz = Math.Max(1, part.Dz);
        return contactFace switch
        {
            PartFace.Left => new Vector3D(dz, 0, 0),
            PartFace.Right => new Vector3D(0, 0, dx),
            _ => new Vector3D(0, 0, 0)
        };
    }
}
