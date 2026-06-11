namespace MaestroGenXcs.Sufle;

/// <summary>
/// Väzba parametrov makier <c>SufelBok2_novy</c> a <c>SufelCeloZad2</c> v rámci jednej šufle.
/// </summary>
public static class SufelMacroSynchronizer
{
    /// <summary>Offset PolohaDieryY boku → PolohaDiery čelo/zad.</summary>
    public const double PolohaDieryCeloZadOffsetMm = 2.0;

    public static void SyncAll(SufelSkupina sk)
    {
        SyncPolohaDieryXFromCeloZad(sk);
        SyncCeloZadFromBok(sk);
    }

    /// <summary>PolohaDieryX na boku = hrúbka čela alebo zadu (Dz) ÷ 2.</summary>
    public static void SyncPolohaDieryXFromCeloZad(SufelSkupina sk)
    {
        var dz = ResolveCeloZadThickness(sk);
        if (dz <= 0)
            return;

        var x = dz / 2.0;
        if (Math.Abs(sk.BokMacro.PolohaDieryXmm - x) < 0.001)
            return;

        sk.BokMacro.PolohaDieryXmm = x;
    }

    public static void SyncCeloZadFromBok(SufelSkupina sk)
    {
        var bok = sk.BokMacro;
        var celoZad = sk.CeloZadMacro;

        if (celoZad.PocetDier != bok.PocetDier)
            celoZad.PocetDier = bok.PocetDier;

        if (Math.Abs(celoZad.RoztecDierMm - bok.RoztecDierMm) > 0.001)
            celoZad.RoztecDierMm = bok.RoztecDierMm;

        var poloha = Math.Max(0, bok.PolohaDieryYmm - PolohaDieryCeloZadOffsetMm);
        if (Math.Abs(celoZad.PolohaDieryMm - poloha) > 0.001)
            celoZad.PolohaDieryMm = poloha;
    }

    public static double ResolveCeloZadThickness(SufelSkupina sk) =>
        sk.CeloPart?.Dz ?? sk.ZadPart?.Dz ?? 0;
}
