using System.Globalization;
using System.Text.RegularExpressions;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;

namespace MaestroGenXcs.Import;

/// <summary>
/// Rozbalí riadky kusovníka typu „1_bok 4“, „3_bok 8,9,11“, „8_bok L“, „14_bok L 8,9“:
/// rozdelí ks medzi zostavy a (ak chýba L/P v názve) na ľavý a pravý bok.
/// </summary>
public sealed class BokKusovnikExpander
{
    private static readonly Regex MaestroBokLinePattern =
        new(@"^\d+_bok\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AssemblyListAtEndPattern =
        new(@"(\d+(?:\s*,\s*\d+)*)\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex BokSideSuffixPattern =
        new(@"bok\s+([lLpP])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MaestroPrefixPattern =
        new(@"^(\d+_)", RegexOptions.CultureInvariant);

    public readonly record struct Variant(string Name, string Zostava, PartKind Kind, int PocetKs);

    public bool TryExpand(string originalName, int? pocetKs, out IReadOnlyList<Variant> variants)
    {
        variants = Array.Empty<Variant>();
        var name = originalName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!MaestroBokLinePattern.IsMatch(name))
            return false;

        var normalized = Normalize(name);
        if (normalized.Contains("sufel") || normalized.Contains("sufl"))
            return false;

        if (CabinetBokClassifier.IsNonStructuralBokName(name))
            return false;

        var trailingAsm = AssemblyListAtEndPattern.Match(name);
        string bodyRaw;
        List<string> assemblies;

        if (trailingAsm.Success)
        {
            bodyRaw = name[..trailingAsm.Index].Trim();
            assemblies = ParseAssemblyNumbers(trailingAsm.Groups[1].Value);
        }
        else
        {
            bodyRaw = name;
            assemblies = new List<string>();
        }

        if (string.IsNullOrWhiteSpace(bodyRaw))
            return false;

        var prefix = "";
        var prefixZostava = (string?)null;
        var pm = MaestroPrefixPattern.Match(bodyRaw);
        if (pm.Success)
        {
            prefix = pm.Groups[1].Value;
            if (int.TryParse(prefix.AsSpan(0, prefix.Length - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                prefixZostava = prefix[..^1];
        }

        var side = TryParseExplicitSide(bodyRaw);
        var zostavy = assemblies.Count > 0
            ? assemblies
            : prefixZostava != null
                ? new List<string> { prefixZostava }
                : new List<string>();

        if (zostavy.Count == 0)
            return false;

        var totalKs = Math.Max(1, pocetKs ?? 1);
        var ksPerZostava = SplitEvenly(totalKs, zostavy.Count);

        var result = new List<Variant>();
        for (var zi = 0; zi < zostavy.Count; zi++)
        {
            var z = zostavy[zi];
            var ksZ = ksPerZostava[zi];

            if (side.HasValue)
            {
                result.Add(CreateVariant(prefix, z, side.Value, ksZ, bodyRaw, assemblies.Count == 0));
                continue;
            }

            var ksPerSide = SplitEvenly(ksZ, 2);
            result.Add(CreateVariant(prefix, z, 'L', ksPerSide[0], bodyRaw, assemblies.Count == 0));
            result.Add(CreateVariant(prefix, z, 'P', ksPerSide[1], bodyRaw, assemblies.Count == 0));
        }

        variants = result;
        return result.Count > 0;
    }

    private static Variant CreateVariant(
        string prefix,
        string zostava,
        char side,
        int ks,
        string bodyRaw,
        bool singleCabinetExplicitSide)
    {
        var kind = side is 'L' or 'l' ? PartKind.BokL : PartKind.BokP;
        var sideLabel = side.ToString().ToUpperInvariant();

        // Jedna skrinka s už určenou stranou (8_bok L) – názov nemeníme.
        if (singleCabinetExplicitSide && TryParseExplicitSide(bodyRaw).HasValue)
            return new Variant(bodyRaw.Trim(), zostava, kind, Math.Max(1, ks));

        var name = $"{prefix}bok {zostava} {sideLabel}".Trim();
        return new Variant(name, zostava, kind, Math.Max(1, ks));
    }

    private static char? TryParseExplicitSide(string bodyRaw)
    {
        var m = BokSideSuffixPattern.Match(bodyRaw.Trim());
        if (!m.Success)
            return null;
        return m.Groups[1].Value[0];
    }

    private static List<string> ParseAssemblyNumbers(string raw)
    {
        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<int> SplitEvenly(int total, int parts)
    {
        if (parts <= 0)
            return new List<int> { total };

        var result = new List<int>(parts);
        var baseAmt = total / parts;
        var remainder = total % parts;
        for (var i = 0; i < parts; i++)
            result.Add(baseAmt + (i < remainder ? 1 : 0));
        return result;
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
}
