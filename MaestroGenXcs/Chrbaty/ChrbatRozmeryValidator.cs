using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Chrbaty;

/// <summary>Kontrola rozmerov chrbátu v zostave – V/S bez ohľadu na prehodené osi, hĺbka pri type 3.</summary>
public static class ChrbatRozmeryValidator
{
    public static void ValidateZostava(
        string zostava,
        AssemblyCorpusMode corpusMode,
        ChrbatZostavaNastavenia nastavenia,
        IReadOnlyList<Part> zostavaParts,
        ICollection<string> warnings)
    {
        if (nastavenia.Typ == ChrbatTyp.Nezadany)
            return;

        if (!ChrbatKorpusDielce.TryFromZostavaParts(zostavaParts, out var korpus))
        {
            warnings.Add($"Zostava „{zostava}“ – chrbát: chýba bok, dno alebo vrch pre kontrolu rozmerov.");
            return;
        }

        var ocekavane = ChrbatRozmeryResolver.Resolve(nastavenia.Typ, corpusMode, korpus);
        if (ocekavane == null)
            return;

        var chrbaty = zostavaParts.Where(p => p.Kind == PartKind.Chrbat).ToList();
        if (chrbaty.Count == 0)
            return;

        ValidateJednotnaHrubka(zostava, chrbaty, warnings);

        foreach (var chrbat in chrbaty)
            ValidateChrbat(zostava, chrbat, nastavenia, korpus, ocekavane, warnings);
    }

