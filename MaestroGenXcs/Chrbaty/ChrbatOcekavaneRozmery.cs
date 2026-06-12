namespace MaestroGenXcs.Chrbaty;

/// <summary>Očakávaná <c>chrbat.Vyska</c> a <c>chrbat.Sirka</c>.</summary>
/// <param name="KontrolujVysku">False pri <see cref="ChrbatTyp.NepoCelejVyske"/> – výška ide z rozmeru dielca.</param>
public sealed record ChrbatOcekavaneRozmery(double Vyska, double Sirka, bool KontrolujVysku = true);
