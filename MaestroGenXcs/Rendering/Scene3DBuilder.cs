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
    private static readonly Brush HoleBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12));

    private const double BoardHoleDiameterMm = 8.0;
    private const double BoardHoleDepthMm = 13.0;
    private static readonly Brush EdgePinBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0x32, 0x32));
    private static readonly Brush PredvrtavanieBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
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

        var boardHoles = CollectBoardHoles(part, dx, dy, dz);
        var board = BuildBoard(dx, dy, dz, boardHoles, BoardBrush);
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
                if (hint is HoleHint h && IsBoardCutoutHole(h) && !IsPredvrtavanieHint(h))
                    continue;
                var visual = MaterializeHint(hint, dx, dy, dz);
                if (visual != null)
                    yield return visual;
            }
        }
    }

    private readonly record struct BoardHole(double X, double Y, PartFace Face, double Diameter, double Depth);

    private List<BoardHole> CollectBoardHoles(Part part, double dx, double dy, double dz)
    {
        var list = new List<BoardHole>();
        var drills = SelectDrillsForBoardHoles(part.Operations.OfType<DrillOperation>()).ToList();

        if (PreviewDrillOverlay is { IsEnabled: true } preview
            && preview.Face is PartFace.Top or PartFace.Bottom)
        {
            drills = drills
                .Where(d => d.KolikPartnerBok != preview.KolikPartnerBok)
                .Append(preview)
                .ToList();
        }

        foreach (var drill in drills)
        {
            if (HideDrillForPreview != null && ReferenceEquals(drill, HideDrillForPreview))
                continue;

            foreach (var (wx, wy) in drill.EnumerateWorldTopBottomCenters(dx, dy))
                TryAddBoardHole(list, new BoardHole(wx, wy, drill.Face, BoardHoleDiameterMm, Math.Min(BoardHoleDepthMm, dz)));
        }

        return list;
    }

    private static IEnumerable<DrillOperation> SelectDrillsForBoardHoles(IEnumerable<DrillOperation> all)
    {
        var enabled = all
            .Where(d => d.IsEnabled && !d.IsPropagated)
            .Where(d => !d.PreviewAsPredvrtavanie)
            .Where(d => d.Face is PartFace.Top or PartFace.Bottom)
            .ToList();
        foreach (var drill in enabled.Where(d => !d.KolikPartnerBok.HasValue))
            yield return drill;
        foreach (var group in enabled.Where(d => d.KolikPartnerBok.HasValue).GroupBy(d => d.KolikPartnerBok!.Value))
            yield return group.Last();
    }

    private static void TryAddBoardHole(List<BoardHole> list, BoardHole hole)
    {
        const double tol = 0.4;
        foreach (var existing in list)
        {
            if (existing.Face != hole.Face)
                continue;
            if (Math.Abs(existing.X - hole.X) < tol && Math.Abs(existing.Y - hole.Y) < tol)
                return;
        }

        list.Add(hole);
    }

    private static bool IsBoardCutoutHole(HoleHint h) =>
        h.Face is PartFace.Top or PartFace.Bottom;

    private static bool IsPredvrtavanieHint(HoleHint h) =>
        string.Equals(h.Tag, VisualHintTags.Predvrtavanie, StringComparison.OrdinalIgnoreCase);

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

    /// <summary>Doska + závrtanie Ø8 / hĺbka 13 (valec + tmavý kruh na ploche) – aj v 3D zostave.</summary>
    public Visual3D BuildBoardVisualForPart(Part part, Brush boardFill)
    {
        var dx = Math.Max(1, part.Dx);
        var dy = Math.Max(1, part.Dy);
        var dz = Math.Max(1, part.Dz);
        var holes = CollectBoardHoles(part, dx, dy, dz);
        return BuildBoard(dx, dy, dz, holes, boardFill);
    }

    /// <summary>Doska + závrtanie Ø8 / hĺbka 13 (valec + tmavý kruh na ploche).</summary>
    private static Visual3D BuildBoard(double dx, double dy, double dz, IReadOnlyList<BoardHole> holes, Brush boardFill)
    {
        var group = new ModelVisual3D();
        group.Children.Add(new BoxVisual3D
        {
            Center = new Point3D(dx / 2.0, dy / 2.0, dz / 2.0),
            Length = dx,
            Width = dy,
            Height = dz,
            Fill = boardFill
        });

        foreach (var hole in holes)
            group.Children.Add(BuildBoardHoleVisual(hole, dz));

        group.Children.Add(BuildBoardEdges(dx, dy, dz));
        return group;
    }

    /// <summary>12 hrán dosky (0..Dx, 0..Dy, 0..Dz) – tenké čierne čiary.</summary>
    private static LinesVisual3D BuildBoardEdges(double dx, double dy, double dz)
    {
        var lines = new LinesVisual3D { Color = Colors.Black, Thickness = 1.0 };
        void Edge(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            lines.Points.Add(new Point3D(x1, y1, z1));
            lines.Points.Add(new Point3D(x2, y2, z2));
        }

        Edge(0, 0, 0, dx, 0, 0);
        Edge(dx, 0, 0, dx, dy, 0);
        Edge(dx, dy, 0, 0, dy, 0);
        Edge(0, dy, 0, 0, 0, 0);

        Edge(0, 0, dz, dx, 0, dz);
        Edge(dx, 0, dz, dx, dy, dz);
        Edge(dx, dy, dz, 0, dy, dz);
        Edge(0, dy, dz, 0, 0, dz);

        Edge(0, 0, 0, 0, 0, dz);
        Edge(dx, 0, 0, dx, 0, dz);
        Edge(dx, dy, 0, dx, dy, dz);
        Edge(0, dy, 0, 0, dy, dz);

        return lines;
    }

    private static Visual3D BuildBoardHoleVisual(BoardHole hole, double panelDz)
    {
        var depth = Math.Min(hole.Depth, panelDz - 0.1);
        var isTop = hole.Face == PartFace.Top;
        var zOpen = isTop ? panelDz : 0;
        var zBottom = isTop ? panelDz - depth : depth;

        var group = new ModelVisual3D();
        group.Children.Add(new PipeVisual3D
        {
            Point1 = new Point3D(hole.X, hole.Y, zOpen),
            Point2 = new Point3D(hole.X, hole.Y, zBottom),
            Diameter = hole.Diameter,
            InnerDiameter = 0,
            Fill = HoleBrush
        });
        group.Children.Add(BuildHoleOpeningDisc(hole, zOpen, isTop));
        return group;
    }

    private static ModelVisual3D BuildHoleOpeningDisc(BoardHole hole, double zSurface, bool faceUp)
    {
        const int segments = 36;
        var radius = hole.Diameter / 2.0;
        var z = zSurface + (faceUp ? 0.35 : -0.35);
        var mesh = new MeshBuilder(false, true);
        var hub = new Point3D(hole.X, hole.Y, z);
        for (var i = 0; i < segments; i++)
        {
            var a0 = 2.0 * Math.PI * i / segments;
            var a1 = 2.0 * Math.PI * (i + 1) / segments;
            var p0 = new Point3D(hole.X + radius * Math.Cos(a0), hole.Y + radius * Math.Sin(a0), z);
            var p1 = new Point3D(hole.X + radius * Math.Cos(a1), hole.Y + radius * Math.Sin(a1), z);
            if (faceUp)
                mesh.AddTriangle(hub, p0, p1);
            else
                mesh.AddTriangle(hub, p1, p0);
        }

        var brush = HoleBrush;
        var material = new MaterialGroup
        {
            Children =
            {
                new DiffuseMaterial(brush),
                new EmissiveMaterial(brush)
            }
        };
        return new ModelVisual3D
        {
            Content = new GeometryModel3D
            {
                Geometry = mesh.ToMesh(),
                Material = material,
                BackMaterial = material
            }
        };
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

    /// <summary>Použitie mimo tejto triedy (napr. 3D skladanie zostavy) pre tvorbu vizuálu z hintu.</summary>
    public static Visual3D? CreateVisualFromHint(VisualHint hint, double dx, double dy, double dz) =>
        MaterializeHint(hint, dx, dy, dz);

    private static Visual3D? BuildHole(HoleHint h, double dx, double dy, double dz)
    {
        if (IsBoardCutoutHole(h) && !IsPredvrtavanieHint(h))
            return null;

        if (IsPredvrtavanieHint(h))
            return BuildPredvrtavanie(h, dx, dy, dz);

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
            ? PredvrtavanieBrush
            : drawEdgePin
                ? EdgePinBrush
                : HoleBrush;

        var pipe = new PipeVisual3D
        {
            Point1 = start,
            Point2 = end,
            Diameter = radius * 2,
            InnerDiameter = 0,
            Fill = brush
        };

        return pipe;
    }

    /// <summary>Predvŕtanie – červený piest len na povrchu (XCS hĺbka sa nerieši v 3D).</summary>
    private static Visual3D BuildPredvrtavanie(HoleHint h, double dx, double dy, double dz)
    {
        var radius = Math.Max(0.5, h.Diameter / 2.0);
        var protrude = VisualHintTags.PredvrtavanieNadPlochouMm;
        var into = Math.Max(0.5, h.Depth);

        Point3D outer;
        Point3D inner;
        switch (h.Face)
        {
            case PartFace.Top:
                outer = new Point3D(h.X, h.Y, dz + protrude);
                inner = new Point3D(h.X, h.Y, dz - into);
                break;
            case PartFace.Bottom:
                outer = new Point3D(h.X, h.Y, -protrude);
                inner = new Point3D(h.X, h.Y, into);
                break;
            case PartFace.Left:
                outer = new Point3D(-protrude, h.X, h.Y);
                inner = new Point3D(into, h.X, h.Y);
                break;
            case PartFace.Right:
                outer = new Point3D(dx + protrude, h.X, h.Y);
                inner = new Point3D(dx - into, h.X, h.Y);
                break;
            case PartFace.Front:
                outer = new Point3D(h.X, -protrude, h.Y);
                inner = new Point3D(h.X, into, h.Y);
                break;
            case PartFace.Back:
                outer = new Point3D(h.X, dy + protrude, h.Y);
                inner = new Point3D(h.X, dy - into, h.Y);
                break;
            default:
                outer = new Point3D(h.X, h.Y, dz + protrude);
                inner = new Point3D(h.X, h.Y, dz - into);
                break;
        }

        return new PipeVisual3D
        {
            Point1 = outer,
            Point2 = inner,
            Diameter = radius * 2,
            InnerDiameter = 0,
            Fill = PredvrtavanieBrush,
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


