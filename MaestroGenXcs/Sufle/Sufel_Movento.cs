using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Kovania;

namespace MaestroGenXcs.Sufle;

/// <summary>
/// Šufľa s kovaním Blum Movento – sekcie v zostave a väzba na montážne vŕtanie.
/// </summary>
public static class Sufel_Movento
{
    public const string Nazov = "Šufľa Movento";

    public static string KovanieId => Blum_Movento.Nazov;

    public static IReadOnlyList<MoventoDlzkaKovaniaChoice> DlzkaKovaniaChoices => MoventoDlzkaKovaniaChoice.Vsetky;

    public static SufelMoventoSekcia CreateDefaultSekcia(int index) => new()
    {
        Nazov = $"Šufľa {index}",
        DlzkaKovania = MoventoDlzkaKovania.Kratka250_270,
        VyskaMm = 0,
        OdsadenieOdPreduMm = 0,
        PocetKovani = 1,
    };

    /// <summary>
    /// Montážne patterny pre jeden bok – Top + refpos (L=2, P=0).
    /// Bez násobenia <see cref="SufelMoventoSekcia.PocetKovani"/>.
    /// </summary>
    public static IReadOnlyList<MoventoVrtaciPattern> ResolveVrtaciePatternyPreBok(
        SufelMoventoSekcia sekcia,
        PartKind bok) =>
        Blum_Movento.ResolveVrtaciePatternyPreBok(
            bok,
            sekcia.VyskaMm,
            sekcia.OdsadenieOdPreduMm,
            sekcia.DlzkaKovania);

    /// <summary>Patterny pre Bok L aj Bok P.</summary>
    public static IReadOnlyList<MoventoVrtaciPattern> ResolveVrtaciePatternyObochBokov(SufelMoventoSekcia sekcia) =>
        Blum_Movento.ResolveVrtaciePatternyObochBokov(
            sekcia.VyskaMm,
            sekcia.OdsadenieOdPreduMm,
            sekcia.DlzkaKovania);

    /// <summary>Rozvinuté body z patternov oboch bokov – pre náhľad.</summary>
    public static IReadOnlyList<MoventoVrtaciBod> ResolveVrtacieBody(SufelMoventoSekcia sekcia) =>
        Blum_Movento.ResolveVrtacieBody(
            sekcia.VyskaMm,
            sekcia.OdsadenieOdPreduMm,
            sekcia.DlzkaKovania);
}

/// <summary>
/// Jedna sekcia šufle v zostave – viaže sa na UI polia Výška, Odsadenie od predu, počet kovaní a dĺžku kovania.
/// </summary>
public sealed partial class SufelMoventoSekcia : ObservableObject
{
    [ObservableProperty]
    private string _nazov = "Šufľa 1";

    /// <summary>
    /// Výška sekcie na boku v osi X (mm od spodu dielca).
    /// Nie je to fixná hodnota <see cref="Blum_Movento.HranaOdSpoduMm"/> (38,5 mm).
    /// </summary>
    [ObservableProperty]
    private double _vyskaMm;

    /// <summary>Posun sekcie v smere Y (odsadenie od prednej hrany, mm).</summary>
    [ObservableProperty]
    private double _odsadenieOdPreduMm;

    /// <summary>Koľko kusov kovania Movento patrí k tejto šufli.</summary>
    [ObservableProperty]
    private int _pocetKovani = 1;

    [ObservableProperty]
    private MoventoDlzkaKovania _dlzkaKovania = MoventoDlzkaKovania.Kratka250_270;

    /// <summary>Popis dĺžky kovania pre combo box.</summary>
    public string DlzkaKovaniaLabel => DlzkaKovania switch
    {
        MoventoDlzkaKovania.Kratka250_270 => "250–270",
        MoventoDlzkaKovania.Dlha270_600 => "270–600",
        _ => DlzkaKovania.ToString(),
    };
}
