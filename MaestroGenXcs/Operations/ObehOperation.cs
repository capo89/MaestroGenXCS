using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Xcs;

namespace MaestroGenXcs.Operations;

/// <summary>
/// Obeh dielca po obvode pre orezanie ABS pásky / čisté hrany.
/// <para>
/// Volá Maestro makro <c>Obeh_novy_DTD</c> cez
/// <see cref="MaestroXcsBuilder.ExecMacroObehNoveDtd"/>. V starom projekte
/// bol obeh emitovaný automaticky podľa <c>AbsPredna/Zadna/Lava/Prava</c>
/// hodnôt dielca; tu je z neho samostatná, viditeľná operácia, ktorú
/// užívateľ vidí v zozname a vie ju kedykoľvek zapnúť / vypnúť.
/// </para>
/// </summary>
public sealed partial class ObehOperation : CncOperation
{
    /// <summary>true → frézovanie po predenej hrane (Y = 0).</summary>
    [ObservableProperty]
    private bool _vpredu;

    /// <summary>true → frézovanie po zadnej hrane (Y = Dy).</summary>
    [ObservableProperty]
    private bool _vzadu;

    /// <summary>true → frézovanie po ľavej hrane (X = 0).</summary>
    [ObservableProperty]
    private bool _vlavo;

    /// <summary>true → frézovanie po pravej hrane (X = Dx).</summary>
    [ObservableProperty]
    private bool _vpravo;

    /// <summary>Hrúbka ABS / odsadenie od hrany v mm (typicky 1 alebo 2 mm).</summary>
    [ObservableProperty]
    private double _absMm = 1.0;

    /// <summary>Frézovanie pod materiál (mm), default 2 mm.</summary>
    [ObservableProperty]
    private double _frezovaniePod = 2.0;

    /// <summary>Nástroj. V starom projekte sa používalo <c>E002</c>.</summary>
    [ObservableProperty]
    private string _freza = "E002";

    /// <summary>Krok odsávača (Maestro discharger step), default 3.</summary>
    [ObservableProperty]
    private int _odsavanie = 3;

    /// <summary>
    /// Ak je <see cref="AbsMm"/> presne <c>1</c> a tento prepínač je zapnutý,
    /// pri exporte sa zapíše ako 0.8 (replikujeme správanie zo starého projektu).
    /// </summary>
    [ObservableProperty]
    private bool _prepisatJednaNa08 = true;

    public ObehOperation()
    {
        Name = "Obeh DTD";
        Face = PartFace.Top;
    }

    /// <summary>
    /// Convenience factory – obeh vytvorený z ABS hodnôt dielca.
    /// Strana je „aktívna" iba ak je k nej priradená nenulová ABS hodnota
    /// (čo v exceli reprezentuje hrúbku ABS pásky).
    /// </summary>
    public static ObehOperation FromPart(Part part)
    {
        var op = new ObehOperation
        {
            Vpredu = part.AbsPredna.GetValueOrDefault() > 0,
            Vzadu = part.AbsZadna.GetValueOrDefault() > 0,
            Vlavo = part.AbsLava.GetValueOrDefault() > 0,
            Vpravo = part.AbsPrava.GetValueOrDefault() > 0,
            AbsMm = ResolveAbs(part),
            PrepisatJednaNa08 = part.PrepisatAbsJednaNaNulaOsemPriExporte
        };
        return op;
    }

    private static double ResolveAbs(Part part)
    {
        int?[] all = { part.AbsPredna, part.AbsZadna, part.AbsLava, part.AbsPrava };
        var raw = all.Where(v => v.HasValue && v.Value > 0).Select(v => v!.Value).DefaultIfEmpty(1).Max();
        return raw;
    }

    public override string ToXcs(MaestroContext ctx)
    {
        _ = ctx;
        // POZOR: Obeh emitujeme aj keď je žiadna strana aktívna (rovnako ako
        // pôvodný projekt). Maestro makro pri všetkých false jednoducho nič
        // nereže – ale riadok je v exporte konzistentne prítomný.
        var abs = AbsMm;
        if (PrepisatJednaNa08 && Math.Abs(abs - 1.0) < 0.001)
            abs = 0.8;

        return MaestroXcsBuilder.ExecMacroObehNoveDtd(
            Name,
            FrezovaniePod,
            abs,
            Vpredu,
            Vlavo,
            Vpravo,
            Vzadu,
            Freza,
            Odsavanie);
    }

    /// <summary>
    /// Vizualizácia obehu = uzavretá slučka odsadená cca <see cref="OutsetMm"/>
    /// MIMO dielca a posunutá pod jeho spodnú plochu o <see cref="BelowMm"/>.
    /// Robí to lepšie rozlíšiteľným voči ABS označeniam aj voči samotnej doske.
    /// </summary>
    public const double OutsetMm = 30.0;

    /// <summary>Pozícia obehu pod spodnou plochou dielca, v mm.</summary>
    public const double BelowMm = 2.0;

    public override IEnumerable<VisualHint> BuildVisualHints(double dx, double dy, double dz)
    {
        _ = dz;
        var z = -BelowMm;
        var x0 = -OutsetMm;
        var x1 = dx + OutsetMm;
        var y0 = -OutsetMm;
        var y1 = dy + OutsetMm;

        // Uzavretá slučka po obvode dielca. Renderer rozlíšuje "obeh" tag
        // a kreslí ju vlastnou farbou + hrúbkou.
        yield return new LineHint(x0, y0, z, x1, y0, z, "obeh"); // predná
        yield return new LineHint(x1, y0, z, x1, y1, z, "obeh"); // pravá
        yield return new LineHint(x1, y1, z, x0, y1, z, "obeh"); // zadná
        yield return new LineHint(x0, y1, z, x0, y0, z, "obeh"); // ľavá
    }
}