    private static void ValidateJednotnaHrubka(string zostava, IReadOnlyList<Part> chrbaty, ICollection<string> warnings)
    {
        if (chrbaty.Count < 2)
            return;

        var hrubky = chrbaty
            .Select(p => ChrbatPanelRozmery.FromPart(p).Hrubka)
            .Distinct()
            .ToList();

        if (hrubky.Count > 1)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – chrbát: rôzna hrúbka v zostave ({string.Join(", ", hrubky.Select(h => $"{h:0.##} mm"))}).");
        }
    }

    private static void ValidateChrbat(
        string zostava,
        Part chrbat,
        ChrbatZostavaNastavenia nastavenia,
        ChrbatKorpusDielce korpus,
        ChrbatOcekavaneRozmery ocekavane,
        ICollection<string> warnings)
    {
        var tol = ChrbatKonstanty.ToleranceMm;
        var panel = ChrbatPanelRozmery.FromPart(chrbat);

        if (ocekavane.KontrolujVysku)
        {
            if (!panel.MatchesVyskaSirka(ocekavane.Vyska, ocekavane.Sirka, tol))
            {
                warnings.Add(
                    $"Zostava „{zostava}“ – {chrbat.Name}: chrbát {panel.RozmerA:0.##}×{panel.RozmerB:0.##} mm, " +
                    $"očakávané Vyska={ocekavane.Vyska:0.##}, Sirka={ocekavane.Sirka:0.##} mm ({nastavenia.Typ}).");
            }
        }
        else if (!panel.MatchesSirka(ocekavane.Sirka, tol))
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {chrbat.Name}: chrbát {panel.RozmerA:0.##}×{panel.RozmerB:0.##} mm, " +
                $"očakávaná Sirka={ocekavane.Sirka:0.##} mm ({nastavenia.Typ}).");
        }

        if (nastavenia.Typ == ChrbatTyp.NepoCelejVyskeDrazkaVBokoch)
            ValidateDlzkaDrazky(zostava, chrbat.Name, nastavenia, panel, ocekavane.Sirka, tol, warnings);

        ValidateHlbka(zostava, chrbat, nastavenia, korpus, panel.Hrubka, warnings);
    }

    private static void ValidateDlzkaDrazky(
        string zostava,
        string chrbatName,
        ChrbatZostavaNastavenia nastavenia,
        ChrbatPanelRozmery panel,
        double ocekSirka,
        double toleranceMm,
        ICollection<string> warnings)
    {
        if (nastavenia.DlzkaDrazkyMm <= 0)
            return;

        var vyskaZDielca = panel.ResolveVyskaZDielca(ocekSirka, toleranceMm);
        if (vyskaZDielca is null)
            return;

        if (Math.Abs(nastavenia.DlzkaDrazkyMm - vyskaZDielca.Value) <= toleranceMm)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {chrbatName}: DlzkaDrazky ({nastavenia.DlzkaDrazkyMm:0.##} mm) " +
                "by nemala byť rovná Vyske chrbátu z dielca – prídavok drážky nie je započítaný.");
        }
    }

    private static void ValidateHlbka(
        string zostava,
        Part chrbat,
        ChrbatZostavaNastavenia nastavenia,
        ChrbatKorpusDielce korpus,
        double chrbatHrubka,
        ICollection<string> warnings)
    {
        var tol = ChrbatKonstanty.ToleranceMm;
        var ods = nastavenia.OdsadenieOdZadnejHranyMm;

        switch (nastavenia.Typ)
        {
            case ChrbatTyp.DrazkaLenBoky:
                ValidateDnoVrchHlbkaKratsie(
                    zostava, chrbat.Name, korpus.BokDy, korpus.DnoDy, korpus.VrchDy,
                    ods, chrbatHrubka, tol, warnings);
                break;

            case ChrbatTyp.DrazkaDnoVrch:
            case ChrbatTyp.NepoCelejVyskeDrazkaVBokoch:
                break;

            case ChrbatTyp.DrazkaLenDnoVrch:
                ValidateBokHlbkaKratsi(
                    zostava, chrbat.Name, korpus.BokDy, korpus.DnoDy,
                    ods, chrbatHrubka, tol, warnings);
                break;
        }
    }

    private static void ValidateDnoVrchHlbkaKratsie(
        string zostava,
        string chrbatName,
        double bokDy,
        double dnoDy,
        double vrchDy,
        double odsadenieMm,
        double chrbatHrubka,
        double toleranceMm,
        ICollection<string> warnings)
    {
        if (bokDy <= dnoDy + toleranceMm)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {chrbatName}: pri 3a musí platiť bok.Dy ({bokDy:0.##}) > dno.Dy ({dnoDy:0.##}).");
        }

        if (bokDy <= vrchDy + toleranceMm)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {chrbatName}: pri 3a musí platiť bok.Dy ({bokDy:0.##}) > vrch.Dy ({vrchDy:0.##}).");
        }

        if (odsadenieMm > 0 || chrbatHrubka > 0)
        {
            var ocekDno = bokDy - odsadenieMm - chrbatHrubka;
            if (Math.Abs(dnoDy - ocekDno) > toleranceMm)
            {
                warnings.Add(
                    $"Zostava „{zostava}“ – {chrbatName}: dno.Dy={dnoDy:0.##}, očakávané {ocekDno:0.##} " +
                    $"(bok.Dy − odsadenie − hrúbka chrbátu).");
            }
        }
    }

    private static void ValidateBokHlbkaKratsi(
        string zostava,
        string chrbatName,
        double bokDy,
        double dnoDy,
        double odsadenieMm,
        double chrbatHrubka,
        double toleranceMm,
        ICollection<string> warnings)
    {
        if (bokDy >= dnoDy - toleranceMm)
        {
            warnings.Add(
                $"Zostava „{zostava}“ – {chrbatName}: pri 3c musí platiť bok.Dy ({bokDy:0.##}) < dno.Dy ({dnoDy:0.##}).");
        }

        if (odsadenieMm > 0 || chrbatHrubka > 0)
        {
            var ocekBok = dnoDy - odsadenieMm - chrbatHrubka;
            if (Math.Abs(bokDy - ocekBok) > toleranceMm)
            {
                warnings.Add(
                    $"Zostava „{zostava}“ – {chrbatName}: bok.Dy={bokDy:0.##}, očakávané {ocekBok:0.##} " +
                    $"(dno.Dy − odsadenie − hrúbka chrbátu).");
            }
        }
    }
}
