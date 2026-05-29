using System.Globalization;
using System.Text;

namespace MaestroGenXcs.Xcs;

/// <summary>
/// Nízkoúrovňové emitery príkazov MaestroScriptingLanguage (XCS).
/// Funkcie vracajú jeden alebo viac riadkov ukončených '\n'.
/// </summary>
public static class MaestroXcsBuilder
{
    public const string DefaultDrillTool = "P001";

    private static string F2(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
    private static string FTrim(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public static string SetMachiningParameters() =>
        "SetMachiningParameters(\"AD\",5,10,16842762,false);\n";

    public static string CreateFinishedWorkpieceBox(string name, double dx, double dy, double dz)
        => FormattableString.Invariant(
            $"CreateFinishedWorkpieceBox(\"{Sanitize(name)}\",{dx:F0},{dy:F0},{dz:F0});\n");

    public static string SetWorkpieceSetupPosition(double x, double y, double z, double rotation = 0)
        => FormattableString.Invariant(
            $"SetWorkpieceSetupPosition({F2(x)},{F2(y)},{F2(z)},{F2(rotation)});\n");

    public static string CreateMessage(string title, string text, bool a = true, bool b = true)
        => $"CreateMessage(\"{Sanitize(title)}\",\"{Sanitize(text)}\",{(a ? "true" : "false")},{(b ? "true" : "false")});\n";

    public static string SelectWorkplane(string workplane) =>
        $"SelectWorkplane(\"{workplane}\");\n";

    public static string SetReferencePosition(int position) =>
        $"SetReferencePosition({position});\n";

    public static string CreatePattern(int countX, int countY, double pitchX, double pitchY)
        => FormattableString.Invariant(
            $"CreatePattern({countX},{countY},{F2(pitchX)},{F2(pitchY)},0,90);\n");

    public static string CreateDrill(
        string name,
        double x,
        double y,
        double depth,
        double diameter,
        string tool = DefaultDrillTool,
        string? kindOfHole = null,
        int dischargerStep = 3,
        string? description = null)
    {
        var kindStr = kindOfHole == null ? "\"-1\"" : $"\"{kindOfHole}\"";
        var desc = description ?? name;
        return $"CreateDrill(\"{Sanitize(name)}\",{F2(x)},{F2(y)},{F2(depth)},{F2(diameter)},"
             + $"\"{Sanitize(desc)}\",TypeOfProcess.Drilling,\"{tool}\",\"-1\",{dischargerStep},-1,-1,{kindStr});\n";
    }

    /// <summary>
    /// Mriežka <c>CreatePattern</c> + <c>CreateDrill</c> v jednom celku,
    /// s voliteľným prepnutím plochy a refpos.
    /// </summary>
    public static string DrillPattern(
        int countX,
        int countY,
        double xStart,
        double yStart,
        double pitchX,
        double pitchY,
        double diameter,
        double depth,
        string name,
        string tool = DefaultDrillTool,
        string? workplane = null,
        int? referencePosition = null,
        string? kindOfHole = null,
        string? description = null,
        int dischargerStep = 3)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(workplane))
            sb.Append(SelectWorkplane(workplane));
        if (referencePosition.HasValue)
            sb.Append(SetReferencePosition(referencePosition.Value));

        sb.Append(CreatePattern(countX, countY, pitchX, pitchY));
        sb.Append(CreateDrill(name, xStart, yStart, depth, diameter, tool, kindOfHole, dischargerStep, description));
        return sb.ToString();
    }

    public static string SetRetractStrategy(
        bool isLinear = true,
        bool isQuote = false,
        double distance = 0.0,
        double overlapLength = 0.0,
        double? speed = null)
    {
        var speedStr = speed.HasValue ? F2(speed.Value) : "-1";
        return $"SetRetractStrategy({(isLinear ? "true" : "false")},{(isQuote ? "true" : "false")},{F2(distance)},{F2(overlapLength)},{speedStr});\n";
    }

    public static string ResetRetractStrategy() => "ResetRetractStrategy();\n";

    public static string ExecMacroObehNoveDtd(
        string name,
        double frezovaniePod,
        double absMm,
        bool vpredu,
        bool vlavo,
        bool vpravo,
        bool vzadu,
        string freza = "E002",
        int odsavanie = 3)
        => $"ExecMacro(\"{Sanitize(name)}\",\"Obeh_novy_DTD\",{FTrim(frezovaniePod)},{FTrim(absMm)},"
         + $"{B(vpredu)},{B(vlavo)},{B(vpravo)},{B(vzadu)},\"{freza}\",{odsavanie.ToString(CultureInfo.InvariantCulture)});\n";

    private static string B(bool v) => v ? "true" : "false";

    private static string Sanitize(string s) =>
        s.Replace("\"", "", StringComparison.Ordinal)
         .Replace("\n", " ", StringComparison.Ordinal)
         .Replace("\r", " ", StringComparison.Ordinal);
}
