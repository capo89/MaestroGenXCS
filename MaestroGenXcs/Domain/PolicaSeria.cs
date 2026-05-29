using CommunityToolkit.Mvvm.ComponentModel;

namespace MaestroGenXcs.Domain;

/// <summary>Jedna séria kolíkov pevnej police (1 séria ≈ 1 kus pri viacerých ks).</summary>
public sealed partial class PolicaSeria : ObservableObject
{
    [ObservableProperty]
    private string _nazov = "Séria 1";

    /// <summary>Výška dna police od vnútorného spodku skrinky (mm) – musí sedieť s vŕtaním do boku.</summary>
    [ObservableProperty]
    private double _odSpoduMm;

    [ObservableProperty]
    private double _odPrednejHranyMm = PolicaDrillingDefaults.DefaultOdPrednejHranyMm;

    [ObservableProperty]
    private double _roztecMm = PolicaDrillingDefaults.DefaultPevnaRoztecMm;

    [ObservableProperty]
    private int _pocetKolikov = PolicaDrillingDefaults.DefaultPocetKolikov;

    /// <summary>Počet polôh pri polohovateľnej polici (raster 32 mm).</summary>
    [ObservableProperty]
    private int _pocetPoloh = PolicaDrillingDefaults.PodperaHoleCount;
}

/// <summary>Predvolby vŕtania police – zhoda s <c>VzoryXcs/Polica-pevna.txt</c>.</summary>
public static class PolicaDrillingDefaults
{
    public const double BasePitchMm = 32.0;
    public const double DefaultOdPrednejHranyMm = 30.0;
    public const double DefaultPevnaRoztecMm = 128.0;
    public const int DefaultPocetKolikov = 4;
    public const int PodperaHoleCount = 5;
    public const double EdgeDepthMm = 23.0;
    public const double FaceDepthMm = 12.0;
    public const double DiameterMm = 8.0;
    public const string DefaultTool = "E071";
    public const int DischargerStep = 3;
    /// <summary>Označenie súboru vŕtaní v paneli Operácie (nie typ dielca).</summary>
    public const string BundleTemplateLabel = "Spoje";

    public const double ScrewDiameterMm = 3.0;
    public const string ScrewKindOfHole = "L";
    public const int ScrewDefaultCount = 2;
    public const double ScrewDefaultPitchMm = 32.0;
    public const int ScrewDischargerStep = 3;
}
