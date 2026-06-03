using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using MaestroGenXcs.Rendering;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Services;

namespace MaestroGenXcs.Views;

/// <summary>
/// Dialóg na vytvorenie inštancie <see cref="DrillOperation"/>.
/// <para>
/// Šablóny sa <b>automaticky</b> volia podľa <see cref="PartFace"/>:
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Hrany</b> (<c>Left/Right/Front/Back</c>) → nová šablóna
///     „<c>Kolík do hrany</c>" z kategórie „Operácie vŕtania (nové)".
///     Operátor vidí len <b>3 polia</b>: <i>Prvý kolík</i>, <i>Počet kolíkov</i>,
///     <i>Rozteč kolíkov</i>. Ostatné parametre operácie (hĺbka, nástroj, refpos)
///     sa odvodia automaticky podľa hrany.
///   </item>
///   <item>
///     <b>Top</b> → stará dvojica šablón
///     „<c>Kolík plocha</c>" + „<c>Plocha po X/Y</c>" s radio pickerom,
///     ostáva pre kompatibilitu kým neprerobíme aj plochu.
///   </item>
/// </list>
/// <para>
/// Z <see cref="TemplateStore"/> sa berú iba <b>editovateľné</b> parametre šablón;
/// fixné/pomocné sú zadrôtované konštanty v definícii šablóny.
/// </para>
/// </summary>
public partial class AssemblyKolikyDialog : Window
{
    // ---- Staré šablóny (Top) ----
    private const string TemplateKolikPlocha = "Kolík plocha";
    private const string TemplatePlochaPoX = "Plocha po X";
    private const string TemplatePlochaPoY = "Plocha po Y";

    // ---- Nová šablóna (hrany) ----
    private const string TemplateKolikDoHrany = "Kolík do hrany";

    private readonly TemplateStore _store = TemplateStore.Instance;
    private readonly PartFace _face;
    private readonly bool _isEdge;

    /// <summary>
    /// Dielec, pre ktorý sa kolíky vytvárajú. Slúži najmä na výpočet
    /// Y-súradnice na hranách – tam sa kolíky vŕtajú do stredu hrúbky,
    /// teda <c>Y = Dz / 2</c>.
    /// </summary>
    private readonly Part? _part;

    /// <summary>Názov procesnej šablóny (pre header/preview).</summary>
    private readonly string _processTpl;

    /// <summary>Aktuálne zvolený pattern (pre Top sa môže meniť radiom).</summary>
    private string _currentPatternTpl;

    public DrillOperation? Result { get; private set; }

    /// <summary>Vyplnené pri traverze – MainWindow zavolá <see cref="TraverzaKolikyApplier"/>.</summary>
    public TraverzaKolikyApplier.Request? TraverzaRequest { get; private set; }

    public bool IsTraverzaMode { get; }

    private readonly TraverzaKolikyApplier.Request _traverzaDefaults;
    private readonly DrillOperation? _editDrill;
    private readonly bool _isEditMode;
    private bool _populating;
    /// <summary>Hrana s počtom kolíkov v CountY (traverza / priečka), nie v CountX.</summary>
    private bool _edgePatternAlongCountY;
    private bool _suppressPreviewRefresh;

