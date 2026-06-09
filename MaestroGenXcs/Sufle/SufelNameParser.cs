using System.Text.RegularExpressions;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Sufle;

/// <summary>Rozparsuje názov dielca šufle z kusovníka (Excel).</summary>
public static class SufelNameParser
{
    public enum SufelRola
    {
        Neznama,
        Bok,
        Celo,
        Zad,
        Dno,
    }

    public readonly record struct Parsed(
        bool JeSufel,
        SufelRola Rola,
        SufelPozicia Pozicia);

    private static readonly Regex SufelTokenPattern = new(
        @"(?:sufel|šufel|sufl[ae]|šufl[ae])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static Parsed Parse(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return new Parsed(false, SufelRola.Neznama, SufelPozicia.Nezadana);

        var normalized = Normalize(rawName);
        if (!SufelTokenPattern.IsMatch(normalized))
            return new Parsed(false, SufelRola.Neznama, SufelPozicia.Nezadana);

        var rola = ResolveRola(normalized);
        var pozicia = ResolvePozicia(normalized);
        return new Parsed(true, rola, pozicia);
    }

    public static PartKind ToPartKind(SufelRola rola) => rola switch
    {
        SufelRola.Bok => PartKind.SufelBok,
        SufelRola.Celo => PartKind.SufelCelo,
        SufelRola.Zad => PartKind.SufelZad,
        SufelRola.Dno => PartKind.SufelDno,
        _ => PartKind.Generic,
    };

    public static bool IsSufelKind(PartKind kind) => kind is
        PartKind.SufelBok or PartKind.SufelCelo or PartKind.SufelZad or PartKind.SufelDno;

    public static string AppendPoziciaToName(string baseName, SufelPozicia pozicia)
    {
        if (pozicia == SufelPozicia.Nezadana)
            return baseName;

        var label = pozicia.ToShortLabel();
        if (baseName.Contains(label, StringComparison.OrdinalIgnoreCase))
            return baseName;

        return $"{baseName.Trim()} {label}".Trim();
    }

    private static string Normalize(string raw)
    {
        var s = raw.ToLowerInvariant();
        s = s.Replace('á', 'a').Replace('ä', 'a')
            .Replace('č', 'c').Replace('ď', 'd').Replace('é', 'e')
            .Replace('í', 'i').Replace('ľ', 'l').Replace('ĺ', 'l')
            .Replace('ň', 'n').Replace('ó', 'o').Replace('ô', 'o')
            .Replace('ŕ', 'r').Replace('š', 's').Replace('ť', 't')
            .Replace('ú', 'u').Replace('ý', 'y').Replace('ž', 'z');
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static SufelRola ResolveRola(string normalized)
    {
        if (Regex.IsMatch(normalized, @"\b(bok\s+sufel|sufel\s+bok)\b"))
            return SufelRola.Bok;
        if (Regex.IsMatch(normalized, @"\b(celo\s+sufel|sufel\s+celo|predne\s+celo\s+sufel)\b"))
            return SufelRola.Celo;
        if (Regex.IsMatch(normalized, @"\b(zad\s+sufel|sufel\s+zad)\b"))
            return SufelRola.Zad;
        if (Regex.IsMatch(normalized, @"\b(dno\s+sufl|sufl\w*\s+dno)\b"))
            return SufelRola.Dno;

        if (Regex.IsMatch(normalized, @"\bbok\b"))
            return SufelRola.Bok;
        if (Regex.IsMatch(normalized, @"\bcelo\b"))
            return SufelRola.Celo;
        if (Regex.IsMatch(normalized, @"\bzad\b"))
            return SufelRola.Zad;
        if (Regex.IsMatch(normalized, @"\bdno\b"))
            return SufelRola.Dno;
        return SufelRola.Neznama;
    }

    private static SufelPozicia ResolvePozicia(string normalized)
    {
        if (Regex.IsMatch(normalized, @"\b(vrchny|vrchna|horny|horna)\b"))
            return SufelPozicia.Vrchny;
        if (Regex.IsMatch(normalized, @"\b(stredny|stredna)\b"))
            return SufelPozicia.Stredny;
        if (Regex.IsMatch(normalized, @"\b(spodny|spodna|dolny|dolna)\b"))
            return SufelPozicia.Spodny;
        return SufelPozicia.Nezadana;
    }
}
