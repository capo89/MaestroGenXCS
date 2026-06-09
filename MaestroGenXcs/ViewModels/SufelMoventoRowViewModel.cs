using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.ViewModels;

/// <summary>Riadok tabuľky Movento v 3D paneli – jedna šufľa.</summary>
public sealed partial class SufelMoventoRowViewModel : ObservableObject
{
    private readonly AssemblyViewModel _parent;

    public SufelMoventoRowViewModel(SufelMoventoSekcia sekcia, AssemblyViewModel parent)
    {
        Sekcia = sekcia;
        _parent = parent;
        Sekcia.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SufelMoventoSekcia.VyskaMm)
                or nameof(SufelMoventoSekcia.OdsadenieOdPreduMm)
                or nameof(SufelMoventoSekcia.DlzkaKovania))
                _parent.ScheduleMoventoApply();
        };
    }

    public SufelMoventoSekcia Sekcia { get; }

    public string Nazov => Sekcia.Nazov;
}
