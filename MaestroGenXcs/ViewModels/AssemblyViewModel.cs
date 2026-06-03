using System.Collections.ObjectModel;
using System.IO;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Import;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Rendering;
using MaestroGenXcs.Services;
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
    private Part? _policaRebuildTarget;
    private AssemblyPlacement? _wiredPlacement;

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
    private Part? _selectedPart;

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
    public string SelectedZostavaText => SelectedPart?.Zostava ?? "–";

    public AssemblyPlacement? SelectedPlacement
    {
        get
        {
            if (SelectedPart == null)
                return null;
            var ctx = _assemblyStore.GetContext(SelectedPart.Zostava);
            return ctx?.Placements.FirstOrDefault(p => ReferenceEquals(p.Part, SelectedPart));
        }
    }

    public bool HasSelectedPlacement => SelectedPlacement != null;

    public string ReferenceBokText =>
        _assemblyStore.GetContext(SelectedPart?.Zostava)?.ReferenceBok?.Name ?? "–";

    public bool HasSelectedZostava => !string.IsNullOrWhiteSpace(SelectedPart?.Zostava);

    public IReadOnlyList<CorpusModeChoice> CorpusModeChoices { get; } =
    [
        new(AssemblyCorpusMode.BokVlozeny, "Bok vložený (dno/vrch nalozené)"),
        new(AssemblyCorpusMode.BokNalozeny, "Bok nalozený (dno/vrch vložené)"),
    ];

    public AssemblyCorpusMode SelectedCorpusMode
    {
        get => _assemblyStore.GetCorpusMode(SelectedPart?.Zostava);
        set => SetCorpusModeForSelectedZostava(value);
    }

    public AssemblyContext? GetAssemblyContext(string? zostava) =>
        _assemblyStore.GetContext(zostava);

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
        _store.Parts.CollectionChanged += OnStorePartsCollectionChanged;
    }

    partial void OnSelectedPartChanged(Part? value)
    {
        UnwirePlacement();
        if (SelectedPlacement != null)
        {
            SelectedPlacement.PropertyChanged += OnPlacementPropertyChanged;
            _wiredPlacement = SelectedPlacement;
        }

        OnPropertyChanged(nameof(SelectedPlacement));
        OnPropertyChanged(nameof(HasSelectedPlacement));

        if (value is { IsPolica: true })
            SchedulePolicaRebuild(value);
        Scene3DRefreshRequested?.Invoke(this, EventArgs.Empty);
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

        var refBok = _assemblyStore.GetContext(SelectedPart?.Zostava)?.ReferenceBok;
        if (refBok == null)
            return;

        var mode = _assemblyStore.GetCorpusMode(SelectedPart?.Zostava);
        var contact = ConnectionMap.GetContactFaceToReferenceBokTop(refBok.Kind, placement.Part.Kind, mode);
        var (footX, footY) = AssemblyPartLayout.GetFootprintOnBokTop(placement.Part, contact);
        var maxX = Math.Max(0, refBok.Dx - footX);
        var maxY = Math.Max(0, refBok.Dy - footY);
        var x = Math.Clamp(offsetX, 0, maxX);
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

        var refBok = _assemblyStore.GetContext(SelectedPart?.Zostava)?.ReferenceBok;
        var max = Math.Max(0, (refBok?.Dx ?? 2000) - placement.Part.Dz);
        var v = Math.Clamp(mm, 0, max);
        if (snapRoztec)
            v = Math.Round(v / 32.0) * 32.0;

        placement.OffsetY = v;
        StatusText = $"Výška „{placement.Part.Name}“: {v:0} mm";
    }

    private void AfterPartsReplaced()
    {
        _assemblyStore.SyncFromParts(_store.Parts);
        foreach (var p in _store.Parts)
            WirePolicaPart(p);
        RebuildPartsTree();
        OnPropertyChanged(nameof(SelectedPlacement));
        OnPropertyChanged(nameof(ReferenceBokText));
    }

    private void RebuildPartsTree()
    {
        PartsTree.Clear();
        foreach (var group in _store.Parts
                     .GroupBy(p => p.Zostava ?? "", StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var node = new ZostavaTreeNode(group.Key);
            foreach (var part in group.OrderBy(p => p.Poradie).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                node.Parts.Add(part);
            PartsTree.Add(node);
        }
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
            SelectedPart = _store.Parts.FirstOrDefault();
            StatusText = list.Count > 0
                ? $"Importovaných položiek: {list.Count}"
                : "Import: nenašli sa žiadne dielce.";
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

        SelectedPart = bokLv;
        StatusText = "Demo: zostava V (bok vložený) a N (bok nalozený). Režim korpusu zmeň v paneli vpravo.";
    }

    private void SetCorpusModeForSelectedZostava(AssemblyCorpusMode mode)
    {
        var zostava = SelectedPart?.Zostava;
        var ctx = _assemblyStore.GetContext(zostava);
        if (ctx == null)
            return;

        if (ctx.CorpusMode == mode)
            return;

        ctx.CorpusMode = mode;
        _assemblyStore.ApplyCorpusModeToPlacements(ctx);
        _store.RegenerateConnections();
        OnPropertyChanged(nameof(SelectedCorpusMode));
        Scene3DRefreshRequested?.Invoke(this, EventArgs.Empty);
        StatusText = $"Zostava „{zostava}“: {CorpusModeLabel(mode)}.";
    }

    private static string CorpusModeLabel(AssemblyCorpusMode mode) =>
        mode switch
        {
            AssemblyCorpusMode.BokVlozeny => "bok vložený (dno/vrch nalozené)",
            AssemblyCorpusMode.BokNalozeny => "bok nalozený (dno/vrch vložené)",
            _ => mode.ToString()
        };

    public DrillOperation? FindEditableDrill(Part part, PartFace? face = null)
    {
        if (TraverzaKolikyApplier.IsTraverzaPart(part))
            return TraverzaKolikyApplier.FindTraverzaMaster(part);

        IEnumerable<DrillOperation> q = part.Operations.OfType<DrillOperation>();
        if (face.HasValue)
            q = q.Where(o => o.Face == face.Value);

        return q.FirstOrDefault();
    }

    public bool SaveDrillOperation(DrillOperation op, bool isNew)
    {
        var part = SelectedPart;
        if (part == null)
        {
            StatusText = "Najprv vyber dielec.";
            return false;
        }

        if (isNew)
            part.Operations.Add(op);

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
                WirePolicaPart(p);
            return;
        }

        if (e.NewItems != null)
        {
            foreach (Part p in e.NewItems)
                WirePolicaPart(p);
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

    [RelayCommand]
    private void ApplyAssembly()
    {
        var zostava = SelectedPart?.Zostava;
        if (string.IsNullOrWhiteSpace(zostava))
        {
            StatusText = "Vyber dielec v zostave.";
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
            StatusText = $"Zostava „{zostava}“: prepocítané ({ctx.Placements.Count} umiestnení).";
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
        var zostava = SelectedPart?.Zostava;
        if (string.IsNullOrWhiteSpace(zostava))
        {
            StatusText = "Vyber dielec v zostave.";
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

        var exporter = new XcsExporter();
        var ok = 0;
        var errors = new List<string>();

        foreach (var part in parts)
        {
            try
            {
                var path = Path.Combine(targetDir, SanitizeFileName(part.Name) + ".xcs");
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
            FileName = SanitizeFileName(part.Name) + ".xcs"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString().Trim();
    }

    public void NotifySceneRefresh() =>
        Scene3DRefreshRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>Uzol stromu zostáv v hlavnom okne.</summary>
public sealed record CorpusModeChoice(AssemblyCorpusMode Mode, string Label);

public sealed class ZostavaTreeNode
{
    public ZostavaTreeNode(string? zostava)
    {
        Zostava = zostava ?? "";
        DisplayName = string.IsNullOrWhiteSpace(Zostava) ? "(bez zostavy)" : $"Zostava {Zostava}";
    }

    public string Zostava { get; }
    public string DisplayName { get; }
    public bool IsExpanded { get; set; } = true;
    public ObservableCollection<Part> Parts { get; } = new();
}
