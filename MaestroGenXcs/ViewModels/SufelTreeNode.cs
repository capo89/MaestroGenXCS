using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaestroGenXcs.Services;
using MaestroGenXcs.Sufle;

namespace MaestroGenXcs.ViewModels;

/// <summary>Uzol stromu – jedna šufľa (vrchný / stredný / spodný) so zoskupenými dielcami.</summary>
public sealed partial class SufelTreeNode : ObservableObject
{
    public SufelTreeNode(SufelSkupina skupina)
    {
        Skupina = skupina;
        DisplayName = skupina.Nazov;

        foreach (var part in skupina.EnumeratePartsInDisplayOrder())
            PartEntries.Add(new PartTreeEntry(part));
    }

    public SufelSkupina Skupina { get; }

    public string DisplayName { get; }

    public bool IsExpanded { get; set; } = true;

    public ObservableCollection<PartTreeEntry> PartEntries { get; } = new();

    [ObservableProperty]
    private bool _isConfigured;

    public void Refresh(AssemblyStore store)
    {
        foreach (var entry in PartEntries)
            entry.Refresh(store);

        IsConfigured = Skupina.JeKompletna && PartEntries.Count > 0 && PartEntries.All(e => e.IsConfigured);
    }
}
