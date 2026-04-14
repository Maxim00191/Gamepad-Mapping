## Changelog v2.2.0 - 2026-04-14
### Added
- **Human-like keyboard noise:** Introduced `HumanizingKeyboardEmulator` to apply Perlin-based jitter to keyboard tap-hold durations (±10ms), enhancing anti-detection and natural input feel.
- **Global exception handling:** Added `StartupDiagnostics` for centralized crash reporting, fatal error dialogs, and environment logging (OS, runtime, process path).
- **Dedicated entry point:** Introduced `Program.cs` as the explicit `[STAThread]` entry point to ensure diagnostic handlers are registered before any WPF components initialize.
- **Logging robustness:** Enhanced `FileLogger` with UTF-8 encoding and an emergency fallback to `%TEMP%` if the primary log directory is inaccessible.
- **Path resolution safety:** Improved `AppPaths` with fallback logic to `LocalAppData` for log storage when the application root is read-only.
### Changed
- **Keyboard tap duration:** Increased default tap-hold duration from 30ms to 70ms and expanded the allowed range to 20ms–100ms for better compatibility with modern games.
- **Startup sequence:** Refactored `App.xaml.cs` to wrap the entire startup process in a diagnostic-aware try-catch block.
- **Noise controller expansion:** Updated `IHumanInputNoiseController` and its implementation to support independent phase tracking for tap-hold jitter.

### Fixed
- **Startup path logging:** Fixed a bug where debug path information was logged outside of the main startup try-catch block.
- **UI Tooltips:** Added missing tooltips for human-like noise settings in `AppSettingsView`.
## 更新日志 v2.2.0 - 2026-04-14
### 新增
- **拟人化键盘抖动：** 引入 `HumanizingKeyboardEmulator`，通过 Perlin 噪声为键盘点按时长加入随机抖动（±10ms），增强防检测能力并提升输入自然度。
- **全局异常处理：** 新增 `StartupDiagnostics` 模块，实现集中化的崩溃报告、致命错误对话框以及详细的环境日志记录（操作系统、运行时、进程路径等）。
- **专用入口点：** 引入 `Program.cs` 作为显式的 `[STAThread]` 入口，确保在任何 WPF 组件初始化前优先注册诊断处理程序。
- **日志稳健性：** 增强 `FileLogger`，支持 UTF-8 编码，并在主日志目录不可访问时自动回退到 `%TEMP%` 临时目录。
- **路径解析安全：** 优化 `AppPaths` 逻辑，当程序根目录只读时，日志存储将自动回退至 `LocalAppData`。
### 更改
- **键盘点按时长：** 将默认点按时长从 30ms 提升至 70ms，并将可调范围扩大至 20ms–100ms，以提升与现代游戏的兼容性。
- **启动流程重构：** 重构 `App.xaml.cs`，将整个启动过程包裹在具备诊断能力的 try-catch 块中。
- **噪声控制器扩展：** 更新 `IHumanInputNoiseController` 接口及其实现，支持独立的点按抖动相位跟踪。
### 修复
- **启动路径日志：** 修复了调试路径信息在主启动异常捕获块之外记录的问题。
- **UI 提示：** 为 `AppSettingsView` 中的类人化噪声设置补充了缺失的工具提示。
**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.5...v2.2.0](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.5...v2.2.0)

## Changelog v2.1.5 - 2026-04-12

### Changed

- **Input Injection keyboard:** `InjectedKeyboardSimulator` now takes an optional `ISendInputChannel` (defaulting to `Win32SendInputChannel`) and, when virtual-key-to-scan-code mapping succeeds, emits injected key events using **scan codes** (`InjectedInputKeyOptions.ScanCode`) instead of relying on virtual keys alone—matching the SendInput path’s layout mapping and improving compatibility for extended keys.

## 更新日志 v2.1.5 - 2026-04-12

### 更改

- **Input Injection 键盘：** `InjectedKeyboardSimulator` 现可注入可选的 `ISendInputChannel`（默认 `Win32SendInputChannel`）；在虚拟键到扫描码映射成功时，优先以 **扫描码**（`InjectedInputKeyOptions.ScanCode`）生成注入按键事件，与 SendInput 路径的布局映射更一致，并改善扩展键等场景的兼容性。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.4...v2.1.5](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.4...v2.1.5)

## Changelog v2.1.4 - 2026-04-12

### Added

- **Input pipeline middleware:** Added `AnalogTransitionMiddleware`, `ButtonTransitionMiddleware`, and `InputStateSyncMiddleware` so analog/button transitions and cross-frame state stay consistent inside the input frame pipeline.
- **Gamepad source abstraction:** Introduced `IGamepadSource` / `XInputSource` and refactored `GamepadReader` around `IXInput`, enabling a pluggable gamepad backend with clearer testing seams.
- **Selectable gamepad & emulation backends:** Settings now expose gamepad source selection and keyboard/mouse emulation backend choice (classic `SendInput` vs Windows **Input Injection** on supported OS builds), with localized labels.
- **Win32 emulators:** Replaced the legacy emulators with `Win32KeyboardEmulator` and `Win32MouseEmulator` built on `SendInput`, plus `ISendInputChannel` / `Win32SendInputChannel` for efficient batched `INPUT` submission.
- **Injection simulators:** Added `InjectedKeyboardSimulator` and `InjectedMouseSimulator` for the Input Injection path, wired through `InputEmulationStackFactory` and `InputEmulationServices`.
- **Human-like mouse noise:** Added Perlin-based `NoiseGenerator`, `HumanInputNoiseController`, and `HumanizingMouseEmulator` so mouse movement can apply smooth, configurable jitter; settings include noise parameters and stick-magnitude-aware blending.
- **Unified emulator commands:** Extended `IKeyboardEmulator` / `IMouseEmulator` with `Execute` / `ExecuteAsync` so the mapping stack dispatches structured commands consistently across backends.
- **Thumbstick deadzone shape:** Added `ThumbstickDeadzoneShape` (axial vs radial) with parser support, integrated through `GamepadReader` / analog processors for more predictable stick gates.
- **Mouse-look & analog tuning:** Expanded `AppSettings` and UI for mouse-look sensitivity, smoothing, hysteresis, and magnitude-aware mouse movement; `GamepadThumbstickFromValueCatalog` standardizes editor thumbstick tokens, with `AnalogMappingProcessor` using stick magnitude for mouse-look mappings.
- **Keyboard chord taps:** `InputTokenResolver` and `ButtonMappingProcessor` now support chord-style keyboard taps (e.g. modifier + key) with normalized key text.
- **UI composition:** Split responsibilities out of `MainViewModel` into `ProfileOrchestrator`, `SettingsOrchestrator`, `UpdateOrchestrator`, and `UiOrchestrator`; added `GamepadService`, `MappingManager`, and `AppTargetingState`-aware `AppStatusMonitor` refresh behavior.
- **UI threading contract:** Introduced `IUiSynchronization` / `DispatcherUiSynchronization` (and test `ImmediateUiSynchronization`) to replace ad-hoc `Action<Action>` UI marshaling in radial/mapping code paths.
- **Process name debouncing:** Added `Debouncer` and applied it to process-name edits to reduce redundant targeting work.
- **Gamepad monitor polish:** Refined `GamepadMonitorWindow` / `GamepadMonitorView` layout, sizing, and pixel snapping; improved reopen behavior when the owner window is hidden.
- **Settings & updates UI:** Expanded `AppSettingsView` for new options; moved update UI into `UpdatesPanelView` and simplified `MainView` structure.
- **Developer docs:** Added `docs/input-pipeline.md` and `docs/emulation-parity.md` describing the processing stack and emulation behavior.

