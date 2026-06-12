using System.Collections.ObjectModel;
using System.IO;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaestroGenXcs.Chrbaty;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Import;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Rendering;
using MaestroGenXcs.Services;
using MaestroGenXcs.Sufle;
using MaestroGenXcs.Xcs;
using WpfOpen = Microsoft.Win32.OpenFileDialog;
using WpfSave = Microsoft.Win32.SaveFileDialog;
using WpfMsg = System.Windows.MessageBox;

namespace MaestroGenXcs.ViewModels;

public sealed partial class AssemblyViewModel : ObservableObject
{
    private readonly ExcelImporter _importer;
    private readonly PartsStore _store;
    private readonly AssemblyStore _assemblyStore = new();
    private readonly OperationPropagator _propagator;
    private readonly DispatcherTimer _policaRebuildTimer;
    private readonly DispatcherTimer _moventoApplyTimer;
    private Part? _policaRebuildTarget;
    private AssemblyPlacement? _wiredPlacement;
    private readonly HashSet<Part> _setupRefreshWiredParts = new();
    private readonly HashSet<AssemblyPlacement> _setupRefreshWiredPlacements = new();
    private SufelAssemblyResolver.Result? _lastSufelResolveResult;
    private ChrbatAssemblyValidator.Result? _lastChrbatValidateResult;

    private static readonly HashSet<string> PolicaRebuildProperties = new(StringComparer.Ordinal)
    {
        nameof(Part.PolicaJePevna),
        nameof(Part.PolicaIbaJedenRad),
        nameof(Part.PolicaFrontOffsetMm),
        nameof(Part.PolicaPocetKolikovVRade),
        nameof(Part.PolicaDefaultVyskaOdSpoduMm),
        nameof(Part.PocetKs),
        nameof(Part.PolicaSerie),
    };

    private static readonly HashSet<string> PolicaSeriaRebuildProperties = new(StringComparer.Ordinal)
    {
        nameof(PolicaSeria.OdSpoduMm),
        nameof(PolicaSeria.OdPrednejHranyMm),
        nameof(PolicaSeria.RoztecMm),
        nameof(PolicaSeria.PocetKolikov),
        nameof(PolicaSeria.PocetPoloh),
    };

    public ObservableCollection<Part> Parts => _store.Parts;

    /// <summary>Strom: zostava → dielce (pre hlavné okno).</summary>
    public ObservableCollection<ZostavaTreeNode> PartsTree { get; } = new();

    private bool _syncingZostavaFromPart;
    private bool _syncingPartFromZostava;

