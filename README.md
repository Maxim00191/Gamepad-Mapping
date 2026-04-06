# Gamepad Mapping

**简体中文:** [README_zh.md](README_zh.md)

Gamepad Mapping is a Windows WPF app that converts **XInput controller input** into **keyboard and mouse output** through editable profile templates.  
It is designed for games that do not have good native controller support, with optional foreground-process targeting to avoid accidental input in other windows.

## What's new in 2.0

- New **radial menu system** with customizable HUD style (color, opacity, scale).
- Expanded **action model** for profile mappings (including richer keyboard/radial workflows).
- Improved **input pipeline behavior** for multi-stage radial interactions and conflict handling.
- Better **template validation** and updated built-in templates (especially Roco Kingdom world/fight sets).
- Stabilized UI rendering and layout behavior for radial-related screens.

## Core capabilities

- **Template-based mappings**: each profile is a JSON file under `Assets/Profiles/templates`.
- **Button / trigger / stick bindings**: supports press, release, tap, hold, thresholds, and analog activation rules.
- **Chord and combo handling**: combo-lead behavior, modifier grace windows, and optional HUD hints.
- **Radial menu actions**: trigger directional action selection from controller input.
- **Foreground process filter**: map output only when a specific process is focused.
- **Application settings**: centralized global settings saved to `Assets/Config/local_settings.json`.

## Requirements

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) for building (`net9.0-windows`)
- XInput-compatible controller

## Quick start

```powershell
dotnet restore "Gamepad Mapping.csproj"
dotnet build "Gamepad Mapping.csproj" -c Release
dotnet run --project "Gamepad Mapping.csproj"
```

Or open `GamepadMapping.sln` in Visual Studio and run the startup project.

## Tests

```powershell
dotnet test "GamepadMapping.sln" -c Release
```

## Configuration model

- `Assets/Config/default_settings.json`: factory defaults.
- `Assets/Config/local_settings.json`: user-overridden global settings (auto-created on first run).
- `Assets/Profiles/templates/*.json`: profile templates (mappings, labels, combo metadata, target process).

## Template schema (overview)

Each template generally includes:

- `profileId`, `templateGroupId`, `displayName` / `displayNames`
- `targetProcessName` (optional)
- `comboLeadButtons` (optional)
- `mappings` collection with input source + output action definitions

See examples in:

- `Assets/Profiles/templates/default.json`
- `Assets/Profiles/templates/flight_sim.json`
- `Assets/Profiles/templates/roco-kingdom-world.json`
- `Assets/Profiles/templates/roco-kingdom-world-fight.json`

## Elevation behavior

Windows can block synthetic input when the target app runs as administrator (UIPI).  
When needed, Gamepad Mapping can prompt to relaunch elevated so mappings continue to work against admin-level targets.

## Tech stack

- WPF (.NET 9)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [InputSimulatorPlus](https://www.nuget.org/packages/InputSimulatorPlus)
- [Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput)

## CI/CD

[GitHub Actions](.github/workflows/build.yml) validates build/test on push and PR to `main`.  
Tagging versions (`v*`) creates release artifacts (single-file and framework-dependent win-x64 packages) and publishes a GitHub Release.

## Changelog

See [`CHANGELOG.md`](CHANGELOG.md) for release history.

## License

MIT — see [`LICENSE`](LICENSE).
