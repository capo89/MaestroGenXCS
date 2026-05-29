using System.IO;
using System.Text;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;

namespace MaestroGenXcs.Xcs;

/// <summary>
/// Skladá obsah .xcs súboru z dielca a jeho operácií.
/// <para>
/// Poradie blokov sa zhoduje s overeným správaním zo starého projektu:
/// </para>
/// <list type="number">
///   <item><c>SetMachiningParameters</c></item>
///   <item><c>CreateFinishedWorkpieceBox</c> + <c>SetWorkpieceSetupPosition</c></item>
///   <item><c>CreateMessage</c> (informačné okno v Maestre)</item>
///   <item><see cref="ObehOperation"/> – pred zvyškom, lebo ovplyvňuje obrys dielca</item>
///   <item><c>SetRetractStrategy</c> + ostatné operácie + <c>ResetRetractStrategy</c></item>
/// </list>
/// </summary>
public sealed class XcsExporter
{
    public string BuildContent(Part part, MaestroContext ctx)
    {
        var sb = new StringBuilder();
        sb.Append(MaestroXcsBuilder.SetMachiningParameters());
        sb.Append(MaestroXcsBuilder.CreateFinishedWorkpieceBox(ctx.WorkpieceName, ctx.BoxLength, ctx.BoxWidth, ctx.BoxThickness));

        var half = ctx.WorkpieceSetupOffsetSecondary / 2.0;
        sb.Append(MaestroXcsBuilder.SetWorkpieceSetupPosition(half, half, ctx.WorkpieceSetupZ));

        sb.Append(MaestroXcsBuilder.CreateMessage("Info", ctx.InfoMessage));

        // Obeh emituj pred zvyškom (zachované z pôvodného exportéra).
        foreach (var obeh in part.Operations.OfType<ObehOperation>().Where(o => o.IsEnabled))
            sb.Append(obeh.ToXcs(ctx));

        sb.Append(MaestroXcsBuilder.SetRetractStrategy(true, false, 0, 0));
        foreach (var op in part.Operations.Where(o => o is not ObehOperation && o.IsEnabled))
            sb.Append(op.ToXcs(ctx));
        sb.Append(MaestroXcsBuilder.ResetRetractStrategy());

        return sb.ToString();
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
