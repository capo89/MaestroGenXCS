using System.Windows;
using System.Windows.Input;
using System.Globalization;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Rendering;
using MaestroGenXcs.Services;
using MaestroGenXcs.ViewModels;

namespace MaestroGenXcs.Views;

public partial class PartEditorWindow : Window
{
    private readonly AssemblyViewModel _vm;
    private Part _part;
    private readonly Scene3DBuilder _builder = new()
    {
        ShowRefposLabels = false,
        ShowFrontEdgeMarker = true,
        ShowFrontEdgeMarkerLabel = false,
        ShowAbsEdges = true,
        ShowAxisLabels = false,
        ShowPartCoordinateArrows = false,
        ShowSelectionFaceLabel = false,
    };
    private PartFace? _selectedFace;
    private bool _suppressEditEvents;
    private DrillOperation? _selectedDrill;

    public PartEditorWindow(AssemblyViewModel vm, Part part)
    {
        _vm = vm;
        _part = part;
        InitializeComponent();

        _vm.SelectedPart = part;
        Title = $"Editor dielca – {part.Name}";
        WirePartOperationsChanged(part);

        if (PolicaKolikyApplier.IsPolicaPart(part) || TraverzaKolikyApplier.IsTraverzaPart(part))
        {
            SpecialKolikyButton.Visibility = Visibility.Visible;
            HintLabel.Text = PolicaKolikyApplier.IsPolicaPart(part)
                ? "Použi tlačidlo Nastaviť kolíky pre policu, alebo klikni na plochu."
                : "Použi tlačidlo Nastaviť kolíky pre traverzu, alebo klikni na plochu.";
        }

        Loaded += (_, _) =>
        {
            UpdateHeader();
            UpdateNotePreview();
            RebuildScene(resetCamera: true);
            RefreshOperationsList();
        };
    }

    private void WirePartOperationsChanged(Part part)
    {
        part.Operations.CollectionChanged -= OnPartOperationsChanged;
        part.Operations.CollectionChanged += OnPartOperationsChanged;
    }