### Changed

- **Folder & namespace organization:** Reorganized `Core`, `Interfaces`, `Models`, `Services`, and `Utils` into clearer subfolders (e.g. Infrastructure, Input, Storage, Update) without changing user-facing behavior by itself.
- **Radial menu controller:** Reworked `RadialMenuController` with factory wiring and UI synchronization updates for more maintainable lifecycle and thread usage.
- **Mapping engine:** Integrated middleware, command execution, chord handling, and UI sync updates; adjusted tests across mapping and radial scenarios.

### Fixed

- **Repository hygiene:** Fixed `.gitignore` rules that accidentally excluded `Services/Win32` sources so `Win32InputStructs.cs` and related interop ship in CI builds.
- **Tests:** Corrected mock/service namespaces (`ISettingsService`, `IWin32Service`) and expanded coverage for new emulation, mapping, and service behaviors.

## 更新日志 v2.1.4 - 2026-04-12

### 新增

- **输入管线中间件：** 新增 `AnalogTransitionMiddleware`、`ButtonTransitionMiddleware` 与 `InputStateSyncMiddleware`，在输入帧管线内统一处理模拟量/按键过渡与跨帧状态同步。
- **手柄数据源抽象：** 引入 `IGamepadSource` / `XInputSource`，并将 `GamepadReader` 重构为基于 `IXInput`，便于替换后端与单元测试。
- **可选手柄与模拟后端：** 设置中可选择手柄数据源及键鼠模拟实现（经典 `SendInput` 与 Windows **Input Injection**，在受支持系统上），并提供中英文文案。
- **Win32 模拟器：** 以 `Win32KeyboardEmulator`、`Win32MouseEmulator` 替代旧实现，基于 `SendInput`；通过 `ISendInputChannel` / `Win32SendInputChannel` 批量提交 `INPUT`。
- **注入式模拟器：** 新增 `InjectedKeyboardSimulator`、`InjectedMouseSimulator`，经 `InputEmulationStackFactory` 与 `InputEmulationServices` 接入。
- **拟人化鼠标噪声：** 基于 Perlin 的 `NoiseGenerator`、`HumanInputNoiseController` 与 `HumanizingMouseEmulator`，支持可配置抖动；设置中包含噪声参数及与摇杆幅度联动的混合。
- **模拟器统一命令接口：** 为 `IKeyboardEmulator` / `IMouseEmulator` 增加 `Execute` / `ExecuteAsync`，映射栈以统一方式调度各后端。
- **摇杆死区形状：** 新增 `ThumbstickDeadzoneShape`（轴向/径向）及解析支持，并贯通 `GamepadReader` 与模拟量处理，使死区行为更可控。
- **鼠标视角与模拟量调节：** 扩展 `AppSettings` 与界面（鼠标视角灵敏度、平滑、迟滞及与幅度相关的鼠标移动）；`GamepadThumbstickFromValueCatalog` 统一编辑器摇杆取值，`AnalogMappingProcessor` 对鼠标视角类映射使用摇杆幅度。
- **组合键点按：** `InputTokenResolver` 与 `ButtonMappingProcessor` 支持组合键形式的键盘点按（如修饰键+字母），并规范化按键文本。
- **界面职责拆分：** 将原 `MainViewModel` 中的职责拆至 `ProfileOrchestrator`、`SettingsOrchestrator`、`UpdateOrchestrator`、`UiOrchestrator`；新增 `GamepadService`、`MappingManager`，`AppStatusMonitor` 结合 `AppTargetingState` 优化状态刷新。
- **UI 线程契约：** 引入 `IUiSynchronization` / `DispatcherUiSynchronization`（及测试用 `ImmediateUiSynchronization`），替代轮盘/映射路径中的 `Action<Action>` 式 UI 投递。
- **进程名防抖：** 新增 `Debouncer` 并用于进程名编辑，减少无效的目标进程解析。
- **手柄监视器：** 优化 `GamepadMonitorWindow` / `GamepadMonitorView` 布局、尺寸与像素对齐；改进主窗口不可见时的再次打开体验。
- **设置与更新界面：** 扩展 `AppSettingsView`；将更新相关 UI 独立为 `UpdatesPanelView` 并精简 `MainView`。
- **开发文档：** 新增 `docs/input-pipeline.md` 与 `docs/emulation-parity.md`，说明处理管线与模拟行为。

### 更改

- **目录与命名空间：** 调整 `Core`、`Interfaces`、`Models`、`Services`、`Utils` 子目录划分（如 Infrastructure、Input、Storage、Update），不改变对外功能语义。
- **轮盘控制器：** 重构 `RadialMenuController`，配合工厂装配与 UI 同步，生命周期与线程使用更清晰。
- **映射引擎：** 接入中间件、命令执行、组合键处理与 UI 同步变更，并同步更新相关单元测试。

### 修复

- **仓库忽略规则：** 修正误排除 `Services/Win32` 下源的 `.gitignore`，确保 `Win32InputStructs.cs` 等互操作代码进入 CI 构建。
- **测试：** 修正 mock/服务命名空间（`ISettingsService`、`IWin32Service`），并补充新模拟、映射与服务行为的测试覆盖。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.3...v2.1.4](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.3...v2.1.4)

## Changelog v2.1.3 - 2026-04-10

### Added

- **Persistent Update Results:** The updater now records a structured result (`update-last-result.json`) upon completion, enabling the application to detect both successful and failed update attempts.
- **Update Failure Notifications:** Implemented a persistent notification system for update failures. If an update fails after the application shuts down, the user will now see a "Try Again" corner toast on the next manual launch.
- **Unified Notification Service:** Introduced `IUpdateNotificationService` to centralize the lifecycle of update-related feedback, replacing the previous success-only implementation.
- **Debug UI Testing:** Added hidden developer flags (`--debug-update-success`, `--debug-update-failed`) available in debug builds to verify corner toast UI without performing a real installation.
- **Enhanced Command-Line Parsing:** Robustified argument detection for the `--updated` flag to support various shell-escaped formats.

