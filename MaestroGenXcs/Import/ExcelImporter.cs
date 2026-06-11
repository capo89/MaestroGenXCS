using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;

namespace MaestroGenXcs.Import;

/// <summary>
/// Import dielcov z Excelu. Hlavný formát je OBJ kusovník (Baranovci):
/// A = poradie, C = názov, D/G/S = DX/DY/DZ, I = počet ks,
/// L/M/N/O = ABS hodnoty.
/// Fallback je jednoduchý formát s hlavičkou v 1. riadku (Názov, Dĺžka, Šírka, Hrúbka).
/// </summary>
public sealed class ExcelImporter
{
    private const int MaxKusovnikScanRows = 2500;

    private static readonly Regex AssemblyFromBokPattern =
        new(@"\bbok\s*(\d+)\s*[lp]\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AnyAssemblyNumberPattern =
        new(@"\b(\d+)(?!\s*mm)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TrailingNumberPattern =
        new(@"(\d+)\s*$", RegexOptions.CultureInvariant);

    private readonly NameVariantExpander _expander = new();
    private readonly BokKusovnikExpander _bokExpander = new();
    private readonly PartTypeHinter _hinter = new();

    public List<Part> Import(string filePath)
    {
        using var wb = new XLWorkbook(filePath);

        var buffer = TryImportKusovnik(wb);
        if (buffer.Count == 0)
            buffer = TryImportSimple(wb);
        return buffer;
    }

    private List<Part> TryImportKusovnik(XLWorkbook wb)
    {
        var sheet = FindKusovnikWorksheet(wb);
        if (sheet != null)
        {
            var result = ImportKusovnikLayouts(sheet);
            if (result.Count > 0)
                return result;
        }

        foreach (var ws in wb.Worksheets)
        {
            var result = ImportKusovnikLayouts(ws);
            if (result.Count > 0)
                return result;
        }

        return new List<Part>();
    }

    private static IXLWorksheet? FindKusovnikWorksheet(XLWorkbook wb)
    {
        foreach (var sheet in wb.Worksheets)
        {
            if (sheet.Name.Trim().Contains("kusov", StringComparison.OrdinalIgnoreCase))
                return sheet;
        }

        return null;
    }

    private List<Part> ImportKusovnikLayouts(IXLWorksheet ws)
    {
        const int nameCol = 3;
        const int dxCol = 4;
        const int dyCol = 7;
        const int dzCol = 19;
        var startRows = new[] { 3, 4, 2, 5, 6 };

        foreach (var startRow in startRows)
        {
            var list = ImportKusovnikLayout(ws, startRow, nameCol, dxCol, dyCol, dzCol);
            if (list.Count > 0)
                return list;
        }

        return new List<Part>();
    }

    private List<Part> ImportKusovnikLayout(
        IXLWorksheet ws,
        int dataStartRow,
        int nameCol,
        int colDx,
        int colDy,
        int colDz)
    {
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < dataStartRow)
            return new List<Part>();

        var buffer = new List<Part>();
        var emptyStreak = 0;
        var foundAny = false;

        for (var row = dataStartRow; row <= lastRow && row <= dataStartRow + MaxKusovnikScanRows; row++)
        {
            var nazov = ws.Cell(row, nameCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(nazov))
            {
                emptyStreak++;
                if (foundAny && emptyStreak >= 8)
                    break;
                continue;
            }

            if (LooksLikeHeader(nazov))
                continue;

            foundAny = true;
            emptyStreak = 0;

            var dx = GetCellDouble(ws, row, colDx);
            var dy = GetCellDouble(ws, row, colDy);
            var dz = GetCellDouble(ws, row, colDz);
            var pocetKs = GetCellIntOrNull(ws, row, 9);
            var poradie = GetCellIntOrNull(ws, row, 1);

            var absPredna = GetCellAbsOrNull(ws, row, 12);
            var absM = GetCellAbsOrNull(ws, row, 13);
            var absZadna = GetCellAbsOrNull(ws, row, 14);
            var absO = GetCellAbsOrNull(ws, row, 15);

            AppendPartsFromName(buffer, nazov, dx, dy, dz, pocetKs, poradie, absPredna, absM, absZadna, absO);
        }

        return buffer;
    }

    private List<Part> TryImportSimple(XLWorkbook wb)
    {
        var result = ImportSimpleSingleHeader(wb.Worksheet(1));
        if (result.Count > 0)
            return result;

        foreach (var ws in wb.Worksheets)
        {
            result = ImportSimpleSingleHeader(ws);
            if (result.Count > 0)
                return result;
        }

        return new List<Part>();
    }

    private List<Part> ImportSimpleSingleHeader(IXLWorksheet ws)
    {
        var firstRow = ws.FirstRowUsed();
        var lastRow = ws.LastRowUsed();
        if (firstRow == null || lastRow == null)
            return new List<Part>();

        var header = MapHeader(ws.Row(firstRow.RowNumber()));
        var dataStart = firstRow.RowNumber() + 1;
        var buffer = new List<Part>();

        for (var r = dataStart; r <= lastRow.RowNumber(); r++)
        {
            var row = ws.Row(r);
            if (row.IsEmpty()) continue;

            var nazov = GetCell(row, header, "nazov", "názov", "name", "diel", "popis");
            if (string.IsNullOrWhiteSpace(nazov)) continue;

            var dx = GetDouble(row, header, "dlzka", "dĺžka", "l", "length", "dx");
            var dy = GetDouble(row, header, "sirka", "šírka", "w", "width", "dy");
            var dz = GetDouble(row, header, "hrubka", "hrúbka", "t", "thick", "dz");
            var pocetKs = GetCellIntFromHeader(row, header, "ks", "pocet", "počet");

            AppendPartsFromName(buffer, nazov.Trim(), dx, dy, dz, pocetKs, poradie: null);
        }

        return buffer;
    }

    private void AppendPartsFromName(
        List<Part> buffer,
        string nazov,
        double dx,
        double dy,
        double dz,
        int? pocetKs,
        int? poradie,
        int? absPredna = null,
        int? absM = null,
        int? absZadna = null,
        int? absO = null)
    {
        if (_bokExpander.TryExpand(nazov, pocetKs, out var bokVariants))
        {
            foreach (var v in bokVariants)
            {
                buffer.Add(new Part(v.Name, dx, dy, dz)
                {
                    Poradie = poradie,
                    PocetKs = v.PocetKs,
                    Zostava = v.Zostava,
                    Kind = v.Kind,
                    AbsPredna = absPredna,
                    AbsZadna = absZadna,
                    AbsLava = absM,
                    AbsPrava = absO
                });
            }

            return;
        }

        foreach (var variant in _expander.Expand(nazov))
        {
            var zostava = variant.Zostava ?? ParseZostavaFromName(variant.Name);
            buffer.Add(new Part(variant.Name, dx, dy, dz)
            {
                Poradie = poradie,
                PocetKs = pocetKs,
                Zostava = string.IsNullOrWhiteSpace(zostava) ? "Bez zostavy" : zostava.Trim(),
                Kind = _hinter.Hint(variant.Name),
                AbsPredna = absPredna,
                AbsZadna = absZadna,
                AbsLava = absM,
                AbsPrava = absO
            });
        }
    }

    private static string? ParseZostavaFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var bokMatch = AssemblyFromBokPattern.Match(name);
        if (bokMatch.Success)
            return bokMatch.Groups[1].Value;

        var any = AnyAssemblyNumberPattern.Match(name);
        if (any.Success)
            return any.Groups[1].Value;

        var tail = TrailingNumberPattern.Match(name);
        return tail.Success ? tail.Groups[1].Value : null;
    }

