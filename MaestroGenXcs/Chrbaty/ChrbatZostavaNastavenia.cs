using CommunityToolkit.Mvvm.ComponentModel;

namespace MaestroGenXcs.Chrbaty;

/// <summary>Nastavenia chrbátu v jednej zostave (typ, odsadenie – UI neskôr pre typ 2 a 3).</summary>
public sealed partial class ChrbatZostavaNastavenia : ObservableObject
{
    [ObservableProperty]
    private ChrbatTyp _typ = ChrbatTyp.Nezadany;

    /// <summary>Odsadenie chrbátu od zadnej hrany boku (mm) – typ 2, 3a–3c.</summary>
    [ObservableProperty]
    private double _odsadenieOdZadnejHranyMm;

    /// <summary>Dĺžka drážky v bokoch (mm) – typ 7; nie je to <c>chrbat.Vyska</c>.</summary>
    [ObservableProperty]
    private double _dlzkaDrazkyMm;
}
