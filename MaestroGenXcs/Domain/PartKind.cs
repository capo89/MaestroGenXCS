namespace MaestroGenXcs.Domain;

/// <summary>
/// Hint pre typ dielca. Slúži iba ako pomôcka pre auto-návrh spojov a default
/// hodnoty operácií. Logika vŕtania a generovania XCS sa naňho nespolieha.
/// </summary>
public enum PartKind
{
    Generic = 0,
    BokL,
    BokP,
    Dno,
    Vrch,
    Polica,
    Priecka,
    Traverza,
    Chrbat,
    SufelBok,
    SufelCelo,
    SufelZad,
    Kovanie
}
