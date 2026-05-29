using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Rendering;
using MaestroGenXcs.ViewModels;

namespace MaestroGenXcs.Views;

public partial class Assembly3DWindow : Window
{
    private readonly AssemblyScene3DBuilder _builder = new();
    private AssemblyViewModel? _vm;
    private Assembly3DPanelViewModel? _panel;
    private bool _dragging;
    private Point _dragStartScreen;
    private double _dragStartX;
    private double _dragStartDepth;

    public Assembly3DPanelViewModel Panel => _panel ??= new Assembly3DPanelViewModel(_vm!, OpenPinSettingsForPlacement);

    public Assembly3DWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild(resetCamera: true);
    }

    public void Attach(AssemblyViewModel vm)
    {
        DetachInternal();
        _vm = vm;
        _panel = new Assembly3DPanelViewModel(vm, OpenPinSettingsForPlacement);
        DataContext = this;
        _vm.Scene3DRefreshRequested += OnRefreshRequested;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _panel.PropertyChanged += OnPanelPropertyChanged;
        Rebuild(resetCamera: true);
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.PropertyName is nameof(Assembly3DPanelViewModel.GuideX)
            or nameof(Assembly3DPanelViewModel.GuideY))
            Rebuild(resetCamera: false);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.PropertyName == nameof(AssemblyViewModel.SelectedPart))
        {
            _panel?.RefreshInsertList();
            Rebuild(resetCamera: false);
        }
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
    {
        _ = sender; _ = e;
        Dispatcher.Invoke(() => Rebuild(resetCamera: false));
    }

    protected override void OnClosed(EventArgs e)
    {
        DetachInternal();
        base.OnClosed(e);
    }

    private void DetachInternal()
    {
        if (_vm == null) return;
        _vm.Scene3DRefreshRequested -= OnRefreshRequested;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        if (_panel != null)
            _panel.PropertyChanged -= OnPanelPropertyChanged;
        _vm = null;
        _panel = null;
    }

    private void Rebuild(bool resetCamera)
    {
        if (_vm == null || _panel == null)
            return;

        var zostava = _vm.SelectedPart?.Zostava;
        var ctx = _vm.GetAssemblyContext(zostava);

        for (var i = Viewport.Children.Count - 1; i >= 0; i--)
        {
            if (Viewport.Children[i] is DefaultLights)
                continue;
            Viewport.Children.RemoveAt(i);
        }

        var dragPart = _vm.ActivePlacementForDrag?.Part ?? _vm.SelectedPart;
        foreach (var visual in _builder.BuildLayout(ctx, _panel.GuideX, _panel.GuideY, dragPart))
            Viewport.Children.Add(visual);

        TitleLabel.Text = string.IsNullOrWhiteSpace(zostava) ? "Bez zostavy" : $"Zostava {zostava}";
        DetailLabel.Text = ctx?.ReferenceBok != null
            ? $"Referenčný bok: {ctx.ReferenceBok.Name} (plocha Top)"
            : "Chýba Bok L v zostave";
        var pl = _vm.ActivePlacementForDrag ?? _vm.SelectedPlacement;
        SelectedPartLabel.Text = pl is { IsPlacedInScene: true }
            ? $"Umiestnenie: X={pl.OffsetY:0} mm, Y={pl.OffsetDepthMm:0} mm"
            : "Vyber dielec – Vložiť alebo Upraviť";

        if (resetCamera)
        {
            SetTopView(ctx);
            Viewport.ZoomExtents(0);
        }
    }

    private void SetTopView(AssemblyContext? ctx)
    {
        var (dx, dy, _) = AssemblyScene3DBuilder.GetSceneExtents(ctx);
        var dist = Math.Max(dx, dy) * 1.6;
        Viewport.Camera.Position = new Point3D(dx / 2, dy / 2, dist);
        Viewport.Camera.LookDirection = new Vector3D(0, 0, -dist);
        Viewport.Camera.UpDirection = new Vector3D(0, 1, 0);
    }

    private void OnViewTop_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        SetTopView(_vm?.GetAssemblyContext(_vm.SelectedPart?.Zostava));
        Viewport.ZoomExtents(0);
    }

    private AssemblyPlacement? GetDraggablePlacement() =>
        _vm?.ActivePlacementForDrag is { IsPlacedInScene: true, IsLocked: false } active
            ? active
            : _vm?.SelectedPlacement is { IsPlacedInScene: true, IsLocked: false } sel
                ? sel
                : null;

    private void OnViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _ = sender;
        var placement = GetDraggablePlacement();
        if (placement == null)
            return;

        _vm!.ActivePlacementForDrag = placement;
        _dragging = true;
        _dragStartScreen = e.GetPosition(Viewport);
        _dragStartX = placement.OffsetY;
        _dragStartDepth = placement.OffsetDepthMm;
        Viewport.CaptureMouse();
        e.Handled = true;
    }

    private void OnViewport_MouseMove(object sender, MouseEventArgs e)
    {
        _ = sender;
        if (!_dragging || _vm == null || _panel == null)
            return;

        var current = e.GetPosition(Viewport);
        var (dx, dy) = ScreenDeltaToWorldXYmm(_dragStartScreen, current);
        _vm.SetPlacementOnTopPlane(_dragStartX + dx, _dragStartDepth + dy);
        e.Handled = true;
    }

    private void OnViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _ = sender;
        EndDrag();
        e.Handled = true;
    }

    private void OnViewport_MouseLeave(object sender, MouseEventArgs e)
    {
        _ = sender;
        if (_dragging)
            EndDrag();
    }

    private void EndDrag()
    {
        if (!_dragging || _vm == null)
            return;

        _dragging = false;
        Viewport.ReleaseMouseCapture();

        if (_vm.ActivePlacementForDrag is { IsPlacedInScene: true })
            _vm.ActivePlacementForDrag = null;

        Rebuild(resetCamera: false);
    }

    private (double Dx, double Dy) ScreenDeltaToWorldXYmm(Point start, Point end)
    {
        var a = Viewport3DHelper.UnProject(Viewport.Viewport, start);
        var b = Viewport3DHelper.UnProject(Viewport.Viewport, end);
        if (!a.HasValue || !b.HasValue)
            return ((end.X - start.X) * 2.0, (start.Y - end.Y) * 2.0);
        return (b.Value.X - a.Value.X, b.Value.Y - a.Value.Y);
    }

    private void OpenPinSettingsForPlacement(AssemblyPlacement placement)
    {
        if (_vm == null)
            return;

        _vm.SelectedPart = placement.Part;
        var editor = new PartEditorWindow(_vm, placement.Part) { Owner = this };
        editor.ShowDialog();
        _panel?.RefreshInsertList();
        Rebuild(resetCamera: false);
    }
}
