using System.IO;
using System.Text;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Xcs;

/// <summary>
/// Skladá obsah .xcs súboru z dielca a jeho operácií.
/// </summary>
public sealed class XcsExporter
{
    public string BuildContent(Part part, MaestroContext ctx)
    {
        var sb = new StringBuilder();
        sb.Append(MaestroXcsBuilder.SetMachiningParameters());
        sb.Append(MaestroXcsBuilder.CreateFinishedWorkpieceBox(ctx.WorkpieceName, ctx.BoxLength, ctx.BoxWidth, ctx.BoxThickness));

        if (IsSufelMacroExport(part.Kind))
        {
            sb.Append(MaestroXcsBuilder.SetWorkpieceSetupPosition(
                SufelBokXcs.SetupPositionX,
                SufelBokXcs.SetupPositionY,
                SufelBokXcs.SetupPositionZ));
        }
        else
        {
            var half = ctx.WorkpieceSetupOffsetSecondary / 2.0;
            sb.Append(MaestroXcsBuilder.SetWorkpieceSetupPosition(half, half, ctx.WorkpieceSetupZ));
        }

        sb.Append(MaestroXcsBuilder.CreateMessage("Info", ctx.InfoMessage));

        if (IsSufelMacroExport(part.Kind))
        {
            AppendSufelMacroOperations(sb, part, ctx);
            return sb.ToString();
        }

        if (!SufelNameParser.IsSufelKind(part.Kind))
        {
            foreach (var obeh in part.Operations.OfType<ObehOperation>().Where(o => o.IsEnabled))
                sb.Append(obeh.ToXcs(ctx));
        }

        sb.Append(MaestroXcsBuilder.SetRetractStrategy(true, false, 0, 0));
        foreach (var op in part.Operations.Where(o => o is not ObehOperation && o.IsEnabled))
            sb.Append(op.ToXcs(ctx));
        sb.Append(MaestroXcsBuilder.ResetRetractStrategy());

        return sb.ToString();
    }

    private static bool IsSufelMacroExport(PartKind kind) =>
        kind is PartKind.SufelBok or PartKind.SufelCelo or PartKind.SufelZad;

    private static void AppendSufelMacroOperations(StringBuilder sb, Part part, MaestroContext ctx)
    {
        foreach (var op in part.Operations.Where(o => o.IsEnabled))
        {
            if (op is SufelBokMacroOperation or SufelCeloZadMacroOperation)
                sb.Append(op.ToXcs(ctx));
        }
    }

    public void ExportToFile(Part part, MaestroContext ctx, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, BuildContent(part, ctx), Encoding.UTF8);
    }

    public static MaestroContext ContextFromPart(Part part, string? infoMessage = null)
    {
        var note = infoMessage ?? part.PoznamkaPreExport;
        if (string.IsNullOrWhiteSpace(note))
            note = part.DefaultPoznamkaPreExport;
        return MaestroContext.ForPart(part.Name, part.Dx, part.Dy, part.Dz, note);
    }
}
