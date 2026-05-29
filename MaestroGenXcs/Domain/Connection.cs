using CommunityToolkit.Mvvm.ComponentModel;

namespace MaestroGenXcs.Domain;

/// <summary>
/// Spoj dvoch dielcov: A.Face &lt;-&gt; B.Face typu T. Slúži pre
/// <see cref="MaestroGenXcs.Services.OperationPropagator"/>.
/// </summary>
public sealed partial class Connection : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private Part _partA = null!;

    [ObservableProperty]
    private PartFace _faceA = PartFace.Top;

    /// <summary>Maestro <c>refpos</c> pre stranu A (0..3).</summary>
    [ObservableProperty]
    private int _refposA;

    [ObservableProperty]
    private Part _partB = null!;

    [ObservableProperty]
    private PartFace _faceB = PartFace.Top;

    /// <summary>Maestro <c>refpos</c> pre stranu B (0..3).</summary>
    [ObservableProperty]
    private int _refposB;

    [ObservableProperty]
    private ConnectionType _type = ConnectionType.Kolikovy;

    [ObservableProperty]
    private bool _autoPropagate = true;

    /// <summary>Ak false, propagátor pri Kolíkoch tento spoj preskočí.</summary>
    [ObservableProperty]
    private bool _propagateOnUserDrill = true;

    /// <summary>Ak true, súradnice x/y sa kopírujú 1:1 (bez výmeny osí).</summary>
    [ObservableProperty]
    private bool _identityCoordinates;

    /// <summary>Bok↔bok na hrane: propagovať len pri <see cref="DrillOperation.PreniestNaDruhyBok"/>.</summary>
    [ObservableProperty]
    private bool _requiresOppositeBokOptIn;

    [ObservableProperty]
    private string _poznamka = "";

    public string Popis =>
        $"{PartA?.Name} [{FaceA} r{RefposA}] <-> {PartB?.Name} [{FaceB} r{RefposB}]  ({Type})";
}
