# Gamepad Mapping

A Windows desktop app that maps **XInput** gamepad input to **keyboard and mouse** output so you can play PC games with a controller profile tuned per title. It uses a **foreground target** (optional process name) so mappings can apply only while your game is focused.

## Features

- **Profile templates** — JSON profiles under `Assets/Profiles/templates` define bindings, display metadata, and optional combo behavior.
- **Bindings** — Buttons, triggers, and thumbsticks map to virtual keys; supports **press / release / tap** triggers, analog thresholds, and **hold vs. tap** dual outputs (`holdKeyboardKey`, `holdThresholdMs`).
- **Chords and combos** — Multi-button chords with modifier grace and **combo lead** semantics (`comboLeadButtons`); optional **combo HUD** overlay while you play.
- **Process targeting** — `targetProcessName` gates output to when a named executable is in the foreground (helps avoid typing into the wrong window).
- **Elevation awareness** — If the focused app is running **as administrator**, the app can prompt to relaunch elevated so Windows does not block synthetic input (UIPI).

## Requirements

- **OS:** Windows (WPF host).
- **.NET SDK:** [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) (target framework `net9.0-windows`).
- **Gamepad:** XInput-compatible controller (via [Vortice.XInput](https://github.com/amerkoleci/Vortice.Windows)).

## Build and run

From the repository root:

```powershell
dotnet build "Gamepad Mapping.csproj" -c Release
dotnet run --project "Gamepad Mapping.csproj"
```

Release binaries include `Assets\Config` and `Assets\Profiles` content via `CopyToOutputDirectory`; run from the output folder or ensure the working directory can resolve those paths (see **Paths** below).

## Configuration

On first run, **`Assets/Config/default_settings.json`** is copied to **`Assets/Config/local_settings.json`**. Edit `local_settings.json` to persist your preferences; defaults ship in `default_settings.json`.

| Setting | Purpose |
|--------|---------|
| `templatesDirectory` | Folder (relative to content root) for profile `*.json` files. |
| `defaultGameId` | Profile id (filename stem) used as the default template. |
| `lastSelectedTemplateProfileId` | Last template chosen in the UI; restored on launch. |
| `modifierGraceMs` | Shared timing for chord modifier grace, combo HUD delay, and hold-threshold fallback. |
| `leadKeyReleaseSuppressMs` | For combo lead buttons: suppress stray solo release / short hold when cancelling a combo. |

## Profile format (templates)

Each template is one JSON file: `{profileId}.json` with a root object similar to:

- `profileId`, `gameId`, `displayName` / `displayNameKey`
- `targetProcessName` (optional) — process **base name** (typically without `.exe`), matching `Process.ProcessName`
- `comboLeadButtons` (optional) — XInput button names (e.g. `LeftShoulder`) for combo lead behavior
- `mappings` — list of entries with `from` (`type`, `value`), `keyboardKey`, `trigger`, optional `analogThreshold`, `holdKeyboardKey`, `holdThresholdMs`, `description`, `descriptionKey`

Shipped examples live in `Assets/Profiles/templates` (for example `default.json`, `flight_sim.json`, `roco-kingdom-world__roco-kingdom.json`).

## UI and localization

The shell follows the **Windows light/dark** apps setting. Strings support **resource-based localization** (`Resources/Strings*.resx`); the app wires a default culture at startup for translated template keys (`displayNameKey`, `descriptionKey`).

## Paths (content root)

The app resolves a **content root** by looking for `Assets/Config/default_settings.json` starting from the current directory, `AppContext.BaseDirectory`, and parent folders—so development runs from the project folder and published runs from the folder that contains the copied `Assets` tree.

## Tech stack

- **WPF** — UI
- **[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)** — MVVM helpers
- **[Newtonsoft.Json](https://www.newtonsoft.com/json)** — Profile and settings serialization
- **[InputSimulatorPlus](https://www.nuget.org/packages/InputSimulatorPlus)** — Synthetic keyboard/mouse input
- **[Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput)** — Gamepad reading

## License

This project is licensed under the [MIT License](LICENSE).
