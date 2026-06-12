using MaestroGenXcs.Services;

namespace MaestroGenXcs.Chrbaty;

/// <summary>Po importe skontroluje chrbáty vo všetkých zostavách (ak je zadaný <see cref="ChrbatTyp"/>).</summary>
public static class ChrbatAssemblyValidator
{
    public sealed record Result(IReadOnlyList<string> Warnings);

    public static Result ValidateAll(AssemblyStore assemblyStore, PartsStore partsStore)
    {
        var warnings = new List<string>();

        foreach (var ctx in assemblyStore.Contexts.Values)
        {
            if (ctx.Chrbat.Typ == ChrbatTyp.Nezadany)
                continue;

            var parts = partsStore.Parts
                .Where(p => string.Equals(p.Zostava, ctx.Zostava, StringComparison.OrdinalIgnoreCase))
                .ToList();

            ChrbatRozmeryValidator.ValidateZostava(
                ctx.Zostava,
                ctx.CorpusMode,
                ctx.Chrbat,
                parts,
                warnings);
        }

        return new Result(warnings);
    }
}
