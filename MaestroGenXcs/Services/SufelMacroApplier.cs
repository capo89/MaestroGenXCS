using MaestroGenXcs.Domain;
using MaestroGenXcs.Operations;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Services;

/// <summary>Aplikuje makrá šufľa (bok, čelo, zad) – bez obehu.</summary>
public static class SufelMacroApplier
{
    public static void ApplyAll(AssemblyStore assemblyStore)
    {
        foreach (var ctx in assemblyStore.Contexts.Values)
        {
            foreach (var sk in ctx.SufelSkupiny)
                Apply(sk);
        }
    }

    public static void Apply(SufelSkupina sk)
    {
        SufelMacroSynchronizer.SyncAll(sk);
        ApplyBok(sk);
        ApplyCeloZad(sk, sk.CeloPart, jeCelo: true);
        ApplyCeloZad(sk, sk.ZadPart, jeCelo: false);
    }

    public static void StripObehFromSufelParts(IEnumerable<Part> parts)
    {
        foreach (var part in parts.Where(p => SufelNameParser.IsSufelKind(p.Kind)))
            StripObeh(part);
    }

    private static void ApplyBok(SufelSkupina sk)
    {
        var part = sk.BokPart;
        if (part == null)
            return;

        StripObeh(part);

        var existing = part.Operations.OfType<SufelBokMacroOperation>().FirstOrDefault();
        if (existing != null)
            existing.SyncFrom(sk.BokMacro, part);
        else
            part.Operations.Add(SufelBokMacroOperation.FromSkupina(sk, part));
    }

    private static void ApplyCeloZad(SufelSkupina sk, Part? part, bool jeCelo)
    {
        if (part == null)
            return;

        StripObeh(part);

        var existing = part.Operations.OfType<SufelCeloZadMacroOperation>().FirstOrDefault();
        if (existing != null)
            existing.SyncFrom(sk.CeloZadMacro, jeCelo);
        else
            part.Operations.Add(SufelCeloZadMacroOperation.FromSkupina(sk, part, jeCelo));
    }

    private static void StripObeh(Part part)
    {
        foreach (var obeh in part.Operations.OfType<ObehOperation>().ToList())
            part.Operations.Remove(obeh);
    }
}