### Changed

- **UI Lifecycle Optimization:** Refactored `UpdateViewModel` to remove active background polling for status files. The application now uses a more efficient check-once-on-startup model for pending toasts.


## 更新日志 v2.1.3 - 2026-04-10

### 新增

- **持久化更新结果：** 更新器现在会在完成后记录结构化结果文件 (`update-last-result.json`)，使主程序能够准确识别更新成功或失败的具体状态。
- **更新失败通知：** 实现了针对更新失败的持久化提醒机制。如果更新在程序关闭后失败，用户在下次手动启动时将看到“请重试”的角标提示。
- **统一通知服务：** 引入 `IUpdateNotificationService` 统一管理更新相关的反馈生命周期，取代了原有的仅限成功提示的实现。
- **调试 UI 测试支持：** 在调试版本中增加了隐藏的开发者指令 (`--debug-update-success`, `--debug-update-failed`)，无需真实安装即可验证通知 UI 的显示效果。
- **增强型命令行解析：** 加固了对 `--updated` 参数检测逻辑，兼容各种 shell 转义格式。

### 更改

- **界面生命周期优化：** 重构了 `UpdateViewModel`，移除了对状态文件的后台主动轮询。主程序现在采用更高效的“启动时单次检查”模式来处理待显示通知。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.3-alpha...v2.1.3-beta](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.3-beta...v2.1.3)

## Changelog v2.1.3-beta - 2026-04-10

### Added

- **Digital Signature Verification:** Implemented Authenticode signature validation in the updater using `WinVerifyTrust`. The updater now verifies that the caller process is signed with a trusted certificate and matches the expected thumbprint, providing a strong defense against spoofing.
- **Process Identity Hardening:** Added **Process StartTime** validation alongside PID and path checks. This creates a unique "fingerprint" for the calling process, effectively preventing PID reuse attacks.
- **MAX_PATH Safety Check:** Added pre-validation for installation path length (260 chars limit). The updater now detects if the path is too deep to safely accommodate `.staging` and `.backup` operations before starting the update.

### Changed

- **Kernel-Level Handshake:** Upgraded the updater startup handshake from file-based polling to a kernel-level **Named Mutex** (`Global\{App}-Install-{GUID}`). This eliminates race conditions, fixes "Updater exited before startup handshake" errors, and ensures atomic handoff between the app and the relocated updater process.
- **Automated CI Signing:** Integrated automated executable and DLL signing into the GitHub Actions pipeline using self-signed certificates and modern **RFC 3161 (SHA-256)** timestamping.
- **UAC De-elevation on Restart:** The updater now explicitly restarts the application with non-elevated privileges after an elevated installation, adhering to the principle of least privilege.
- **Generic Updater Architecture:** Decoupled hardcoded project identifiers. The updater is now a generic component driven by `AppDisplayName` provided in the execution plan.

### Fixed

- **Local Build Robustness:** Enhanced `publish-release.ps1` with improved `signtool.exe` discovery (supporting custom paths like `D:\Windows Kits`) and local Windows Certificate Store support.
- **CI Path Quoting:** Fixed potential failures in the CI pipeline when handling filenames or paths containing spaces.

## 更新日志 v2.1.3-beta - 2026-04-10

### 新增

- **数字签名验证：** 在更新器中实现了基于 `WinVerifyTrust` 的 Authenticode 签名校验。更新器现在会验证调用进程是否由受信任证书签名并匹配预期的指纹，彻底杜绝身份冒充风险。
- **进程身份加固：** 在 PID 和路径校验的基础上新增了 **进程启动时间 (StartTime)** 校验。通过“指纹级”绑定，有效防御 PID 复用攻击。
- **路径长度预检：** 增加了对安装路径长度（260 字符 MAX_PATH）的预校验。在更新开始前识别过深的目录结构，防止因无法创建 `.staging` 或 `.backup` 目录导致的更新失败。

### 更改

- **内核级握手机制：** 将更新器启动握手从不稳定的文件轮询升级为内核级 **命名互斥体 (Named Mutex)**。消除了竞态条件，修复了“更新器在握手前退出”的报错，确保主程序与重定向后的更新进程实现原子交接。
- **自动化 CI 签名：** 在 GitHub Actions 流水中集成了自动化的 EXE/DLL 签名流程，采用自签名证书及现代 **RFC 3161 (SHA-256)** 时间戳协议。
- **重启时自动降权：** 更新器在完成管理员权限安装后，会主动以非提升权限重启主程序，符合“最小权限原则”，防止权限蔓延。
- **通用更新器架构：** 解耦了硬编码的项目标识符。更新器现在是一个完全由执行计划中的 `AppDisplayName` 驱动的通用组件。

### 修复

- **本地构建鲁棒性：** 增强了 `publish-release.ps1` 脚本，支持自动探测不同位置的 `signtool.exe`（如 `D:\Windows Kits`）并支持从本地 Windows 证书存储中提取证书。
- **CI 路径引号加固：** 修复了 CI 脚本在处理带空格的文件名或路径时可能导致的构建失败。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.3-alpha...v2.1.3-beta](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.3-alpha...v2.1.3-beta)


## Changelog v2.1.3-alpha - 2026-04-09

### Note
- **Public testing build (alpha):** This release is for update-chain verification and may contain breaking issues. Please avoid downloading/installing unless you are explicitly participating in alpha validation.
- **Upgrade path validation focus:** Intended to verify real-world migration from `v2.1.2-beta` to signed updater releases (`v2.1.3-alpha` and later), including download, integrity verification, install handoff, and restart flow.


## 更新日志 v2.1.3-alpha - 2026-04-09

### 说明
- **公开测试版（alpha）：** 该版本用于更新链路验证，可能存在稳定性或兼容性问题。若你不是明确参与测试，请尽量不要下载或安装。
- **升级链路验证优先：** 该版本重点验证从 `v2.1.2-beta` 升级到后续签名版本（`v2.1.3-alpha` 及以后）的全流程，包括下载、完整性校验、安装交接与重启路径。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.2-alpha...v2.1.3-alpha](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.2-alpha...v2.1.3-alpha)


## Changelog v2.1.2-beta - 2026-04-09

### Changed

- **Updater startup handoff reliability:** Added an explicit startup handshake (`--ack`) between main app and updater. The main app now waits for updater acknowledgment before shutdown, reducing "black window flash then no update" failures.
- **Release-note extraction in CI:** Updated release-body extraction to capture the full same-version block (English + Chinese), preventing truncation at the Chinese section heading.
- **Local reinstall ergonomics with signature retention:** Persist verified `SHA256SUMS` and `SHA256SUMS.sig` under `Updates/` so retry installs can reuse local artifacts instead of forcing a full re-download.

