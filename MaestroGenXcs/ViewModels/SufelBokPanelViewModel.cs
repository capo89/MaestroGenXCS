using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.ViewModels;

/// <summary>Panel šufľového boku – 3D dielca a parametre makra SufelBok2_novy.</summary>
public sealed partial class SufelBokPanelViewModel : ObservableObject
{
    private readonly AssemblyViewModel _parent;

    [ObservableProperty]
    private Part? _part;

    [ObservableProperty]
    private SufelSkupina? _skupina;

    public SufelBokPanelViewModel(AssemblyViewModel parent) => _parent = parent;

    public SufelBokMacroParams? Macro => Skupina?.BokMacro;

    public SufelCeloZadMacroParams? CeloZadMacro => Skupina?.CeloZadMacro;

    public string CeloZadHrubkaText
    {
        get
        {
            if (Skupina == null)
                return "";
            var dz = SufelMacroSynchronizer.ResolveCeloZadThickness(Skupina);
            return dz > 0
                ? FormattableString.Invariant($"Hrúbka čelo/zad: {dz:0.##} mm → PolohaDieryX = {dz / 2:0.##}")
                : "Hrúbka čelo/zad: —";
        }
    }

    public string Title => Part?.Name ?? "Šufľový bok";

    public string SkupinaNazov => Skupina?.Nazov ?? "";

    public string RozmeryText
    {
        get
        {
            if (Part == null)
                return "";
            return FormattableString.Invariant(
                $"{Part.Dx:0.##} × {Part.Dy:0.##} × {Part.Dz:0.##} mm (dx1 × dy1 × dz1)");
        }
    }

    public string PocetKsText =>
        Part?.PocetKs is int ks && ks > 0
            ? $"{ks} ks"
            : "1 ks";

    public void RefreshFromSelection()
    {
        var part = _parent.SelectedPart?.Kind == PartKind.SufelBok
            ? _parent.SelectedPart
            : null;

        Part = part;
        Skupina = _parent.FindSufelSkupina(part);
        if (Skupina != null)
        {
            Skupina.BokMacro.SyncDno18FromDno(Skupina.DnoPart);
            SufelMacroSynchronizer.SyncAll(Skupina);
            SufelMacroApplier.Apply(Skupina);
        }

        OnPropertyChanged(nameof(Macro));
        OnPropertyChanged(nameof(CeloZadMacro));
        OnPropertyChanged(nameof(CeloZadHrubkaText));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SkupinaNazov));
        OnPropertyChanged(nameof(RozmeryText));
        OnPropertyChanged(nameof(PocetKsText));
    }
}
