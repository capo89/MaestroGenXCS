using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;
using MaestroGenXcs.ViewModels;

namespace MaestroGenXcs.Views;

public partial class PartOperationsListWindow : Window
{
    private readonly Part _part;
    private readonly AssemblyViewModel? _vm;
    private readonly ObservableCollection<OperationRow> _rows = new();

    public PartOperationsListWindow(Part part, AssemblyViewModel? vm = null)
    {
        InitializeComponent();
        _part = part;
        _vm = vm;
        PartNameLabel.Text = part.Name;
        PartMetaLabel.Text = $"{part.Zostava} · {part.Kind} · {part.Dx:0.##} × {part.Dy:0.##} × {part.Dz:0.##} mm";
        ReloadRows();
        OperationsList.ItemsSource = _rows;
        UpdateDeleteButton();
    }

    private void ReloadRows()
    {
        foreach (var row in _rows)
            row.PropertyChanged -= OnRowPropertyChanged;

        _rows.Clear();
        foreach (var op in _part.Operations)
        {
            var row = OperationRow.From(op);
            row.PropertyChanged += OnRowPropertyChanged;
            _rows.Add(row);
        }

        CountLabel.Text = _rows.Count == 0
            ? "Žiadne operácie."
            : $"{_rows.Count} operácií";
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationRow.IsSelected))
            UpdateDeleteButton();
    }

    private void UpdateDeleteButton() =>
        DeleteButton.IsEnabled = _rows.Any(r => r.IsSelected);

    private void OnDelete_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        var toRemove = _rows.Where(r => r.IsSelected).Select(r => r.Operation).ToList();
        if (toRemove.Count == 0)
            return;

        if (_vm != null)
            _vm.RemovePartOperations(_part, toRemove);
        else
        {
            foreach (var op in toRemove)
                _part.Operations.Remove(op);
        }

        ReloadRows();
        UpdateDeleteButton();
    }

    private void OnClose_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        Close();
    }

    private sealed partial class OperationRow : ObservableObject
    {
        public required CncOperation Operation { get; init; }

        [ObservableProperty]
        private bool _isSelected;

        public string Name { get; init; } = "";

        public string TypeLabel { get; init; } = "";

        public PartFace Face { get; init; }

        public int RefPos { get; init; }

        public string EnabledText { get; init; } = "";

        public static OperationRow From(CncOperation op) => new()
        {
            Operation = op,
            IsSelected = false,
            Name = string.IsNullOrWhiteSpace(op.Name) ? "(bez názvu)" : op.Name,
            TypeLabel = op.TypeLabel,
            Face = op.Face,
            RefPos = op.RefPos,
            EnabledText = op.IsEnabled ? "zapnutá" : "vypnutá",
        };
    }
}
