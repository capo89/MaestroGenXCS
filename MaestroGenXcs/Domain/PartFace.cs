namespace MaestroGenXcs.Domain;

/// <summary>
/// Plocha/hrana dielca. Mapuje sa priamo na Maestro <c>SelectWorkplane(...)</c>.
/// <para>
/// V UI sa pracuje len s hornou plochou (<see cref="Top"/>) a štyrmi
/// hranami. Hodnota <c>Bottom</c> existuje len pre interné účely
/// (napr. <see cref="PartFaceExtensions.Opposite"/>) a nikdy sa nesmie
/// dostať do používateľského rozhrania ako voľba pre operáciu.
/// </para>
/// </summary>
public enum PartFace
{
    Top = 0,
    Bottom = 1, // interne – v UI sa nepoužíva
    Left = 2,
    Right = 3,
    Front = 4,
    Back = 5
}

public static class PartFaceExtensions
{
    public static string ToWorkplane(this PartFace face) => face switch
    {
        PartFace.Top => "Top",
        PartFace.Bottom => "Bottom",
        PartFace.Left => "Left",
        PartFace.Right => "Right",
        PartFace.Front => "Front",
        PartFace.Back => "Back",
        _ => "Top"
    };

    public static PartFace Opposite(this PartFace face) => face switch
    {
        PartFace.Top => PartFace.Bottom,
        PartFace.Bottom => PartFace.Top,
        PartFace.Left => PartFace.Right,
        PartFace.Right => PartFace.Left,
        PartFace.Front => PartFace.Back,
        PartFace.Back => PartFace.Front,
        _ => PartFace.Top
    };
}
