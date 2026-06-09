using MaestroGenXcs.Domain;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Services;

/// <summary>
/// Priradí šufľové dielce bez čísla zostavy do zostavy skrinky (bok L/P, dno, vrch).
/// </summary>
public static class SufelZostavaInferrer
{
    public static int Infer(IReadOnlyList<Part> parts)
    {
        var reassigned = 0;
        var cabinetByZostava = parts
            .Where(IsCabinetAnchor)
            .Select(p => p.Zostava)
            .Where(z => !string.IsNullOrWhiteSpace(z) && !IsPlaceholderZostava(z))
            .GroupBy(z => z, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        if (cabinetByZostava.Count == 0)
            return 0;

        foreach (var part in parts.Where(IsSufelPart))
        {
            if (!IsPlaceholderZostava(part.Zostava))
                continue;

            var target = ResolveTargetZostava(part, cabinetByZostava);
            if (target == null)
                continue;

            part.Zostava = target;
            reassigned++;
        }

        return reassigned;
    }

    private static string? ResolveTargetZostava(Part part, IReadOnlyDictionary<string, int> corpusByZostava)
    {
        if (corpusByZostava.Count == 1)
            return corpusByZostava.Keys.First();

        var fromName = ParseZostavaFromPartName(part.Name);
        if (fromName != null && corpusByZostava.ContainsKey(fromName))
            return fromName;

        return null;
    }

    private static string? ParseZostavaFromPartName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var us = name.IndexOf('_');
        if (us > 0 && int.TryParse(name.AsSpan(0, us), out _))
            return name[..us].Trim();

        var tail = System.Text.RegularExpressions.Regex.Match(name, @"(\d+)\s*$");
        return tail.Success ? tail.Groups[1].Value : null;
    }

    private static bool IsSufelPart(Part part) =>
        SufelNameParser.IsSufelKind(part.Kind) || SufelNameParser.Parse(part.Name).JeSufel;

    private static bool IsCabinetAnchor(Part part) => part.Kind is
        PartKind.BokL or PartKind.BokP or PartKind.Dno or PartKind.Vrch;

    private static bool IsPlaceholderZostava(string? zostava) =>
        string.IsNullOrWhiteSpace(zostava)
        || string.Equals(zostava, "Bez zostavy", StringComparison.OrdinalIgnoreCase);
}
