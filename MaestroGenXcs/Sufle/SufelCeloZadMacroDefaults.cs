namespace MaestroGenXcs.Sufle;

/// <summary>Predvolené hodnoty makra <c>SufelCeloZad2.xsp</c>.</summary>
public static class SufelCeloZadMacroDefaults
{
    public const double ToleranceMm = 0.001;

    public const double PolohaDiery = 20;
    public const int PocetDier = 2;
    public const double RoztecDier = 59;
    public const double LDieraNaSkrX = 52;
    public const double PDieraNaSkrX = 52;
    public const double DieryNaSkrutkyY = 30;
    public const double HlbkaDierNaSkrutky = 19;
    public const bool CeloZad = false;

    public static bool Differs(double value, double defaultValue) =>
        Math.Abs(value - defaultValue) > ToleranceMm;
}
