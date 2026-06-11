namespace MaestroGenXcs.Sufle;

/// <summary>Predvolené hodnoty parametrov makra <c>SufelBok2_novy.xsp</c> – export len zmeny oproti nim.</summary>
public static class SufelBokMacroDefaults
{
    public const double ToleranceMm = 0.001;

    public const double Paska = 0.8;
    public const double Frezpod = 2;
    public const double PolohaDieryX = 9;
    public const double PolohaDieryY = 22;
    public const int PocetDier = 3;
    public const double RoztecDier = 59;
    public const double HlbkaDier = 13;
    public const double HlbkaDrazky = 12;
    public const double Pridavok = -0.5;
    public const double Dx1 = 500;
    public const double Dy1 = 200;
    public const double Dz1 = 18;
    public const double HlbkaZafrezovania = 4;
    public const bool Dno18 = false;

    public static bool Differs(double value, double defaultValue) =>
        Math.Abs(value - defaultValue) > ToleranceMm;
}
