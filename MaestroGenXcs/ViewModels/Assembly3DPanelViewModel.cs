using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Rendering;
using MaestroGenXcs.Services;

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

    public ObservableCollection<SufelMoventoRowViewModel> MoventoRows { get; } = new();

    public bool HasMoventoRows => MoventoRows.Count > 0;

    public Assembly3DPanelViewModel(
        AssemblyViewModel parent,
        Action<AssemblyPlacement>? openPinSettings = null)
    {
        _parent = parent;
        _openPinSettings = openPinSettings;
        RefreshInsertList();
        RefreshMoventoRows();
    }

    public void RefreshMoventoRows()
    {
        MoventoRows.Clear();
        var ctx = _parent.GetAssemblyContext(_parent.ActiveZostavaForBinding);
        if (ctx == null)
        {
            OnPropertyChanged(nameof(HasMoventoRows));
            return;
        }

        foreach (var sekcia in ctx.MoventoSekcie)
            MoventoRows.Add(new SufelMoventoRowViewModel(sekcia, _parent));

        OnPropertyChanged(nameof(HasMoventoRows));
    }

    public void RefreshInsertList()
    {
        InsertItems.Clear();
        var ctx = _parent.GetAssemblyContext(_parent.ActiveZostavaForBinding);
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
        if (refBok == null)
            return;

        var refDx = refBok.Dx;
        var refKind = refBok.Kind;
        var mode = ctx?.CorpusMode ?? AssemblyCorpusMode.BokVlozeny;
        var (minX, maxX, maxY) = AssemblyPartLayout.GetPlacementOffsetLimits(
            placement.Part, refBok, refKind, mode);

        if (ConnectionMap.UsesVlozenyDnoVrchPlacement(placement.Part.Kind, mode))
        {
            var span = maxX - minX;
            placement.OffsetY = Math.Clamp(minX + Math.Min(GuideX, span), minX, maxX);
        }
        else
            placement.OffsetY = Math.Clamp(refDx - GuideX - placement.Part.Dz, 0, maxX);
        placement.OffsetDepthMm = Math.Clamp(GuideY, 0, maxY);
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