### Fixed

- **Installer caller PID timing mismatch:** Relaxed caller-process validation for the expected handoff case where the original process exits before updater validation starts, avoiding silent early exit with no install progress.
- **Settings initialization race in tests/CI:** Hardened first-run local settings bootstrap against concurrent file creation (`already exists`) during parallel test execution.
- **Local package reinstall validation path:** Local install now re-verifies checksum signature from cached `SHA256SUMS(.sig)` and resolves expected SHA-256 for the selected ZIP before install handoff.

## 更新日志 v2.1.2-beta - 2026-04-09

### 更改

- **Updater 启动交接可靠性：** 新增主程序与 updater 的启动握手机制（`--ack`）。主程序仅在收到 updater 接管确认后才退出，降低“黑窗一闪后无更新”的失败概率。
- **CI 发布说明提取：** 调整 release body 提取规则，按同版本范围提取完整中英文段，避免在中文标题处被截断。
- **本地重装体验优化并保留签名文件：** 在 `Updates/` 下保留已验证通过的 `SHA256SUMS` 与 `SHA256SUMS.sig`，失败重试安装时可复用本地校验资产，减少重复下载。

### 修复

- **安装调用方 PID 时序不匹配：** 兼容“主进程先退出、updater 后校验”的正常交接时序，避免因 PID 已退出导致 updater 过早失败且无安装进展。
- **测试/CI 设置初始化并发竞态：** 加固首次本地设置初始化流程，规避并行测试场景下 `already exists` 文件竞争导致的偶发失败。
- **本地安装包重试校验路径：** 本地安装前会基于缓存的 `SHA256SUMS(.sig)` 重新验签并解析目标 ZIP 的期望 SHA-256，再交给安装流程，防止篡改包绕过。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.2-alpha...v2.1.2-beta](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.2-alpha...v2.1.2-beta)

## Changelog v2.1.2-alpha - 2026-04-09

### Added

- **Updater payload preflight script:** Added `publish/preflight-release.ps1` to validate updater payload manifest integrity and publish output consistency before release packaging.
- **Single-source updater payload manifest:** Added `publish/updater-required-files.txt` as the canonical required-file list for updater release payload validation.

### Changed

- **Updater module naming cleanup:** Renamed `AtomicInstall*` types/files to `UpdaterInstall*` for clearer module intent without changing install behavior.
- **Release packaging robustness:** Updated both local publish script and GitHub Actions release flow to explicitly publish/copy/verify updater runtime payload (`Updater.exe/.dll/.deps/.runtimeconfig`) in both `single` and `fx` artifacts.
- **CI quality gates:** Added a dedicated `quality` job (tests + updater preflight), timeout controls (`20m/20m/30m`), release diagnostics artifact upload on failure, and path-aware preflight execution to reduce unnecessary CI time.
- **Update-chain localization consistency:** Replaced remaining update-flow hardcoded status texts with resource keys and aligned EN/zh-CN resource entries.

### Fixed

- **Update download button enablement race:** Ensured update commands refresh `CanExecute` when `IsUpdateAvailable` / `IsChecking` changes so the download action becomes clickable immediately after a successful update check.
- **RID publish path mismatch for updater payload:** Removed release-time gaps where updater files could be missing due to runtime-identifier output path differences.

## 更新日志 v2.1.2-alpha - 2026-04-09

### 新增

- **更新器发布前自检脚本：** 新增 `publish/preflight-release.ps1`，在正式打包前校验 updater 载荷清单与发布产物一致性。
- **Updater 载荷单一清单源：** 新增 `publish/updater-required-files.txt`，作为 updater 发布必需文件的统一来源。

### 更改

- **Updater 模块命名优化：** 将 `AtomicInstall*` 类型/文件统一重命名为 `UpdaterInstall*`，仅优化语义，不改变安装逻辑。
- **发布打包链路加固：** 本地发布脚本与 GitHub Actions 发布流程均显式发布/拷贝/校验 updater 运行时载荷（`Updater.exe/.dll/.deps/.runtimeconfig`），覆盖 `single` 与 `fx` 两种产物。
- **CI 质量门禁增强：** 新增 `quality` 作业（测试 + updater preflight）、超时控制（`20m/20m/30m`）、失败诊断产物上传，以及基于变更路径的 preflight 条件执行以减少不必要耗时。
- **更新链路本地化一致性：** 将更新流程中剩余硬编码状态文案改为资源键，并补齐/对齐中英文资源项。

### 修复

- **下载按钮可点击状态竞态：** 在 `IsUpdateAvailable` / `IsChecking` 变化时刷新命令可执行状态，修复“检测到新版本后下载按钮不可点击”的问题。
- **Updater 载荷 RID 路径不一致：** 修复因运行时标识输出目录差异导致的发布阶段 updater 文件缺失风险。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.1...v2.1.2-alpha](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.1...v2.1.2-alpha)

## Changelog v2.1.1 - 2026-04-08

### Added

- **Trusted update quota architecture:** Introduced `IUpdateQuotaPolicyProvider` + `UpdateQuotaPolicy` with a default `StaticUpdateQuotaPolicyProvider` so runtime limits are controlled by a policy layer rather than user-editable settings.
- **Cached latest-version fallback model:** Added structured cache contracts (`IUpdateVersionCacheService`, `CachedUpdateVersionInfo`) and persisted latest successful release metadata under `Updates/`.
- **Trusted UTC source abstraction:** Added `ITrustedUtcTimeService` / `TrustedUtcTimeService` to support network-backed UTC with local monotonic fallback for quota decisions.

### Changed

- **Asset resolution hardening:** Enforced whitelist matching for update packages (`-fx` / `-single`) and removed first-zip fallback to prevent selecting wrong assets when release attachments change order.
- **Update failure messaging:** Unified update status-message composition so API/network error messages can also include the latest cached successful version hint.
- **Version display normalization:** Current version display now strips build metadata (`+...`) while preserving pre-release labels (`-alpha`, `-beta`, `-rc`) to avoid user confusion.
- **Quota ownership migration:** Runtime quota values are now policy-driven; legacy AppSettings quota fields remain for compatibility but are marked obsolete/ignored at runtime.
- **Localization:** Localized cached-version fallback hint in both English and Chinese resources.

### Fixed

- **Cache persistence guard:** Latest-version cache is only written on successful release resolution; failed checks (timeout/network/API errors) no longer overwrite cache state.
- **Time rollback resilience:** Quota state now enforces monotonic observed time to reduce bypass attempts via local clock rollback.

## 更新日志 v2.1.1 - 2026-04-08

