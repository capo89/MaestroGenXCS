using System.Globalization;
using System.Windows;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Services;

namespace MaestroGenXcs.Views;

public partial class AssemblyEdgeKolikyDialog : Window
{
    private const string TemplateKolikDoHrany = "Kolík do hrany";

    private readonly Part _part;
    private readonly PartFace _face;
    private readonly DrillOperation? _editDrill;

    public DrillOperation? Result { get; private set; }

    public AssemblyEdgeKolikyDialog(Part part, PartFace face, DrillOperation? editDrill = null)
    {
        _part = part;
        _face = face;
        _editDrill = editDrill;
        InitializeComponent();

        Title = $"Kolíky – {part.Name}";
        TitleLabel.Text = part.Name;
        FaceLabel.Text = $"Plocha {face.ToWorkplane()}";

        if (_editDrill != null)
        {
            var alongCountY = _editDrill.CountY > 1 && _editDrill.CountX == 1;
            OdPrednejHranyBox.Text = _editDrill.XStart.ToString("0.###", CultureInfo.InvariantCulture);
            PocetBox.Text = (alongCountY ? _editDrill.CountY : _editDrill.CountX).ToString(CultureInfo.InvariantCulture);
            RoztecBox.Text = (alongCountY ? _editDrill.PitchY : _editDrill.PitchX).ToString("0.###", CultureInfo.InvariantCulture);
        }
        else
        {
            var bag = TemplateStore.Instance.GetAll(TemplateKolikDoHrany);
            OdPrednejHranyBox.Text = ReadEditable(bag, "x", "37");
            PocetBox.Text = ReadEditable(bag, "numOfColumns", "2");
            RoztecBox.Text = ReadEditable(bag, "columnsDistance", "32");
        }
    }

    private void OnOk_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        if (!TryBuild(out var op, out var err))
        {
            MessageBox.Show(this, err, "Neplatný vstup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = op;
        DialogResult = true;
        Close();
    }

    private bool TryBuild(out DrillOperation op, out string error)
    {
        op = _editDrill ?? new DrillOperation { Face = _face };
        error = string.Empty;

        if (!int.TryParse(PocetBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pocet) || pocet < 1)
        {
            error = "Počet kolíkov musí byť aspoň 1.";
            return false;
        }

        if (!TryNumber(OdPrednejHranyBox.Text, out var odPrednej, out error))
        {
            error = "Od prednej hrany: " + error;
            return false;
        }

        if (!TryNumber(RoztecBox.Text, out var roztec, out error))
        {
            error = "Rozteč kolíkov: " + error;
            return false;
        }

        var yStart = Math.Round(_part.Dz / 2.0, 3);

        if (_editDrill != null)
        {
            Apply(op, odPrednej, pocet, roztec, yStart);
            return true;
        }

        op = new DrillOperation { Face = _face };
        Apply(op, odPrednej, pocet, roztec, yStart);
        return true;
    }

    private void Apply(DrillOperation op, double odPrednej, int pocet, double roztec, double yStart)
    {
        op.Name = string.IsNullOrWhiteSpace(op.Name) ? $"Kolik_{_face}" : op.Name;
        op.Face = _face;
        op.RefPos = 0;
        op.CountX = pocet;
        op.CountY = 1;
        op.PitchX = roztec;
        op.PitchY = 0;
        op.XStart = odPrednej;
        op.YStart = yStart;
        op.Depth = _editDrill?.Depth ?? 23.0;
        op.Diameter = 8.0;
        op.Tool = _editDrill?.Tool ?? "E071";
        op.DischargerStep = _editDrill?.DischargerStep ?? 3;
        op.Description = _editDrill?.Description;
        op.TemplateLabel = TemplateKolikDoHrany;
    }

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
}
