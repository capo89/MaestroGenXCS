using System.Globalization;
using System.Text.RegularExpressions;

namespace MaestroGenXcs.Import;

/// <summary>
/// Rozbalí kombinované názvy z kusovníka, napr.
/// <c>41_bok 7,9</c> -&gt; <c>41_bok 7</c>, <c>41_bok 9</c>;
/// <c>42_dno,vrch 7,9</c> -&gt; <c>42_dno 7</c>, <c>42_vrch 7</c>, <c>42_dno 9</c>, <c>42_vrch 9</c>.
/// </summary>
public sealed class NameVariantExpander
{
    private static readonly Regex AssemblyListAtEndPattern =
        new(@"(\d+(?:\s*,\s*\d+)*)\s*$", RegexOptions.CultureInvariant);

    public readonly record struct Variant(string Name, string? Zostava);

    public IEnumerable<Variant> Expand(string originalName)
    {
        var name = originalName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            yield break;

        var m = AssemblyListAtEndPattern.Match(name);
        if (!m.Success)
        {
            yield return new Variant(name, null);
            yield break;
        }

        var asmListRaw = m.Groups[1].Value;
        var baseRaw = name[..m.Index].Trim().TrimEnd('-', ',', ';');
        if (string.IsNullOrWhiteSpace(baseRaw))
        {
            yield return new Variant(name, null);
            yield break;
        }

        var prefix = "";
        var core = baseRaw;
        var us = baseRaw.IndexOf('_');
        if (us > 0 && int.TryParse(baseRaw[..us], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            prefix = baseRaw[..(us + 1)];
            core = baseRaw[(us + 1)..].Trim();
        }

        var assemblies = asmListRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (assemblies.Count == 0)
        {
            yield return new Variant(name, null);
            yield break;
        }

        var parts = core
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (parts.Count == 0)
        {
            yield return new Variant(name, null);
            yield break;
        }

        foreach (var asm in assemblies)
        foreach (var part in parts)
        {
            var partName = $"{prefix}{part} {asm}".Trim();
            yield return new Variant(partName, asm);
        }
    }
}
