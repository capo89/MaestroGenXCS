using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.Domain;

/// <summary>Kontext skladania jednej zostavy – referenčný bok L a umiestnenia dielcov.</summary>
public sealed partial class AssemblyContext : ObservableObject
{
    [ObservableProperty]
    private string _zostava = "";

    /// <summary>Referenčný bok (typicky Bok L).</summary>
    [ObservableProperty]
    private Part? _referenceBok;

    /// <summary>Spôsob osadenia dna/vrchu voči bokom (kolíky + 3D).</summary>
    [ObservableProperty]
    private AssemblyCorpusMode _corpusMode = AssemblyCorpusMode.BokVlozeny;

    public ObservableCollection<AssemblyPlacement> Placements { get; } = new();

    /// <summary>Sekcie šuflí Blum Movento v tejto zostave.</summary>
    public ObservableCollection<SufelMoventoSekcia> MoventoSekcie { get; } = new();

    /// <summary>Zoskupené šufle po importe (2× bok, čelo, zad, dno).</summary>
    public ObservableCollection<SufelSkupina> SufelSkupiny { get; } = new();
}
