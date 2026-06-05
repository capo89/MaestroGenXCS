using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Domain;
using MaestroGenXcs.Services;

namespace MaestroGenXcs.ViewModels;

/// <summary>Položka dielca v strome zostavy – stav nastavenia pre ikonu.</summary>
public sealed partial class PartTreeEntry : ObservableObject
{
    public PartTreeEntry(Part part) => Part = part;

    public Part Part { get; }

    public string Name => Part.Name;

    [ObservableProperty]
    private bool _isConfigured;

    public void Refresh(AssemblyStore store) =>
        IsConfigured = AssemblySetupEvaluator.IsPartConfigured(Part, store.GetContext(Part.Zostava));
}