### 新增

- **更新配额策略架构：** 引入 `IUpdateQuotaPolicyProvider` + `UpdateQuotaPolicy`，并提供默认实现 `StaticUpdateQuotaPolicyProvider`，使运行时限额由策略层统一控制，而不是由可编辑本地设置直接决定。
- **最新版本缓存模型：** 新增结构化缓存契约（`IUpdateVersionCacheService`、`CachedUpdateVersionInfo`），将最近一次成功检查到的版本信息持久化到 `Updates/`。
- **可信 UTC 抽象：** 新增 `ITrustedUtcTimeService` / `TrustedUtcTimeService`，支持网络 UTC 与本地单调时间回退，用于配额判定。

### 更改

- **更新包解析加固：** 对安装包实施白名单匹配（仅 `-fx` / `-single`），移除“取第一个 zip”的兜底，避免发布附件顺序变化导致误选资产。
- **更新失败提示统一：** 统一状态文案构建流程，使 API/网络错误场景也可附带“上次成功检查版本”的缓存提示。
- **版本显示规范化：** 当前版本显示仅移除构建元数据（`+...`），保留预发布标识（`-alpha`、`-beta`、`-rc`），减少用户理解歧义。
- **配额控制权迁移：** 运行时配额改为策略驱动；`AppSettings` 中旧配额字段仅保留兼容用途，已标记为过时且运行时忽略。
- **本地化：** 为缓存回退提示补充英文与中文资源文本。

### 修复

- **缓存写入保护：** 仅在成功解析 release 时写入最新版本缓存；超时/网络/API 错误不再污染缓存状态。
- **时间回拨防护：** 配额状态增加单调时间约束，降低通过本地回拨系统时间绕过限制的可能性。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.0...v2.1.1](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.0...v2.1.1)

## Changelog v2.1.0 - 2026-04-07

### Added

- **No new features in stable cut:** `v2.1.0` primarily promotes the `v2.1.0-beta` update system to stable without introducing additional user-facing features.

### Fixed

- **Installer hardening:** Improved installer execution safeguards for edge environments (permissions/path/process handoff) to reduce failed handoff cases during update.
- **Update UX reliability:** Refined status/progress transitions and error fallback messages for clearer outcomes when update resolution or download fails.

### Changed

- **Release promotion:** Promoted the pre-release update pipeline from `v2.1.0-beta` to stable `v2.1.0`.
- **Version bump:** Confirmed project version as `2.1.0` in release artifacts.

## 更新日志 v2.1.0 - 2026-04-07

### 新增

- **稳定版未新增功能：** `v2.1.0` 主要将 `v2.1.0-beta` 的更新系统提升为稳定版，未引入额外的用户可见新功能。

### 修复

- **安装器稳健性：** 针对权限/路径/进程交接等边界环境增强安装执行保护，降低更新交接失败概率。
- **更新体验可靠性：** 优化状态与进度切换及错误回退文案，使解析或下载失败时结果反馈更清晰。

### 更改

- **正式版发布：** 将 `v2.1.0-beta` 的预发布更新链路发布为稳定版 `v2.1.0`。
- **版本确认：** 在发布产物中确认项目版本为 `2.1.0`。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.0-beta...v2.1.0](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.0-beta...v2.1.0)

## Changelog v2.1.0-beta - 2026-04-07

### Added

- **GitHub-based self-update pipeline:** Added release check, asset resolution, download orchestration, and fallback handling through `IUpdateService` / `UpdateService`.
- **Installer execution flow:** Added `IUpdateInstallerService` / `UpdateInstallerService` with package verification (SHA-256), installation mode detection, and handoff request model (`UpdateInstallRequest`).
- **Download progress model:** Added update progress/status models (`ReleaseDownloadProgress`, `ReleaseResolutionResult`, `ReleaseAssetInfo`) for UI binding and state tracking.
- **Service abstractions:** Added `IGitHubContentService`, `ILocalFileService` and related implementations to support reusable GitHub content fetch and local file operations.

### Changed

- **UI integration:** Integrated update workflow into `MainView`, `MainViewModel`, and `UpdateViewModel`, including progress display, install actions, and localized status text.
- **Localization:** Expanded update-related EN/zh-CN resources in `Strings.resx` and `Strings.zh-CN.resx`.
- **Settings/config:** Extended default settings and app startup wiring (`App.xaml`, `App.xaml.cs`) to support update behavior.
- **Version bump:** Promoted the project version from `2.1.0-alpha` to `2.1.0-beta`.

## 更新日志 v2.1.0-beta - 2026-04-07

### 新增

- **基于 GitHub 的自更新流程：** 通过 `IUpdateService` / `UpdateService` 增加版本检查、资源解析、下载编排与回退处理能力。
- **安装执行流程：** 新增 `IUpdateInstallerService` / `UpdateInstallerService`，支持安装包校验（SHA-256）、安装模式检测与安装请求模型（`UpdateInstallRequest`）。
- **下载进度模型：** 新增 `ReleaseDownloadProgress`、`ReleaseResolutionResult`、`ReleaseAssetInfo` 等模型用于界面绑定与状态跟踪。
- **服务抽象：** 增加 `IGitHubContentService`、`ILocalFileService` 及其实现，用于复用 GitHub 内容拉取与本地文件操作。

### 更改

- **界面集成：** 在 `MainView`、`MainViewModel` 与 `UpdateViewModel` 中接入更新流程，支持进度显示、安装操作与本地化状态提示。
- **本地化：** 在 `Strings.resx` 与 `Strings.zh-CN.resx` 中补充更新相关的中英文资源。
- **设置与启动：** 扩展默认设置与应用启动装配（`App.xaml`、`App.xaml.cs`）以支持更新行为。
- **版本更新：** 将项目版本从 `2.1.0-alpha` 升级为 `2.1.0-beta`。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.0-alpha...v2.1.0-beta](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.1.0-alpha...v2.1.0-beta)

## Changelog v2.1.0-alpha - 2026-04-07

### Added