    private static bool LooksLikeHeader(string nazov)
    {
        var k = nazov.Trim().ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal);
        k = NormalizeDiakritics(k);
        return k is "nazov" or "diel" or "popis" or "rozmer" or "dlzka" or "sirka" or "hrubka" or "pocet" or "poradie";
    }

    private static string NormalizeDiakritics(string s) =>
        s.Replace("í", "i", StringComparison.Ordinal)
         .Replace("á", "a", StringComparison.Ordinal)
         .Replace("é", "e", StringComparison.Ordinal)
         .Replace("ý", "y", StringComparison.Ordinal)
         .Replace("ú", "u", StringComparison.Ordinal)
         .Replace("ó", "o", StringComparison.Ordinal)
         .Replace("ô", "o", StringComparison.Ordinal)
         .Replace("ľ", "l", StringComparison.Ordinal)
         .Replace("ň", "n", StringComparison.Ordinal)
         .Replace("č", "c", StringComparison.Ordinal);

    private static double GetCellDouble(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell.TryGetValue(out double d))
            return d;
        var s = cell.GetString().Replace(',', '.').Trim();
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static int? GetCellIntOrNull(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell.TryGetValue(out int i))
            return i;
        if (cell.TryGetValue(out double d))
            return (int)Math.Round(d);
        var s = cell.GetString().Trim();
        if (string.IsNullOrEmpty(s))
            return null;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return v;
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            return (int)Math.Round(f);
        return null;
    }

    private static int? GetCellAbsOrNull(IXLWorksheet ws, int row, int col)
    {
        var v = GetCellIntOrNull(ws, row, col);
        return v is 1 or 2 or 8 ? v : null;
    }

    private static Dictionary<string, int> MapHeader(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (string.IsNullOrEmpty(text)) continue;
            map[NormalizeHeader(text)] = cell.Address.ColumnNumber;
        }

        return map;
    }

    private static string NormalizeHeader(string s) =>
        NormalizeDiakritics(s.ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal));

    private static string GetCell(IXLRow row, IReadOnlyDictionary<string, int> header, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (header.TryGetValue(NormalizeHeader(key), out var col))
                return row.Cell(col).GetString().Trim();
        }

        return "";
    }

    private static double GetDouble(IXLRow row, IReadOnlyDictionary<string, int> header, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!header.TryGetValue(NormalizeHeader(key), out var col))
                continue;
            var cell = row.Cell(col);
            if (cell.TryGetValue(out double d))
                return d;
            var s = cell.GetString().Replace(',', '.');
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }

        return 0;
    }

    private static int? GetCellIntFromHeader(IXLRow row, IReadOnlyDictionary<string, int> header, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!header.TryGetValue(NormalizeHeader(key), out var col))
                continue;
            var cell = row.Cell(col);
            if (cell.TryGetValue(out int i))
                return i;
            if (cell.TryGetValue(out double d))
                return (int)Math.Round(d);
            var s = cell.GetString().Trim();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
        }

        return null;
    }
}