    private void OnPartOperationsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _ = sender; _ = e;
        if (_suppressEditEvents)
            return;
        Dispatcher.BeginInvoke(() => RebuildScene(resetCamera: false),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void AfterKolikySaved(PartFace face)
    {
        _selectedFace = null;
        RefreshOperationsList();
        RebuildScene(resetCamera: false);
        if (face == PartFace.Top)
            SetTopView();
    }

    private void RebuildScene(bool resetCamera)
    {
        for (var i = Viewport.Children.Count - 1; i >= 0; i--)
        {
            if (Viewport.Children[i] is DefaultLights)
                continue;
            Viewport.Children.RemoveAt(i);
        }

        foreach (var visual in _builder.Build(_part, _selectedFace))
            Viewport.Children.Add(visual);

        SelectedFaceLabel.Text = _selectedFace.HasValue
            ? $"Vybraná plocha: {_selectedFace.Value.ToWorkplane()}"
            : "Klikni na plochu pre kolíky";

        if (resetCamera)
        {
            SetIsoView();
            Viewport.ZoomExtents(0);
        }
    }

    private void SetIsoView()
    {
        var dx = Math.Max(1, _part.Dx);
        var dy = Math.Max(1, _part.Dy);
        var dz = Math.Max(1, _part.Dz);
        var dist = Math.Max(dx, Math.Max(dy, dz)) * 2.1;
        Viewport.Camera.Position = new Point3D(dx * 1.3, -dy * 1.1, dz * 1.4 + dist * 0.2);
        Viewport.Camera.LookDirection = new Vector3D((dx / 2.0) - Viewport.Camera.Position.X, (dy / 2.0) - Viewport.Camera.Position.Y, (dz / 2.0) - Viewport.Camera.Position.Z);
        Viewport.Camera.UpDirection = new Vector3D(0, 0, 1);
    }

    private void SetTopView()
    {
        var dx = Math.Max(1, _part.Dx);
        var dy = Math.Max(1, _part.Dy);
        var dist = Math.Max(dx, dy) * 1.6;
        Viewport.Camera.Position = new Point3D(dx / 2, dy / 2, dist);
        Viewport.Camera.LookDirection = new Vector3D(0, 0, -dist);
        Viewport.Camera.UpDirection = new Vector3D(0, 1, 0);
    }

    private void SetLeftView()
    {
        var dx = Math.Max(1, _part.Dx);
        var dy = Math.Max(1, _part.Dy);
        var dz = Math.Max(1, _part.Dz);
        var dist = Math.Max(dx, Math.Max(dy, dz)) * 2.0;
        Viewport.Camera.Position = new Point3D(-dist, dy / 2.0, dz / 2.0);
        Viewport.Camera.LookDirection = new Vector3D(dist + dx / 2.0, 0, 0);
        Viewport.Camera.UpDirection = new Vector3D(0, 0, 1);
    }

    private void SetRightView()
    {
        var dx = Math.Max(1, _part.Dx);
        var dy = Math.Max(1, _part.Dy);
        var dz = Math.Max(1, _part.Dz);
        var dist = Math.Max(dx, Math.Max(dy, dz)) * 2.0;
        Viewport.Camera.Position = new Point3D(dx + dist, dy / 2.0, dz / 2.0);
        Viewport.Camera.LookDirection = new Vector3D(-(dist + dx / 2.0), 0, 0);
        Viewport.Camera.UpDirection = new Vector3D(0, 0, 1);
    }

    private void SetFrontView()
    {
        var dx = Math.Max(1, _part.Dx);
        var dy = Math.Max(1, _part.Dy);
        var dz = Math.Max(1, _part.Dz);
        var dist = Math.Max(dx, Math.Max(dy, dz)) * 2.0;
        Viewport.Camera.Position = new Point3D(dx / 2.0, -dist, dz / 2.0);
        Viewport.Camera.LookDirection = new Vector3D(0, dist + dy / 2.0, 0);
        Viewport.Camera.UpDirection = new Vector3D(0, 0, 1);
    }

    private void SetBackView()
    {
        var dx = Math.Max(1, _part.Dx);
        var dy = Math.Max(1, _part.Dy);
        var dz = Math.Max(1, _part.Dz);
        var dist = Math.Max(dx, Math.Max(dy, dz)) * 2.0;
        Viewport.Camera.Position = new Point3D(dx / 2.0, dy + dist, dz / 2.0);
        Viewport.Camera.LookDirection = new Vector3D(0, -(dist + dy / 2.0), 0);
        Viewport.Camera.UpDirection = new Vector3D(0, 0, 1);
    }

    private void RefreshOperationsList()
    {
        OperationsList.ItemsSource = null;
        OperationsList.ItemsSource = _part.Operations.ToList();
        UpdateHeader();
    }

    private void UpdateHeader()
    {
        HeaderPartLabel.Text = _part.Name;
        var enabled = _part.Operations.Count(o => o.IsEnabled);
        HeaderCountLabel.Text = $"{enabled}/{_part.Operations.Count} aktívnych · {_part.Dx:0.##}x{_part.Dy:0.##}x{_part.Dz:0.##}";
        Title = $"Editor dielca – {_part.Name}";
        TitleLabel.Text = $"{_part.Name}  ({_part.Dx:0} × {_part.Dy:0} × {_part.Dz:0} mm)";
    }

    private void UpdateNotePreview()
    {
        var note = _part.PoznamkaPreExport;
        NotePreviewLabel.Text = string.IsNullOrWhiteSpace(note)
            ? $"(predvolené: {_part.DefaultPoznamkaPreExport})"
            : note;
    }

    private void OnViewTop_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        SetTopView();
        Viewport.ZoomExtents(0);
    }

