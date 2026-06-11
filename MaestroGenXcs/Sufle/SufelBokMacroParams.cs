using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Sufle;

/// <summary>
/// Parametre makra Maestro <c>SufelBok2_novy</c> – viazané na jednu šufľu (nie na každý kus boku).
/// </summary>
public sealed partial class SufelBokMacroParams : ObservableObject
{
    [ObservableProperty]
    private double _paskaMm;

    [ObservableProperty]
    private double _polohaDieryXmm;

    [ObservableProperty]
    private double _polohaDieryYmm;

    [ObservableProperty]
    private int _pocetDier = 2;

    [ObservableProperty]
    private double _roztecDierMm = 32;

    [ObservableProperty]
    private double _hlbkaDierMm = 13;

    [ObservableProperty]
    private double _hlbkaDrazkyMm;

    [ObservableProperty]
    private double _pridavokMm;

    [ObservableProperty]
    private double _hlbkaZafrezovaniaMm;

    [ObservableProperty]
    private double _frezPodMm;

    /// <summary>Vypočítané z hrúbky dna šufľa – nie ručný vstup v UI.</summary>
    [ObservableProperty]
    private bool _dno18;

    public const double Dno18HrubaMm = 18.0;
    public const double Dno18ToleranceMm = 0.5;

    /// <summary>Hrúbka dosky dna – najmenší rozmer (po importe nemusí byť v <see cref="Part.Dz"/>).</summary>
    public static double ResolveDnoThicknessMm(Part part) =>
        Math.Min(part.Dx, Math.Min(part.Dy, part.Dz));

    public static bool IsDnoThickness18Mm(double thicknessMm) =>
        thicknessMm > 0 && Math.Abs(thicknessMm - Dno18HrubaMm) <= Dno18ToleranceMm;

    public static bool IsDnoThickness18Mm(Part? dnoPart) =>
        dnoPart != null && IsDnoThickness18Mm(ResolveDnoThicknessMm(dnoPart));

    /// <summary>Nastaví <see cref="Dno18"/> podľa hrúbky dna šufľa.</summary>
    public void SyncDno18FromDno(Part? dnoPart)
    {
        var is18 = IsDnoThickness18Mm(dnoPart);
        if (Dno18 != is18)
            Dno18 = is18;
    }
}
