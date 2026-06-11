using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;

namespace MaestroGenXcs.Sufle;

/// <summary>
/// Jedna šufľa v zostave – 2× bok, 1× čelo, 1× zad, 1× dno.
/// </summary>
public sealed partial class SufelSkupina : ObservableObject
{
    private Part? _wiredDnoPart;
    private Part? _wiredCeloPart;
    private Part? _wiredZadPart;

    public SufelSkupina()
    {
        BokMacro.PropertyChanged += OnBokMacroPropertyChanged;
        CeloZadMacro.PropertyChanged += OnCeloZadMacroPropertyChanged;
    }

    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private SufelPozicia _pozicia = SufelPozicia.Nezadana;

    [ObservableProperty]
    private int _poradieOdSpodu = 1;

    [ObservableProperty]
    private Part? _bokPart;

    [ObservableProperty]
    private Part? _celoPart;

    [ObservableProperty]
    private Part? _zadPart;

    [ObservableProperty]
    private Part? _dnoPart;

    /// <summary>Parametre makra <c>SufelBok2_novy</c> pre bok tejto šufle.</summary>
    public SufelBokMacroParams BokMacro { get; } = new();

    /// <summary>Parametre makra <c>SufelCeloZad2</c> – odvodené z <see cref="BokMacro"/>.</summary>
    public SufelCeloZadMacroParams CeloZadMacro { get; } = new();

    public string Nazov => $"Šufel {PoradieOdSpodu}";

    public int BokKsOcekavane => 2;

    public int BokKsSkutocne => BokPart?.PocetKs ?? 0;

    public bool MaBok => BokPart != null && BokKsSkutocne > 0;

    public bool MaCelo => CeloPart != null && (CeloPart.PocetKs ?? 1) > 0;

    public bool MaZad => ZadPart != null && (ZadPart.PocetKs ?? 1) > 0;

    public bool MaDno => DnoPart != null && (DnoPart.PocetKs ?? 1) > 0;

    public bool JeKompletna =>
        MaBok && BokKsSkutocne == BokKsOcekavane
        && MaCelo && (CeloPart!.PocetKs ?? 1) == 1
        && MaZad && (ZadPart!.PocetKs ?? 1) == 1
        && MaDno && (DnoPart!.PocetKs ?? 1) == 1;

    /// <summary>Dielce v poradí pre strom: bok (2 ks), čelo, zad, dno – bez delenia na L/P.</summary>
    public IEnumerable<Part> EnumeratePartsInDisplayOrder()
    {
        if (BokPart != null)
            yield return BokPart;
        if (CeloPart != null)
            yield return CeloPart;
        if (ZadPart != null)
            yield return ZadPart;
        if (DnoPart != null)
            yield return DnoPart;
    }

    public HashSet<Guid> EnumeratePartIds() =>
        EnumeratePartsInDisplayOrder().Select(p => p.Id).ToHashSet();

    private void OnBokMacroPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.PropertyName is nameof(SufelBokMacroParams.PocetDier)
            or nameof(SufelBokMacroParams.RoztecDierMm)
            or nameof(SufelBokMacroParams.PolohaDieryYmm))
        {
            SufelMacroSynchronizer.SyncCeloZadFromBok(this);
        }

        if (e.PropertyName is not null)
            SufelMacroApplier.Apply(this);
    }

    private void OnCeloZadMacroPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.PropertyName is not null)
            SufelMacroApplier.Apply(this);
    }

    partial void OnDnoPartChanged(Part? value)
    {
        if (_wiredDnoPart != null)
            _wiredDnoPart.PropertyChanged -= OnDnoPartGeometryChanged;

        _wiredDnoPart = value;
        if (value != null)
            value.PropertyChanged += OnDnoPartGeometryChanged;

        BokMacro.SyncDno18FromDno(value);
        SufelMacroApplier.Apply(this);
    }

    partial void OnBokPartChanged(Part? value) => SufelMacroApplier.Apply(this);

    partial void OnCeloPartChanged(Part? value)
    {
        WireCeloZadThickness(ref _wiredCeloPart, value);
        SufelMacroSynchronizer.SyncAll(this);
        SufelMacroApplier.Apply(this);
    }

    partial void OnZadPartChanged(Part? value)
    {
        WireCeloZadThickness(ref _wiredZadPart, value);
        SufelMacroSynchronizer.SyncAll(this);
        SufelMacroApplier.Apply(this);
    }

    private void WireCeloZadThickness(ref Part? wired, Part? value)
    {
        if (wired != null)
            wired.PropertyChanged -= OnCeloZadGeometryChanged;

        wired = value;
        if (value != null)
            value.PropertyChanged += OnCeloZadGeometryChanged;
    }

    private void OnDnoPartGeometryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Part.Dx) or nameof(Part.Dy) or nameof(Part.Dz))
        {
            BokMacro.SyncDno18FromDno(DnoPart);
            SufelMacroApplier.Apply(this);
        }
    }

    private void OnCeloZadGeometryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Part.Dz))
        {
            SufelMacroSynchronizer.SyncPolohaDieryXFromCeloZad(this);
            SufelMacroApplier.Apply(this);
        }
    }
}
