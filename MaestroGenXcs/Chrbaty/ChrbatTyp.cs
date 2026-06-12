namespace MaestroGenXcs.Chrbaty;

/// <summary>Konstrukčný typ chrbátu skrinky – určuje vzorce pre <see cref="ChrbatRozmeryResolver"/>.</summary>
public enum ChrbatTyp
{
    Nezadany = 0,

    /// <summary>Bod 1 – naložený, vonkajší obrys skrinky (plná výška).</summary>
    Nalozeny = 1,

    /// <summary>Bod 2 – vložený, vnútorný rozmer (plná výška).</summary>
    Vlozeny = 2,

    /// <summary>Bod 3a – drážka len v bokoch; <c>bok.Dy &gt; dno.Dy</c>.</summary>
    DrazkaLenBoky = 3,

    /// <summary>Bod 3b – drážka aj v dne/vrchu; V/S z bodu 1 − 12 mm.</summary>
    DrazkaDnoVrch = 4,

    /// <summary>Bod 3c – drážka len v dne/vrchu (zriedkavé).</summary>
    DrazkaLenDnoVrch = 5,

    /// <summary>Nie po celej výške – kontrola len <see cref="ChrbatOcekavaneRozmery.Sirka"/>; Vyska z dielca.</summary>
    NepoCelejVyske = 6,

    /// <summary>Nie po celej výške + drážka v bokoch; parameter <see cref="ChrbatZostavaNastavenia.DlzkaDrazkyMm"/> (≠ Vyska).</summary>
    NepoCelejVyskeDrazkaVBokoch = 7,
}