    public IReadOnlyList<PartFace> DrillFaceChoices { get; } =
    [
        PartFace.Top,
        PartFace.Left,
        PartFace.Right,
        PartFace.Front,
        PartFace.Back
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedDx))]
    [NotifyPropertyChangedFor(nameof(SelectedDy))]
    [NotifyPropertyChangedFor(nameof(SelectedDz))]
    [NotifyPropertyChangedFor(nameof(SelectedKindText))]
    [NotifyPropertyChangedFor(nameof(SelectedZostavaText))]
    [NotifyPropertyChangedFor(nameof(IsPolicaSelected))]
    [NotifyPropertyChangedFor(nameof(SelectedPlacement))]
    [NotifyPropertyChangedFor(nameof(HasSelectedPlacement))]
    [NotifyPropertyChangedFor(nameof(ReferenceBokText))]
    [NotifyPropertyChangedFor(nameof(SelectedCorpusMode))]
    [NotifyPropertyChangedFor(nameof(HasSelectedZostava))]
    [NotifyPropertyChangedFor(nameof(ActiveZostavaForBinding))]
    [NotifyPropertyChangedFor(nameof(IsSufelBokView))]
    [NotifyPropertyChangedFor(nameof(IsCabinetAssemblyView))]
    [NotifyPropertyChangedFor(nameof(CanAddObeh))]
    private Part? _selectedPart;

    public bool IsSufelBokView => SelectedPart?.Kind == PartKind.SufelBok;

    public bool IsCabinetAssemblyView => !IsSufelBokView;

    /// <summary>Šufľové dielce nemajú obeh – ABS rieši makro, nie Obeh_novy_DTD.</summary>
    public bool CanAddObeh =>
        SelectedPart != null && !SufelNameParser.IsSufelKind(SelectedPart.Kind);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedZostavaText))]
    [NotifyPropertyChangedFor(nameof(HasSelectedZostava))]
    [NotifyPropertyChangedFor(nameof(SelectedCorpusMode))]
    [NotifyPropertyChangedFor(nameof(ReferenceBokText))]
    [NotifyPropertyChangedFor(nameof(SelectedPlacement))]
    [NotifyPropertyChangedFor(nameof(HasSelectedPlacement))]
    [NotifyPropertyChangedFor(nameof(ActiveZostavaForBinding))]
    private string? _selectedZostava;

    [ObservableProperty]
    private PartFace _selectedDrillFace = PartFace.Top;

    [ObservableProperty]
    private string _statusText = "Pripravené. Načítaj Excel alebo demo zostavu.";

    public bool HasSelection => SelectedPart != null;
    public bool IsPolicaSelected => SelectedPart?.IsPolica == true;
    public string SelectedDx => SelectedPart?.Dx.ToString("0.##", CultureInfo.InvariantCulture) ?? "–";
    public string SelectedDy => SelectedPart?.Dy.ToString("0.##", CultureInfo.InvariantCulture) ?? "–";
    public string SelectedDz => SelectedPart?.Dz.ToString("0.##", CultureInfo.InvariantCulture) ?? "–";
    public string SelectedKindText => SelectedPart?.Kind.ToString() ?? "–";
    public string SelectedZostavaText => ActiveZostava ?? "–";

    /// <summary>Pre väzby v UI – aktívna zostava na skladanie.</summary>
    public string? ActiveZostavaForBinding => ActiveZostava;

    private string? ActiveZostava =>
        !string.IsNullOrWhiteSpace(SelectedZostava)
            ? SelectedZostava
            : SelectedPart?.Zostava;

    public AssemblyPlacement? SelectedPlacement
    {
        get
        {
            if (SelectedPart == null)
                return null;
            var ctx = _assemblyStore.GetContext(ActiveZostava);
            return ctx?.Placements.FirstOrDefault(p => ReferenceEquals(p.Part, SelectedPart));
        }
    }

    public bool HasSelectedPlacement => SelectedPlacement != null;

    public string ReferenceBokText =>
        _assemblyStore.GetContext(ActiveZostava)?.ReferenceBok?.Name ?? "–";

    public bool HasSelectedZostava => !string.IsNullOrWhiteSpace(ActiveZostava);

    public IReadOnlyList<CorpusModeChoice> CorpusModeChoices { get; } =
    [
        new(AssemblyCorpusMode.BokVlozeny, "Bok vložený (dno/vrch nalozené)"),
        new(AssemblyCorpusMode.BokNalozeny, "Bok nalozený (dno/vrch vložené)"),
    ];

    public AssemblyCorpusMode SelectedCorpusMode
    {
        get => _assemblyStore.GetCorpusMode(ActiveZostava);
        set => SetCorpusModeForSelectedZostava(value);
    }

    public AssemblyContext? GetAssemblyContext(string? zostava) =>
        _assemblyStore.GetContext(zostava);

    public SufelSkupina? FindSufelSkupina(Part? part)
    {
        if (part == null)
            return null;

        var ctx = GetAssemblyContext(part.Zostava);
        if (ctx == null)
            return null;

        if (part.SufelSkupinaId is Guid id)
        {
            var byId = ctx.SufelSkupiny.FirstOrDefault(s => s.Id == id);
            if (byId != null)
                return byId;
        }

        return ctx.SufelSkupiny.FirstOrDefault(s => ReferenceEquals(s.BokPart, part));
    }

    [RelayCommand]
    private void BackToCabinetAssembly()
    {
        var zostava = ActiveZostava;
        if (string.IsNullOrWhiteSpace(zostava))
        {
            SelectedPart = null;
            return;
        }

        SelectedPart = GetAssemblyContext(zostava)?.ReferenceBok
            ?? Parts.FirstOrDefault(p =>
                string.Equals(p.Zostava, zostava, StringComparison.OrdinalIgnoreCase)
                && p.Kind is PartKind.BokL or PartKind.BokP);
    }

    public event EventHandler? Scene3DRefreshRequested;

    /// <summary>Dielec práve vkladaný / ťahaný v 3D okne.</summary>
    public AssemblyPlacement? ActivePlacementForDrag { get; set; }

    public AssemblyViewModel()
        : this(new PartsStore(), new ExcelImporter())
    {
    }

    public AssemblyViewModel(PartsStore store, ExcelImporter importer)
    {
        _store = store;
        _importer = importer;
        _store.ResolveCorpusMode = z => _assemblyStore.GetCorpusMode(z);
        _propagator = new OperationPropagator(_store);
        _propagator.StatusMessage += (_, msg) => StatusText = msg;

        _policaRebuildTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _policaRebuildTimer.Tick += OnPolicaRebuildTimerTick;
        _moventoApplyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _moventoApplyTimer.Tick += OnMoventoApplyTimerTick;
        _store.Parts.CollectionChanged += OnStorePartsCollectionChanged;
    }

    partial void OnSelectedPartChanged(Part? value)
    {
        if (!_syncingPartFromZostava
            && !string.IsNullOrWhiteSpace(value?.Zostava)
            && !string.Equals(value.Zostava, SelectedZostava, StringComparison.OrdinalIgnoreCase))
        {
            _syncingZostavaFromPart = true;
            SelectedZostava = value.Zostava;
            _syncingZostavaFromPart = false;
        }

        UnwirePlacement();
        if (SelectedPlacement != null)
        {
            SelectedPlacement.PropertyChanged += OnPlacementPropertyChanged;
            _wiredPlacement = SelectedPlacement;
        }

        OnPropertyChanged(nameof(SelectedPlacement));
        OnPropertyChanged(nameof(HasSelectedPlacement));
        OnPropertyChanged(nameof(ReferenceBokText));
        OnPropertyChanged(nameof(SelectedCorpusMode));

        if (value is { IsPolica: true })
            SchedulePolicaRebuild(value);
        Scene3DRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedZostavaChanged(string? value)
    {
        if (_syncingZostavaFromPart)
            return;

        var current = SelectedPart;
        if (current == null
            || !string.Equals(current.Zostava, value, StringComparison.OrdinalIgnoreCase))
        {
            var part = FindDefaultPartForZostava(value);
            if (part != null && !ReferenceEquals(SelectedPart, part))
            {
                _syncingPartFromZostava = true;
                SelectedPart = part;
                _syncingPartFromZostava = false;
            }
        }

        OnPropertyChanged(nameof(SelectedPlacement));
        OnPropertyChanged(nameof(HasSelectedPlacement));
        OnPropertyChanged(nameof(ReferenceBokText));
        OnPropertyChanged(nameof(SelectedCorpusMode));
        OnPropertyChanged(nameof(SelectedZostavaText));
        Scene3DRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Nastaví aktívnu zostavu zo stromu – pri prepnutí zostavy zladí <see cref="SelectedPart"/>.</summary>
    public void SelectZostava(string? zostava)
    {
        if (string.IsNullOrWhiteSpace(zostava))
            return;
        SelectedZostava = zostava;
    }

    private void SelectZostavaAfterImport()
    {
        var zostava = _store.Parts
            .Where(p => p.Kind is PartKind.BokL or PartKind.BokP)
            .Select(p => p.Zostava)
            .FirstOrDefault(z => !string.IsNullOrWhiteSpace(z));

        zostava ??= _assemblyStore.Contexts.Values
            .FirstOrDefault(c => c.ReferenceBok != null)
            ?.Zostava;

        zostava ??= _store.Parts.FirstOrDefault()?.Zostava;

        if (!string.IsNullOrWhiteSpace(zostava))
        {
            _syncingZostavaFromPart = true;
            SelectedZostava = zostava;
            _syncingZostavaFromPart = false;
        }

        SelectedPart = FindDefaultPartForZostava(SelectedZostava)
            ?? _store.Parts.FirstOrDefault();
    }

    private Part? FindDefaultPartForZostava(string? zostava)
    {
        if (string.IsNullOrWhiteSpace(zostava))
            return null;

        var inZostava = _store.Parts
            .Where(p => string.Equals(p.Zostava, zostava, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Poradie)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return inZostava.FirstOrDefault(p => p.Kind == PartKind.BokL)
            ?? inZostava.FirstOrDefault();
    }

    private void UnwirePlacement()
    {
        if (_wiredPlacement == null)
            return;
        _wiredPlacement.PropertyChanged -= OnPlacementPropertyChanged;
        _wiredPlacement = null;
    }

    private void OnPlacementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AssemblyPlacement.OffsetY)
            or nameof(AssemblyPlacement.OffsetDepthMm)
            or nameof(AssemblyPlacement.IsPlacedInScene))
            Scene3DRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Poloha ľavého predného rohu na ploche Top referenčného boku (mm).</summary>
    public void SetPlacementOnTopPlane(double offsetX, double offsetDepthMm)
    {
        var placement = ActivePlacementForDrag ?? SelectedPlacement;
        if (placement is not { IsPlacedInScene: true })
            return;

        var refBok = _assemblyStore.GetContext(ActiveZostava)?.ReferenceBok;
        if (refBok == null)
            return;

        var mode = _assemblyStore.GetCorpusMode(ActiveZostava);
        var (minX, maxX, maxY) = AssemblyPartLayout.GetPlacementOffsetLimits(
            placement.Part, refBok, refBok.Kind, mode);
        var x = Math.Clamp(offsetX, minX, maxX);
        var y = Math.Clamp(offsetDepthMm, 0, maxY);

        placement.OffsetY = x;
        placement.OffsetDepthMm = y;
        StatusText = FormattableString.Invariant(
            $"„{placement.Part.Name}“: X={x:0} mm, Y={y:0} mm");
    }

    /// <summary>Nastaví výšku umiestnenia (mm od spodu) pre vybraný skladaný dielec.</summary>
    public void SetPlacementOffsetY(double mm, bool snapRoztec = false)
    {
        var placement = SelectedPlacement;
        if (placement == null)
            return;

        var refBok = _assemblyStore.GetContext(ActiveZostava)?.ReferenceBok;
        var max = Math.Max(0, (refBok?.Dx ?? 2000) - placement.Part.Dz);
        var v = Math.Clamp(mm, 0, max);
        if (snapRoztec)
            v = Math.Round(v / 32.0) * 32.0;

        placement.OffsetY = v;
        StatusText = $"Výška „{placement.Part.Name}“: {v:0} mm";
    }

    private string BuildImportStatusText(int importedCount)
    {
        if (importedCount <= 0)
            return "Import: nenašli sa žiadne dielce.";

        var text = $"Importovaných položiek: {importedCount}";
        if (_lastSufelResolveResult is { SkupinyCount: > 0 } sufel)
        {
            text += $", šuflí: {sufel.SkupinyCount}";
            if (sufel.Warnings.Count > 0)
                text += $" ({sufel.Warnings.Count} upozornení)";
        }

        if (_lastChrbatValidateResult is { Warnings.Count: > 0 } chrbat)
            text += $", chrbát: {chrbat.Warnings.Count} upozornení";

        return text;
    }

    private void AfterPartsReplaced()
    {
        SufelZostavaInferrer.Infer(_store.Parts.ToList());
        CabinetBokClassifier.Apply(_store.Parts);
        _assemblyStore.SyncFromParts(_store.Parts);
        _lastSufelResolveResult = SufelAssemblyResolver.Resolve(_store, _assemblyStore);
        _lastChrbatValidateResult = ChrbatAssemblyValidator.ValidateAll(_assemblyStore, _store);
        MoventoSekcieSynchronizer.SyncAll(_assemblyStore);
        _store.RegenerateConnections();
        foreach (var p in _store.Parts)
            WirePolicaPart(p);
        RebuildPartsTree();
        WireAllSetupRefreshHooks();
        EnsureSelectedZostavaStillValid();
        TryInferCorpusModeForAllZostavy();
        OnPropertyChanged(nameof(SelectedPlacement));
        OnPropertyChanged(nameof(ReferenceBokText));
        OnPropertyChanged(nameof(HasSelectedZostava));
        NotifySceneRefresh();
    }

    private void EnsureSelectedZostavaStillValid()
    {
        var keys = _store.Parts
            .Select(p => p.Zostava)
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            SelectedZostava = null;
            return;
        }

        var current = ActiveZostava;
        if (!string.IsNullOrWhiteSpace(current)
            && keys.Any(z => string.Equals(z, current, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedZostava = keys.OrderBy(z => z, StringComparer.OrdinalIgnoreCase).First();
    }

    private void RebuildPartsTree()
    {
        PartsTree.Clear();
        foreach (var group in _store.Parts
                     .GroupBy(p => p.Zostava ?? "", StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var node = new ZostavaTreeNode(group.Key);
            var ctx = _assemblyStore.GetContext(group.Key);
            var sufelPartIds = new HashSet<Guid>();
            var sufelNodes = new List<SufelTreeNode>();

            if (ctx != null)
            {
                foreach (var sk in ctx.SufelSkupiny
                             .OrderBy(s => s.Pozicia.SortOrder())
                             .ThenBy(s => s.PoradieOdSpodu))
                {
                    sufelPartIds.UnionWith(sk.EnumeratePartIds());
                    var sufelNode = new SufelTreeNode(sk);
                    sufelNode.Refresh(_assemblyStore);
                    sufelNodes.Add(sufelNode);
                }
            }

            foreach (var part in group
                         .Where(p => !sufelPartIds.Contains(p.Id))
                         .OrderBy(p => p.Poradie)
                         .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                var entry = new PartTreeEntry(part);
                entry.Refresh(_assemblyStore);
                node.Children.Add(entry);
            }

            foreach (var sufelNode in sufelNodes)
                node.Children.Add(sufelNode);

            node.RefreshChildren(_assemblyStore);
            PartsTree.Add(node);
        }
    }

    private void RefreshTreeSetupStatus()
    {
        foreach (var node in PartsTree)
            node.RefreshChildren(_assemblyStore);
    }

    private void WireAllSetupRefreshHooks()
    {
        foreach (var part in _store.Parts)
            WirePartSetupRefresh(part);

        foreach (var ctx in _assemblyStore.Contexts.Values)
        {
            foreach (var placement in ctx.Placements)
                WirePlacementSetupRefresh(placement);
        }
    }

    private void WirePartSetupRefresh(Part part)
    {
        if (!_setupRefreshWiredParts.Add(part))
            return;

        part.Operations.CollectionChanged += (_, _) => RefreshTreeSetupStatus();
    }

    private void WirePlacementSetupRefresh(AssemblyPlacement placement)
    {
        if (!_setupRefreshWiredPlacements.Add(placement))
            return;

        placement.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AssemblyPlacement.IsPlacedInScene))
                RefreshTreeSetupStatus();
        };
    }

    [RelayCommand]
    private void ImportExcel()
    {
        var dlg = new WpfOpen
        {
            Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|Všetky|*.*"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var list = _importer.Import(dlg.FileName);
            _store.ReplaceParts(list);
            AfterPartsReplaced();
            SelectZostavaAfterImport();
            StatusText = BuildImportStatusText(list.Count);
            if (list.Count == 0)
            {
                WpfMsg.Show(
                    "V súbore sa nenašli žiadne dielce.\n\n"
                    + "Očakáva sa hárok s názvom obsahujúcim \"kusov\" alebo tabuľka s hlavičkou (Názov, Dĺžka, Šírka, Hrúbka).",
                    "Import Excel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            WpfMsg.Show(ex.Message, "Chyba importu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void LoadDemo()
    {
        const string zV = "V";
        const string zN = "N";

        var bokLv = new Part("Bok L", 564, 320, 18) { Zostava = zV, Kind = PartKind.BokL, Poradie = 1 };
        var bokPv = new Part("Bok P", 564, 320, 18) { Zostava = zV, Kind = PartKind.BokP, Poradie = 2 };
        var dnoV = new Part("Dno", 600, 320, 18) { Zostava = zV, Kind = PartKind.Dno, Poradie = 3 };
        var vrchV = new Part("Vrch", 600, 320, 18) { Zostava = zV, Kind = PartKind.Vrch, Poradie = 4 };

        var bokLn = new Part("Bok L", 1600, 320, 18) { Zostava = zN, Kind = PartKind.BokL, Poradie = 1 };
        var bokPn = new Part("Bok P", 1600, 320, 18) { Zostava = zN, Kind = PartKind.BokP, Poradie = 2 };
        var dnoN = new Part("Dno", 600, 320, 18) { Zostava = zN, Kind = PartKind.Dno, Poradie = 3 };
        var vrchN = new Part("Vrch", 600, 320, 18) { Zostava = zN, Kind = PartKind.Vrch, Poradie = 4 };

        _store.ReplaceParts(new[] { bokLv, bokPv, dnoV, vrchV, bokLn, bokPn, dnoN, vrchN });
        AfterPartsReplaced();

        if (_assemblyStore.GetContext(zV) is { } ctxV)
        {
            ctxV.CorpusMode = AssemblyCorpusMode.BokVlozeny;
            _assemblyStore.ApplyCorpusModeToPlacements(ctxV);
        }

        if (_assemblyStore.GetContext(zN) is { } ctxN)
        {
            ctxN.CorpusMode = AssemblyCorpusMode.BokNalozeny;
            _assemblyStore.ApplyCorpusModeToPlacements(ctxN);
        }

        _store.RegenerateConnections();

        SelectedZostava = zV;
        SelectedPart = bokLv;
        StatusText = "Demo: zostavy V a N – v strome označ zostavu alebo dielec.";
    }

    private void SetCorpusModeForSelectedZostava(AssemblyCorpusMode mode)
    {
        var zostava = ActiveZostava;
        ApplyCorpusMode(zostava, mode, "nastavený");
    }

    private void TryInferAndApplyCorpusMode(string? zostava)
    {
        if (string.IsNullOrWhiteSpace(zostava))
            return;

        var parts = _store.Parts.Where(p =>
            string.Equals(p.Zostava, zostava, StringComparison.OrdinalIgnoreCase));
        var inferred = CorpusModeDetector.Infer(parts);
        if (inferred is not { } mode)
            return;

        var ctx = _assemblyStore.GetContext(zostava);
        if (ctx == null || ctx.CorpusMode == mode)
            return;

        ApplyCorpusMode(zostava, mode, "podľa kolíkov");
    }

    private void TryInferCorpusModeForAllZostavy()
    {
        foreach (var zostava in _store.Parts
                     .Select(p => p.Zostava)
                     .Where(z => !string.IsNullOrWhiteSpace(z))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            TryInferAndApplyCorpusMode(zostava);
    }

    private void ApplyCorpusMode(string? zostava, AssemblyCorpusMode mode, string reason)
    {
        var ctx = _assemblyStore.GetContext(zostava);
        if (ctx == null || ctx.CorpusMode == mode)
            return;

        ctx.CorpusMode = mode;
        _assemblyStore.ApplyCorpusModeToPlacements(ctx);
        _store.RegenerateConnections();
        OnPropertyChanged(nameof(SelectedCorpusMode));
        Scene3DRefreshRequested?.Invoke(this, EventArgs.Empty);
        StatusText = $"Zostava „{zostava}“: {CorpusModeLabel(mode)} ({reason}).";
    }

    private static string CorpusModeLabel(AssemblyCorpusMode mode) =>
        mode switch
        {
            AssemblyCorpusMode.BokVlozeny => "bok vložený (dno/vrch nalozené)",
            AssemblyCorpusMode.BokNalozeny => "bok nalozený (dno/vrch vložené)",
            _ => mode.ToString()
        };

    public bool UsesDnoVrchDualTopKoliky(Part part, PartFace face) =>
        DnoVrchTopKolikyHelper.UsesDualTopKoliky(
            part, face, _assemblyStore.GetCorpusMode(part.Zostava));

    public DrillOperation? FindEditableDrill(Part part, PartFace? face = null, PartKind? kolikPartnerBok = null)
    {
        if (TraverzaKolikyApplier.IsTraverzaPart(part))
            return TraverzaKolikyApplier.FindTraverzaMaster(part);

        IEnumerable<DrillOperation> q = part.Operations.OfType<DrillOperation>()
            .Where(o => !o.IsPropagated);
        if (face.HasValue)
            q = q.Where(o => o.Face == face.Value);
        if (kolikPartnerBok.HasValue)
            q = q.Where(o => o.KolikPartnerBok == kolikPartnerBok.Value);

        return q.FirstOrDefault();
    }

    public bool SaveDrillOperation(DrillOperation op, bool isNew, Part? targetPart = null)
    {
        var part = targetPart ?? SelectedPart;
        if (part == null)
        {
            StatusText = "Najprv vyber dielec.";
            return false;
        }

        if (isNew)
        {
            _propagator.ExecuteWithoutPropagation(() => part.Operations.Add(op));
            _propagator.PropagateDrillAdded(part, op);
        }

        TryInferAndApplyCorpusMode(part.Zostava);

        RefreshTreeSetupStatus();
        StatusText = isNew
            ? $"Pridané vŕtanie: {op.Name} ({op.CountX}×{op.CountY} kolíkov, plocha {op.Face})."
            : $"Upravené vŕtanie: {op.Name} ({op.CountX}×{op.CountY} kolíkov, plocha {op.Face}).";
        NotifySceneRefresh();
        return true;
    }

    public bool AddTraverzaKoliky(TraverzaKolikyApplier.Request request)
    {
        var part = SelectedPart;
        if (part == null || !TraverzaKolikyApplier.IsTraverzaPart(part))
        {
            StatusText = "Vyber traverzu v zostave.";
            return false;
        }

        try
        {
            var wasEditing = TraverzaKolikyApplier.FindTraverzaMaster(part) != null;
            _propagator.ExecuteWithoutPropagation(() =>
                TraverzaKolikyApplier.Apply(_store, part, request));
            StatusText = wasEditing
                ? "Traverza: kolíky upravené (Left + Right + boky)."
                : "Traverza: kolíky nastavené (Left + Right + boky).";
            NotifySceneRefresh();
            return true;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private void AddOrUpdateObeh()
    {
        var part = SelectedPart;
        if (part == null)
        {
            StatusText = "Najprv vyber dielec.";
            return;
        }

        if (SufelNameParser.IsSufelKind(part.Kind))
        {
            StatusText = "Šufľové dielce nemajú obeh.";
            return;
        }

        var existing = part.Operations.OfType<ObehOperation>().FirstOrDefault();
        if (existing != null)
        {
            existing.Vpredu = part.AbsPredna.GetValueOrDefault() > 0;
            existing.Vzadu = part.AbsZadna.GetValueOrDefault() > 0;
            existing.Vlavo = part.AbsLava.GetValueOrDefault() > 0;
            existing.Vpravo = part.AbsPrava.GetValueOrDefault() > 0;
            StatusText = "Obeh aktualizovaný podľa ABS hodnôt dielca.";
        }
        else
        {
            var obeh = ObehOperation.FromPart(part);
            part.Operations.Add(obeh);
            var aktivne = (obeh.Vpredu ? 1 : 0) + (obeh.Vzadu ? 1 : 0) + (obeh.Vlavo ? 1 : 0) + (obeh.Vpravo ? 1 : 0);
            StatusText = aktivne > 0
                ? $"Obeh pridaný (aktívne strany: {aktivne})."
                : "Obeh pridaný (dielec nemá ABS – všetky strany = 0).";
        }
        NotifySceneRefresh();
    }

    [RelayCommand]
    private void ClearPolicaSeria(PolicaSeria? seria)
    {
        if (SelectedPart is not { IsPolica: true } part || seria == null)
            return;
        part.ClearPolicaSeria(seria);
        SchedulePolicaRebuild(part);
    }

    [RelayCommand]
    private void CopyPolicaSeria(PolicaSeria? seria)
    {
        if (SelectedPart is not { IsPolica: true } part || seria == null)
            return;
        part.CopyPolicaSeriaFromPrevious(seria);
        SchedulePolicaRebuild(part);
    }

    public bool ApplyPolicaKoliky(bool showErrors = true)
    {
        var part = SelectedPart;
        if (part == null || !PolicaKolikyApplier.IsPolicaPart(part))
        {
            StatusText = "Vyber dielec typu Polica.";
            return false;
        }

        return ApplyPolicaKoliky(part, showErrors);
    }

    private bool ApplyPolicaKoliky(Part part, bool showErrors)
    {
        try
        {
            part.EnsurePolicaSerie();
            var wasEditing = PolicaKolikyApplier.HasPolicaBundle(_store, part);
            _propagator.ExecuteWithoutPropagation(() =>
                PolicaKolikyApplier.Apply(_store, part));

            var typ = part.PolicaJePevna ? "pevná" : "polohovateľná";
            StatusText = wasEditing
                ? $"Polica ({typ}): spoje prepočítané."
                : $"Polica ({typ}): spoje nastavené.";
            NotifySceneRefresh();
            return true;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            if (showErrors)
                WpfMsg.Show(ex.Message, "Polica", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void SchedulePolicaRebuild(Part polica)
    {
        if (!polica.IsPolica)
            return;
        _policaRebuildTarget = polica;
        _policaRebuildTimer.Stop();
        _policaRebuildTimer.Start();
    }

    private void OnPolicaRebuildTimerTick(object? sender, EventArgs e)
    {
        _policaRebuildTimer.Stop();
        var target = _policaRebuildTarget;
        _policaRebuildTarget = null;
        if (target == null)
            return;
        ApplyPolicaKoliky(target, showErrors: false);
    }

    private void OnStorePartsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var p in _store.Parts)
            {
                WirePolicaPart(p);
                WirePartSetupRefresh(p);
            }
            return;
        }

        if (e.NewItems != null)
        {
            foreach (Part p in e.NewItems)
            {
                WirePolicaPart(p);
                WirePartSetupRefresh(p);
            }
        }

        if (e.OldItems != null)
        {
            foreach (Part p in e.OldItems)
                UnwirePolicaPart(p);
        }
    }

    private void WirePolicaPart(Part part)
    {
        if (!part.IsPolica)
            return;

        part.PropertyChanged += OnPolicaPartPropertyChanged;
        part.PolicaSerie.CollectionChanged += OnPolicaSerieCollectionChanged;
        foreach (var seria in part.PolicaSerie)
            seria.PropertyChanged += OnPolicaSeriaPropertyChanged;
    }

    private void UnwirePolicaPart(Part part)
    {
        part.PropertyChanged -= OnPolicaPartPropertyChanged;
        part.PolicaSerie.CollectionChanged -= OnPolicaSerieCollectionChanged;
        foreach (var seria in part.PolicaSerie)
            seria.PropertyChanged -= OnPolicaSeriaPropertyChanged;
    }

    private void OnPolicaPartPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Part part || e.PropertyName == null)
            return;
        if (!PolicaRebuildProperties.Contains(e.PropertyName))
            return;
        SchedulePolicaRebuild(part);
    }

    private void OnPolicaSerieCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (PolicaSeria seria in e.NewItems)
                seria.PropertyChanged += OnPolicaSeriaPropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (PolicaSeria seria in e.OldItems)
                seria.PropertyChanged -= OnPolicaSeriaPropertyChanged;
        }

        if (sender is not ObservableCollection<PolicaSeria> serie)
            return;

        var part = _store.Parts.FirstOrDefault(p => ReferenceEquals(p.PolicaSerie, serie));
        if (part != null)
            SchedulePolicaRebuild(part);
    }

    private void OnPolicaSeriaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PolicaSeria seria || e.PropertyName == null)
            return;
        if (!PolicaSeriaRebuildProperties.Contains(e.PropertyName))
            return;

        var part = _store.Parts.FirstOrDefault(p => p.PolicaSerie.Contains(seria));
        if (part != null)
            SchedulePolicaRebuild(part);
    }

    public void ScheduleMoventoApply()
    {
        _moventoApplyTimer.Stop();
        _moventoApplyTimer.Start();
    }

    public void ApplyMoventoForActiveZostava()
    {
        var zostava = ActiveZostava;
        if (string.IsNullOrWhiteSpace(zostava))
            return;

        var ctx = GetAssemblyContext(zostava);
        if (ctx == null || ctx.MoventoSekcie.Count == 0)
            return;

        if (!ctx.MoventoSekcie.Any(s => s.VyskaMm > 0))
            return;

        MoventoKolikyApplier.Apply(_store, ctx, zostava);
        RefreshTreeSetupStatus();
        NotifySceneRefresh();
    }

    private void OnMoventoApplyTimerTick(object? sender, EventArgs e)
    {
        _moventoApplyTimer.Stop();
        ApplyMoventoForActiveZostava();
    }

    [RelayCommand]
    private void ApplyAssembly()
    {
        var zostava = ActiveZostava;
        if (string.IsNullOrWhiteSpace(zostava))
        {
            StatusText = "V strome označ zostavu alebo dielec.";
            return;
        }

        var ctx = _assemblyStore.GetContext(zostava);
        if (ctx == null)
        {
            StatusText = "Zostava nemá kontext skladania.";
            return;
        }

        try
        {
            AssemblySolverApplier.Apply(_store, ctx, _propagator);
            ApplyMoventoForActiveZostava();
            StatusText = $"Zostava „{zostava}“: prepocítané ({ctx.Placements.Count} umiestnení).";
            RefreshTreeSetupStatus();
            NotifySceneRefresh();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            WpfMsg.Show(ex.Message, "Prepocítaj zostavu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void ExportAssemblyXcs()
    {
        var zostava = ActiveZostava;
        if (string.IsNullOrWhiteSpace(zostava))
        {
            StatusText = "V strome označ zostavu alebo dielec.";
            return;
        }

        var parts = _store.Parts.Where(p => p.Zostava == zostava).ToList();
        if (parts.Count == 0)
        {
            StatusText = "Zostava je prázdna.";
            return;
        }

        var pickDlg = new WpfSave
        {
            Title = "Vyber priečinok – zadaj ľubovoľný názov súboru v cieľovom priečinku",
            Filter = "Priečinok|*.",
            FileName = "tu_exportuj",
            OverwritePrompt = false
        };
        if (pickDlg.ShowDialog() != true)
            return;

        var targetDir = Path.GetDirectoryName(pickDlg.FileName);
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            StatusText = "Neplatný priečinok.";
            return;
        }

        EnsureSufelMacroOperations();

        var exporter = new XcsExporter();
        var ok = 0;
        var errors = new List<string>();

        foreach (var part in parts)
        {
            try
            {
                var path = Path.Combine(targetDir, BuildExportFileName(part));
                var ctx = XcsExporter.ContextFromPart(part);
                exporter.ExportToFile(part, ctx, path);
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add($"{part.Name}: {ex.Message}");
            }
        }

        StatusText = errors.Count == 0
            ? $"Export zostavy OK: {ok} súborov → {targetDir}"
            : $"Export: {ok} OK, {errors.Count} chýb.";
        if (errors.Count > 0)
            WpfMsg.Show(string.Join(Environment.NewLine, errors), "Export zostavy – chyby",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [RelayCommand]
    private void ExportXcs()
    {
        var part = SelectedPart;
        if (part == null)
        {
            StatusText = "Najprv vyber dielec.";
            return;
        }

        var dlg = new WpfSave
        {
            Filter = "XCS súbor (*.xcs)|*.xcs",
            FileName = BuildExportFileName(part)
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            EnsureSufelMacroOperations();
            var exporter = new XcsExporter();
            var ctx = XcsExporter.ContextFromPart(part);
            exporter.ExportToFile(part, ctx, dlg.FileName);
            StatusText = $"Export OK: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            WpfMsg.Show(ex.Message, "Chyba exportu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EnsureSufelMacroOperations()
    {
        SufelMacroApplier.ApplyAll(_assemblyStore);
        SufelMacroApplier.StripObehFromSufelParts(_store.Parts);
    }

    private static string BuildExportFileName(Part part) =>
        SufelXcsExportNames.IsSufelExportPart(part)
            ? SufelXcsExportNames.BuildFileName(part)
            : SanitizeFileName(part.Name) + ".xcs";

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString().Trim();
    }

    public void RemovePartOperations(Part part, IEnumerable<CncOperation> operations)
    {
        MoventoKolikyApplier.RemoveWithPartnerMirror(_store, part, operations);
        NotifySceneRefresh();
    }

    public void NotifySceneRefresh()
    {
        RefreshTreeSetupStatus();
        Scene3DRefreshRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Uzol stromu zostáv v hlavnom okne.</summary>
public sealed record CorpusModeChoice(AssemblyCorpusMode Mode, string Label);

public sealed partial class ZostavaTreeNode : ObservableObject
{
    public ZostavaTreeNode(string? zostava)
    {
        Zostava = zostava ?? "";
        DisplayName = string.IsNullOrWhiteSpace(Zostava) ? "(bez zostavy)" : $"Zostava {Zostava}";
    }

    public string Zostava { get; }
    public string DisplayName { get; }
    public bool IsExpanded { get; set; } = true;

    /// <summary>Dielce zostavy a uzly šuflí (<see cref="SufelTreeNode"/>).</summary>
    public ObservableCollection<object> Children { get; } = new();

    [ObservableProperty]
    private bool _isConfigured;

    public void RefreshChildren(AssemblyStore store)
    {
        foreach (var child in Children)
        {
            switch (child)
            {
                case PartTreeEntry entry:
                    entry.Refresh(store);
                    break;
                case SufelTreeNode sufel:
                    sufel.Refresh(store);
                    break;
            }
        }

        IsConfigured = Children.Count > 0 && Children.All(child => child switch
        {
            PartTreeEntry entry => entry.IsConfigured,
            SufelTreeNode sufel => sufel.IsConfigured,
            _ => false,
        });
    }
}
