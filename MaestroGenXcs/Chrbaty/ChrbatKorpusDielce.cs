using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Chrbaty;

/// <summary>Rozmery korpusu potrebné pre výpočet očakávaných rozmerov chrbátu.</summary>
public sealed record ChrbatKorpusDielce(
    double BokDx,
    double BokDy,
    double BokLDz,
    double BokPDz,
    double DnoDx,
    double DnoDy,
    double DnoDz,
    double VrchDx,
    double VrchDy,
    double VrchDz)
{
    public static bool TryFromZostavaParts(IReadOnlyList<Part> parts, out ChrbatKorpusDielce korpus)
    {
        korpus = null!;
        var bokL = parts.FirstOrDefault(p => p.Kind == PartKind.BokL);
        var bokP = parts.FirstOrDefault(p => p.Kind == PartKind.BokP);
        var bok = bokL ?? bokP;
        var dno = parts.FirstOrDefault(p => p.Kind == PartKind.Dno);
        var vrch = parts.FirstOrDefault(p => p.Kind == PartKind.Vrch);

        if (bok == null || dno == null || vrch == null)
            return false;

        korpus = new ChrbatKorpusDielce(
            BokDx: bok.Dx,
            BokDy: bok.Dy,
            BokLDz: bokL?.Dz ?? bokP!.Dz,
            BokPDz: bokP?.Dz ?? bokL!.Dz,
            DnoDx: dno.Dx,
            DnoDy: dno.Dy,
            DnoDz: dno.Dz,
            VrchDx: vrch.Dx,
            VrchDy: vrch.Dy,
            VrchDz: vrch.Dz);
        return true;
    }
}
