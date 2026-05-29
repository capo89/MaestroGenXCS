using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MaestroGenXcs.Domain;

/// <summary>Kontext skladania jednej zostavy – referenčný bok L a umiestnenia dielcov.</summary>
public sealed partial class AssemblyContext : ObservableObject
{
    [ObservableProperty]
    private string _zostava = "";

    /// <summary>Referenčný bok (typicky Bok L).</summary>
    [ObservableProperty]
    private Part? _referenceBok;

    public ObservableCollection<AssemblyPlacement> Placements { get; } = new();
}
