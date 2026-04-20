# Gamepad Mapping

**简体中文:** [README_zh.md](README_zh.md)

Gamepad Mapping is a Windows desktop app (WPF) that maps **XInput-compatible gamepad input** to **keyboard and mouse output** using editable JSON profile templates. It targets games that lack native controller support: you work in one workspace with an SVG **Visual Editor**, mapping lists, keyboard-action and radial-menu catalogs, optional **foreground process** targeting, **Community** template download and upload, and **in-app updates** from GitHub.

## Trust and network access

> [!TIP]
> **How this app uses the internet**
>
> - **Community templates (download):** Opening the **Community** tab and choosing **Refresh** downloads the public catalog index over HTTPS (GitHub Raw with a CDN fallback). Choosing **Download** on an entry fetches **JSON profile templates** into your local templates folder. The app parses that JSON locally; it does not execute remote code from those files.
> - **Community templates (upload):** **Upload…** sends your bundle and metadata over HTTPS to the configured **Cloudflare Worker**, after a **Cloudflare Turnstile** verification step in the upload dialog. The Worker creates a **pull request** on GitHub; your GitHub account token is **not** stored in the app.
> - **Application updates:** The **Updates** section contacts **GitHub** (release metadata and release assets) to check for new versions, optionally download the published installer, and verify package integrity before the install handoff.

> [!WARNING]
> **Third-party or modified installers**
>
> Unofficial mirrors, repackaged builds, or “portable” redistributions can **change how updating works**, **swap download URLs**, or bundle **malware**—including software that logs keystrokes or other input. The developers only support binaries that match the open-source tree. **Get the app from this repository’s [Releases](https://github.com/Maxim00191/Gamepad-Mapping/releases) or build it yourself** from a verified clone of this repo.

## Pricing and official distribution

Gamepad Mapping is **free, open-source software** under the MIT License. Official releases are published on GitHub at no charge. The developers do not sell the app, license keys, or paid tiers through third-party stores, resellers, or paywalled download pages.

**If someone asks you to pay for downloads, activation, license keys, or “premium” access to this software, treat that as spam or a scam** and obtain installers only from this repository’s [Releases](https://github.com/Maxim00191/Gamepad-Mapping/releases) (or builds you compile yourself from source).

## Features

### Mapping and output

- Read input from XInput-compatible controllers; synthesize keyboard and mouse via classic Win32 **`SendInput`** or, on supported builds, **Windows Input Injection** (configurable).
- **Button, trigger, and stick** bindings: press, release, tap, hold, thresholds, and analog activation rules.
- **Chords and combos:** combo-lead behavior, modifier grace windows, optional HUD hints, and **keyboard chord** output for shortcut-style actions.
- **Radial menu actions:** directional action selection from controller input with a customizable HUD.
- **Foreground process filter:** restrict mapping output to when a chosen process is focused.
- **Feel tuning:** thumbstick **deadzone shape** (axial vs radial), **mouse-look** sensitivity and smoothing, and optional **human-like** cursor movement noise (in settings).

### Profile workspace

- **Template-based profiles:** each profile is a validated JSON file under `Assets/Profiles/templates`.
- **Visual Editor (SVG):** interactive controller art with hit-tested regions, **pan/zoom**, optional **overlay labels** (action summary vs physical names), and a side panel listing mappings for the selected control.
- **Mappings** tab plus built-in **keyboard actions** and **radial menu** catalogs.
- **Gamepad monitor:** live controller state in a readable layout.

### Community templates

- **Browse and download** public templates from the in-app **Community** tab; the catalog is loaded from [`GamepadMapping-CommunityProfiles`](https://github.com/Maxim00191/GamepadMapping-CommunityProfiles) (GitHub Raw, with jsDelivr CDN fallback when needed).
- **Upload** your own layout: guided metadata, automated pre-submit checks, then submission through a **Cloudflare Worker** with **Turnstile**; on success, a **pull request** is opened for review (no personal GitHub token in the client). See [Community templates](#community-templates-workflow) below for steps and advanced configuration.

### Updates, settings, and Windows integration

- **In-app updates:** the **Updates** section resolves GitHub release metadata, downloads release assets with progress, verifies integrity, and hands off to installation (with sensible fallbacks for network issues).
- **Elevation:** when targets run as administrator, the app can prompt to relaunch with matching elevation so synthetic input is not blocked by UIPI.
- **Global settings** are stored in `Assets/Config/local_settings.json` (created on first run); factory defaults live in `Assets/Config/default_settings.json`.

## Release notes

Per-version changes are listed in [`CHANGELOG.md`](CHANGELOG.md). Installers and release assets are on **[GitHub Releases](https://github.com/Maxim00191/Gamepad-Mapping/releases)**.

## Community templates workflow

Open a profile in the main window, then select the **Community** tab.

**Browse and install:** Use **Refresh** to load the latest catalog from the network, then **Download** on an entry to save that template into your local templates folder (same JSON schema as built-in templates). After a successful download, the profile list updates so you can pick the new template.

**Share your layout:** Choose **Upload…** (with a template selected in the profile picker). The app collects linked templates in your bundle, walks you through **game folder**, **author**, and **listing description**, runs **compliance checks** (mappings, IDs, size limits, etc.), then submits through a **Cloudflare Worker** using a **Cloudflare Turnstile** ticket. On success, it opens a **pull request** against [`GamepadMapping-CommunityProfiles`](https://github.com/Maxim00191/GamepadMapping-CommunityProfiles) for developers to review—no personal GitHub token is required in the client.

Contributions and catalog rules live in the community repository; the app does not ship catalog JSON—it is fetched at runtime. Very frequent refreshes are throttled to avoid hammering the index endpoint.

**Advanced:** Default upload endpoint and Turnstile action live in `Assets/Config/default_settings.json` (`communityProfilesUploadWorkerUrl`, `communityProfilesUploadTurnstileAction`). Override them in `Assets/Config/local_settings.json` if you run your own Worker stack.

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

## Developer documentation

- [`docs/input-pipeline.md`](docs/input-pipeline.md): how input frames flow through middleware and mapping.
- [`docs/emulation-parity.md`](docs/emulation-parity.md): behavior notes across emulation backends.

## Template schema (overview)

Each template generally includes:

- `profileId`, `templateGroupId`, `displayName` / `displayNames`
- `targetProcessName` (optional)
- `comboLeadButtons` (optional)
- `mappings` collection with input source + output action definitions
- `keyboardActions` is where all in-game key bindings (actions) are defined
- `radialMenus` defines bindings for radial (wheel) menus

See examples in:

- `Assets/Profiles/templates/Roco Kingdom/explore-maxim0191.json`
- `Assets/Profiles/templates/Roco Kingdom/fight-maxim0191.json`

## Elevation and UIPI

Windows can block synthetic input when the target app runs as administrator (UIPI). When needed, Gamepad Mapping can prompt to relaunch elevated so mappings continue to work against admin-level targets.

## Tech stack

- WPF (.NET 9)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput) for reading controllers
- Win32 **`SendInput`** for synthesized keyboard/mouse by default, with optional **Windows Input Injection** on supported OS builds

## CI/CD

[GitHub Actions](.github/workflows/build.yml) validates build/test on push and PR to `main`.  
Tagging versions (`v*`) creates release artifacts (single-file and framework-dependent win-x64 packages) and publishes a GitHub Release.

## License

MIT — see [`LICENSE`](LICENSE).
