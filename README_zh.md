# Gamepad Mapping

**English:** [README.md](README.md)

Gamepad Mapping 是一款 Windows WPF 应用程序，它可以通过可编辑的配置文件模板将 **XInput 手柄输入**转换为**键盘和鼠标输出**。
它专为没有良好原生手柄支持的游戏而设计，并提供可选的前台进程锁定功能，以避免在其他窗口中产生意外输入。

## 2.0 版本的新特性

- 全新的**轮盘菜单系统**，具有可自定义的 HUD 样式（颜色、透明度、缩放比例）。
- 扩展了配置文件映射的**操作模型 (action model)**（包含更丰富的键盘/轮盘工作流）。
- 改进了多阶段轮盘交互和冲突处理的**输入管道行为**。
- 更好的**模板验证**以及更新的内置模板（特别是《洛克王国：世界》大世界/战斗预设）。
- 稳定了与轮盘相关的屏幕的 UI 渲染和布局行为。

## 核心功能

- **基于模板的映射**：每个配置文件都是位于 `Assets/Profiles/templates` 目录下的一个 JSON 文件。
- **按键 / 扳机 / 摇杆绑定**：支持按下 (press)、释放 (release)、轻触 (tap)、长按 (hold)、阈值 (thresholds) 和模拟轴激活规则。
- **组合键和连招处理**：支持连招前导键 (combo-lead) 行为、修饰键宽限窗口以及可选的 HUD 提示。
- **轮盘菜单操作**：通过手柄输入触发方向性操作选择。
- **前台进程过滤**：仅当特定进程处于焦点时才映射输出。
- **应用程序设置**：集中式全局设置，保存至 `Assets/Config/local_settings.json`。

## 环境要求

- Windows 10/11
- 用于构建的 [.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0) (`net9.0-windows`)
- 兼容 XInput 的手柄

## 快速开始

```powershell
dotnet restore "Gamepad Mapping.csproj"
dotnet build "Gamepad Mapping.csproj" -c Release
dotnet run --project "Gamepad Mapping.csproj"
````

或者在 Visual Studio 中打开 `GamepadMapping.sln` 并运行启动项目。

## 测试

```powershell
dotnet test "GamepadMapping.sln" -c Release
```

## 配置模型

  - `Assets/Config/default_settings.json`：出厂默认设置。
  - `Assets/Config/local_settings.json`：用户重写的全局设置（首次运行时自动创建）。
  - `Assets/Profiles/templates/*.json`：配置文件模板（映射、标签、组合键元数据、目标进程）。

## 模板结构（概览）

每个模板通常包含以下内容：

  - `profileId`、`templateGroupId`、`displayName` / `displayNames`
  - `targetProcessName`（可选）
  - `comboLeadButtons`（可选）
  - 包含输入源和输出操作定义的 `mappings` 集合

参考示例：

  - `Assets/Profiles/templates/default.json`
  - `Assets/Profiles/templates/flight_sim.json`
  - `Assets/Profiles/templates/roco-kingdom-world.json`
  - `Assets/Profiles/templates/roco-kingdom-world-fight.json`

## 提权行为

当目标应用程序以管理员身份运行时，Windows 可能会拦截模拟输入 (UIPI)。
在需要时，Gamepad Mapping 可以提示以管理员权限重新启动，以便映射可以继续在具有管理员权限的目标程序上生效。

## 技术栈

  - WPF (.NET 9)
  - [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
  - [Newtonsoft.Json](https://www.newtonsoft.com/json)
  - [InputSimulatorPlus](https://www.nuget.org/packages/InputSimulatorPlus)
  - [Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput)

## CI/CD (持续集成/持续部署)

[GitHub Actions](https://www.google.com/search?q=.github/workflows/build.yml) 会在向 `main` 分支推送代码 (push) 和提交拉取请求 (PR) 时验证构建和测试。
打上版本标签（如 `v*`）会创建发布构建（包含单文件版本和依赖框架的 win-x64 压缩包）并发布一个 GitHub Release。

## 更新日志

有关发布历史，请参阅 [`CHANGELOG.md`](https://www.google.com/search?q=CHANGELOG.md)。

## 许可证

MIT — 详见 [`LICENSE`](https://www.google.com/search?q=LICENSE)。