using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Sufle;

/// <summary>
/// Jedna šufľa v zostave – 2× bok, 1× čelo, 1× zad, 1× dno.
/// </summary>
public sealed partial class SufelSkupina : ObservableObject
{
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
}
