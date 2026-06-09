using System.Text.RegularExpressions;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Services;

/// <summary>
/// Rozpozná boky skrinky z kusovníka (napr. „bok (1)“, „bok 2“) – nie šufľové „bok sufel …“.
/// </summary>
public static class CabinetBokClassifier
{
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
            var forL = candidates.FirstOrDefault(p => p.Kind != PartKind.BokP) ?? candidates[0];
            forL.Kind = PartKind.BokL;
        }

        if (!assignedP)
        {
            var forP = candidates.FirstOrDefault(p => p.Kind != PartKind.BokL) ?? candidates.Last();
            if (forP.Kind != PartKind.BokL || candidates.Count > 1)
                forP.Kind = PartKind.BokP;
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
        if (Regex.IsMatch(n, @"\b(bok\s*)?(\(1\)|_l\b|\bl\b|lav|lavy|lave)\b")
            && !Regex.IsMatch(n, @"\bbok\s*2\b"))
            return PartKind.BokL;

        if (Regex.IsMatch(n, @"\b(bok\s*)?(\(2\)|_p\b|\bp\b|prav|pravy|prave)\b"))
            return PartKind.BokP;

        if (Regex.IsMatch(n, @"\bbok\s+2\b"))
            return PartKind.BokP;

        if (Regex.IsMatch(n, @"\bbok\s+\(1\)\b") || Regex.IsMatch(n, @"\bbok\s+1\b"))
            return PartKind.BokL;

        return null;
    }

    /// <summary>Uprednostní konštrukčný bok L/P podľa názvu (nie číslo zostavy v „bok 3“).</summary>
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
