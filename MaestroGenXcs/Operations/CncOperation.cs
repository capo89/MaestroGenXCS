using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Xcs;

namespace MaestroGenXcs.Operations;

/// <summary>
/// Abstraktný príkaz CNC – generuje časť XCS výstupu a má nepovinný
/// "hint" pre 3D vizualizér (zoznam otvorov, segmentov, kvádrov).
/// </summary>
public abstract partial class CncOperation : ObservableObject
{
    /// <summary>
    /// Stabilný identifikátor operácie. Slúži <see cref="MaestroGenXcs.Services.OperationPropagator"/>
    /// na párovanie zrkadlovej kópie so zdrojom – mirror si pamätá
    /// <see cref="SourceOperationId"/> svojho originálu.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Ak je nastavený, táto operácia je <b>zrkadlová kópia</b> vytvorená propagátorom.
    /// Hodnota odkazuje na <see cref="Id"/> zdrojovej operácie na partnerovom dielci.
    /// V UI sa <b>nezobrazuje</b> – mirror operácia vyzerá pre používateľa identicky
    /// ako originál; pole slúži iba propagátorovi na synchronizáciu.
    /// </summary>
    [ObservableProperty]
    private Guid? _sourceOperationId;

    /// <summary>
    /// Identifikátor spoja (<see cref="MaestroGenXcs.Domain.Connection.Id"/>),
    /// z ktorého táto kópia vznikla. Pri zmazaní spoja propagátor takéto kópie odstráni.
    /// </summary>
    [ObservableProperty]
    private Guid? _sourceConnectionId;

    [ObservableProperty]
    private string _name = "";

    /// <summary>
    /// Plocha dielca, na ktorej operácia žije. Slúži pre 3D umiestnenie
    /// aj pre Maestro <c>SelectWorkplane</c>.
    /// </summary>
    [ObservableProperty]
    private PartFace _face = PartFace.Top;

    /// <summary>
    /// Maestro referenčný roh (0..3). Význam podľa REFPOS_PATTERN_MAP.
    /// </summary>
    [ObservableProperty]
    private int _refPos;

    /// <summary>
    /// True ak operáciu vygeneroval propagátor (je to zrkadlová kópia z partnera).
    /// Slúži <b>iba</b> propagátoru – v UI sa operácia neoznačuje žiadnym badgeom
    /// a edituje sa normálne. Edit zdrojovej operácie ju však pretlačí.
    /// </summary>
    [ObservableProperty]
    private bool _isPropagated;

    /// <summary>
    /// True = operácia je aktívna – kreslí sa v 2D/3D náhľade a zapisuje sa
    /// do XCS exportu. False = používateľ ju „vypol" cez panel Operácie,
    /// vizuálne aj v exporte sa preskočí.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    public abstract string ToXcs(MaestroContext ctx);

    /// <summary>
    /// 3D vizuálne hinty pre vrstvu HelixToolkit. Operácia ich produkuje
    /// v lokálnych súradniciach dielca (0..Dx, 0..Dy, 0..Dz).
    /// </summary>
    public virtual IEnumerable<VisualHint> BuildVisualHints(double dx, double dy, double dz)
        => Array.Empty<VisualHint>();

    /// <summary>
    /// Krátky čitateľný popis typu operácie pre UI zoznam (napr. „Obeh DTD",
    /// „Vŕtanie kolíkov", …). Default je názov triedy bez „Operation" suffixu.
    /// </summary>
    public virtual string TypeLabel => GetType().Name.Replace("Operation", string.Empty);
}

/// <summary>
/// Jednoduchá popisná štruktúra pre 3D vrstvu. Renderer si z nej poskladá
/// konkrétne <see cref="System.Windows.Media.Media3D.Visual3D"/> objekty.
/// </summary>
public abstract record VisualHint;

public sealed record HoleHint(
    double X,
    double Y,
    double Z,
    PartFace Face,
    double Diameter,
    double Depth,
    string? Tag = null) : VisualHint;

public sealed record LineHint(
    double X1,
    double Y1,
    double Z1,
    double X2,
    double Y2,
    double Z2,
    string? Tag = null) : VisualHint;
