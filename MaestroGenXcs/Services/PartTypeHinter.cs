using System.Text.RegularExpressions;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Services;

/// <summary>
/// Tipne typ dielca z textového názvu. Výsledok je iba pomôcka pre
/// defaulty operácií a auto-návrh spojov; nemení správanie generátora.
/// </summary>
public sealed class PartTypeHinter
{
    private static readonly Regex SidePattern = new(
        @"\bbok\s*(\d+)?\s*([lp])\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public PartKind Hint(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return PartKind.Generic;

        var name = rawName.ToLowerInvariant();
        var isSufel = name.Contains("sufel", StringComparison.Ordinal)
                   || name.Contains("šufel", StringComparison.Ordinal);

        if (isSufel)
        {
            if (name.Contains("bok", StringComparison.Ordinal))
                return PartKind.SufelBok;
            if (name.Contains("celo", StringComparison.Ordinal) || name.Contains("čelo", StringComparison.Ordinal))
                return PartKind.SufelCelo;
            if (name.Contains("zad", StringComparison.Ordinal))
                return PartKind.SufelZad;
            return PartKind.Kovanie;
        }

        if (name.Contains("movento", StringComparison.Ordinal))
            return PartKind.Kovanie;

        if (name.Contains("polica", StringComparison.Ordinal)
            || name.Contains("polička", StringComparison.Ordinal))
            return PartKind.Polica;

        if (Regex.IsMatch(name, @"\bdno\b"))
            return PartKind.Dno;
        if (Regex.IsMatch(name, @"\bvrch\b"))
            return PartKind.Vrch;
        if (Regex.IsMatch(name, @"\bpriecka\b") || Regex.IsMatch(name, @"\bpriečka\b"))
            return PartKind.Priecka;
        if (Regex.IsMatch(name, @"\bchrbat\b") || Regex.IsMatch(name, @"\bchrbát\b"))
            return PartKind.Chrbat;

        if (name.Contains("bok", StringComparison.Ordinal))
        {
            if (name.Contains("_l", StringComparison.Ordinal) || Regex.IsMatch(name, @"\bľav"))
                return PartKind.BokL;
            if (name.Contains("_p", StringComparison.Ordinal) || Regex.IsMatch(name, @"\bprav"))
                return PartKind.BokP;
            var side = SidePattern.Match(rawName);
            if (side.Success)
            {
                var s = side.Groups[2].Value;
                if (string.Equals(s, "L", StringComparison.OrdinalIgnoreCase))
                    return PartKind.BokL;
                if (string.Equals(s, "P", StringComparison.OrdinalIgnoreCase))
                    return PartKind.BokP;
            }
            return PartKind.Generic;
        }

        return PartKind.Generic;
    }
}
