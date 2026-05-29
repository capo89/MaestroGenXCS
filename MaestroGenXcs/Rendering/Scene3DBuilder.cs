using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Rendering;

/// <summary>
/// Skladá 3D scénu pre HelixViewport3D.
/// <para>
/// AXIS CONVENTION (overené voči SCM Maestro Xilog 2026-05-13):
/// </para>
/// <list type="bullet">
///   <item>X = 0..Dx (DLZKA, zľava doprava)</item>
///   <item>Y = 0..Dy (HLBKA, od prednej hrany dozadu)</item>
///   <item>Z = 0..Dz (HRUBKA, zdola hore)</item>
/// </list>
/// <para>
/// Refpos rohy na Top ploche:
/// 0 = ľavý predný (X=0, Y=0),
/// 1 = ľavý zadný (X=0, Y=Dy),
/// 2 = pravý predný (X=Dx, Y=0),
/// 3 = pravý zadný (X=Dx, Y=Dy).
/// </para>
/// <para>
/// V Maestro Top pohľade je predná hrana DOLE › keď v aplikácii klikneš
/// tlačidlo TOP, kamera je nastavená tak, že Y=0 (predná hrana) je dole.
/// </para>
/// </summary>
public sealed class Scene3DBuilder
{
    private static readonly Brush BoardBrush = new SolidColorBrush(Color.FromRgb(0xDE, 0xB8, 0x87)); // jaseň
    private static readonly Brush HoleBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    private static readonly Brush EdgePinBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0x32, 0x32));
    private static readonly Brush ScrewBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
    private static readonly Color FrontEdgeColor = Color.FromRgb(0xFF, 0xA5, 0x00);

    public bool ShowAxisLabels { get; set; } = true;
    public bool ShowRefposLabels { get; set; } = true;
    public bool ShowFrontEdgeMarker { get; set; } = true;

    /// <summary>Text „PREDNA HRANA“ pri oranžovej čiare (čiara ostáva pri <see cref="ShowFrontEdgeMarker"/>).</summary>
    public bool ShowFrontEdgeMarkerLabel { get; set; } = true;

    /// <summary>RGB šípky osí pri diele (nie viewportová os v rohu).</summary>
    public bool ShowPartCoordinateArrows { get; set; } = true;

    /// <summary>Text pri výbere plochy (napr. „¦ TOP“).</summary>
    public bool ShowSelectionFaceLabel { get; set; } = true;

    /// <summary>Farebné čiary ABS na hranách Top plochy (ako v 2D náhľade).</summary>
    public bool ShowAbsEdges { get; set; } = true;

    /// <summary>Iba doska – bez operácií a výberu plôch (3D skladanie zostavy).</summary>
    public bool BoardOnly { get; set; }

    public DrillOperation? HideDrillForPreview { get; set; }

    public DrillOperation? PreviewDrillOverlay { get; set; }

    /// <summary>
    /// Posledná postavená "doska" – referenciu drží <see cref="Scene3DWindow"/>
    /// aby vedel filtrovať hit-testy iba na ňu a ignoroval ostatné Visual3D
    /// (osi, popisky, overlay výberu).
    /// </summary>
    public Visual3D? LastBoard { get; private set; }

    /// <summary>
    /// Postaví zoznam <see cref="Visual3D"/> objektov pre konkrétny dielec.
    /// </summary>
    public IEnumerable<Visual3D> Build(Part? part, PartFace? selectedFace = null)
    {
        LastBoard = null;
        if (part == null)
            yield break;

        var dx = Math.Max(1, part.Dx);
        var dy = Math.Max(1, part.Dy);
        var dz = Math.Max(1, part.Dz);

        var board = BuildBoard(dx, dy, dz);
        LastBoard = board;
        yield return board;

        if (ShowPartCoordinateArrows)
        {
            foreach (var visual in BuildAxes(dx, dy, dz))
                yield return visual;
        }

        if (ShowRefposLabels)
        {
            foreach (var v in BuildRefposLabels(dx, dy, dz))
                yield return v;
        }

        if (ShowFrontEdgeMarker)
        {
            foreach (var v in BuildFrontEdgeMarker(dx, dy, dz))
                yield return v;
        }

        if (ShowAbsEdges)
        {
            foreach (var v in BuildAbsEdges(part, dx, dy, dz))
                yield return v;
        }

        if (!BoardOnly && selectedFace.HasValue)
        {
            yield return BuildSelectionOverlay(selectedFace.Value, dx, dy, dz);
            foreach (var v in BuildSelectionEdges(selectedFace.Value, dx, dy, dz))
                yield return v;
            if (ShowSelectionFaceLabel)
                yield return BuildSelectionLabel(selectedFace.Value, dx, dy, dz);
        }

        if (BoardOnly)
            yield break;

        foreach (var op in part.Operations)
        {
            if (!op.IsEnabled) continue;
            if (HideDrillForPreview != null && ReferenceEquals(op, HideDrillForPreview))
                continue;
            foreach (var hint in op.BuildVisualHints(dx, dy, dz))
            {
                var visual = MaterializeHint(hint, dx, dy, dz);
                if (visual != null)
                    yield return visual;
            }
        }

        if (PreviewDrillOverlay != null)
        {
            foreach (var hint in PreviewDrillOverlay.BuildVisualHints(dx, dy, dz))
            {
                var visual = MaterializeHint(hint, dx, dy, dz);
                if (visual != null)
                    yield return visual;
            }
        }
    }

    private static Visual3D BuildSelectionOverlay(PartFace face, double dx, double dy, double dz)
    {
        const double offset = 0.6;
        var mesh = new MeshBuilder(true, false);
        switch (face)
        {
            case PartFace.Top:
                AddQuad(mesh,
                    new Point3D(0, 0, dz + offset),
                    new Point3D(dx, 0, dz + offset),
                    new Point3D(dx, dy, dz + offset),
                    new Point3D(0, dy, dz + offset));
                break;
            case PartFace.Bottom:
                AddQuad(mesh,
                    new Point3D(0, 0, -offset),
                    new Point3D(0, dy, -offset),
                    new Point3D(dx, dy, -offset),
                    new Point3D(dx, 0, -offset));
                break;
            case PartFace.Left:
                AddQuad(mesh,
                    new Point3D(-offset, 0, 0),
                    new Point3D(-offset, 0, dz),
                    new Point3D(-offset, dy, dz),
                    new Point3D(-offset, dy, 0));
                break;
            case PartFace.Right:
                AddQuad(mesh,
                    new Point3D(dx + offset, 0, 0),
                    new Point3D(dx + offset, dy, 0),
                    new Point3D(dx + offset, dy, dz),
                    new Point3D(dx + offset, 0, dz));
                break;
            case PartFace.Front:
                AddQuad(mesh,
                    new Point3D(0, -offset, 0),
                    new Point3D(dx, -offset, 0),
                    new Point3D(dx, -offset, dz),
                    new Point3D(0, -offset, dz));
                break;
            case PartFace.Back:
                AddQuad(mesh,
                    new Point3D(0, dy + offset, 0),
                    new Point3D(0, dy + offset, dz),
                    new Point3D(dx, dy + offset, dz),
                    new Point3D(dx, dy + offset, 0));
                break;
        }

        // Krytie zámerne nízke – výber má byť priehľadná „fólia" cez plochu,
        // nie premaľovaný dielec. Hlavné vizuálne posolstvo dáva obrys cez
        // <see cref="BuildSelectionEdges"/>.
        var diffuse = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0xBF, 0xFF)));
        var emissive = new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0xBF, 0xFF)));
        var material = new MaterialGroup { Children = { diffuse, emissive } };
        var model = new GeometryModel3D
        {
            Geometry = mesh.ToMesh(),
            Material = material,
            BackMaterial = material
        };
        return new ModelVisual3D { Content = model };
    }

    private static IEnumerable<Visual3D> BuildSelectionEdges(PartFace face, double dx, double dy, double dz)
    {
        const double offset = 0.6;
        Point3D[] corners = face switch
        {
            PartFace.Top => new[]
            {
                new Point3D(0, 0, dz + offset),
                new Point3D(dx, 0, dz + offset),
                new Point3D(dx, dy, dz + offset),
                new Point3D(0, dy, dz + offset)
            },
            PartFace.Bottom => new[]
            {
                new Point3D(0, 0, -offset),
                new Point3D(dx, 0, -offset),
                new Point3D(dx, dy, -offset),
                new Point3D(0, dy, -offset)
            },
            PartFace.Left => new[]
            {
                new Point3D(-offset, 0, 0),
                new Point3D(-offset, dy, 0),
                new Point3D(-offset, dy, dz),
                new Point3D(-offset, 0, dz)
            },
            PartFace.Right => new[]
            {
                new Point3D(dx + offset, 0, 0),
                new Point3D(dx + offset, dy, 0),
                new Point3D(dx + offset, dy, dz),
                new Point3D(dx + offset, 0, dz)
            },
            PartFace.Front => new[]
            {
                new Point3D(0, -offset, 0),
                new Point3D(dx, -offset, 0),
                new Point3D(dx, -offset, dz),
                new Point3D(0, -offset, dz)
            },
            PartFace.Back => new[]
            {
                new Point3D(0, dy + offset, 0),
                new Point3D(dx, dy + offset, 0),
                new Point3D(dx, dy + offset, dz),
                new Point3D(0, dy + offset, dz)
            },
            _ => Array.Empty<Point3D>()
        };

        if (corners.Length != 4) yield break;

        var lines = new LinesVisual3D
        {
            Color = Color.FromRgb(0x00, 0xE5, 0xFF),
            Thickness = 4
        };
        for (var i = 0; i < 4; i++)
        {
            lines.Points.Add(corners[i]);
            lines.Points.Add(corners[(i + 1) % 4]);
        }
        yield return lines;
    }

    private static void AddQuad(MeshBuilder mesh, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
    {
        mesh.AddTriangle(p0, p1, p2);
        mesh.AddTriangle(p0, p2, p3);
    }

    private static Visual3D BuildSelectionLabel(PartFace face, double dx, double dy, double dz)
    {
        const double offset = 1.0;
        var p = face switch
        {
            PartFace.Top    => new Point3D(dx / 2,        dy / 2,        dz + offset),
            PartFace.Bottom => new Point3D(dx / 2,        dy / 2,        -offset),
            PartFace.Left   => new Point3D(-offset,       dy / 2,        dz / 2),
            PartFace.Right  => new Point3D(dx + offset,   dy / 2,        dz / 2),
            PartFace.Front  => new Point3D(dx / 2,        -offset,       dz / 2),
            PartFace.Back   => new Point3D(dx / 2,        dy + offset,   dz / 2),
            _ => new Point3D(dx / 2, dy / 2, dz / 2)
        };

        var labelHeight = Math.Max(10, Math.Min(dx, Math.Min(dy, dz)) * 0.25);
        return new BillboardTextVisual3D
        {
            Position = p,
            Text = "¦ " + face.ToString().ToUpperInvariant(),
            Height = labelHeight,
            Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x00, 0x70, 0xB0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)),
            BorderThickness = new System.Windows.Thickness(2),
            Padding = new System.Windows.Thickness(6, 2, 6, 2),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = System.Windows.FontWeights.Bold
        };
    }

    private static Visual3D BuildBoard(double dx, double dy, double dz)
    {
        var center = new Point3D(dx / 2.0, dy / 2.0, dz / 2.0);
        var box = new BoxVisual3D
        {
            Center = center,
            Length = dx,
            Width = dy,
            Height = dz,
            Fill = BoardBrush
        };
        return box;
    }

    private IEnumerable<Visual3D> BuildAxes(double dx, double dy, double dz)
    {
        var size = Math.Max(dx, Math.Max(dy, dz)) * 0.18;

        var axes = new CoordinateSystemVisual3D
        {
            ArrowLengths = size,
            XAxisColor = Colors.Red,
            YAxisColor = Colors.LimeGreen,
            ZAxisColor = Colors.DeepSkyBlue
        };
        axes.Transform = new TranslateTransform3D(-size * 0.2, -size * 0.2, dz + 1);
        yield return axes;

        if (!ShowAxisLabels)
            yield break;

        var labelHeight = Math.Max(8, size * 0.18);

        yield return MakeLabel(
            new Point3D(-size * 0.2 + size, -size * 0.2, dz + 1),
            "X  (DLZKA, 0 -> Dx)", labelHeight, Colors.Red);
        yield return MakeLabel(
            new Point3D(-size * 0.2, -size * 0.2 + size, dz + 1),
            "Y  (HLBKA, 0 -> Dy)", labelHeight, Colors.LimeGreen);
        yield return MakeLabel(
            new Point3D(-size * 0.2, -size * 0.2, dz + 1 + size),
            "Z  (HRUBKA, 0 -> Dz)", labelHeight, Colors.DeepSkyBlue);
    }

    private static IEnumerable<Visual3D> BuildRefposLabels(double dx, double dy, double dz)
    {
        var labelHeight = Math.Max(8, Math.Max(dx, dy) * 0.04);

        yield return MakeLabel(
            new Point3D(-labelHeight * 0.3, -labelHeight * 0.3, dz + 0.5),
            "refpos 0", labelHeight, Colors.Yellow);
        yield return MakeLabel(
            new Point3D(-labelHeight * 0.3, dy + labelHeight * 0.3, dz + 0.5),
            "refpos 1", labelHeight, Colors.Yellow);
        yield return MakeLabel(
            new Point3D(dx + labelHeight * 0.3, -labelHeight * 0.3, dz + 0.5),
            "refpos 2", labelHeight, Colors.Yellow);
        yield return MakeLabel(
            new Point3D(dx + labelHeight * 0.3, dy + labelHeight * 0.3, dz + 0.5),
            "refpos 3", labelHeight, Colors.Yellow);
    }

    private IEnumerable<Visual3D> BuildFrontEdgeMarker(double dx, double dy, double dz)
    {
        _ = dy;
        var lines = new LinesVisual3D
        {
            Color = FrontEdgeColor,
            Thickness = 5
        };
        lines.Points.Add(new Point3D(0, 0, dz + 0.5));
        lines.Points.Add(new Point3D(dx, 0, dz + 0.5));
        yield return lines;

        if (!ShowFrontEdgeMarkerLabel)
            yield break;

        var labelHeight = Math.Max(8, dx * 0.04);
        yield return MakeLabel(
            new Point3D(dx / 2.0, -labelHeight * 1.5, dz + 0.5),
            "PREDNA HRANA (Y=0)", labelHeight, FrontEdgeColor);
    }

    private static IEnumerable<Visual3D> BuildAbsEdges(Part part, double dx, double dy, double dz)
    {
        var z = dz + 0.5;
        if (TryAbsLine(part.AbsPredna, 0, 0, z, dx, 0, z) is { } predna) yield return predna;
        if (TryAbsLine(part.AbsZadna, 0, dy, z, dx, dy, z) is { } zadna) yield return zadna;
        if (TryAbsLine(part.AbsLava, 0, 0, z, 0, dy, z) is { } lava) yield return lava;
        if (TryAbsLine(part.AbsPrava, dx, 0, z, dx, dy, z) is { } prava) yield return prava;
    }

    private static LinesVisual3D? TryAbsLine(int? abs, double x1, double y1, double z1, double x2, double y2, double z2)
    {
        var color = AbsToColor(abs);
        if (!color.HasValue)
            return null;

        var lines = new LinesVisual3D { Color = color.Value, Thickness = 4 };
        lines.Points.Add(new Point3D(x1, y1, z1));
        lines.Points.Add(new Point3D(x2, y2, z2));
        return lines;
    }

    private static Color? AbsToColor(int? abs) => abs switch
    {
        1 => Color.FromRgb(0x5B, 0x9B, 0xD5),
        2 => Color.FromRgb(0xFF, 0xA5, 0x00),
        8 => Color.FromRgb(0xE8, 0x55, 0x55),
        > 0 => Color.FromRgb(0xFF, 0xA5, 0x00),
        _ => null
    };

    private static Visual3D MakeLabel(Point3D position, string text, double height, Color color)
    {
        return new BillboardTextVisual3D
        {
            Position = position,
            Text = text,
            Height = height,
            Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x10, 0x10, 0x14)),
            BorderBrush = new SolidColorBrush(color),
            BorderThickness = new System.Windows.Thickness(1),
            Padding = new System.Windows.Thickness(3, 1, 3, 1),
            Foreground = new SolidColorBrush(color),
            FontWeight = System.Windows.FontWeights.Bold
        };
    }

    private static Visual3D? MaterializeHint(VisualHint hint, double dx, double dy, double dz)
    {
        return hint switch
        {
            HoleHint h => BuildHole(h, dx, dy, dz),
            LineHint l => BuildLine(l),
            _ => null
        };
    }

    private static Visual3D BuildHole(HoleHint h, double dx, double dy, double dz)
    {
        // Otvor zobrazujeme ako valec posunutý "do dosky" smerom kolmo
        // na danú plochu. Pri hranových kolíkoch kreslíme fyzický kolík:
        // 35 mm celkom = 22 mm v hrane + 13 mm vytŕča von.
        var depth = Math.Min(h.Depth, dz);
        var radius = Math.Max(0.5, h.Diameter / 2.0);

        Point3D start;
        Point3D end;
        var isEdgeFace = h.Face is PartFace.Left or PartFace.Right or PartFace.Front or PartFace.Back;
        var drawEdgePin = isEdgeFace && string.Equals(h.Tag, "drill", StringComparison.OrdinalIgnoreCase);

        switch (h.Face)
        {
            case PartFace.Top:
                start = new Point3D(h.X, h.Y, dz);
                end = start + new Vector3D(0, 0, -1) * depth;
                break;
            case PartFace.Bottom:
                start = new Point3D(h.X, h.Y, 0);
                end = start + new Vector3D(0, 0, 1) * depth;
                break;
            case PartFace.Left:
            {
                var facePoint = new Point3D(0, h.X, h.Y);
                if (drawEdgePin)
                {
                    start = facePoint + new Vector3D(-1, 0, 0) * 13.0;
                    end = facePoint + new Vector3D(1, 0, 0) * 22.0;
                }
                else
                {
                    start = facePoint;
                    end = start + new Vector3D(1, 0, 0) * depth;
                }
                break;
            }
            case PartFace.Right:
            {
                var facePoint = new Point3D(dx, h.X, h.Y);
                if (drawEdgePin)
                {
                    start = facePoint + new Vector3D(1, 0, 0) * 13.0;
                    end = facePoint + new Vector3D(-1, 0, 0) * 22.0;
                }
                else
                {
                    start = facePoint;
                    end = start + new Vector3D(-1, 0, 0) * depth;
                }
                break;
            }
            case PartFace.Front:
            {
                var facePoint = new Point3D(h.X, 0, h.Y);
                if (drawEdgePin)
                {
                    start = facePoint + new Vector3D(0, -1, 0) * 13.0;
                    end = facePoint + new Vector3D(0, 1, 0) * 22.0;
                }
                else
                {
                    start = facePoint;
                    end = start + new Vector3D(0, 1, 0) * depth;
                }
                break;
            }
            case PartFace.Back:
            {
                var facePoint = new Point3D(h.X, dy, h.Y);
                if (drawEdgePin)
                {
                    start = facePoint + new Vector3D(0, 1, 0) * 13.0;
                    end = facePoint + new Vector3D(0, -1, 0) * 22.0;
                }
                else
                {
                    start = facePoint;
                    end = start + new Vector3D(0, -1, 0) * depth;
                }
                break;
            }
            default:
                start = new Point3D(h.X, h.Y, dz);
                end = start + new Vector3D(0, 0, -1) * depth;
                break;
        }

        var brush = string.Equals(h.Tag, "screw", StringComparison.OrdinalIgnoreCase)
            ? ScrewBrush
            : drawEdgePin
                ? EdgePinBrush
                : HoleBrush;

        return new PipeVisual3D
        {
            Point1 = start,
            Point2 = end,
            Diameter = radius * 2,
            InnerDiameter = 0,
            Fill = brush
        };
    }

    private static Visual3D BuildLine(LineHint l)
    {
        var isObeh = string.Equals(l.Tag, "obeh", StringComparison.OrdinalIgnoreCase);
        var lines = new LinesVisual3D
        {
            // Magenta – odlišuje obeh od oranžového FRONT EDGE markeru aj od
            // yellow/oranžovej farby ABS=2 v 2D náhľade.
            Color = isObeh ? Color.FromRgb(0xFF, 0x14, 0x93) : Colors.DarkGray,
            Thickness = isObeh ? 4.0 : 1.5
        };
        lines.Points.Add(new Point3D(l.X1, l.Y1, l.Z1));
        lines.Points.Add(new Point3D(l.X2, l.Y2, l.Z2));
        return lines;
    }
}


