using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Chrbaty;

/// <summary>
/// Vzorce pre <c>chrbat.Vyska</c> a <c>chrbat.Sirka</c> podľa typu chrbátu a režimu korpusu.
/// Zdroje: <c>bok.Dx</c>, <c>dno.Dx</c>, <c>dno.Dz</c>, <c>bokL.Dz</c>, <c>bokP.Dz</c>, <c>vrch.Dz</c>.
/// </summary>
public static class ChrbatRozmeryResolver
{
    public static ChrbatOcekavaneRozmery? Resolve(
        ChrbatTyp typ,
        AssemblyCorpusMode corpusMode,
        ChrbatKorpusDielce korpus)
    {
        return typ switch
        {
            ChrbatTyp.Nalozeny => ResolveNalozeny(corpusMode, korpus),
            ChrbatTyp.Vlozeny => ResolveVlozeny(corpusMode, korpus),
            ChrbatTyp.DrazkaLenBoky => ResolveDrazkaLenBoky(corpusMode, korpus),
            ChrbatTyp.DrazkaDnoVrch => ResolveDrazkaDnoVrch(corpusMode, korpus),
            ChrbatTyp.DrazkaLenDnoVrch => ResolveDrazkaLenDnoVrch(corpusMode, korpus),
            ChrbatTyp.NepoCelejVyske => ResolveNepoCelejVyskeSirka(corpusMode, korpus),
            ChrbatTyp.NepoCelejVyskeDrazkaVBokoch => ResolveNepoCelejVyskeSirka(corpusMode, korpus),
            _ => null,
        };
    }

    /// <summary>Bod 1 – vonkajší obrys.</summary>
    public static ChrbatOcekavaneRozmery ResolveNalozeny(
        AssemblyCorpusMode corpusMode,
        ChrbatKorpusDielce k) =>
        corpusMode == AssemblyCorpusMode.BokNalozeny
            ? new ChrbatOcekavaneRozmery(k.BokDx, k.DnoDx + k.BokLDz + k.BokPDz)
            : new ChrbatOcekavaneRozmery(k.DnoDz + k.BokDx + k.VrchDz, k.DnoDx);

    /// <summary>Bod 2 – vnútorný rozmer.</summary>
    public static ChrbatOcekavaneRozmery ResolveVlozeny(
        AssemblyCorpusMode corpusMode,
        ChrbatKorpusDielce k) =>
        corpusMode == AssemblyCorpusMode.BokNalozeny
            ? new ChrbatOcekavaneRozmery(k.BokDx - k.DnoDz - k.VrchDz, k.DnoDx)
            : new ChrbatOcekavaneRozmery(k.BokDx, k.DnoDx - k.BokLDz - k.BokPDz);

    /// <summary>Bod 3a – V/S ako vonkajšia výška + vnútorná šírka (body 1 + 2).</summary>
    public static ChrbatOcekavaneRozmery ResolveDrazkaLenBoky(
        AssemblyCorpusMode corpusMode,
        ChrbatKorpusDielce k)
    {
        var vonkajsie = ResolveNalozeny(corpusMode, k);
        var vnutorne = ResolveVlozeny(corpusMode, k);
        return new ChrbatOcekavaneRozmery(vonkajsie.Vyska, vnutorne.Sirka);
    }

    /// <summary>Bod 3b – vonkajší obrys − 12 mm na oboch rozmeroch.</summary>
    public static ChrbatOcekavaneRozmery ResolveDrazkaDnoVrch(
        AssemblyCorpusMode corpusMode,
        ChrbatKorpusDielce k)
    {
        var vonkajsie = ResolveNalozeny(corpusMode, k);
        var odp = ChrbatKonstanty.DrazkaObvodOdpocetMm;
        return new ChrbatOcekavaneRozmery(vonkajsie.Vyska - odp, vonkajsie.Sirka - odp);
    }

    /// <summary>Bod 3c – rovnaké V/S ako 3a (zatiaľ); hĺbková kontrola je zrkadlená v validátore.</summary>
    public static ChrbatOcekavaneRozmery ResolveDrazkaLenDnoVrch(
        AssemblyCorpusMode corpusMode,
        ChrbatKorpusDielce k) =>
        ResolveDrazkaLenBoky(corpusMode, k);

    /// <summary>Čiastočná výška – vonkajší obrys − 12 mm; kontroluje sa len Sirka.</summary>
    public static ChrbatOcekavaneRozmery ResolveNepoCelejVyskeSirka(
        AssemblyCorpusMode corpusMode,
        ChrbatKorpusDielce k)
    {
        var sirka = corpusMode == AssemblyCorpusMode.BokNalozeny
            ? k.DnoDx + k.BokLDz + k.BokPDz - ChrbatKonstanty.DrazkaObvodOdpocetMm
            : k.DnoDx - ChrbatKonstanty.DrazkaObvodOdpocetMm;
        return new ChrbatOcekavaneRozmery(0, sirka, KontrolujVysku: false);
    }
}
