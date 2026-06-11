using CommunityToolkit.Mvvm.ComponentModel;

namespace MaestroGenXcs.Sufle;

/// <summary>
/// Parametre makra Maestro <c>SufelCeloZad2</c> – odvodené z <see cref="SufelBokMacroParams"/>.
/// </summary>
public sealed partial class SufelCeloZadMacroParams : ObservableObject
{
    [ObservableProperty]
    private int _pocetDier = 2;

    [ObservableProperty]
    private double _roztecDierMm = 32;

    /// <summary>PolohaDiery v makre čelo/zad = PolohaDieryY boku − 2 mm.</summary>
    [ObservableProperty]
    private double _polohaDieryMm;

    [ObservableProperty]
    private double _lDieraNaSkrXmm = SufelCeloZadMacroDefaults.LDieraNaSkrX;

    [ObservableProperty]
    private double _pDieraNaSkrXmm = SufelCeloZadMacroDefaults.PDieraNaSkrX;

    [ObservableProperty]
    private double _dieryNaSkrutkyYmm = SufelCeloZadMacroDefaults.DieryNaSkrutkyY;

    [ObservableProperty]
    private double _hlbkaDierNaSkrutkyMm = SufelCeloZadMacroDefaults.HlbkaDierNaSkrutky;
}
