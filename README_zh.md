# Gamepad Mapping（手柄按键映射）

**English:** [README.md](https://www.google.com/search?q=README.md)

这是一款 Windows 桌面应用：可将 **XInput** 手柄输入映射为 **键盘与鼠标**，以便使用手柄游玩 PC 游戏，并支持按游戏保存独立配置。支持绑定 **目标前台进程**（游戏可执行文件名），限制映射仅在对应窗口处于焦点时生效，避免在其他程序中发生误触。

**概述**

- **输入：** 兼容 XInput 的手柄。
- **输出：** 模拟键盘与鼠标按键（若目标游戏以管理员权限运行，请参阅下文的 [功能说明](https://www.google.com/search?q=%23%E5%8A%9F%E8%83%BD)）。
- **配置：** 映射模板 JSON 位于 `Assets/Profiles/templates`；全局设置参数保存在 `Assets/Config/local_settings.json`。

## 功能

- **配置模板 (Profiles)** — 通过 JSON 定义按键绑定、显示名称以及可选的组合键行为。
- **丰富的按键绑定** — 手柄按键、扳机、摇杆均可映射到键鼠；支持 **按下 / 松开 / 单击 (Tap)** 触发、模拟轴阈值判定，以及 **长按与单击** 双重输出（`holdKeyboardKey`、`holdThresholdMs`）。
- **组合键 (Combos)** — 支持多键组合、修饰键判定容差以及 **组合引导键**（`comboLeadButtons`）；游玩时可开启 **组合键 HUD** 显示当前状态。
- **全局应用设置** — 顶部菜单栏的设置面板可全局调整时序、模拟轴默认参数、手柄轮询率与键盘模拟参数；修改将写入 `Assets/Config/local_settings.json`（与独立的游戏模板 JSON 分开存储）。
- **目标进程绑定** — 设定 `targetProcessName` 后，仅当指定的可执行文件（通常不带 `.exe`）处于前台时，映射才会输出。
- **权限自动提示** — 若选中的目标程序以 **管理员** 权限运行，应用将提示并以更高权限重启自身，避免 Windows 因 UIPI（用户界面特权隔离）拦截模拟输入。

## 系统要求

- **操作系统：** Windows（基于 WPF）。
- **.NET SDK：** [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0)（目标框架 `net9.0-windows`）。
- **手柄：** 兼容 XInput 的控制器（基于 [Vortice.XInput](https://github.com/amerkoleci/Vortice.Windows)）。

## 构建与运行

在仓库根目录下执行以下命令：

```powershell
dotnet build "Gamepad Mapping.csproj" -c Release
dotnet run --project "Gamepad Mapping.csproj"
```

你也可以在 Visual Studio 中打开 `GamepadMapping.sln` 并直接运行启动项目。

Release/Debug 构建时会通过 `CopyToOutputDirectory` 自动将 `Assets\Config` 与 `Assets\Profiles` 复制到输出目录中。请确保从包含完整 `Assets` 文件夹的路径运行程序（开发时通常为项目根目录，构建后通常为 `bin\...\net9.0-windows\`）。详见下文的 [内容根路径](https://www.google.com/search?q=%23%E5%86%85%E5%AE%B9%E6%A0%B9%E8%B7%AF%E5%BE%84)。

### 测试

```powershell
dotnet test "GamepadMapping.sln" -c Release
```

## 配置说明

首次运行时，若 `Assets/Config/local_settings.json` 不存在，应用会自动将 `Assets/Config/default_settings.json` 复制一份作为本地配置。后续所有的全局选项均读写 `**local_settings.json**`。

**按键绑定模板**（包含映射表、目标进程名、组合引导键等）仅存放在 `**Assets/Profiles/templates/{profileId}.json`\*\* 中，不会被写入全局应用设置。

### 应用内设置

在 **顶部栏** 点击进入 **应用设置**（位于模板下拉框与「新建配置 (+)」按钮右侧）。面板分为两组配置：

1.  **时序与键盘**

<!-- end list -->

- **组合修饰键容差 / HUD 延迟（`modifierGraceMs`）** — 用于判定组合键的宽限期以及组合键 HUD 的显示延迟；当某条映射未单独设定 `holdThresholdMs` 时，此数值也将作为点按/长按双重绑定的**默认**长按触发阈值。
- **引导键释放防误触（`leadKeyReleaseSuppressMs`）** — 专用于组合引导键：当长按引导键但未完成后续组合键即松开时，可抑制其输出单独的「松开」或短按事件，防止误触发。
- **键盘点按持续时间（`keyboardTapHoldDurationMs`）** — 模拟“点按 (Tap)”操作时，按键保持被按下的物理时长（应用底层会将其限制在安全范围内）。
- **按键连发间隔（`tapInterKeyDelayMs`）** — 连续多次点按操作之间的间隔时间。
- **模拟打字字符间隔（`textInterCharDelayMs`）** — 通过键盘模拟连续逐字输入时的停顿间隔。

<!-- end list -->

2.  **模拟轴与轮询**

<!-- end list -->

- **默认模拟轴激活阈值（`defaultAnalogActivationThreshold`）** — 归一化值 **0–1**；用于那些未单独指定 `analogThreshold` 的摇杆/扳机映射。
- **鼠标视角灵敏度（`mouseLookSensitivity`）** — 映射为鼠标视角移动时的默认缩放系数。
- **模拟轴防抖阈值（`analogChangeEpsilon`）** — 过滤底层传感器的微小抖动，模拟量变化幅度超过该值才视为有效更新（值越小越灵敏，但更新越频繁，占用更高）。
- **手柄轮询间隔（`gamepadPollingIntervalMs`）** — 两次读取 XInput 状态之间的间隔毫秒数（值越小响应越快，值越大越节省 CPU）。

上述修改会实时写入 `local_settings.json`。**摇杆死区** 则需要在主界面的 **手柄实时监视器 → 模拟轴**（齿轮图标）中进行单独调整，对应配置文件中的 `leftThumbstickDeadzone` 和 `rightThumbstickDeadzone`。若未单独设置，则默认使用全局的 `thumbstickDeadzone`。

### `local_settings.json` 字段参考

| 字段名                             | 说明                                                        |
| ---------------------------------- | ----------------------------------------------------------- |
| `templatesDirectory`               | 存放模板 `*.json` 的目录（相对于内容根路径）。              |
| `defaultProfileId`                 | 默认模板的 ID（即不含扩展名的文件名）。                     |
| `lastSelectedTemplateProfileId`    | 上次在界面中选择的模板，用于下次启动时恢复状态。            |
| `modifierGraceMs`                  | 组合键修饰键判定容差、组合键 HUD 延迟及默认的长按触发阈值。 |
| `leadKeyReleaseSuppressMs`         | 组合引导键：中断长按时抑制误触发输出的时间。                |
| `thumbstickDeadzone`               | 左右摇杆未独立设置时的默认归一化死区。                      |
| `leftThumbstickDeadzone`           | 左摇杆独立死区 `[0–1]`（可通过实时监视器调整）。            |
| `rightThumbstickDeadzone`          | 右摇杆独立死区 `[0–1]`（可通过实时监视器调整）。            |
| `leftTriggerInnerDeadzone`         | 左扳机起始死区（归一化）；低于此值视为未按下。              |
| `leftTriggerOuterDeadzone`         | 左扳机触顶死区；高于此值直接视为按满。                      |
| `rightTriggerInnerDeadzone`        | 右扳机起始死区（语义同左）。                                |
| `rightTriggerOuterDeadzone`        | 右扳机触顶死区（语义同左）。                                |
| `gamepadPollingIntervalMs`         | 硬件轮询间隔（毫秒）。                                      |
| `defaultAnalogActivationThreshold` | 未设置 `analogThreshold` 时的默认触发阈值 `[0–1]`。         |
| `mouseLookSensitivity`             | 默认的鼠标视角移动灵敏度系数。                              |
| `analogChangeEpsilon`              | 模拟轴防抖容差，变化超过此幅度才触发状态更新。              |
| `keyboardTapHoldDurationMs`        | 模拟单击时，键盘按键保持按下的物理时长（毫秒）。            |
| `tapInterKeyDelayMs`               | 触发多次单击事件时的连击间隔（毫秒）。                      |
| `textInterCharDelayMs`             | 模拟逐字文本输入时的停顿间隔（毫秒）。                      |

## 模板格式（Profile）

每个配置文件以 `{profileId}.json` 命名，其根对象通常包含：

- `profileId`、`templateGroupId`、`displayName`、可选的 `displayNames`（按语言区域名的映射，例如 `"zh-CN"`）、可选的 `displayNameKey`（从 `Resources/Strings*.resx` 取值）
- `targetProcessName`（可选）— 目标进程的**名称**（通常不含 `.exe`），需与 `Process.ProcessName` 匹配。
- `comboLeadButtons`（可选）— 定义为组合引导键的 XInput 键名，例如 `LeftShoulder`。
- `mappings` — 映射列表：`from`（`type`、`value`）、`keyboardKey`、`trigger`，以及可选的 `analogThreshold`、`holdKeyboardKey`、`holdThresholdMs`、`description`、可选的 `descriptions`（按语言的映射）、可选的 `descriptionKey`（资源键）。

示例配置文件位于 `Assets/Profiles/templates` 目录中（例如 `default.json`、`flight_sim.json`、`roco-kingdom-world.json`）。

## 界面与本地化

应用的界面主题会自动适配 **Windows 系统**的深/浅色偏好。界面文案使用**资源文件**（`Resources/Strings*.resx`）。配置模板标题与映射说明可使用 Profile JSON 中的 **`displayNames` / `descriptions` 映射**（用户自建模板时推荐）；若仍提供 `displayNameKey` / `descriptionKey`，则继续从 `.resx` 解析。程序启动时会按当前 UI 文化 (Culture) 套用以上规则。

## 内容根路径

应用在启动时，会依次在当前目录、`AppContext.BaseDirectory` 及其上级目录中查找 `Assets/Config/default_settings.json` 来确定**内容根目录**。因此，无论是在项目根目录下进行开发调试，还是运行发布后的独立文件夹（可执行文件旁附带 `Assets` 目录），程序均能正常工作。

## 技术栈

- **WPF** — 桌面 UI 框架
- **[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)** — MVVM 架构支持
- **[Newtonsoft.Json](https://www.newtonsoft.com/json)** — JSON 序列化与反序列化
- **[InputSimulatorPlus](https://www.nuget.org/packages/InputSimulatorPlus)** — 键盘与鼠标事件合成
- **[Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput)** — 底层手柄状态读取

## CI/CD 自动化构建

本项目已配置 [GitHub Actions](https://www.google.com/search?q=.github/workflows/build.yml)。当代码推送到 `main` 分支或提交相关 PR 时，将自动执行 `dotnet build` (Release) 校验。
当推送匹配 `v*` 规则的标签（如 `v1.2.0`）时，流水线会构建两种 `win-x64` 产物并打包为 zip，通过 [softprops/action-gh-release](https://github.com/softprops/action-gh-release) 创建 **GitHub Release**：

- **`Gamepad-Mapping-<tag>-win-x64-single.zip`** — **单文件**、自包含运行（无需单独安装 .NET 运行时）。
- **`Gamepad-Mapping-<tag>-win-x64-fx.zip`** — **框架依赖**包（体积更小；需已安装 [.NET 9 桌面运行时](https://dotnet.microsoft.com/download/dotnet/9.0)）。

```powershell
git tag v1.2.0
git push origin v1.2.0
```

## 许可证

本项目采用 [MIT License](https://www.google.com/search?q=LICENSE) 许可证。
