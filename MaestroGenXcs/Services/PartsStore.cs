using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;

namespace MaestroGenXcs.Services;

/// <summary>
/// Centrálny zoznam dielcov a spojov. View-modely si naňho držia referenciu.
/// </summary>
public sealed partial class PartsStore : ObservableObject
{
    public ObservableCollection<Part> Parts { get; } = new();
    public ObservableCollection<Connection> Connections { get; } = new();

    /// <summary>Režim korpusu podľa zostavy (nastaví <see cref="AssemblyStore"/>).</summary>
    public Func<string, AssemblyCorpusMode>? ResolveCorpusMode { get; set; }

    public void ReplaceParts(IEnumerable<Part> parts)
    {
        Parts.Clear();
        Connections.Clear();
        foreach (var p in parts)
        {
            if (p.IsPolica)
                p.EnsurePolicaSerie();
            Parts.Add(p);
        }

        RegenerateConnections();
    }

    /// <summary>
    /// Prerobí <see cref="Connections"/> podľa pevne zadefinovaných pravidiel
    /// v <see cref="ConnectionMap"/>. Volá sa po <see cref="ReplaceParts"/> a
    /// dá sa zavolať aj manuálne (napr. keď používateľ zmení <c>PartKind</c>).
    /// </summary>
    public void RegenerateConnections()
    {
        Connections.Clear();
        foreach (var c in ConnectionMap.GenerateConnections(Parts, ResolveCorpusMode))
            Connections.Add(c);
    }
}