    private void OnViewIso_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        SetIsoView();
        Viewport.ZoomExtents(0);
    }

    private void OnViewLeft_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        SetLeftView();
        Viewport.ZoomExtents(0);
    }

    private void OnViewRight_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        SetRightView();
        Viewport.ZoomExtents(0);
    }

    private void OnViewFront_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        SetFrontView();
        Viewport.ZoomExtents(0);
    }

    private void OnViewBack_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        SetBackView();
        Viewport.ZoomExtents(0);
    }

    private void OnViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _ = sender;
        var pos = e.GetPosition(Viewport);
        if (!TryGetPickRay(pos, out var origin, out var direction))
            return;

        var face = PartFacePicker.PickFromRay(origin, direction, _part.Dx, _part.Dy, _part.Dz);
        if (face is null or PartFace.Bottom)
        {
            MessageBox.Show(this, "Kolíky sa nastavujú na ploche Top alebo na bočných hranách.", "Výber plochy",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _selectedFace = face;
        RebuildScene(resetCamera: false);
        OpenKolikyForFace(face.Value);
        e.Handled = true;
    }

    private bool TryGetPickRay(Point pos, out Point3D origin, out Vector3D direction)
    {
        origin = default;
        direction = default;

        var ray = Viewport3DHelper.GetRay(Viewport.Viewport, pos);
        if (ray == null)
            return false;

        origin = ray.Origin;
        direction = ray.Direction;
        return direction.LengthSquared > 1e-12;
    }

    private void OnSpecialKoliky_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        if (PolicaKolikyApplier.IsPolicaPart(_part))
            OpenPolicaKoliky();
        else if (TraverzaKolikyApplier.IsTraverzaPart(_part))
            OpenTraverzaKoliky();
    }

    private void OpenKolikyForFace(PartFace face)
    {
        if (TraverzaKolikyApplier.IsTraverzaPart(_part))
        {
            OpenTraverzaKoliky();
            return;
        }

        if (PolicaKolikyApplier.IsPolicaPart(_part))
        {
            OpenPolicaKoliky();
            return;
        }

        if (face == PartFace.Top)
            OpenTopKoliky(face);
        else
            OpenEdgeKoliky(face);
    }

    private void OpenEdgeKoliky(PartFace face)
    {
        var existing = _vm.FindEditableDrill(_part, face);
        var dlg = new AssemblyEdgeKolikyDialog(_part, face, existing) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result == null)
            return;

        _vm.SaveDrillOperation(dlg.Result, isNew: existing == null, targetPart: _part);
        AfterKolikySaved(face);
    }

    private void OpenTopKoliky(PartFace face)
    {
        if (_vm.UsesDnoVrchDualTopKoliky(_part, face))
        {
            OpenDnoVrchDualTopKoliky();
            return;
        }

        var existing = _vm.FindEditableDrill(_part, face);
        var dlg = new AssemblyKolikyDialog(face, _part, existing) { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        if (dlg.TraverzaRequest != null)
        {
            if (!_vm.AddTraverzaKoliky(dlg.TraverzaRequest))
                MessageBox.Show(this, _vm.StatusText, "Traverza", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (dlg.Result != null)
            _vm.SaveDrillOperation(dlg.Result, isNew: existing == null, targetPart: _part);

        AfterKolikySaved(face);
    }

    private void OpenDnoVrchDualTopKoliky()
    {
        foreach (var partner in new[] { PartKind.BokL, PartKind.BokP })
        {
            var existing = _vm.FindEditableDrill(_part, PartFace.Top, partner);
            var dlg = new AssemblyKolikyDialog(PartFace.Top, _part, existing, partner) { Owner = this };
            if (dlg.ShowDialog() != true)
                return;

            if (dlg.Result == null)
                continue;

            _vm.SaveDrillOperation(dlg.Result, isNew: existing == null, targetPart: _part);
        }

        AfterKolikySaved(PartFace.Top);
    }

    private void OpenTraverzaKoliky()
    {
        var travMaster = _vm.FindEditableDrill(_part);
        var dlg = new AssemblyKolikyDialog(PartFace.Left, _part, travMaster) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.TraverzaRequest != null
            && !_vm.AddTraverzaKoliky(dlg.TraverzaRequest))
            MessageBox.Show(this, _vm.StatusText, "Traverza", MessageBoxButton.OK, MessageBoxImage.Warning);

        RefreshOperationsList();
        RebuildScene(resetCamera: false);
    }

    private void OpenPolicaKoliky()
    {
        var policaWin = new Window
        {
            Owner = this,
            Title = $"Polica – kolíky · {_part.Name}",
            Width = 420,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var dock = new System.Windows.Controls.DockPanel { Margin = new Thickness(12) };
        var footer = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var btnCancel = new System.Windows.Controls.Button
        {
            Content = "Zrušiť",
            IsCancel = true,
            Padding = new Thickness(14, 6, 14, 6),
            MinWidth = 80,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var btnOk = new System.Windows.Controls.Button
        {
            Content = "Uložiť",
            IsDefault = true,
            Padding = new Thickness(14, 6, 14, 6),
            MinWidth = 90,
        };
        btnOk.Click += (_, _) => policaWin.DialogResult = true;
        footer.Children.Add(btnCancel);
        footer.Children.Add(btnOk);
        System.Windows.Controls.DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom);
        dock.Children.Add(footer);
        dock.Children.Add(new PolicaSettingsPanel { DataContext = _vm });
        policaWin.Content = dock;
        if (policaWin.ShowDialog() == true)
            _vm.ApplyPolicaKoliky();

        RefreshOperationsList();
        RebuildScene(resetCamera: false);
    }

    private void OnDone_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        DialogResult = true;
        Close();
    }

    private void OnNoteRow_Click(object sender, MouseButtonEventArgs e)
    {
        _ = sender; _ = e;

        if (NotePopup.IsOpen)
        {
            NotePopup.IsOpen = false;
            return;
        }

        NotePopupTextBox.Text = string.IsNullOrWhiteSpace(_part.PoznamkaPreExport)
            ? _part.DefaultPoznamkaPreExport
            : _part.PoznamkaPreExport;

        NotePopup.IsOpen = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            NotePopupTextBox.Focus();
            NotePopupTextBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnNotePopupSave_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        _part.PoznamkaPreExport = NotePopupTextBox.Text ?? string.Empty;
        UpdateNotePreview();
        _vm.NotifySceneRefresh();
        NotePopup.IsOpen = false;
    }

    private void OnNotePopupCancel_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        NotePopup.IsOpen = false;
    }

    private void OnNotePopupReset_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        NotePopupTextBox.Text = _part.DefaultPoznamkaPreExport;
        NotePopupTextBox.Focus();
        NotePopupTextBox.SelectAll();
    }

    private void OnNotePopupTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        _ = sender;
        if (e.Key == Key.Escape)
        {
            NotePopup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            OnNotePopupSave_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void OnOperationEnabledChanged(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        UpdateHeader();
        RebuildScene(resetCamera: false);
        _vm.NotifySceneRefresh();
    }

    private void OnEnableAll_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        foreach (var op in _part.Operations)
            op.IsEnabled = true;
        RefreshOperationsList();
        RebuildScene(resetCamera: false);
        _vm.NotifySceneRefresh();
    }

    private void OnDisableAll_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        foreach (var op in _part.Operations)
            op.IsEnabled = false;
        RefreshOperationsList();
        RebuildScene(resetCamera: false);
        _vm.NotifySceneRefresh();
    }

    private void OnRemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        if (OperationsList.SelectedItem is not CncOperation op)
            return;
        _part.Operations.Remove(op);
        _selectedDrill = null;
        DrillEditorRoot.Visibility = Visibility.Collapsed;
        DrillEditorBorder.Visibility = Visibility.Collapsed;
        RefreshOperationsList();
        RebuildScene(resetCamera: false);
        _vm.NotifySceneRefresh();
    }

    private void OnOperationsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _ = sender; _ = e;
        if (OperationsList.SelectedItem is not CncOperation op)
        {
            _selectedDrill = null;
            DrillEditorRoot.Visibility = Visibility.Collapsed;
            DrillEditorBorder.Visibility = Visibility.Collapsed;
            return;
        }

        if (op is not DrillOperation drill)
        {
            _selectedDrill = null;
            DrillEditorRoot.Visibility = Visibility.Collapsed;
            DrillEditorBorder.Visibility = Visibility.Collapsed;
            return;
        }

        _selectedDrill = drill;
        _suppressEditEvents = true;
        EditNameBox.Text = drill.Name;
        EditRefPosBox.Text = drill.RefPos.ToString(CultureInfo.InvariantCulture);
        EditCountXBox.Text = drill.CountX.ToString(CultureInfo.InvariantCulture);
        EditCountYBox.Text = drill.CountY.ToString(CultureInfo.InvariantCulture);
        EditPitchXBox.Text = drill.PitchX.ToString(CultureInfo.InvariantCulture);
        EditPitchYBox.Text = drill.PitchY.ToString(CultureInfo.InvariantCulture);
        EditStartXBox.Text = drill.XStart.ToString(CultureInfo.InvariantCulture);
        EditStartYBox.Text = drill.YStart.ToString(CultureInfo.InvariantCulture);
        EditDepthBox.Text = drill.Depth.ToString(CultureInfo.InvariantCulture);
        EditDiameterBox.Text = drill.Diameter.ToString(CultureInfo.InvariantCulture);
        EditToolBox.Text = drill.Tool ?? string.Empty;
        _suppressEditEvents = false;
        DrillEditorRoot.Visibility = Visibility.Visible;
        DrillEditorBorder.Visibility = Visibility.Visible;
    }

    private void OnDrillEditChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _ = sender; _ = e;
        if (_suppressEditEvents || _selectedDrill == null)
            return;

        var drill = _selectedDrill;
        if (!string.IsNullOrWhiteSpace(EditNameBox.Text))
            drill.Name = EditNameBox.Text.Trim();
        if (TryParseInt(EditRefPosBox.Text, out var refPos))
            drill.RefPos = Math.Clamp(refPos, 0, 3);
        if (TryParseInt(EditCountXBox.Text, out var countX) && countX > 0)
            drill.CountX = countX;
        if (TryParseInt(EditCountYBox.Text, out var countY) && countY > 0)
            drill.CountY = countY;
        if (TryParseDouble(EditPitchXBox.Text, out var pitchX) && pitchX > 0)
            drill.PitchX = pitchX;
        if (TryParseDouble(EditPitchYBox.Text, out var pitchY) && pitchY > 0)
            drill.PitchY = pitchY;
        if (TryParseDouble(EditStartXBox.Text, out var startX))
            drill.XStart = startX;
        if (TryParseDouble(EditStartYBox.Text, out var startY))
            drill.YStart = startY;
        if (TryParseDouble(EditDepthBox.Text, out var depth) && depth > 0)
            drill.Depth = depth;
        if (TryParseDouble(EditDiameterBox.Text, out var diameter) && diameter > 0)
            drill.Diameter = diameter;
        if (!string.IsNullOrWhiteSpace(EditToolBox.Text))
            drill.Tool = EditToolBox.Text.Trim();

        RebuildScene(resetCamera: false);
        _vm.NotifySceneRefresh();
    }

    private static bool TryParseInt(string? value, out int parsed) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);

    private static bool TryParseDouble(string? value, out double parsed) =>
        double.TryParse((value ?? string.Empty).Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);

    private void UpdateSpecialKolikyButtonVisibility()
    {
        if (PolicaKolikyApplier.IsPolicaPart(_part) || TraverzaKolikyApplier.IsTraverzaPart(_part))
        {
            SpecialKolikyButton.Visibility = Visibility.Visible;
            HintLabel.Text = PolicaKolikyApplier.IsPolicaPart(_part)
                ? "Použi tlačidlo Nastaviť kolíky pre policu, alebo klikni na plochu."
                : "Použi tlačidlo Nastaviť kolíky pre traverzu, alebo klikni na plochu.";
            return;
        }

        SpecialKolikyButton.Visibility = Visibility.Collapsed;
        HintLabel.Text = "Klikni na plochu (Top alebo hrany) – otvorí sa dialóg kolíkov. Po dokončení zavri editor a v zostave stlač Vložiť.";
    }
}
