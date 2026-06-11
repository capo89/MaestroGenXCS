using System.Text.RegularExpressions;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Services;

/// <summary>
/// Rozpozná konštrukčné boky skrinky z kusovníka.
/// „bok N“ = bok zostavy č. N (číslo zostavy, nie ľavý/pravý).
/// L/P len z explicitných značiek: (1), (2), bok l/p, ľavý/pravý – nie šufľové „bok sufel …“.
/// </summary>
public static class CabinetBokClassifier
{
    private static readonly Regex BokAssemblyNumberPattern =
        new(@"\bbok\s+(\d+)\b", RegexOptions.CultureInvariant);
    public static void Apply(IEnumerable<Part> parts)
    {
        foreach (var grp in parts.GroupBy(p => p.Zostava ?? "Bez zostavy", StringComparer.OrdinalIgnoreCase))
            ApplyZostava(grp.ToList());
    }

    /// <summary>Bok, ktorý sa len prikladá zvonku – nie konštrukčný bok skrinky (referencia 3D).</summary>
    public static bool IsNonStructuralBok(Part part) => IsNonStructuralBokName(part.Name);

    public static bool IsNonStructuralBokName(string? rawName)
    {
        var n = Normalize(rawName ?? "");
        if (string.IsNullOrWhiteSpace(n))
            return false;

        return n.Contains("kryci")
            || n.Contains("krycie")
            || n.Contains("pomocn")
            || n.Contains("oblozk")
            || n.Contains("fals")
            || n.Contains("falš")
            || n.Contains("doplnkov")
            || n.Contains("dekor");
    }

    private static void ApplyZostava(List<Part> members)
    {
        foreach (var part in members)
        {
            if (part.Kind is PartKind.BokL or PartKind.BokP && IsNonStructuralBok(part))
                part.Kind = PartKind.Generic;
        }

        if (members.Any(p => p.Kind == PartKind.BokL && !IsNonStructuralBok(p)))
            return;

        var candidates = members
            .Where(IsCabinetBokCandidate)
            .OrderBy(p => StructuralBokPriority(p.Name))
            .ThenBy(p => p.Poradie ?? int.MaxValue)
            .ThenBy(p => SideSortKey(p.Name))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
            return;

        if (candidates.Count == 1)
        {
            candidates[0].Kind = InferSide(candidates[0].Name) ?? PartKind.BokL;
            return;
        }

        var assignedL = false;
        var assignedP = false;
        foreach (var part in candidates)
        {
            var side = InferSide(part.Name);
            if (side == PartKind.BokL && !assignedL)
            {
                part.Kind = PartKind.BokL;
                assignedL = true;
                continue;
            }

            if (side == PartKind.BokP && !assignedP)
            {
                part.Kind = PartKind.BokP;
                assignedP = true;
            }
        }

        if (!assignedL)
        {
            var zostava = members.FirstOrDefault()?.Zostava;
            var forL = candidates.FirstOrDefault(p => BokNumberMatchesZostava(p.Name, zostava))
                ?? candidates.FirstOrDefault(p => p.Kind != PartKind.BokP)
                ?? candidates[0];
            forL.Kind = PartKind.BokL;
        }
    }

    public static bool IsCabinetBokCandidate(Part part)
    {
        if (part.Kind is PartKind.BokL or PartKind.BokP)
            return false;

        if (SufelNameParser.IsSufelKind(part.Kind) || SufelNameParser.Parse(part.Name).JeSufel)
            return false;

        var n = Normalize(part.Name);
        if (!Regex.IsMatch(n, @"\bbok\b"))
            return false;

        if (n.Contains("sufel") || n.Contains("sufl"))
            return false;

        if (n.Contains("dvere") || n.Contains("biela-boky") || n.Contains("biela boky"))
            return false;

        if (IsNonStructuralBokName(rawName: part.Name))
            return false;

        return true;
    }

    private static PartKind? InferSide(string? rawName)
    {
        var n = Normalize(rawName ?? "");
        if (Regex.IsMatch(n, @"\bbok\s+\(1\)"))
            return PartKind.BokL;
        if (Regex.IsMatch(n, @"\bbok\s+\(2\)"))
            return PartKind.BokP;

        if (Regex.IsMatch(n, @"\b_l\b|\bbok\s+l\b|lav|lavy|lave"))
            return PartKind.BokL;
        if (Regex.IsMatch(n, @"\b_p\b|\bbok\s+p\b|prav|pravy|prave"))
            return PartKind.BokP;

        return null;
    }

    private static bool BokNumberMatchesZostava(string? rawName, string? zostava)
    {
        if (string.IsNullOrWhiteSpace(zostava))
            return false;

        var n = TryParseBokAssemblyNumber(rawName);
        return n != null && string.Equals(n, zostava.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryParseBokAssemblyNumber(string? rawName)
    {
        var m = BokAssemblyNumberPattern.Match(Normalize(rawName ?? ""));
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Uprednostní bok s explicitnou stranou L/P pred „bok N“ (číslo zostavy).</summary>
    private static int StructuralBokPriority(string? rawName)
    {
        var side = InferSide(rawName);
        return side switch
        {
            PartKind.BokL => 0,
            PartKind.BokP => 2,
            _ => 1,
        };
    }

    private static int SideSortKey(string? rawName)
    {
        var side = InferSide(rawName);
        return side switch
        {
            PartKind.BokL => 0,
            PartKind.BokP => 1,
            _ => 5,
        };
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
