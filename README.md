# MaestroGenXcs

Nástupca [MaestroGenratorXcs](../MaestroGenratorXcs/) – skladanie zostavy na boku, batch export XCS.

**Stav:** R0–R10 hotové (MVP refaktor). Panel výšky + ťahanie v 3D okne zostavy, smoke test pre XCS vzor.

Legacy projekt `MaestroGenratorXcs` zostáva **nedotknutý** – zmeny sú len v tomto priečinku.

## Spustenie

```bash
dotnet run --project MaestroGenXcs/MaestroGenXcs.csproj
```

## Automatické testy

```bash
dotnet run --project MaestroGenXcs.SmokeTest/MaestroGenXcs.SmokeTest.csproj -c Release
```

Kontroluje: XCS jadro operácií (vzor `VzoryXcs/Polica-pevna.txt`), `AssemblyStore`, `AssemblySolverApplier`, export 3 súborov. Pri úspechu exit code **0**.

## Ďalšie fázy

Pozri `NEDOLADENE.md` (import, ribbon, presnejší 3D layout).
