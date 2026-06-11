using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Rendering;
using MaestroGenXcs.ViewModels;

namespace MaestroGenXcs.Views;

public partial class SufelBokPanel : UserControl
{
    private readonly Scene3DBuilder _builder = new()
    {
        ShowRefposLabels = false,
        ShowFrontEdgeMarker = true,
        ShowFrontEdgeMarkerLabel = true,
        ShowAbsEdges = true,
        ShowAxisLabels = false,
        ShowPartCoordinateArrows = false,
        ShowSelectionFaceLabel = false,
    };

    private AssemblyViewModel? _vm;
    private SufelBokPanelViewModel? _panel;

    public AssemblyViewModel? AssemblyVm => _vm;

    public SufelBokPanelViewModel Panel => _panel ??= new SufelBokPanelViewModel(_vm!);

    public SufelBokPanel()
    {
        InitializeComponent();
        Unloaded += (_, _) => DetachInternal();
    }

    public void Attach(AssemblyViewModel vm)
    {
        DetachInternal();
        _vm = vm;
        _panel = new SufelBokPanelViewModel(vm);
        DataContext = this;
        _vm.PropertyChanged += OnVmPropertyChanged;
        Refresh();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.PropertyName is nameof(AssemblyViewModel.SelectedPart)
            or nameof(AssemblyViewModel.IsSufelBokView))
            Dispatcher.Invoke(Refresh);
    }

    private void DetachInternal()
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = null;
        _panel = null;
    }

    private void Refresh()
    {
        if (_vm == null || _panel == null)
            return;

        _panel.RefreshFromSelection();

        TitleLabel.Text = _panel.Title;
        DetailLabel.Text = string.IsNullOrWhiteSpace(_panel.SkupinaNazov)
            ? _panel.RozmeryText
            : $"{_panel.SkupinaNazov} · {_panel.RozmeryText}";

        RebuildScene(resetCamera: _panel.Part != null);
    }

    private void RebuildScene(bool resetCamera)
    {
        for (var i = Viewport.Children.Count - 1; i >= 0; i--)
        {
            if (Viewport.Children[i] is DefaultLights)
                continue;
            Viewport.Children.RemoveAt(i);
        }

        var part = _panel?.Part;
        if (part == null)
            return;

        foreach (var visual in _builder.Build(part, selectedFace: null))
            Viewport.Children.Add(visual);

        if (resetCamera)
        {
            SetTopView(part);
            Viewport.ZoomExtents(0);
        }
    }

    private void SetTopView(Part part)
    {
        var dx = Math.Max(1, part.Dx);
        var dy = Math.Max(1, part.Dy);
        var dist = Math.Max(dx, dy) * 1.6;
        Viewport.Camera.Position = new Point3D(dx / 2, dy / 2, dist);
        Viewport.Camera.LookDirection = new Vector3D(0, 0, -dist);
        Viewport.Camera.UpDirection = new Vector3D(0, 1, 0);
    }

    private void SetIsoView(Part part)
    {
        var dx = Math.Max(1, part.Dx);
        var dy = Math.Max(1, part.Dy);
        var dz = Math.Max(1, part.Dz);
        var dist = Math.Max(dx, Math.Max(dy, dz)) * 2.1;
        Viewport.Camera.Position = new Point3D(dx * 1.3, -dy * 1.1, dz * 1.4 + dist * 0.2);
        Viewport.Camera.LookDirection = new Vector3D(
            dx / 2.0 - Viewport.Camera.Position.X,
            dy / 2.0 - Viewport.Camera.Position.Y,
            dz / 2.0 - Viewport.Camera.Position.Z);
        Viewport.Camera.UpDirection = new Vector3D(0, 0, 1);
    }

    private void OnViewTop_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        var part = _panel?.Part;
        if (part == null)
            return;
        SetTopView(part);
    }

    private void OnViewIso_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        var part = _panel?.Part;
        if (part == null)
            return;
        SetIsoView(part);
        Viewport.ZoomExtents(0);
    }
}
