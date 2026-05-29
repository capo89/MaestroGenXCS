using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Rendering;

namespace MaestroGenXcs.ViewModels;

public sealed partial class Assembly3DPanelViewModel : ObservableObject
{
    private readonly AssemblyViewModel _parent;
    private readonly Action<AssemblyPlacement>? _openPinSettings;

    [ObservableProperty]
    private double _guideX = 200;

    [ObservableProperty]
    private double _guideY = 30;

    public ObservableCollection<AssemblyInsertItemViewModel> InsertItems { get; } = new();

    public Assembly3DPanelViewModel(
        AssemblyViewModel parent,
        Action<AssemblyPlacement>? openPinSettings = null)
    {
        _parent = parent;
        _openPinSettings = openPinSettings;
        RefreshInsertList();
    }

    public void RefreshInsertList()
    {
        InsertItems.Clear();
        var ctx = _parent.GetAssemblyContext(_parent.SelectedPart?.Zostava);
        if (ctx?.ReferenceBok == null)
            return;

        foreach (var placement in ctx.Placements.OrderBy(p => p.Part.Poradie).ThenBy(p => p.Part.Name))
        {
            InsertItems.Add(new AssemblyInsertItemViewModel(placement, this));
        }
    }

    partial void OnGuideXChanged(double value) => _parent.NotifySceneRefresh();

    partial void OnGuideYChanged(double value) => _parent.NotifySceneRefresh();

    [RelayCommand]
    private void ActivatePart(AssemblyInsertItemViewModel? item)
    {
        if (item == null)
            return;

        var placement = item.Placement;

        if (placement.IsPlacedInScene)
        {
            _parent.SelectedPart = placement.Part;
            _parent.ActivePlacementForDrag = placement;
            _parent.NotifySceneRefresh();
            return;
        }

        var ctx = _parent.GetAssemblyContext(placement.Part.Zostava);
        var refBok = ctx?.ReferenceBok;
        var refDx = refBok?.Dx ?? placement.Part.Dx;
        var refKind = refBok?.Kind ?? PartKind.BokL;
        var contact = AssemblyPartLayout.ResolveContactFace(refKind, placement.Part.Kind, placement.AnchorFace);
        var (footX, _) = AssemblyPartLayout.GetFootprintOnBokTop(placement.Part, contact);
        // X pravítka od pravej hrany = vertikálna čiara; dielec má byť vľavo od nej (pravý okraj dielca na čiare).
        var maxX = Math.Max(0, refDx - footX);
        placement.OffsetY = Math.Clamp(refDx - GuideX - footX, 0, maxX);
        placement.OffsetDepthMm = GuideY;
        placement.IsPlacedInScene = true;
        _parent.ActivePlacementForDrag = placement;
        _parent.SelectedPart = placement.Part;
        item.NotifyPlacedChanged();
        _parent.NotifySceneRefresh();
    }

    [RelayCommand]
    private void OpenPinSettings(AssemblyInsertItemViewModel? item)
    {
        if (item == null)
            return;

        var placement = item.Placement;
        _parent.SelectedPart = placement.Part;
        _openPinSettings?.Invoke(placement);
        _parent.NotifySceneRefresh();
    }
}

public sealed partial class AssemblyInsertItemViewModel : ObservableObject
{
    private readonly Assembly3DPanelViewModel _panel;

    public AssemblyPlacement Placement { get; }

    public string Name => Placement.Part.Name;

    public string Dimensions => FormattableString.Invariant(
        $"{Placement.Part.Dx:0} × {Placement.Part.Dy:0} × {Placement.Part.Dz:0} mm");

    public bool IsPlaced => Placement.IsPlacedInScene;

    public string InsertButtonText => IsPlaced ? "Upraviť" : "Vložiť";

    public AssemblyInsertItemViewModel(AssemblyPlacement placement, Assembly3DPanelViewModel panel)
    {
        Placement = placement;
        _panel = panel;
        placement.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AssemblyPlacement.IsPlacedInScene))
            {
                OnPropertyChanged(nameof(IsPlaced));
                OnPropertyChanged(nameof(InsertButtonText));
            }
        };
    }

    public void NotifyPlacedChanged()
    {
        OnPropertyChanged(nameof(IsPlaced));
        OnPropertyChanged(nameof(InsertButtonText));
    }

    [RelayCommand]
    private void Insert() => _panel.ActivatePartCommand.Execute(this);

    [RelayCommand]
    private void OpenPinSettings() => _panel.OpenPinSettingsCommand.Execute(this);
}
