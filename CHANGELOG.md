## Changelog `v2.0.0-alpha` - 2026-04-05

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

## 更新日志 `v2.0.0-alpha` - 2026-04-05

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

**Full Changelog**: https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.3...v2.0.0-alpha



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

**Full Changelog**: https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.2...v1.4.3


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

**Full Changelog**: https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.1...v1.4.2


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

**Full Changelog**: https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.4.0...v1.4.1


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

**Full Changelog**: https://github.com/Maxim00191/Gamepad-Mapping/compare/v1.3.0...v1.4.0
