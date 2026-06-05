using System.Windows;
using System.Windows.Controls;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Kovania;
using MaestroGenXcs.Sufle;
using MaestroGenXcs.ViewModels;

namespace MaestroGenXcs.Views;

public partial class MoventoSettingsDialog : Window
{
    private readonly AssemblyViewModel _vm;
    private readonly AssemblyContext _ctx;

    public IReadOnlyList<MoventoDlzkaKovaniaChoice> DlzkaChoices { get; } = MoventoDlzkaKovaniaChoice.Vsetky;

    public MoventoSettingsDialog(AssemblyViewModel vm, string zostava)
    {
        _vm = vm;
        _ctx = vm.GetAssemblyContext(zostava)
            ?? throw new InvalidOperationException($"Zostava „{zostava}“ nemá kontext skladania.");

        InitializeComponent();
        ZostavaLabel.Text = $"Zostava: {zostava}";
        SekcieList.ItemsSource = _ctx.MoventoSekcie;
        DataContext = this;
    }

    private void OnAddSekcia_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        var index = _ctx.MoventoSekcie.Count + 1;
        _ctx.MoventoSekcie.Add(Sufel_Movento.CreateDefaultSekcia(index));
    }

    private void OnRemoveSekcia_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SufelMoventoSekcia sekcia })
            return;
        _ctx.MoventoSekcie.Remove(sekcia);
        for (var i = 0; i < _ctx.MoventoSekcie.Count; i++)
            _ctx.MoventoSekcie[i].Nazov = $"Šufľa {i + 1}";
    }

    private void OnOk_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        DialogResult = true;
        Close();
    }
}