    public AssemblyKolikyDialog(PartFace face, Part? part = null, DrillOperation? editDrill = null)
    {
        _face = face;
        _part = part;
        _isEdge = face != PartFace.Top;
        // Traverza: vždy Left + Right, nezávisle od kliknutej plochy v náhľade.
        IsTraverzaMode = part != null && TraverzaKolikyApplier.IsTraverzaPart(part);

        _store.Load();

        if (IsTraverzaMode && part != null)
        {
            var master = editDrill?.IsTraverzaMaster == true
                ? editDrill
                : TraverzaKolikyApplier.FindTraverzaMaster(part);
            _editDrill = master ?? editDrill;
        }
        else
            _editDrill = editDrill;

        _isEditMode = _editDrill != null;

        if (IsTraverzaMode && _editDrill != null)
            _traverzaDefaults = TraverzaKolikyApplier.RequestFromMaster(_editDrill);
        else
            _traverzaDefaults = TraverzaKolikyApplier.DefaultsFromStore(_store);

        if (_isEdge)
        {
            _processTpl = TemplateKolikDoHrany;
            _currentPatternTpl = TemplateKolikDoHrany;
        }
        else
        {
            _processTpl = TemplateKolikPlocha;
            _currentPatternTpl = TemplatePlochaPoX;
        }

        InitializeComponent();
        Populate();
        WirePreviewInputs();
        RefreshPartPreview();

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    /// <summary>
    /// Y súradnica kolíkov pre danú plochu. Pre hrany (L/R/F/B) je to vždy
    /// stred hrúbky dielca = <c>Dz / 2</c>; pre Top to počíta zo store /
    /// user editu (tam sa Y zadáva manuálne).
    /// </summary>
    private double DefaultEdgeY => _part != null ? Math.Round(_part.Dz / 2.0, 3) : 9.0;

    private bool ShowOppositeBokOption =>
        _part is { Kind: PartKind.BokL or PartKind.BokP }
        && (_face == PartFace.Right || _face == PartFace.Left);

    private void Populate()
    {
        _populating = true;
        _suppressPreviewRefresh = true;
        try
        {
            HeaderLabel.Text = _part != null
                ? $"Kolíky · {_part.Name}"
                : "Kolíky – skladanie zostavy";
            SubHeaderLabel.Text = $"Plocha {_face.ToWorkplane()} · kontakt voči boku L (Top)";

            FaceLabel.Text = _face.ToWorkplane();

            if (IsTraverzaMode)
                ConfigureForTraverza();
            else if (_isEdge)
                ConfigureForEdge();
            else
                ConfigureForTop();

            if (_isEditMode)
                PopulateFromDrill(_editDrill!);
            else
            {
                NameBox.Text = $"Kolik_{_face}_{DateTime.Now:HHmmss}";
                LoadPatternIntoFields(_currentPatternTpl);
            }

            Title = _part != null
                ? $"Kolíky – {_part.Name}"
                : "Kolíky – skladanie zostavy";

            UpdateTemplatePreview();

            TraverzaPanel.Visibility = IsTraverzaMode ? Visibility.Visible : Visibility.Collapsed;
            OppositeBokPanel.Visibility = !IsTraverzaMode && ShowOppositeBokOption ? Visibility.Visible : Visibility.Collapsed;
            if (ShowOppositeBokOption)
                RadioPreniestNaBok.IsChecked = _isEditMode && _editDrill != null
                    ? _editDrill.PreniestNaDruhyBok
                    : true;
        }
        finally
        {
            _populating = false;
            _suppressPreviewRefresh = false;
            RefreshPartPreview();
        }
    }

    private void PopulateFromDrill(DrillOperation d)
    {
        NameBox.Text = d.Name;
        RefposBox.Text = d.RefPos.ToString(CultureInfo.InvariantCulture);
        XStartBox.Text = d.XStart.ToString("0.###", CultureInfo.InvariantCulture);
        YStartBox.Text = d.YStart.ToString("0.###", CultureInfo.InvariantCulture);

        if (IsTraverzaMode)
        {
            NameBox.Text = d.Name;
            var pocet = d.TraverzaPatternAlongX ? Math.Max(1, d.CountX) : Math.Max(1, d.CountY);
            var roztec = d.TraverzaPatternAlongX ? d.PitchX : d.PitchY;
            NumberOfColumnsBox.Text = pocet.ToString(CultureInfo.InvariantCulture);
            ColumnDistanceBox.Text = roztec.ToString("0.###", CultureInfo.InvariantCulture);
            TraverzaBokXBox.Text = d.TraverzaBokXStart.ToString("0.###", CultureInfo.InvariantCulture);
            TraverzaBokYBox.Text = d.TraverzaBokYStart.ToString("0.###", CultureInfo.InvariantCulture);
            if (d.TraverzaPatternAlongX)
                RadioTraverzaLavPrav.IsChecked = true;
            else
                RadioTraverzaPredZad.IsChecked = true;
            return;
        }

        if (_isEdge)
        {
            // Priečka/traverza hrana: CountY + PitchY; šablóna hrany: CountX + PitchX
            _edgePatternAlongCountY = d.CountY > 1 && d.CountX == 1;
            if (_edgePatternAlongCountY)
            {
                NumberOfColumnsBox.Text = d.CountY.ToString(CultureInfo.InvariantCulture);
                ColumnDistanceBox.Text = d.PitchY.ToString("0.###", CultureInfo.InvariantCulture);
            }
            else
            {
                NumberOfColumnsBox.Text = d.CountX.ToString(CultureInfo.InvariantCulture);
                ColumnDistanceBox.Text = d.PitchX.ToString("0.###", CultureInfo.InvariantCulture);
            }
            NumberOfRowsBox.Text = "1";
            RowsDistanceBox.Text = "0";
            RefposBox.Text = d.RefPos.ToString(CultureInfo.InvariantCulture);
            return;
        }

        _edgePatternAlongCountY = false;

        NumberOfRowsBox.Text = d.CountY.ToString(CultureInfo.InvariantCulture);
        NumberOfColumnsBox.Text = d.CountX.ToString(CultureInfo.InvariantCulture);
        RowsDistanceBox.Text = d.PitchY.ToString("0.###", CultureInfo.InvariantCulture);
        ColumnDistanceBox.Text = d.PitchX.ToString("0.###", CultureInfo.InvariantCulture);

        if (d.CountY > 1 && d.CountX == 1)
        {
            RadioPlochaY.IsChecked = true;
            _currentPatternTpl = TemplatePlochaPoY;
        }
        else
        {
            RadioPlochaX.IsChecked = true;
            _currentPatternTpl = TemplatePlochaPoX;
        }

        if (ShowOppositeBokOption)
            RadioPreniestNaBok.IsChecked = d.PreniestNaDruhyBok;
    }

    private void ConfigureForTraverza()
    {
        HeaderLabel.Text = "Vŕtanie kolíkov · Traverza";
        SubHeaderLabel.Text = _isEditMode
            ? "Úpravou sa prepíšu kolíky na všetkých plochách nižšie."
            : "Jedným potvrdením sa vytvorí kolíky na všetkých plochách nižšie.";
        FaceLabel.Text = "Left + Right + boky";
        TraverzaPloskyLabel.Text = string.Join(Environment.NewLine, TraverzaKolikyApplier.RequiredPlosky);

        PatternPickerForTop.Visibility = Visibility.Collapsed;
        PatternFixedLabel.Visibility = Visibility.Visible;
        PatternFixedLabel.Text = TraverzaKolikyApplier.SettingsTemplate;

        SetRowVisible(LblNumberOfRows, NumberOfRowsBox, null, Visibility.Collapsed);
        SetRowVisible(LblRowsDistance, RowsDistanceBox, UnitRowsDistance, Visibility.Collapsed);
        SetRowVisible(LblRefpos, RefposBox, null, Visibility.Collapsed);

        LblNumberOfColumns.Text = "Počet kolíkov";
        LblColumnDistance.Text = "Rozteč kolíkov";
        LblXStart.Text = "Prvý kolík na traverze";

        NumberOfRowsBox.Text = "1";
        RowsDistanceBox.Text = "0";
        RefposBox.Text = "2";
        YStartBox.Text = DefaultEdgeY.ToString("0.###", CultureInfo.InvariantCulture);

        if (!_isEditMode)
        {
            NameBox.Text = _traverzaDefaults.Name;
            XStartBox.Text = _traverzaDefaults.PrvyKolikTraverza.ToString("0.###", CultureInfo.InvariantCulture);
            NumberOfColumnsBox.Text = _traverzaDefaults.Pocet.ToString(CultureInfo.InvariantCulture);
            ColumnDistanceBox.Text = _traverzaDefaults.Roztec.ToString("0.###", CultureInfo.InvariantCulture);
            TraverzaBokXBox.Text = _traverzaDefaults.BokXStart.ToString("0.###", CultureInfo.InvariantCulture);
            TraverzaBokYBox.Text = _traverzaDefaults.BokYStart.ToString("0.###", CultureInfo.InvariantCulture);
            if (_traverzaDefaults.PatternAlongX)
                RadioTraverzaLavPrav.IsChecked = true;
            else
                RadioTraverzaPredZad.IsChecked = true;
        }
    }

    /// <summary>
    /// UI pre hrany: zobrazíme len 3 polia z novej šablóny „Kolík do hrany"
    /// a relabelujeme ich na pekné názvy. Zvyšok skryjeme – ich hodnoty
    /// dopočítame programovo (numberOfRows=1, rowsDistance=0, yStart=0, refpos=0).
    /// </summary>
    private void ConfigureForEdge()
    {
        PatternPickerForTop.Visibility = Visibility.Collapsed;
        PatternFixedLabel.Visibility = Visibility.Visible;
        PatternFixedLabel.Text = $"Šablóna: {TemplateKolikDoHrany}";

        // Pattern: skry numberOfRows + rowsDistance (sú vždy 1, 0).
        SetRowVisible(LblNumberOfRows, NumberOfRowsBox, null, Visibility.Collapsed);
        SetRowVisible(LblRowsDistance, RowsDistanceBox, UnitRowsDistance, Visibility.Collapsed);

        // Relabel zostávajúcich pattern polí na „pekné" názvy.
        LblNumberOfColumns.Text = "Počet kolíkov";
        LblColumnDistance.Text = "Rozteč kolíkov";

        // Position: skry Refpos a YStart, relabeluj XStart.
        SetRowVisible(LblRefpos, RefposBox, null, Visibility.Collapsed);
        SetRowVisible(LblYStart, YStartBox, UnitYStart, Visibility.Collapsed);
        LblXStart.Text = "Od prednej hrany";

        // Skryté boxy musia obsahovať platné defaulty – TryBuild ich číta.
        NumberOfRowsBox.Text = "1";
        RowsDistanceBox.Text = "0";
        RefposBox.Text = "0";
        // Pre hrany sa kolíky vŕtajú do stredu hrúbky = Dz / 2.
        YStartBox.Text = DefaultEdgeY.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// UI pre Top: pôvodné správanie – všetky polia viditeľné, raw XCS labely,
    /// radio picker Plocha po X / Plocha po Y.
    /// </summary>
    private void ConfigureForTop()
    {
        PatternPickerForTop.Visibility = Visibility.Visible;
        PatternFixedLabel.Visibility = Visibility.Collapsed;
        RadioPlochaX.IsChecked = true;

        SetRowVisible(LblNumberOfRows, NumberOfRowsBox, null, Visibility.Visible);
        SetRowVisible(LblRowsDistance, RowsDistanceBox, UnitRowsDistance, Visibility.Visible);
        SetRowVisible(LblRefpos, RefposBox, null, Visibility.Visible);
        SetRowVisible(LblYStart, YStartBox, UnitYStart, Visibility.Visible);

        LblNumberOfRows.Text = "numberOfRows";
        LblNumberOfColumns.Text = "numberOfColumns";
        LblRowsDistance.Text = "rowsDistance";
        LblColumnDistance.Text = "columnDistance";
        LblXStart.Text = "Prvý kolík X";
        LblYStart.Text = "Prvý kolík Y";
    }

    /// <summary>Zviditeľní/skryje celý riadok v Grid-e (label + value + optional unit).</summary>
    private static void SetRowVisible(UIElement label, UIElement value, UIElement? unit, Visibility v)
    {
        label.Visibility = v;
        value.Visibility = v;
        if (unit != null) unit.Visibility = v;
    }

    private void OnPatternRadio_Checked(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        if (!IsLoaded || _isEdge || _populating) return;

        _currentPatternTpl = RadioPlochaY.IsChecked == true
            ? TemplatePlochaPoY
            : TemplatePlochaPoX;

        // Pri úprave nenačítavaj predvolby zo šablóny – používateľ mení len typ patternu.
        if (!_isEditMode)
            LoadPatternIntoFields(_currentPatternTpl);
        UpdateTemplatePreview();
        RefreshPartPreview();
    }

    /// <summary>
    /// Predvyplní polia patternu zo store. Z <see cref="TemplateStore"/> sa
    /// čítajú iba editovateľné parametre danej šablóny – fixné sú konštanty.
    /// </summary>
    private void LoadPatternIntoFields(string patternTpl)
    {
        var bag = _store.GetAll(patternTpl);

        switch (patternTpl)
        {
            case TemplateKolikDoHrany:
                // Nová šablóna pre hrany – mapovanie:
                //   x              → XStartBox        (Prvý kolík)
                //   numOfColumns   → NumberOfColumnsBox (Počet kolíkov)
                //   columnsDistance → ColumnDistanceBox  (Rozteč kolíkov)
                XStartBox.Text = ReadEditable(bag, "x", "37");
                NumberOfColumnsBox.Text = ReadEditable(bag, "numOfColumns", "2");
                ColumnDistanceBox.Text = ReadEditable(bag, "columnsDistance", "32");
                // Skryté pevné hodnoty: 1 rad, Y = Dz/2 (stred hrúbky dielca).
                NumberOfRowsBox.Text = "1";
                RowsDistanceBox.Text = "0";
                YStartBox.Text = DefaultEdgeY.ToString("0.###", CultureInfo.InvariantCulture);
                break;

            case TemplatePlochaPoX:
                NumberOfRowsBox.Text = "1";
                NumberOfColumnsBox.Text = ReadEditable(bag, "numberOfColumns", "3");
                RowsDistanceBox.Text = "0";
                ColumnDistanceBox.Text = ReadEditable(bag, "columnDistance", "32");
                break;

            case TemplatePlochaPoY:
                NumberOfRowsBox.Text = ReadEditable(bag, "numberOfRows", "3");
                NumberOfColumnsBox.Text = "1";
                RowsDistanceBox.Text = ReadEditable(bag, "rowsDistance", "32");
                ColumnDistanceBox.Text = "0";
                break;

            default:
                // bezpečnostná vetva – nemali by sme tu pristáť.
                break;
        }
    }

    private void UpdateTemplatePreview()
    {
        if (IsTraverzaMode)
        {
            TemplatePreviewLabel.Text =
                $"Šablóna: {TraverzaKolikyApplier.SettingsTemplate}\n" +
                $"  hĺbka hrany = {_traverzaDefaults.DepthEdge} mm\n" +
                $"  nástroj     = {_traverzaDefaults.Tool}\n" +
                $"  predvolené X/Y na bokoch zo Nastavenia operácií";
            return;
        }

        if (_isEdge)
        {
            // Nová šablóna: proces parametre sú automaticky odvodené,
            // operátor ich tu nemení.
            TemplatePreviewLabel.Text =
                $"Šablóna:  {TemplateKolikDoHrany}\n" +
                $"  plocha   = {_face.ToWorkplane()}  (automaticky)\n" +
                $"  hĺbka    = 23 mm                 (predvolené pre hrany)\n" +
                $"  nástroj  = E071                  (predvolený)\n" +
                $"  refpos   = 0                     (predvolený)";
            return;
        }

        var pbag = _store.GetAll(_processTpl);
        var depth = ReadEditable(pbag, "depth", "13");
        var tool = ReadEditable(pbag, "tool", "E071");
        var dStep = ReadEditable(pbag, "dischargeSteps", "3");
        var rotSpd = ReadEditable(pbag, "rotSpeed", "-1");
        var borSpd = ReadEditable(pbag, "boringSpeed", "-1");
        var taper = ReadEditable(pbag, "taperHeight", "0");

        TemplatePreviewLabel.Text =
            $"Proces:  {_processTpl}\n" +
            $"  depth          = {depth} mm\n" +
            $"  tool           = {tool}\n" +
            $"  dischargeSteps = {dStep}\n" +
            $"  rotSpeed       = {rotSpd}\n" +
            $"  boringSpeed    = {borSpd}\n" +
            $"  taperHeight    = {taper}\n" +
            $"\nPattern: {_currentPatternTpl}";
    }

    private void OnOk_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        if (IsTraverzaMode)
        {
            if (!TryBuildTraverza(out var treq, out var errT))
            {
                MessageBox.Show(this, errT, "Neplatný vstup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            TraverzaRequest = treq;
            DialogResult = true;
            Close();
            return;
        }

        if (!TryBuild(out var op, out var err))
        {
            MessageBox.Show(this, err, "Neplatný vstup",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = op;
        DialogResult = true;
        Close();
    }

    private bool TryBuildTraverza(out TraverzaKolikyApplier.Request req, out string error)
    {
        req = null!;
        error = string.Empty;

        if (!int.TryParse(NumberOfColumnsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pocet) || pocet < 1)
        { error = "Počet kolíkov musí byť aspoň 1."; return false; }
        if (!TryNumber(ColumnDistanceBox.Text, out var roztec, out error)) { error = "Rozteč: " + error; return false; }
        if (!TryNumber(XStartBox.Text, out var prvy, out error)) { error = "Prvý kolík na traverze: " + error; return false; }
        if (!TryNumber(TraverzaBokXBox.Text, out var bokX, out error)) { error = "Prvý kolík X na boku: " + error; return false; }
        if (!TryNumber(TraverzaBokYBox.Text, out var bokY, out error)) { error = "Prvý kolík Y na boku: " + error; return false; }

        req = _traverzaDefaults with
        {
            Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Kolik_traverza" : NameBox.Text.Trim(),
            Pocet = pocet,
            Roztec = roztec,
            PrvyKolikTraverza = prvy,
            BokXStart = bokX,
            BokYStart = bokY,
            PatternAlongX = RadioTraverzaLavPrav.IsChecked == true,
        };
        return true;
    }

    private void OnCancel_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        DialogResult = false;
        Close();
    }

    private bool TryBuild(out DrillOperation op, out string error)
    {
        if (_editDrill != null)
        {
            if (!TryBuildInto(_editDrill, out error))
            {
                op = null!;
                return false;
            }

            op = _editDrill;
            return true;
        }

        op = new DrillOperation { Face = _face };
        return TryBuildInto(op, out error);
    }

    private bool TryBuildInto(DrillOperation op, out string error)
    {
        error = string.Empty;

        if (!int.TryParse(RefposBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var refpos)
            || refpos < 0 || refpos > 3)
        {
            error = "Refpos musí byť celé číslo 0, 1, 2 alebo 3.";
            return false;
        }

        if (!int.TryParse(NumberOfRowsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nRows) || nRows < 1)
        { error = _isEdge ? "Vnútorná chyba: numberOfRows." : "numberOfRows musí byť aspoň 1."; return false; }
        if (!int.TryParse(NumberOfColumnsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nCols) || nCols < 1)
        { error = _isEdge ? "Počet kolíkov musí byť aspoň 1." : "numberOfColumns musí byť aspoň 1."; return false; }

        if (!TryNumber(RowsDistanceBox.Text, out var rDist, out error)) { error = "rowsDistance: " + error; return false; }
        if (!TryNumber(ColumnDistanceBox.Text, out var cDist, out error))
        { error = (_isEdge ? "Rozteč kolíkov: " : "columnDistance: ") + error; return false; }
        if (!TryNumber(XStartBox.Text, out var xStart, out error))
        { error = (_isEdge ? "Prvý kolík (poloha): " : "Prvý kolík X: ") + error; return false; }
        if (!TryNumber(YStartBox.Text, out var yStart, out error)) { error = "Prvý kolík Y: " + error; return false; }

        if (_isEdge && _part != null)
            yStart = DefaultEdgeY;

        if (_isEdge && _edgePatternAlongCountY)
        {
            nRows = nCols;
            nCols = 1;
            rDist = cDist;
            cDist = _editDrill?.PitchX ?? 32;
        }

        double depth;
        string tool;
        int dStep;
        string? description;

        if (_editDrill != null)
        {
            depth = _editDrill.Depth;
            tool = _editDrill.Tool;
            dStep = _editDrill.DischargerStep;
            description = _editDrill.Description;
        }
        else if (_isEdge)
        {
            depth = 23.0;
            tool = "E071";
            dStep = 3;
            description = null;
        }
        else
        {
            var pbag = _store.GetAll(_processTpl);
            depth = TryGetDouble(pbag, "depth", 13.0);
            tool = ReadEditable(pbag, "tool", "E071");
            dStep = (int)TryGetDouble(pbag, "dischargeSteps", 3);
            description = pbag.TryGetValue("description", out var desc) && !string.IsNullOrWhiteSpace(desc) ? desc : null;
        }

        ApplyBuiltValues(op, refpos, nRows, nCols, rDist, cDist, xStart, yStart, depth, tool, dStep, description);
        return true;
    }

    private void ApplyBuiltValues(
        DrillOperation op,
        int refpos, int nRows, int nCols,
        double rDist, double cDist, double xStart, double yStart,
        double depth, string tool, int dStep, string? description)
    {
        op.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? $"Drill_{_face}" : NameBox.Text.Trim();
        op.Face = _face;
        op.RefPos = refpos;
        op.CountX = nCols;
        op.CountY = nRows;
        op.PitchX = cDist;
        op.PitchY = rDist;
        op.XStart = xStart;
        op.YStart = yStart;
        op.Depth = depth;
        op.Diameter = 8.0;
        op.Tool = tool;
        op.DischargerStep = dStep;
        op.Description = description;
        op.TemplateLabel = _isEdge ? TemplateKolikDoHrany : $"{_processTpl} · {_currentPatternTpl}";
        op.PreniestNaDruhyBok = !ShowOppositeBokOption || RadioPreniestNaBok.IsChecked == true;
    }

    /// <summary>
    /// Bezpečné čítanie editovateľnej hodnoty zo store: ak chýba alebo je
    /// prázdna, vráti zadaný fallback. Pre fixné parametre šablón sa táto
    /// metóda nikdy nepoužíva (sú zadrôtované v kóde podľa definície).
    /// </summary>
    private static string ReadEditable(IReadOnlyDictionary<string, string> bag, string key, string fallback)
    {
        if (bag.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            return v.Trim();
        return fallback;
    }

    private static bool TryNumber(string s, out double v, out string err)
    {
        err = string.Empty;
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            return true;
        err = "očakávané číslo (mm).";
        return false;
    }

    private static double TryGetDouble(IReadOnlyDictionary<string, string> bag, string key, double fallback)
    {
        if (bag.TryGetValue(key, out var s) &&
            double.TryParse((s ?? string.Empty).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;
        return fallback;
    }
    private void WirePreviewInputs()
    {
        void Hook(TextBox box) => box.TextChanged += (_, _) => RefreshPartPreview();
        Hook(NameBox);
        Hook(RefposBox);
        Hook(NumberOfRowsBox);
        Hook(NumberOfColumnsBox);
        Hook(RowsDistanceBox);
        Hook(ColumnDistanceBox);
        Hook(XStartBox);
        Hook(YStartBox);
        Hook(TraverzaBokXBox);
        Hook(TraverzaBokYBox);
        RadioTraverzaLavPrav.Checked += (_, _) => RefreshPartPreview();
        RadioTraverzaPredZad.Checked += (_, _) => RefreshPartPreview();
        RadioPreniestNaBok.Checked += (_, _) => RefreshPartPreview();
    }

    private void RefreshPartPreview()
    {
        if (_suppressPreviewRefresh || _part == null)
            return;

        for (var i = PreviewViewport.Children.Count - 1; i >= 0; i--)
        {
            if (PreviewViewport.Children[i] is DefaultLights)
                continue;
            PreviewViewport.Children.RemoveAt(i);
        }

        var builder = new Scene3DBuilder
        {
            ShowAxisLabels = false,
            ShowRefposLabels = true,
            ShowFrontEdgeMarker = true,
            HideDrillForPreview = _editDrill,
        };

        if (TryBuildPreview(out var overlay))
            builder.PreviewDrillOverlay = overlay;

        foreach (var visual in builder.Build(_part, PartFace.Top))
            PreviewViewport.Children.Add(visual);

        SetPreviewTopView();
    }

    private void SetPreviewTopView()
    {
        var dx = Math.Max(1, _part?.Dx ?? 1);
        var dy = Math.Max(1, _part?.Dy ?? 1);
        var dist = Math.Max(dx, dy) * 1.6;
        PreviewViewport.Camera.Position = new Point3D(dx / 2.0, dy / 2.0, dist);
        PreviewViewport.Camera.LookDirection = new Vector3D(0, 0, -dist);
        PreviewViewport.Camera.UpDirection = new Vector3D(0, 1, 0);
    }

    private bool TryBuildPreview(out DrillOperation? preview)
    {
        preview = null;
        if (_part == null || IsTraverzaMode)
            return false;

        var draft = new DrillOperation { Face = _face };
        return TryBuildInto(draft, out _) && (preview = draft) != null;
    }

}




