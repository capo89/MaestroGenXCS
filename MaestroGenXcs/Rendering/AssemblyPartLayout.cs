using System.Windows.Media.Media3D;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;

namespace MaestroGenXcs.Rendering;

/// <summary>
/// Orientácia dielca v 3D zostave: referenčný bok = pohľad Top, ostatné = kontaktná plocha k Top boku.
/// </summary>
public static class AssemblyPartLayout
{
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

    public static PartFace ResolveContactFace(PartKind referenceBokKind, PartKind partKind, PartFace anchorFace) =>
        anchorFace is PartFace.Top or PartFace.Bottom
            ? GetContactFaceToReferenceBokTop(referenceBokKind, partKind)
            : anchorFace;

    public static Transform3D BuildPlacementTransform(
        AssemblyPlacement placement,
        PartKind referenceBokKind,
        double refDz)
    {
        var part = placement.Part;
        var contact = ResolveContactFace(referenceBokKind, part.Kind, placement.AnchorFace);

        var group = new Transform3DGroup();
        group.Children.Add(CreateContactFaceRotation(contact));
        group.Children.Add(new TranslateTransform3D(AlignmentAfterRotation(part, contact)));
        group.Children.Add(new TranslateTransform3D(placement.OffsetY, placement.OffsetDepthMm, refDz));
        return group;
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

    private static PartFace GetContactFaceToReferenceBokTop(PartKind referenceBokKind, PartKind partKind) =>
        ConnectionMap.GetContactFaceToReferenceBokTop(referenceBokKind, partKind);
}
