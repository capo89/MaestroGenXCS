using System.Text;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Sufle;
using MaestroGenXcs.Xcs;

namespace MaestroGenXcs.Operations;

/// <summary>
/// Export čela alebo zadu šufľa cez makro <c>SufelCeloZad2</c>.
/// Čelo: <c>CeloZad=true</c>, zad: default <c>false</c> (nepíše sa).
/// </summary>
public sealed partial class SufelCeloZadMacroOperation : CncOperation
{
    public bool JeCelo { get; set; }

    public double PolohaDieryMm { get; set; }
    public int PocetDier { get; set; }
    public double RoztecDierMm { get; set; }
    public double LDieraNaSkrXmm { get; set; }
    public double PDieraNaSkrXmm { get; set; }
    public double DieryNaSkrutkyYmm { get; set; }
    public double HlbkaDierNaSkrutkyMm { get; set; }

    public SufelCeloZadMacroOperation()
    {
        Name = SufelCeloZadXcs.MacroDisplayName;
    }

    public static SufelCeloZadMacroOperation FromSkupina(SufelSkupina sk, Part part, bool jeCelo)
    {
        var op = new SufelCeloZadMacroOperation { JeCelo = jeCelo, Name = part.Name };
        op.SyncFrom(sk.CeloZadMacro, jeCelo);
        return op;
    }

    public void SyncFrom(SufelCeloZadMacroParams macro, bool jeCelo)
    {
        JeCelo = jeCelo;
        PolohaDieryMm = macro.PolohaDieryMm;
        PocetDier = macro.PocetDier;
        RoztecDierMm = macro.RoztecDierMm;
        LDieraNaSkrXmm = macro.LDieraNaSkrXmm;
        PDieraNaSkrXmm = macro.PDieraNaSkrXmm;
        DieryNaSkrutkyYmm = macro.DieryNaSkrutkyYmm;
        HlbkaDierNaSkrutkyMm = macro.HlbkaDierNaSkrutkyMm;
    }

    public override string TypeLabel => SufelCeloZadXcs.MacroFileName;

    public override string ToXcs(MaestroContext ctx)
    {
        _ = ctx;
        var sb = new StringBuilder();

        AppendIfChanged(sb, SufelCeloZadXcs.Param.PolohaDiery, PolohaDieryMm, SufelCeloZadMacroDefaults.PolohaDiery);
        if (PocetDier != SufelCeloZadMacroDefaults.PocetDier)
            sb.Append(MaestroXcsBuilder.SetMacroParam(SufelCeloZadXcs.Param.PocetDier, PocetDier));
        AppendIfChanged(sb, SufelCeloZadXcs.Param.RoztecDier, RoztecDierMm, SufelCeloZadMacroDefaults.RoztecDier);
        AppendIfChanged(sb, SufelCeloZadXcs.Param.LDieraNaSkrX, LDieraNaSkrXmm, SufelCeloZadMacroDefaults.LDieraNaSkrX);
        AppendIfChanged(sb, SufelCeloZadXcs.Param.PDieraNaSkrX, PDieraNaSkrXmm, SufelCeloZadMacroDefaults.PDieraNaSkrX);
        AppendIfChanged(sb, SufelCeloZadXcs.Param.DieryNaSkrutkyY, DieryNaSkrutkyYmm, SufelCeloZadMacroDefaults.DieryNaSkrutkyY);
        AppendIfChanged(sb, SufelCeloZadXcs.Param.HlbkaDierNaSkrutky, HlbkaDierNaSkrutkyMm, SufelCeloZadMacroDefaults.HlbkaDierNaSkrutky);

        if (JeCelo)
            sb.Append(MaestroXcsBuilder.SetMacroParam(SufelCeloZadXcs.Param.CeloZad, true));

        sb.Append(MaestroXcsBuilder.CreateMacro(SufelCeloZadXcs.MacroDisplayName, SufelCeloZadXcs.MacroFileName));
        return sb.ToString();
    }

    private static void AppendIfChanged(StringBuilder sb, string name, double value, double defaultValue)
    {
        if (SufelCeloZadMacroDefaults.Differs(value, defaultValue))
            sb.Append(MaestroXcsBuilder.SetMacroParam(name, value));
    }
}
