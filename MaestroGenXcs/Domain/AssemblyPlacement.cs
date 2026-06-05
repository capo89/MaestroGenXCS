using CommunityToolkit.Mvvm.ComponentModel;

namespace MaestroGenXcs.Domain;

/// <summary>
/// Umiestnenie dielca voči referenčnému boku v zostave (rovina Top boku L).
/// <see cref="OffsetY"/> = os X na ploche Top boku (mm); pri vloženom dne/vrchu môže byť záporná (−dz…0 u vrchu).
/// <see cref="OffsetDepthMm"/> = os Y (od prednej hrany boku, mm).
/// </summary>
public sealed partial class AssemblyPlacement : ObservableObject
{
    [ObservableProperty]
    private Part _part = null!;

    /// <summary>Kontaktná plocha voči Top referenčného boku (Left/Right/…).</summary>
    [ObservableProperty]
    private PartFace _anchorFace = PartFace.Left;

    /// <summary>Poloha ľavého predného rohu dielca na ploche Top (os X, mm).</summary>
    [ObservableProperty]
    private double _offsetY;

    /// <summary>Poloha ľavého predného rohu na ploche Top (os Y / hĺbka, mm).</summary>
    [ObservableProperty]
    private double _offsetDepthMm;

    /// <summary>Dielec je vložený a viditeľný v 3D okne skladania.</summary>
    [ObservableProperty]
    private bool _isPlacedInScene;

    [ObservableProperty]
    private bool _isLocked;
}
