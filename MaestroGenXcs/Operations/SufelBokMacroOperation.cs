using System.Text;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Sufle;
using MaestroGenXcs.Xcs;

namespace MaestroGenXcs.Operations;

/// <summary>
/// Export šufľového boku: <c>SetMacroParam</c> (zmeny oproti makru) + <c>CreateMacro</c>.
/// </summary>
public sealed partial class SufelBokMacroOperation : CncOperation
{
    public double PaskaMm { get; set; }
    public double FrezPodMm { get; set; }
    public double PolohaDieryXmm { get; set; }
    public double PolohaDieryYmm { get; set; }
    public int PocetDier { get; set; }
    public double RoztecDierMm { get; set; }
    public double HlbkaDierMm { get; set; }
    public double HlbkaDrazkyMm { get; set; }
    public double PridavokMm { get; set; }
    public double Dx1 { get; set; }
    public double Dy1 { get; set; }
    public double Dz1 { get; set; }
    public double HlbkaZafrezovaniaMm { get; set; }
    public bool Dno18 { get; set; }

    public SufelBokMacroOperation()
    {
        Name = SufelBokXcs.MacroDisplayName;
    }

    public static SufelBokMacroOperation FromSkupina(SufelSkupina sk, Part bokPart)
    {
        var op = new SufelBokMacroOperation();
        op.SyncFrom(sk.BokMacro, bokPart);
        return op;
    }

    public void SyncFrom(SufelBokMacroParams macro, Part bokPart)
    {
        PaskaMm = macro.PaskaMm;
        FrezPodMm = macro.FrezPodMm;
        PolohaDieryXmm = macro.PolohaDieryXmm;
        PolohaDieryYmm = macro.PolohaDieryYmm;
        PocetDier = macro.PocetDier;
        RoztecDierMm = macro.RoztecDierMm;
        HlbkaDierMm = macro.HlbkaDierMm;
        HlbkaDrazkyMm = macro.HlbkaDrazkyMm;
        PridavokMm = macro.PridavokMm;
        HlbkaZafrezovaniaMm = macro.HlbkaZafrezovaniaMm;
        Dno18 = macro.Dno18;
        Dx1 = bokPart.Dx;
        Dy1 = bokPart.Dy;
        Dz1 = bokPart.Dz;
    }

    public override string TypeLabel => SufelBokXcs.MacroFileName;

    public override string ToXcs(MaestroContext ctx)
    {
        _ = ctx;
        var sb = new StringBuilder();

        AppendIfChanged(sb, SufelBokXcs.Param.Paska, PaskaMm, SufelBokMacroDefaults.Paska);
        AppendIfChanged(sb, SufelBokXcs.Param.Frezpod, FrezPodMm, SufelBokMacroDefaults.Frezpod);
        AppendIfChanged(sb, SufelBokXcs.Param.PolohaDieryX, PolohaDieryXmm, SufelBokMacroDefaults.PolohaDieryX);
        AppendIfChanged(sb, SufelBokXcs.Param.PolohaDieryY, PolohaDieryYmm, SufelBokMacroDefaults.PolohaDieryY);
        if (PocetDier != SufelBokMacroDefaults.PocetDier)
            sb.Append(MaestroXcsBuilder.SetMacroParam(SufelBokXcs.Param.PocetDier, PocetDier));
        AppendIfChanged(sb, SufelBokXcs.Param.RoztecDier, RoztecDierMm, SufelBokMacroDefaults.RoztecDier);
        AppendIfChanged(sb, SufelBokXcs.Param.HlbkaDier, HlbkaDierMm, SufelBokMacroDefaults.HlbkaDier);
        AppendIfChanged(sb, SufelBokXcs.Param.HlbkaDrazky, HlbkaDrazkyMm, SufelBokMacroDefaults.HlbkaDrazky);
        AppendIfChanged(sb, SufelBokXcs.Param.Pridavok, PridavokMm, SufelBokMacroDefaults.Pridavok);
        // dx1/dy1/dz1 nie – rozmery sú v CreateFinishedWorkpieceBox (2. riadok XCS).
        AppendIfChanged(sb, SufelBokXcs.Param.HlbkaZafrezovania, HlbkaZafrezovaniaMm, SufelBokMacroDefaults.HlbkaZafrezovania);
        if (Dno18 != SufelBokMacroDefaults.Dno18)
            sb.Append(MaestroXcsBuilder.SetMacroParam(SufelBokXcs.Param.Dno18, Dno18));

        sb.Append(MaestroXcsBuilder.CreateMacro(SufelBokXcs.MacroDisplayName, SufelBokXcs.MacroFileName));
        return sb.ToString();
    }

    private static void AppendIfChanged(StringBuilder sb, string name, double value, double defaultValue)
    {
        if (SufelBokMacroDefaults.Differs(value, defaultValue))
            sb.Append(MaestroXcsBuilder.SetMacroParam(name, value));
    }
}
