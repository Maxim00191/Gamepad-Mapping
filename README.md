

# Gamepad Mapping

A Windows desktop app that maps **XInput** gamepad input to **keyboard and mouse** output so you can play PC games with a controller profile tuned per title. It uses a **foreground target** (optional process name) so mappings can apply only while your game is focused.

## Features

- **Profile templates** — JSON profiles under `Assets/Profiles/templates` define bindings, display metadata, and optional combo behavior.
- **Bindings** — Buttons, triggers, and thumbsticks map to virtual keys; supports **press / release / tap** triggers, analog thresholds, and **hold vs. tap** dual outputs (`holdKeyboardKey`, `holdThresholdMs`).
- **Chords and combos** — Multi-button chords with modifier grace and **combo lead** semantics (`comboLeadButtons`); optional **combo HUD** overlay while you play.
- **Application settings** — **Application settings** in the top bar edits global timing, analog defaults, polling, and keyboard emulation tuning; values persist in `Assets/Config/local_settings.json` (separate from per-game profile JSON).
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

On first run, `**Assets/Config/default_settings.json`** is copied to `**Assets/Config/local_settings.json**`. The app reads and writes `**local_settings.json**` for global options. **Profile bindings** (mappings, process name, combo leads) live in `**Assets/Profiles/templates/{profileId}.json`**—they are not stored in app settings.

### Application settings (in-app)

Use the **Application settings** control in the **top bar**, to the right of the template dropdown and the “create new profile” (+) button. It opens a panel with two groups:

1. **Timing & keyboard**
  - **Modifier grace / combo HUD delay (`modifierGraceMs`)** — Milliseconds used for chord modifier grace, when the combo HUD appears, and as the **fallback** hold duration for tap/hold dual bindings when a mapping does not set `holdThresholdMs`.
  - **Lead key long-hold suppress (`leadKeyReleaseSuppressMs`)** — For **combo lead** buttons (from `comboLeadButtons` or inferred): if you hold longer than this and release without completing a combo path, solo “released” / short-hold outputs can be suppressed so cancelling a combo does not fire stray keys.
  - **Keyboard tap hold (`keyboardTapHoldDurationMs`)** — How long a simulated key stays down for a tap (clamped to a safe range in the app).
  - **Tap repeat gap (`tapInterKeyDelayMs`)** — Optional delay between repeated taps when the mapper issues multiple taps.
  - **Text between chars (`textInterCharDelayMs`)** — Optional delay between characters when sending text via the keyboard emulator.
2. **Analog & polling**
  - **Default analog threshold (`defaultAnalogActivationThreshold`)** — Normalized **0–1** default for stick/trigger mappings that do not set `analogThreshold` on the binding.
  - **Mouse-look sensitivity (`mouseLookSensitivity`)** — Scale for right-stick (or configured) mouse-look output when no per-mapping override exists.
  - **Analog change epsilon (`analogChangeEpsilon`)** — How much analog values must change before the reader treats the state as updated (smaller = more sensitive, more updates).
  - **Gamepad poll interval (`gamepadPollingIntervalMs`)** — Milliseconds between XInput polls; lower is snappier, higher uses less CPU.

Changes from this panel are **saved to `local_settings.json` as you edit** (same schema as below). **Thumbstick deadzones** for the live reader are adjusted under **Live Gamepad Monitor → Analog** (gear icon); those values are also written to `**leftThumbstickDeadzone` / `rightThumbstickDeadzone`** (and align with the shared default `**thumbstickDeadzone**` when per-stick values are unset).


| Setting                            | Purpose                                                                                   |
| ---------------------------------- | ----------------------------------------------------------------------------------------- |
| `templatesDirectory`               | Folder (relative to content root) for profile `*.json` files.                             |
| `defaultGameId`                    | Profile id (filename stem) used as the default template.                                  |
| `lastSelectedTemplateProfileId`    | Last template chosen in the UI; restored on launch.                                       |
| `modifierGraceMs`                  | Shared timing for chord modifier grace, combo HUD delay, and hold-threshold fallback.     |
| `leadKeyReleaseSuppressMs`         | For combo lead buttons: suppress stray solo release / short hold when cancelling a combo. |
| `thumbstickDeadzone`               | Default normalized deadzone for thumbsticks when per-stick values are not set.            |
| `leftThumbstickDeadzone`           | Left stick deadzone `[0–1]` (UI: Live Gamepad Monitor).                                   |
| `rightThumbstickDeadzone`          | Right stick deadzone `[0–1]` (UI: Live Gamepad Monitor).                                  |
| `gamepadPollingIntervalMs`         | Delay between gamepad polls (ms).                                                         |
| `defaultAnalogActivationThreshold` | Default analog activation threshold `0–1` for mappings without `analogThreshold`.         |
| `mouseLookSensitivity`             | Default mouse-look sensitivity scaler.                                                    |
| `analogChangeEpsilon`              | Minimum analog delta to treat input as changed.                                           |
| `keyboardTapHoldDurationMs`        | Simulated key-down duration for taps (ms).                                                |
| `tapInterKeyDelayMs`               | Delay between repeated taps (ms).                                                         |
| `textInterCharDelayMs`             | Delay between typed characters (ms).                                                      |


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

## CI/CD

[GitHub Actions](.github/workflows/build.yml) runs on every push and pull request to `main` (`**dotnet build`** Release). When you push an annotated or lightweight tag named like `v1.0.0`, the workflow also **publishes** a **self-contained** `win-x64` build, zips it, and creates a **GitHub Release** with that zip attached (via [softprops/action-gh-release](https://github.com/softprops/action-gh-release)).

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## License

This project is licensed under the [MIT License](LICENSE).