- **Community template catalog:** In-app **Community** tab to browse the public index and download templates into your local library (GitHub Raw with jsDelivr CDN fallback, refresh throttling). Source repository: [GamepadMapping-CommunityProfiles](https://github.com/Maxim00191/GamepadMapping-CommunityProfiles).
- `**ICommunityTemplateService` / `CommunityTemplateService`:** Network fetch, validation-friendly download path, and integration with `ProfileService` reload after install.
- **UI:** Template identity tooltips in the profile area for clearer `profileId` / group context.

### Changed

- **Template grouping:** Unified `templateGroupId` usage (including Roco Kingdom sets) and added `EffectiveTemplateGroupId` on `GameProfileTemplate` for consistent group resolution.
- **Validation:** Stricter checks in `ProfileValidator` around template group metadata.
- **Bundled templates & layout:** Per-game template folders (e.g. Flight Sim, Roco Kingdom), refreshed flight-sim content, and additional sample presets (e.g. Elden Ring default).
- **UI styling:** Shared `ToolbarButtonStyle` for consistent toolbar buttons (including community catalog actions).
- **Version bump:** Updated project version to `2.1.0`.

## 更新日志 v2.1.0-alpha - 2026-04-07

### 新增

- **社区模板目录：** 应用内 **Community** 标签页浏览公开索引并下载模板到本地库（GitHub Raw，失败时降级 jsDelivr CDN；刷新节流）。模板索引与 JSON 由社区仓库维护：[GamepadMapping-CommunityProfiles](https://github.com/Maxim00191/GamepadMapping-CommunityProfiles)。
- `**ICommunityTemplateService` / `CommunityTemplateService`：** 网络拉取、下载与安装后通过 `ProfileService` 重载模板列表。
- **界面：** 配置相关区域增加模板标识类提示，便于理解 `profileId` 与分组。

### 更改

- **模板分组：** 统一 `templateGroupId`（含洛克王国等套件），并在 `GameProfileTemplate` 上增加 `EffectiveTemplateGroupId` 以一致解析分组。
- **校验：** `ProfileValidator` 对模板分组元数据加强校验。
- **内置模板与目录：** 按游戏分子目录（如 Flight Sim、Roco Kingdom），更新飞行模拟模板，并补充示例预设（如 Elden Ring `default`）。
- **界面样式：** 新增共享 `ToolbarButtonStyle`，统一工具栏按钮（含社区目录相关操作）。
- **版本更新：** 项目版本更新至 `2.1.0`。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.0.0...v2.1.0](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.0.0...v2.1.0)

## Changelog v2.0.0 - 2026-04-07

### Added

- **Advanced Input Actions:** Added richer action support for profile mappings, including keyboard actions and radial-menu driven actions.
- **Template Validation:** Added stronger template validation to catch malformed mapping definitions earlier.
- **Roco Kingdom Radial Templates:** Added and refined radial menu templates for Roco Kingdom world and fight scenarios.

### Fixed

- **Radial Menu Input Routing:** Improved filtering and conflict handling between radial menu activation and standard mappings.
- **Radial HUD Stability:** Fixed edge cases in radial menu rendering/layout transitions across templates.

### Changed

- **Release Promotion:** Promoted the 2.0 feature set from pre-release (`2.0.0-alpha` / `2.0.0-beta`) to stable `2.0.0`.
- **Version Bump:** Updated project version to `2.0.0`.

## 更新日志 v2.0.0 - 2026-04-07

### 新增

- **高级输入动作：** 为映射配置增加了更丰富的动作能力，包括键盘动作与基于轮盘菜单的动作。
- **模板校验：** 增强了模板校验逻辑，可更早发现不合法的映射定义。
- **洛克王国轮盘模板：** 为洛克王国探索与战斗场景新增并完善了轮盘菜单模板。

### 修复

- **轮盘输入路由：** 改进了轮盘激活与常规映射之间的输入过滤和冲突处理。
- **轮盘 HUD 稳定性：** 修复了模板切换过程中轮盘渲染与布局的边界问题。

### 更改

- **正式版发布：** 将 `2.0.0-alpha` / `2.0.0-beta` 的预发布能力整合并发布为稳定版 `2.0.0`。
- **版本更新：** 将项目版本更新至 `2.0.0`。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.0.0-beta...v2.0.0](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.0.0-beta...v2.0.0)

## Changelog v2.0.0-beta - 2026-04-06

### Added

- **Radial Menu Customization:** Added support for customizing radial menu colors, opacity, and scale in settings.
- **Radial Menu Templates:** Added specialized radial menu templates for Roco Kingdom (World and Fight modes).
- **Dynamic HUD Layout:** Improved radial HUD layout logic with support for dynamic segment generation and label positioning.
- **Input Middleware:** Enhanced `RadialMenuMiddleware` to support multi-stage radial menu interactions and better input filtering.

### Fixed

- **UI Rendering:** Fixed potential rendering issues when switching between different radial menu templates.
- **Input Conflict:** Resolved input conflicts between standard button mappings and radial menu activation.

### Changed

- **Version Bump:** Updated project version to `2.0.0-beta`.
- **Settings UI:** Reorganized settings view to include a dedicated section for Radial Menu configuration.

## 更新日志 v2.0.0-beta - 2026-04-06

### 新增

- **轮盘菜单自定义：** 在设置中添加了对轮盘菜单颜色、透明度和缩放的自定义支持。
- **轮盘菜单模板：** 为洛克王国（探索和战斗模式）添加了专门的轮盘菜单模板。
- **动态 HUD 布局：** 改进了轮盘 HUD 布局逻辑，支持动态分段生成和标签定位。
- **输入中间件：** 增强了 `RadialMenuMiddleware`，以支持多阶段轮盘菜单交互和更好的输入过滤。

### 修复

- **UI 渲染：** 修复了在不同轮盘菜单模板之间切换时可能出现的渲染问题。
- **输入冲突：** 解决了标准按键映射与轮盘菜单激活之间的输入冲突。

### 更改

- **版本更新：** 将项目版本更新至 `2.0.0-beta`。
- **设置界面：** 重新组织了设置视图，包含专门的轮盘菜单配置部分。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.0.0-alpha...v2.0.0-beta](https://github.com/Maxim00191/Gamepad-Mapping/compare/v2.0.0-alpha...v2.0.0-beta)

## Changelog v2.0.0-alpha - 2026-04-05

### Added

- **Radial Menu System:** Introduced a new radial menu functionality for quick action selection.
- **Radial HUD:** Added a highly customizable radial HUD with support for scaling, color adjustments, and dynamic geometry.
- **Enhanced Localization:** Added comprehensive localization support for radial menus and HUD elements.
- **New Actions:** Implemented new action classes for item cycling, keyboard actions, radial menus, and template toggling.
- **Roco Kingdom Templates:** Added new keyboard action catalogs for exploration and battle, including 'Reveal Mouse' action.

### Fixed

- **Radial Menu Logic:** Improved radial menu closing logic and input handling for a smoother user experience.

### Changed

- **Version Bump:** Updated project version to `2.0.0-alpha`.

## 更新日志 v2.0.0-alpha - 2026-04-05

### 新增

- **轮盘菜单系统：** 引入了全新的轮盘菜单功能，用于快速选择操作。
- **轮盘 HUD：** 添加了高度可定制的轮盘 HUD，支持缩放、颜色调整和动态几何图形。
- **增强的本地化：** 为轮盘菜单和 HUD 元素添加了全面的本地化支持。
- **新操作类型：** 实现了用于物品循环、键盘操作、轮盘菜单和模板切换的新操作类。
- **洛克王国模板：** 为探索和战斗添加了新的键盘操作目录，包括“显示鼠标”操作。

### 修复

- **轮盘菜单逻辑：** 改进了轮盘菜单的关闭逻辑和输入处理，提供更流畅的用户体验。

### 更改

- **版本更新：** 将项目版本更新至 `2.0.0-alpha`。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.3...v2.0.0-alpha](https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.3...v2.0.0-alpha)

