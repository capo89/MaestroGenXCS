using System.IO;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Sufle;

/// <summary>Pomenovanie .xcs súborov pre dielce šufľa (poradie + názov + zostava).</summary>
public static class SufelXcsExportNames
{
    public static bool IsSufelExportPart(Part part) =>
        part.Kind is PartKind.SufelBok or PartKind.SufelCelo or PartKind.SufelZad;

    /// <summary>Napr. <c>14_sufel bok 4.xcs</c> – poradie z Excelu, názov dielca, číslo zostavy.</summary>
    public static string BuildFileName(Part part)
    {
        var name = part.Name.Trim();
        var zostava = part.Zostava?.Trim();
        if (!string.IsNullOrWhiteSpace(zostava)
            && !string.Equals(zostava, "Bez zostavy", StringComparison.OrdinalIgnoreCase)
            && !NameAlreadyContainsZostava(name, zostava))
        {
            name = $"{name} {zostava}";
        }

        var prefix = part.Poradie is int p ? $"{p}_" : "";
        return Sanitize(prefix + name) + ".xcs";
    }

    private static bool NameAlreadyContainsZostava(string name, string zostava) =>
        name.EndsWith($" {zostava}", StringComparison.Ordinal)
        || name.EndsWith(zostava, StringComparison.Ordinal);

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString().Trim();
    }
}
