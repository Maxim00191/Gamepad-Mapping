# Gamepad Mapping

**简体中文:** [README_zh.md](README_zh.md)

A Windows desktop app that maps **XInput** gamepad input to **keyboard and mouse** so you can play PC games with per-title controller profiles. Optionally limit output to a **foreground target** (game process name) so mappings do not fire in other windows.

**Quick facts**

- **Input:** XInput-compatible controllers.
- **Output:** Simulated keyboard and mouse (see [Elevation awareness](#features) for admin-target games).
- **Profiles:** JSON templates under `Assets/Profiles/templates`; global tuning in `Assets/Config/local_settings.json`.

## Features

- **Profile templates** — JSON files define bindings, display metadata, and optional combo behavior.
- **Bindings** — Buttons, triggers, and thumbsticks map to keys; **press / release / tap** triggers, analog thresholds, and **hold vs. tap** pairs (`holdKeyboardKey`, `holdThresholdMs`).
- **Chords and combos** — Multi-button chords with modifier grace and **combo lead** semantics (`comboLeadButtons`); optional **combo HUD** while playing.
- **Application settings** — Top-bar panel for global timing, analog defaults, polling, and keyboard emulation; values persist in `Assets/Config/local_settings.json` (separate from profile JSON).
- **Process targeting** — `targetProcessName` limits mapping to when a named executable (typically without `.exe`) is in the foreground.
- **Elevation awareness** — If the focused app runs **as administrator**, the app can offer to relaunch elevated so Windows does not block synthetic input (UIPI).

## Requirements

- **OS:** Windows (WPF).
- **.NET SDK:** [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) (`net9.0-windows`).
- **Gamepad:** XInput-compatible controller ([Vortice.XInput](https://github.com/amerkoleci/Vortice.Windows)).

## Build and run

From the repository root (powershell or any shell):

```powershell
dotnet build "Gamepad Mapping.csproj" -c Release
dotnet run --project "Gamepad Mapping.csproj"
```

You can also open `GamepadMapping.sln` in Visual Studio and run the startup project.

Release and Debug outputs copy `Assets\Config` and `Assets\Profiles` into the build folder (`CopyToOutputDirectory`). Run the app with working directory set to a folder that contains the `Assets` tree (the project root in dev, or `bin\...\net9.0-windows\` after build). See [Paths (content root)](#paths-content-root).

### Tests

```powershell
dotnet test "GamepadMapping.sln" -c Release
```

## Configuration

On first launch, if `Assets/Config/local_settings.json` is missing, the app copies `Assets/Config/default_settings.json` to `local_settings.json`. Global options are read and written in **`local_settings.json`**. **Bindings** (mappings, process name, combo leads) live only in **`Assets/Profiles/templates/{profileId}.json`**, not in app settings.

### Application settings (in-app)

In the **top bar**, open **Application settings** (to the right of the template dropdown and the “new profile” (+) button). The panel has two groups:

1. **Timing & keyboard**
   - **Modifier grace / combo HUD delay (`modifierGraceMs`)** — Chord modifier grace, combo HUD timing, and **fallback** hold duration for tap/hold bindings when `holdThresholdMs` is not set on a mapping.
   - **Lead key long-hold suppress (`leadKeyReleaseSuppressMs`)** — For combo lead buttons: suppress stray solo release / short-hold output when you cancel a long hold without finishing a combo path.
   - **Keyboard tap hold (`keyboardTapHoldDurationMs`)** — How long a simulated key stays down for a tap (clamped in-app).
   - **Tap repeat gap (`tapInterKeyDelayMs`)** — Delay between repeated taps when the mapper sends multiple taps.
   - **Text between chars (`textInterCharDelayMs`)** — Delay between characters when sending text via the keyboard emulator.
2. **Analog & polling**
   - **Default analog threshold (`defaultAnalogActivationThreshold`)** — Normalized **0–1** default for stick/trigger mappings without `analogThreshold`.
   - **Mouse-look sensitivity (`mouseLookSensitivity`)** — Default scale for mouse-look when a mapping does not override it.
   - **Analog change epsilon (`analogChangeEpsilon`)** — Minimum change before analog input is treated as updated (smaller = more updates).
   - **Gamepad poll interval (`gamepadPollingIntervalMs`)** — Milliseconds between XInput polls (lower = snappier, higher = less CPU).

Edits save to `local_settings.json` as you change them. **Thumbstick deadzones** for live reading are adjusted under **Live Gamepad Monitor → Analog** (gear icon); those map to `leftThumbstickDeadzone` and `rightThumbstickDeadzone` in settings, aligned with the shared default `thumbstickDeadzone` when per-stick values are unset.

### Settings reference (`local_settings.json`)

| Setting | Purpose |
| -------- | -------- |
| `templatesDirectory` | Folder (relative to content root) for profile `*.json` files. |
| `defaultProfileId` | Profile id (filename stem) used as the default template. |
| `lastSelectedTemplateProfileId` | Last template selected in the UI; restored on launch. |
| `modifierGraceMs` | Chord grace, combo HUD delay, and hold-threshold fallback. |
| `leadKeyReleaseSuppressMs` | Combo lead: suppress stray solo release / short hold when cancelling. |
| `thumbstickDeadzone` | Default normalized deadzone when per-stick values are not set. |
| `leftThumbstickDeadzone` | Left stick deadzone `[0–1]` (Live Gamepad Monitor). |
| `rightThumbstickDeadzone` | Right stick deadzone `[0–1]` (Live Gamepad Monitor). |
| `leftTriggerInnerDeadzone` | Left trigger inner deadzone (normalized); values at or below map to 0. |
| `leftTriggerOuterDeadzone` | Left trigger outer threshold; values at or above map to full pull. |
| `rightTriggerInnerDeadzone` | Right trigger inner deadzone (same semantics as left). |
| `rightTriggerOuterDeadzone` | Right trigger outer threshold (same semantics as left). |
| `gamepadPollingIntervalMs` | Delay between gamepad polls (ms). |
| `defaultAnalogActivationThreshold` | Default `0–1` activation for mappings without `analogThreshold`. |
| `mouseLookSensitivity` | Default mouse-look sensitivity scaler. |
| `analogChangeEpsilon` | Minimum analog delta to treat input as changed. |
| `keyboardTapHoldDurationMs` | Simulated key-down duration for taps (ms). |
| `tapInterKeyDelayMs` | Delay between repeated taps (ms). |
| `textInterCharDelayMs` | Delay between typed characters (ms). |

## Profile format (templates)

Each file is `{profileId}.json` with a root object such as:

- `profileId`, `templateGroupId`, `displayName`, optional `displayNames` (per-culture map, e.g. `"zh-CN"`), optional `displayNameKey` (looks up `Resources/Strings*.resx`)
- `targetProcessName` (optional) — process **base name** (usually without `.exe`), same as `Process.ProcessName`
- `comboLeadButtons` (optional) — XInput button names (e.g. `LeftShoulder`)
- `mappings` — list of entries with `from` (`type`, `value`), `keyboardKey`, `trigger`, optional `analogThreshold`, `holdKeyboardKey`, `holdThresholdMs`, `description`, optional `descriptions` (per-culture), optional `descriptionKey` (resx)

Examples ship in `Assets/Profiles/templates` (e.g. `default.json`, `flight_sim.json`, `roco-kingdom-world.json`).

## UI and localization

The shell follows the **Windows** light/dark apps setting. UI strings use **resource-based** localization (`Resources/Strings*.resx`). Template titles and mapping descriptions can use optional **`displayNames` / `descriptions` maps in the profile JSON** (recommended for user templates); `displayNameKey` / `descriptionKey` still resolve through `.resx` when present. Startup sets culture for all of the above.

## Paths (content root)

The app finds **content root** by locating `Assets/Config/default_settings.json` starting from the current directory, `AppContext.BaseDirectory`, and parent folders—so development runs from the project folder and published layouts work when the copied `Assets` tree sits beside the executable.

## Tech stack

- **WPF** — UI
- **[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)** — MVVM helpers
- **[Newtonsoft.Json](https://www.newtonsoft.com/json)** — Profiles and settings
- **[InputSimulatorPlus](https://www.nuget.org/packages/InputSimulatorPlus)** — Synthetic keyboard/mouse
- **[Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput)** — Gamepad input

## CI/CD

[GitHub Actions](.github/workflows/build.yml) runs on every push and pull request to `main` (`dotnet build` Release). Pushing a tag matching `v*` (e.g. `v1.4.0`) **publishes** two `win-x64` artifacts and creates a **GitHub Release** ([softprops/action-gh-release](https://github.com/softprops/action-gh-release)):

- **`Gamepad-Mapping-<tag>-win-x64-single.zip`** — self-contained **single-file** build (no separate runtime install).
- **`Gamepad-Mapping-<tag>-win-x64-fx.zip`** — **framework-dependent** build (smaller; requires [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) on the machine).

```powershell
git tag v1.4.0
git push origin v1.4.0
```

## License

This project is licensed under the [MIT License](LICENSE).