## Changelog `v1.4.3` — 2026-04-03

### Added

- Roco exploration template: RT + D-pad left/right → mouse wheel up/down; LB + RB → hold/release left mouse button (map/UI drag with right stick).
- Roco exploration template: LT + D-pad Up → reveal mouse (LeftAlt).
- Roco battle template: RB alone → left click; RB + D-pad (up/right/down/left) → keys 1–4; D-pad up/down → item cycle across slots 1–5.

### Changed

- Roco battle template: slot selection layout updated; bindings and copy aligned with skill slots (1–5 cycle + RB shortcuts).
- Roco exploration: updated `LeftShoulder + X` to map to `Q` for star magic action (previously `M` for map).
- Bumped app version to 1.4.3.

### Note

- Battle template no longer maps solo D-pad left/right to direct keys; slot 5 is reached via up/down cycling only.

## 更新日志 `v1.4.3` — 2026-04-03

### 添加

- Roco 探索模板：RT + 方向键左/右 → 鼠标滚轮上/下；LB + RB → 按住/释放鼠标左键（使用右摇杆进行地图/用户界面拖动）。
- Roco 探索模板：LT + 方向键上 → 显示鼠标。
- Roco 战斗模板：仅使用 RB 键 → 左键点击；RB 键 + 方向键（上/右/下/左）→ 对应 1-4 键；方向键上/下 → 在 1-5 个槽位间循环切换物品。

### 更改

- Roco 战斗模板：槽位选择布局已更新；绑定和复制已与技能槽位对齐（1-5 循环+RB 快捷键）。
- Roco 探索：更新 `LeftShoulder + X` 映射至 `Q` 以执行星星魔法操作（原为 `M` 打开地图）。
- 将应用程序版本更新至 1.4.3。

### 备注

- 战斗模板不再将单独的 D-pad 左/右映射到方向键；第 5 槽位只能通过上/下循环切换到达。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.2...v1.4.3](https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.2...v1.4.3)

## Changelog `v1.4.2`

### Added

- Added a new application icon asset: `Assets/Icons/Gamepad Mapping.ico`.
- Registered the icon as a project resource to ensure it is included correctly at build and runtime.

### Changed

- Bumped the app version from `1.4.1` to `1.4.2`.
- Set the application icon in the project file via `ApplicationIcon`.
- Configured the main window icon in `MainWindow.xaml` via the `Icon` property.

## 更新日志 `v1.4.2`

### 新增

- 添加了一个新的应用程序图标资源：`Assets/Icons/Gamepad Mapping.ico`。
- 将该图标注册为项目资源，以确保在构建和运行时能正确包含它。

### 更改

- 将应用程序版本从`1.4.1`更新为`1.4.2`。
- 通过`ApplicationIcon`在项目文件中设置应用程序图标。
- 通过`Icon`属性在`MainWindow.xaml`中配置了主窗口图标。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.1...v1.4.2](https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.1...v1.4.2)

## Changelog `v1.4.1`

### Fixed

- Improved app shutdown behavior by setting `ShutdownMode="OnMainWindowClose"` in `App.xaml` (app now exits cleanly when main window closes).
- Added additional cleanup in `MainViewModel` disposal logic to properly stop/release template-switch HUD timer and window resources.

### Improved

- Updated profile templates for Roco Kingdom:
  - `roco-kingdom-world-fight.json`: added `comboLeadButtons`.
  - `roco-kingdom-world.json`: adjusted combo lead buttons, updated Back-button mapping from `LeftAlt` to `Escape`, refined some trigger/threshold settings, and simplified B-button behavior text/handling.
- Updated UI localization text (EN + zh-CN):
  - `Reload template` → `Reload current template`
  - `From Value` → `Gamepad Input` (and corresponding Chinese translations)

### Note

- Project version bumped in `Gamepad Mapping.csproj`: `1.4.0` → `1.4.1`.

## 更新日志 `v1.4.1`

### 修复

- 通过在`App.xaml`中设置`ShutdownMode="OnMainWindowClose"`，改进了应用程序的关闭行为（现在，当主窗口关闭时，应用程序会干净退出）。
- 在`MainViewModel`的析构逻辑中增加了额外的清理操作，以正确停止/释放模板切换 HUD 定时器和窗口资源。

### 改进

- 更新了 Roco Kingdom 的配置文件模板： - `roco-kingdom-world-fight.json`：添加了`comboLeadButtons`。
  - `roco-kingdom-world.json`：调整了组合键引导按钮，将后退按钮映射从`LeftAlt`更新为`Escape`，优化了一些触发/阈值设置，并简化了 B 按钮行为文本/处理。
- 更新用户界面本地化文本（英语+简体中文）： - `Reload template` → `重新加载当前模板` - `From Value` → `游戏手柄输入`（以及相应的中文翻译）

### 备注

- 在`Gamepad Mapping.csproj`中项目版本已更新：`1.4.0` → `1.4.1`。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.0...v1.4.1](https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.0...v1.4.1)

### Changelog `v1.4.0`

#### Added

- **Template Switch HUD:** Introduced `TemplateSwitchHudWindow` for on-screen notifications, with duration settings in `AppSettings` and localized labels (English/Chinese).
- **Time Management Abstractions:** Added `RealTimeProvider` and `ITimeProvider` for centralized, testable time management.
- **Input Validation:** Added semantic validation in `ChordResolver` to reject impossible button combinations.
- **Analog Enhancements:** Introduced a new `StateKey` record, hysteresis for keyboard/trigger transitions (to reduce jitter), and magnitude-based thresholds for analog parsing.
- **File System Abstractions:** Created `IFileSystem`, `IPathProvider`, `PhysicalFileSystem`, and `AppPathProvider` to centralize file and path handling.
- **Logging System:** Introduced the `ILogger` interface and `FileLogger` implementation, integrating logging into app startup/shutdown, input processing, elevation handling, and emulators.
- **UI State Management:** Added `GamepadMonitorUiSnapshot` to handle UI state representation for the monitor.

#### Improved

