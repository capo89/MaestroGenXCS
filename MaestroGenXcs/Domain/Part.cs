using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Domain;

/// <summary>
/// Jednotný dielec – nahrádza pôvodné špecifické triedy
/// (BokL/BokP/Dno/Vrch/...). Typ je len hint cez <see cref="Kind"/>,
/// logika je riadená operáciami a spojmi.
/// </summary>
public sealed partial class Part : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZobrazenieVZozname))]
    private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZobrazenieVZozname))]
    private int? _poradie;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZobrazenieVZozname))]
    private int? _pocetKs;

    [ObservableProperty]
    private string _zostava = "Bez zostavy";

    [ObservableProperty]
    private PartKind _kind = PartKind.Generic;

    /// <summary>Pozícia šufle v skrinke (z názvu: spodný / stredný / vrchný).</summary>
    [ObservableProperty]
    private SufelPozicia? _sufelPozicia;

    /// <summary>Väzba na <see cref="Sufle.SufelSkupina"/> v kontexte zostavy.</summary>
    [ObservableProperty]
    private Guid? _sufelSkupinaId;

    [ObservableProperty]
    private double _dx;

    [ObservableProperty]
    private double _dy;

    [ObservableProperty]
    private double _dz;

    [ObservableProperty]
    private int? _absPredna;

    [ObservableProperty]
    private int? _absZadna;

    [ObservableProperty]
    private int? _absLava;

    [ObservableProperty]
    private int? _absPrava;

    [ObservableProperty]
    private string _poznamkaPreExport = "";

    /// <summary>
    /// Ak je ABS hodnota presne 1, pri exporte sa zapíše ako 0.8.
    /// V starom projekte bolo "predprípravené" pre obeh tenkej ABS pásky.
    /// </summary>
    [ObservableProperty]
    private bool _prepisatAbsJednaNaNulaOsemPriExporte = true;

    public ObservableCollection<CncOperation> Operations { get; } = new();

    /// <summary>Pevná polica – série kolíkov (počet = <see cref="PocetKs"/>).</summary>
    public ObservableCollection<PolicaSeria> PolicaSerie { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PolicaJePolohovatelna))]
    private bool _policaJePevna = true;

    /// <summary>Protipol <see cref="PolicaJePevna"/> – podpery v bokoch (raster 32 mm).</summary>
    public bool PolicaJePolohovatelna
    {
        get => !PolicaJePevna;
        set
        {
            if (value == !PolicaJePevna)
                return;
            PolicaJePevna = !value;
        }
    }

    partial void OnPolicaJePevnaChanged(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(PolicaJePolohovatelna));
    }

    [ObservableProperty]
    private bool _policaIbaJedenRad;

    /// <summary>Fallback počet kolíkov v rade, ak séria má PocetKolikov = 0.</summary>
    [ObservableProperty]
    private int _policaPocetKolikovVRade = PolicaDrillingDefaults.DefaultPocetKolikov;

    [ObservableProperty]
    private bool _policaSkrutkyZapnute;

    [ObservableProperty]
    private int _policaPocetSkrutiek = PolicaDrillingDefaults.ScrewDefaultCount;

    [ObservableProperty]
    private double _policaRozostupSkrutiekMm = PolicaDrillingDefaults.ScrewDefaultPitchMm;

    /// <summary>Utopenie police od prednej hrany (mm) – odpočíta sa pri vŕtaní do bokov.</summary>
    [ObservableProperty]
    private double _policaFrontOffsetMm;

    /// <summary>Posun prednej hrany boku (mm) – pre kolíky police / dna / vrchu.</summary>
    [ObservableProperty]
    private double _frontOffsetMm;

    [ObservableProperty]
    private double _policaDefaultVyskaOdSpoduMm = 900;

    public bool IsPolica => Kind == PartKind.Polica;

    public void EnsurePolicaSerie()
    {
        if (!IsPolica) return;

        var count = Math.Max(1, PocetKs ?? 1);
        while (PolicaSerie.Count < count)
        {
            PolicaSerie.Add(CreateDefaultPolicaSeria(PolicaSerie.Count + 1));
        }

        while (PolicaSerie.Count > count)
            PolicaSerie.RemoveAt(PolicaSerie.Count - 1);

        for (var i = 0; i < PolicaSerie.Count; i++)
            PolicaSerie[i].Nazov = $"Séria {i + 1}";
    }

    private PolicaSeria CreateDefaultPolicaSeria(int index) => new()
    {
        Nazov = $"Séria {index}",
        OdSpoduMm = PolicaDefaultVyskaOdSpoduMm,
        OdPrednejHranyMm = PolicaDrillingDefaults.DefaultOdPrednejHranyMm,
        RoztecMm = PolicaDrillingDefaults.DefaultPevnaRoztecMm,
        PocetKolikov = PolicaDrillingDefaults.DefaultPocetKolikov,
        PocetPoloh = PolicaDrillingDefaults.PodperaHoleCount,
    };

    public void ClearPolicaSeria(PolicaSeria? seria)
    {
        if (seria == null) return;
        seria.OdSpoduMm = 0;
        seria.OdPrednejHranyMm = PolicaDrillingDefaults.DefaultOdPrednejHranyMm;
        seria.RoztecMm = PolicaDrillingDefaults.DefaultPevnaRoztecMm;
        seria.PocetKolikov = PolicaDrillingDefaults.DefaultPocetKolikov;
        seria.PocetPoloh = PolicaDrillingDefaults.PodperaHoleCount;
    }

    public void CopyPolicaSeriaFromPrevious(PolicaSeria? seria)
    {
        if (seria == null) return;
        var idx = PolicaSerie.IndexOf(seria);
        if (idx <= 0) return;
        var prev = PolicaSerie[idx - 1];
        seria.OdPrednejHranyMm = prev.OdPrednejHranyMm;
        seria.RoztecMm = prev.RoztecMm;
        seria.PocetKolikov = prev.PocetKolikov;
        seria.PocetPoloh = prev.PocetPoloh;
    }

    public string ZobrazenieVZozname
    {
        get
        {
            var core = Poradie.HasValue ? $"{Poradie}. {Name}" : Name;
            if (PocetKs.HasValue && PocetKs.Value > 0)
                return $"{core} ({PocetKs} ks)";
            return core;
        }
    }

    /// <summary>
    /// Automaticky zložená poznámka pre export v tvare
    /// <c>"1ks - ABS vpredu vlavo"</c> (alebo <c>"2ks - ABS 4x"</c>).
    /// Ak dielec nemá žiadnu ABS, vráti iba <c>"1ks"</c>.
    /// </summary>
    public string DefaultPoznamkaPreExport
    {
        get
        {
            var ks = Math.Max(1, PocetKs ?? 1);
            var sides = new List<string>();
            if (HasAbs(AbsPredna)) sides.Add("vpredu");
            if (HasAbs(AbsLava))   sides.Add("vlavo");
            if (HasAbs(AbsPrava))  sides.Add("vpravo");
            if (HasAbs(AbsZadna))  sides.Add("vzadu");

            if (sides.Count == 4)
                return $"{ks}ks - ABS 4x";
            return sides.Count > 0
                ? $"{ks}ks - ABS {string.Join(" ", sides)}"
                : $"{ks}ks";
        }
    }

    public Part()
    {
    }

    public Part(string name, double dx, double dy, double dz)
    {
        Name = name;
        Dx = dx;
        Dy = dy;
        Dz = dz;
    }

    private static bool HasAbs(int? value) => value.HasValue && value.Value > 0;

    /// <summary>
    /// Naposledy automaticky vyplnená hodnota – ak sa <see cref="PoznamkaPreExport"/>
    /// zhoduje s ňou, znamená to, že používateľ ju manuálne nemenil a smieme
    /// ju aktualizovať na nový default. Akonáhle ju užívateľ prepíše ručne,
    /// auto-fill ju už nikdy neprepíše (až kým ju nevyprázdni).
    /// </summary>
    private string? _lastAutoPoznamkaPreExport;

    private void EnsurePoznamkaPreExportDefault()
    {
        var nextDefault = DefaultPoznamkaPreExport;
        if (string.IsNullOrWhiteSpace(PoznamkaPreExport)
            || string.Equals(PoznamkaPreExport, _lastAutoPoznamkaPreExport, StringComparison.Ordinal))
        {
            _lastAutoPoznamkaPreExport = nextDefault;
            PoznamkaPreExport = nextDefault;
        }
    }

    partial void OnPocetKsChanged(int? value)
    {
        _ = value;
        EnsurePoznamkaPreExportDefault();
        EnsurePolicaSerie();
    }

    partial void OnKindChanged(PartKind value)
    {
        _ = value;
        EnsurePolicaSerie();
    }
    partial void OnAbsPrednaChanged(int? value) { _ = value; EnsurePoznamkaPreExportDefault(); }
    partial void OnAbsZadnaChanged(int? value)  { _ = value; EnsurePoznamkaPreExportDefault(); }
    partial void OnAbsLavaChanged(int? value)   { _ = value; EnsurePoznamkaPreExportDefault(); }
    partial void OnAbsPravaChanged(int? value)  { _ = value; EnsurePoznamkaPreExportDefault(); }

    partial void OnPoznamkaPreExportChanged(string value)
    {
        // Ak ju užívateľ vyprázdni, znova povolíme auto-fill pri ďalšej zmene
        // PocetKs/Abs*.
        if (string.IsNullOrWhiteSpace(value))
            _lastAutoPoznamkaPreExport = null;
    }
}
