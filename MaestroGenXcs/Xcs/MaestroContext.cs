namespace MaestroGenXcs.Xcs;

/// <summary>
/// Kontext exportu jedného dielca: rozmery, popis, posun nuly.
/// </summary>
public sealed class MaestroContext
{
    public string WorkpieceName { get; init; } = "diel";
    public double BoxLength { get; init; }
    public double BoxWidth { get; init; }
    public double BoxThickness { get; init; }

    /// <summary>Posun nuly v osi X/Y (Maestro <c>SetWorkpieceSetupPosition</c>).</summary>
    public double WorkpieceSetupOffset { get; init; } = 30.0;
    public double WorkpieceSetupOffsetSecondary { get; init; } = 30.0;
    public double WorkpieceSetupZ { get; init; }

    public string InfoMessage { get; init; } = "";

    public static MaestroContext ForPart(
        string name,
        double dx,
        double dy,
        double dz,
        string? infoMessage = null)
    {
        return new MaestroContext
        {
            WorkpieceName = name,
            BoxLength = dx,
            BoxWidth = dy,
            BoxThickness = dz,
            WorkpieceSetupZ = dz,
            InfoMessage = infoMessage ?? "1ks"
        };
    }
}