- **Combo HUD:** Extended `ComboHudBuilder` to detect modifier prefixes, expanded `ComboHudManager` with gate hints, and updated UI bindings to clarify when output dispatch is blocked.
- **Mapping Engine:** Decomposed the former monolithic `MappingEngine` into specialized processors (`ComboHudManager`, `AnalogMappingProcessor`, `ButtonMappingProcessor`) for improved maintainability and SRP. It now acts as a coordinating orchestrator.
- **Mapping Engine Control:** Added `TrySyncComboHud`, `RefreshComboHud`, and `InvalidateComboHudPresentation` for finer control.
- **Input Dispatching:** Improved `InputDispatcher` to better manage idle states and cap the output queue size to prevent memory issues.
- **Button Processing:** Updated `ButtonMappingProcessor` to support deferred solo lead buttons and combo lead semantics.
- **Async Emulation:** Refactored `KeyboardEmulator`, `MouseEmulator`, `MappingEngine`, and `InputDispatcher` to support asynchronous key taps, mouse clicks, and cancellation. Interfaces were extended accordingly.
- **Timer Handling:** Migrated `RealTimeProvider` to use `ThreadPoolOneShotTimer` instead of `DispatcherTimer`, decoupling time management from the WPF message pump. Improved `HoldSessionManager` for synchronized execution with input frames.
- **UI Updates:** Updated `MainView` and `GamepadMonitorView` to support new HUD elements and hints. Extended `MainViewModel` and `GamepadMonitorViewModel` to control HUD visibility and timing.
- **Service Migration:** Migrated `ProfileService` and `SettingsService` to utilize the new file system abstractions.
- **Status Monitoring:** Updated `AppStatusMonitor.EvaluateNow` to return a boolean, better expressing the status evaluation result.

#### Fixed

- **Analog Safety:** Hardened `ResolveStickAxisValue` to handle NaN/Infinity values and properly clamp to the unit range.
- **Timer Initialization:** Fixed `ComboHudManager` to correctly initialize its `DispatcherTimer` with the current dispatcher.

#### Note

- **Testing Coverage Expansion:** Significantly increased test coverage across the board. Added comprehensive tests for `InputFramePipeline`, middleware, `MappingEngine` (robustness and race conditions), async behavior, precision jitter, and logging. Introduced mocks for file systems and Win32 services.
- **Maintenance:** Minor cleanup in `MainViewModel.DispatchToUi` (removed unnecessary blank line).
- **Repository Updates:** Updated `.gitignore`, bumped the version for the `v1.4.0` release, and updated CI/CD documentation.

### 更新日志 `v1.4.0`

#### 新增

- **模板切换 HUD：** 引入了 `TemplateSwitchHudWindow` 用于显示屏幕上的模板切换通知，在 `AppSettings` 中添加了显示时长设置，并提供了本地化标签（英文/中文）。
- **时间管理抽象：** 添加了 `RealTimeProvider` 和 `ITimeProvider`，实现集中且可测试的时间管理。
- **输入验证：** 在 `ChordResolver` 中添加了语义验证，以拒绝不可能的按键组合。
- **摇杆/扳机增强：** 引入了新的 `StateKey` 记录，为键盘/扳机过渡添加了死区迟滞（Hysteresis）以减少抖动，并为模拟量输入解析添加了基于幅度的阈值。
- **文件系统抽象：** 创建了 `IFileSystem`、`IPathProvider`、`PhysicalFileSystem` 和 `AppPathProvider`，用于集中处理文件和路径。
- **日志系统：** 引入了 `ILogger` 接口和 `FileLogger` 实现，将日志记录集成到应用启动/关闭、输入处理、提权处理以及模拟器中。
- **UI 状态管理：** 添加了 `GamepadMonitorUiSnapshot`，用于管理监控器的 UI 状态表示。

#### 改进

- **组合键 HUD：** 扩展了 `ComboHudBuilder` 以检测修饰键前缀，为 `ComboHudManager` 增加了按键门控提示（gate hints），并更新了 UI 绑定以明确提示输出分发何时被阻止。
- **映射引擎架构：** 将以前单一庞大的 `MappingEngine` 拆分为专门的处理器（如 `ComboHudManager`、`AnalogMappingProcessor`、`ButtonMappingProcessor`），提高了核心逻辑的可维护性并符合单一职责原则（SRP）。`MappingEngine` 现在主要作为协调器运行。
- **映射引擎控制：** 添加了 `TrySyncComboHud`、`RefreshComboHud` 和 `InvalidateComboHudPresentation`，以实现对 HUD 显示更精细的控制。
- **输入分发：** 改进了 `InputDispatcher` 以更好地管理空闲状态，并限制了输出队列的大小以防止内存问题。
- **按键处理：** 更新了 `ButtonMappingProcessor` 及其相关流程，以支持延迟的单键引导（deferred solo lead buttons）和组合键引导语义。
- **异步模拟支持：** 重构了 `KeyboardEmulator`、`MouseEmulator`、`MappingEngine` 和 `InputDispatcher`，以支持支持取消操作的异步按键敲击和鼠标点击，并扩展了相应的接口。
- **定时器处理：** `RealTimeProvider` 现在使用 `ThreadPoolOneShotTimer` 替代 `DispatcherTimer`，将时间管理与 WPF 消息泵解耦。改进了 `HoldSessionManager`，使其与输入帧同步执行。
- **UI 更新：** 更新了 `MainView` 和 `GamepadMonitorView` 以支持新的 HUD 元素和提示。扩展了 `MainViewModel` 和 `GamepadMonitorViewModel` 以控制 HUD 的可见性与时间。
- **服务迁移：** 将 `ProfileService` 和 `SettingsService` 迁移为使用新的文件系统抽象。
- **状态监控：** 更新了 `AppStatusMonitor.EvaluateNow` 使其返回布尔值，从而更好地表达状态评估的结果。

#### 修复

- **模拟量安全：** 强化了 `ResolveStickAxisValue`，使其能够妥善处理 NaN/Infinity（非数字/无穷大）值，并正确将其限制在单位范围内。
- **定时器初始化：** 修复了 `ComboHudManager`，确保使用当前调度程序（dispatcher）正确初始化其内部的 `DispatcherTimer`。

#### 补充

- **测试覆盖率大幅扩展：** 显著提高了核心处理、服务和视图模型的测试覆盖率。为 `InputFramePipeline`、中间件、`MappingEngine`（包含鲁棒性和竞态条件测试集）、异步行为、精度抖动和日志记录添加了全面的单元测试。引入了文件系统和 Win32 服务的模拟对象（mocks）。
- **代码维护：** 清理了 `MainViewModel.DispatchToUi` 中的细节（移除了不必要的空行）。
- **仓库更新：** 微调了 `.gitignore`，为 `v1.4.0` 发布更新了版本号及 CI/CD 文档。

**Full Changelog**: [https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.3.0...v1.4.0](https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.3.0...v1.4.0)