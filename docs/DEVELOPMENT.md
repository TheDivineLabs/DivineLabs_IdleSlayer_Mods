# Development

## Projects

- `src/Launcher`: WPF launcher and maintenance windows.
- `src/DesktopUI`: shared desktop styles and controls.
- `src/Runtime`: Steam discovery, component verification, installation and recovery.
- `src/Installer` and `src/Bootstrap`: self-contained native Windows entry points.
- `src/Mod` and `src/Contracts`: game features, in-game UI, settings and IPC.
- `DivineLabs_Loader`: pinned MelonLoader fork; see its `UPSTREAM.txt` and license.

## Build

Use Windows x64, .NET 10 SDK and C++ Build Tools with the Windows SDK for NativeAOT.
A full game-component build also needs a local Steam installation and generated
IL2CPP references. Do not commit game binaries, generated references or saves.

```powershell
./scripts/Build.ps1 -GamePath 'W:\steam\steamapps\common\Idle Slayer' -Dotnet 'W:\DEV\tools\dotnet\dotnet.exe'
```

For a desktop-only release, reuse a verified local distribution:

```powershell
./scripts/Build-DesktopRelease.ps1 -BasePortable 'W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.0\DivineLabs-IdleSlayer-0.5.0-portable.zip'
```

Keep the matching `SHA256SUMS.txt` and components ZIP beside that base archive.
For a relocated C++ toolchain, pass `-NativeBin` and `-NativeLib`. Outputs go to
`W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods`; scratch work goes to `W:\DEV\.cache\temp`.

Application and game-component versions are independent. Desktop 0.5.2 uses
components 0.5.0. The download URL and SHA256 are pinned in the runtime package index.
Each current release must host its own component archive before older releases are removed.

## Checks

Choose checks for the affected behavior; these commands are entry points, not
a requirement to run every scenario for every edit.

- `tests/Checks.csproj`: settings, arithmetic, IPC and installation contracts.
- `tests/CopyRemoval`: removal boundaries and retained user files.
- `tests/Layout`: shown WPF windows, routed-event checks and renders.
- `scripts/CheckPortable.ps1`: root EXE, package integrity and relocation.
- `scripts/Check-CopyRemoval.ps1`: isolated installations and removal.

Latest release evidence: [validation](VALIDATION-0.5.2.md).
Previous reports remain in this directory; [the previous README](archive/README-0.5.1.ru.md)
is an archived development snapshot, not current installation guidance.

## Android

Android work is experimental and is not part of the Windows release.
See [Android status and build notes](../android/README.ru.md).

## Dependencies and artwork

DivineLabs code is MIT. Upstream dependencies and game artwork retain their own
terms: [third-party notices](../THIRD-PARTY-NOTICES.txt), [licenses](../licenses).
The game itself is not distributed.
