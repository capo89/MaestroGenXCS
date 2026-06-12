namespace MaestroGenXcs.Chrbaty;

/// <summary>Konštanty pre chrbát v drážke (bod 3b) a tolerancie kontroly rozmerov.</summary>
public static class ChrbatKonstanty
{
    public const double ToleranceMm = 0.5;

    /// <summary>Odpočet od vonkajšieho obrysu pri 3b (−12 mm = 2×6 mm po obvode).</summary>
    public const double DrazkaObvodOdpocetMm = 12.0;

    /// <summary>6 mm na jednu stranu (súčet dvoch strán = <see cref="DrazkaObvodOdpocetMm"/>).</summary>
    public const double DrazkaNaStranuMm = 6.0;
}
