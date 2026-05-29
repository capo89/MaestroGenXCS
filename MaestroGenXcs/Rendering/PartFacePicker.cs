using System.Windows.Media.Media3D;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Rendering;

/// <summary>Určí plochu dielca z lokálneho bodu zásahu (0..Dx, 0..Dy, 0..Dz).</summary>
public static class PartFacePicker
{
    public static PartFace? PickFromLocalPoint(double dx, double dy, double dz, Point3D p)
    {
        if (dx < 1 || dy < 1 || dz < 1)
            return null;

        var x = Math.Clamp(p.X, 0, dx);
        var y = Math.Clamp(p.Y, 0, dy);
        var z = Math.Clamp(p.Z, 0, dz);

        var dLeft = x;
        var dRight = dx - x;
        var dFront = y;
        var dBack = dy - y;
        var dBottom = z;
        var dTop = dz - z;

        var min = Math.Min(Math.Min(Math.Min(dLeft, dRight), Math.Min(dFront, dBack)), Math.Min(dBottom, dTop));

        if (min == dTop) return PartFace.Top;
        if (min == dBottom) return PartFace.Bottom;
        if (min == dLeft) return PartFace.Left;
        if (min == dRight) return PartFace.Right;
        if (min == dFront) return PartFace.Front;
        return PartFace.Back;
    }

    public static PartFace? PickFromNormal(Vector3D normal)
    {
        var n = normal;
        n.Normalize();
        var ax = Math.Abs(n.X);
        var ay = Math.Abs(n.Y);
        var az = Math.Abs(n.Z);

        if (az >= ax && az >= ay)
            return n.Z > 0 ? PartFace.Top : PartFace.Bottom;
        if (ax >= ay)
            return n.X > 0 ? PartFace.Right : PartFace.Left;
        return n.Y > 0 ? PartFace.Back : PartFace.Front;
    }

    /// <summary>
    /// Určí plochu prieniku lúča s kvádrom dielca (0..Dx, 0..Dy, 0..Dz).
    /// Spodná plocha sa ignoruje – používateľ pracuje s 5 plochami.
    /// </summary>
    public static PartFace? PickFromRay(Point3D origin, Vector3D direction, double dx, double dy, double dz)
    {
        if (dx < 1 || dy < 1 || dz < 1)
            return null;

        var d = direction;
        if (d.LengthSquared < 1e-12)
            return null;
        d.Normalize();

        const double eps = 1e-4;
        const double tol = 0.5;
        PartFace? bestFace = null;
        var bestT = double.PositiveInfinity;

        void Consider(PartFace face, double t, double px, double py, double pz)
        {
            if (t < eps || t >= bestT)
                return;
            if (px < -tol || px > dx + tol || py < -tol || py > dy + tol || pz < -tol || pz > dz + tol)
                return;
            bestT = t;
            bestFace = face;
        }

        if (Math.Abs(d.Z) > eps)
        {
            var t = (dz - origin.Z) / d.Z;
            var p = origin + d * t;
            Consider(PartFace.Top, t, p.X, p.Y, p.Z);
        }

        if (Math.Abs(d.X) > eps)
        {
            var t = (0 - origin.X) / d.X;
            var p = origin + d * t;
            Consider(PartFace.Left, t, p.X, p.Y, p.Z);

            t = (dx - origin.X) / d.X;
            p = origin + d * t;
            Consider(PartFace.Right, t, p.X, p.Y, p.Z);
        }

        if (Math.Abs(d.Y) > eps)
        {
            var t = (0 - origin.Y) / d.Y;
            var p = origin + d * t;
            Consider(PartFace.Front, t, p.X, p.Y, p.Z);

            t = (dy - origin.Y) / d.Y;
            p = origin + d * t;
            Consider(PartFace.Back, t, p.X, p.Y, p.Z);
        }

        return bestFace;
    }
}
