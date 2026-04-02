# Gamepad Mapping（手柄按键映射）

**English:** [README.md](README.md)

这是一款 Windows 桌面应用：可将 **XInput** 手柄输入映射为 **键盘与鼠标**，以便使用手柄游玩 PC 游戏，并支持按游戏保存独立配置。支持绑定 **目标前台进程**（游戏可执行文件名），限制映射仅在对应窗口处于焦点时生效，避免在其他程序中发生误触。

**概述**

- **输入：** 兼容 XInput 的手柄。
- **输出：** 模拟键盘与鼠标（若目标游戏以管理员权限运行，请参阅下文的 [功能](#功能)）。
- **配置：** 映射模板 JSON 位于 `Assets/Profiles/templates`；全局设置保存在 `Assets/Config/local_settings.json`。

## 功能

- **配置模板 (Profiles)** — 通过 JSON 定义按键绑定、显示名称以及可选的组合键行为。
- **丰富的按键绑定** — 手柄按键、扳机、摇杆均可映射到键鼠；支持 **按下 / 松开 / 单击 (Tap)** 触发、模拟轴阈值判定，以及 **长按与单击** 双重输出（`holdKeyboardKey`、`holdThresholdMs`）。
- **组合键 (Combos)** — 支持多键组合、修饰键判定容差以及 **组合引导键**（`comboLeadButtons`）；游玩时可开启 **组合键 HUD** 显示当前状态。
- **全局应用设置** — 顶部栏的设置面板可调整时序、模拟轴默认参数、手柄轮询与键盘模拟；修改写入 `Assets/Config/local_settings.json`（与模板 JSON 分开存储）。
- **目标进程绑定** — 设定 `targetProcessName` 后，仅当指定可执行文件（通常不带 `.exe`）处于前台时，映射才会输出。
- **权限自动提示** — 若焦点程序以 **管理员** 权限运行，应用可提示并以更高权限重启，避免 Windows 因 UIPI 拦截模拟输入。

## 系统要求

- **操作系统：** Windows（WPF）。
- **.NET SDK：** [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0)（`net9.0-windows`）。
- **手柄：** 兼容 XInput 的控制器（[Vortice.XInput](https://github.com/amerkoleci/Vortice.Windows)）。

## 构建与运行

在仓库根目录执行：

```powershell
dotnet build "Gamepad Mapping.csproj" -c Release
dotnet run --project "Gamepad Mapping.csproj"
```

也可以在 Visual Studio 中打开 `GamepadMapping.sln` 并运行启动项目。

构建会通过 `CopyToOutputDirectory` 将 `Assets\Config` 与 `Assets\Profiles` 复制到输出目录。请从包含完整 `Assets` 的目录运行（开发时一般为项目根目录，构建后一般为 `bin\...\net9.0-windows\`）。详见 [内容根路径](#内容根路径)。

### 测试

```powershell
dotnet test "GamepadMapping.sln" -c Release
```

## 配置说明

首次运行时，若 `Assets/Config/local_settings.json` 不存在，应用会将 `Assets/Config/default_settings.json` 复制为本地配置。全局选项读写 **`local_settings.json`**。按键绑定、`targetProcessName`、`comboLeadButtons` 等仅存放在 **`Assets/Profiles/templates/{profileId}.json`**，不会写入应用设置。

在 **顶部栏** 打开 **应用设置**（模板下拉框与「新建配置 (+)」右侧），**时序与键盘**、**模拟轴与轮询** 两组与下表字段一一对应。**摇杆死区** 在 **手柄实时监视器 → 模拟轴**（齿轮）中调整，对应 `leftThumbstickDeadzone`、`rightThumbstickDeadzone`；未单独设置时使用 `thumbstickDeadzone`。

### `local_settings.json` 字段参考

| 字段名 | 说明 |
| -------- | -------- |
| `templatesDirectory` | 存放模板 `*.json` 的目录（相对于内容根）。 |
| `defaultProfileId` | 默认模板 ID（文件名不含扩展名）。 |
| `lastSelectedTemplateProfileId` | 上次选择的模板，启动时恢复。 |
| `modifierGraceMs` | 组合修饰容差、HUD 延迟及默认长按阈值。 |
| `leadKeyReleaseSuppressMs` | 组合引导键：中断长按时抑制误触输出的时间。 |
| `thumbstickDeadzone` | 未按摇杆单独设置时的默认归一化死区。 |
| `leftThumbstickDeadzone` | 左摇杆死区 `[0–1]`（实时监视器）。 |
| `rightThumbstickDeadzone` | 右摇杆死区 `[0–1]`（实时监视器）。 |
| `leftTriggerInnerDeadzone` | 左扳机起始死区；低于此值视为未按下。 |
| `leftTriggerOuterDeadzone` | 左扳机触顶阈值；高于此值视为按满。 |
| `rightTriggerInnerDeadzone` | 右扳机起始死区（同左）。 |
| `rightTriggerOuterDeadzone` | 右扳机触顶阈值（同左）。 |
| `gamepadPollingIntervalMs` | XInput 轮询间隔（毫秒）。 |
| `defaultAnalogActivationThreshold` | 未设置 `analogThreshold` 时的默认阈值 `[0–1]`。 |
| `mouseLookSensitivity` | 默认鼠标视角灵敏度。 |
| `analogChangeEpsilon` | 模拟轴变化超过此幅度才视为更新。 |
| `keyboardTapHoldDurationMs` | 模拟点按时键位保持按下时长（毫秒）。 |
| `tapInterKeyDelayMs` | 多次点按之间的间隔（毫秒）。 |
| `textInterCharDelayMs` | 逐字输入时字符间隔（毫秒）。 |

## 模板格式（Profile）

每个文件名为 `{profileId}.json`，根对象通常包含：

- `profileId`、`templateGroupId`、`displayName`、可选 `displayNames`（如 `"zh-CN"`）、可选 `displayNameKey`（`Resources/Strings*.resx`）
- `targetProcessName`（可选）— 进程名（通常不含 `.exe`），与 `Process.ProcessName` 一致
- `comboLeadButtons`（可选）— XInput 键名，如 `LeftShoulder`
- `mappings` — `from`（`type`、`value`）、`keyboardKey`、`trigger`，以及可选 `analogThreshold`、`holdKeyboardKey`、`holdThresholdMs`、`description`、可选 `descriptions`、`descriptionKey`

示例见 `Assets/Profiles/templates`（如 `default.json`、`flight_sim.json`、`roco-kingdom-world.json`）。

## 界面与本地化

界面主题跟随 **Windows** 深浅色。文案使用 `Resources/Strings*.resx`。模板标题与映射说明可在 JSON 中使用 **`displayNames` / `descriptions`**（自建模板推荐）；`displayNameKey` / `descriptionKey` 仍可从 `.resx` 解析。

## 内容根路径

应用从当前目录、`AppContext.BaseDirectory` 及其上级查找 `Assets/Config/default_settings.json` 以确定**内容根**。开发时在项目根运行，发布后 `Assets` 与可执行文件同目录即可。

## 技术栈

- **WPF**
- **[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)**
- **[Newtonsoft.Json](https://www.newtonsoft.com/json)**
- **[InputSimulatorPlus](https://www.nuget.org/packages/InputSimulatorPlus)**
- **[Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput)**

## CI/CD

已配置 [GitHub Actions](.github/workflows/build.yml)：向 `main` 推送或 PR 时执行 Release 构建（含 NuGet 缓存）；推送 `v*` 标签（如 `v1.4.3`）会打包两种 `win-x64` 产物并通过 [softprops/action-gh-release](https://github.com/softprops/action-gh-release) 创建 GitHub Release（自动生成发布说明）。zip 内会附带仓库中的 `README.md`、`README_zh.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`（若存在）。

- **`Gamepad-Mapping-<tag>-win-x64-single.zip`** — 单文件、自包含（无需单独安装运行时）。
- **`Gamepad-Mapping-<tag>-win-x64-fx.zip`** — 框架依赖（更小；需安装 [.NET 9 桌面运行时](https://dotnet.microsoft.com/download/dotnet/9.0)）。

```powershell
git tag v1.4.3
git push origin v1.4.3
```

## 许可证

[MIT License](LICENSE)
