# Gamepad Mapping

**English:** [README.md](README.md)

Gamepad Mapping 是一款 Windows WPF 应用程序，它可以通过可编辑的配置文件模板将 **兼容 XInput 的手柄输入**转换为**键盘和鼠标输出**。
它专为没有良好原生手柄支持的游戏而设计，并提供可选的前台进程锁定功能，以避免在其他窗口中产生意外输入。

## 最新版本

- **输入栈可配置：** 可选择键鼠模拟的实现方式——经典 Win32 **`SendInput`**，或在受支持系统上使用 Windows **Input Injection**；同时可调整手柄读取相关选项。
- **模拟量与鼠标手感：** 支持摇杆**死区形状**（轴向/径向）、**鼠标视角**灵敏度与平滑，以及可选的**拟人化**鼠标移动噪声（在设置中调节）。
- **组合键快捷键：** 除单键点按外，配置动作可输出**组合键**（如修饰键 + 字母等同时或顺序触发的快捷键）。
- **界面：** **设置**内容更丰富；**更新**使用独立面板；**手柄监视器**窗口布局与体验优化。

完整变更列表见 [`CHANGELOG.md`](CHANGELOG.md)。

## 软件特色

- **复杂手柄映射能力**：支持组合键、连招前导、长按/轻触与阈值，以及手柄上的**轮盘菜单**与可自定义 HUD，适合键位多、原生手柄支持弱的游戏。
- **模板驱动、可维护**：映射以经校验的 JSON 保存，便于编辑、备份与分享；可选的**前台进程过滤**让输出只作用于指定游戏窗口。
- **对接社区模板**：应用内即可浏览、下载社区贡献的布局，无需从零搭配置。
- **贴合 Windows 桌面环境**：目标进程以管理员运行时，应用可提示以同级权限重启，减轻 UIPI 对模拟输入的拦截。
- **模拟后端可切换**：可按环境选择输出路径；可选的移动噪声有助于减轻鼠标移动过于“机械”的感觉。

## 核心功能

- **基于模板的映射**：每个配置文件都是位于 `Assets/Profiles/templates` 目录下的一个 JSON 文件。
- **按键 / 扳机 / 摇杆绑定**：支持按下 (press)、释放 (release)、轻触 (tap)、长按 (hold)、阈值 (thresholds) 和模拟轴激活规则。
- **组合键和连招处理**：支持连招前导键 (combo-lead) 行为、修饰键宽限窗口、可选的 HUD 提示，以及**组合键形式**的快捷键输出。
- **轮盘菜单操作**：通过手柄输入触发方向性操作选择。
- **前台进程过滤**：仅当特定进程处于焦点时才映射输出。
- **应用程序设置**：集中式全局设置，保存至 `Assets/Config/local_settings.json`，包含**摇杆死区形状**、**鼠标视角**相关调节及适用的**模拟后端**选择等。
- **手柄监视器**：实时查看手柄状态，布局便于阅读。
- **内置自动更新**：在应用内独立 **Updates** 区域检查新版本、下载安装包并发起安装。
- **社区模板**：在配置编辑区与键盘动作、轮盘菜单并列的 **Community** 标签中浏览并下载社区贡献的配置。应用从 [`GamepadMapping-CommunityProfiles`](https://github.com/Maxim00191/GamepadMapping-CommunityProfiles) 拉取目录索引（优先 GitHub Raw，不可用时自动降级到 jsDelivr CDN）。

## 自动更新

Gamepad Mapping 内置了基于 GitHub Release 的更新流程，可解析最新发布资源、显示下载进度并发起安装。  
更新链路包含元数据/网络访问回退策略，并在安装交接前执行安装包完整性校验。

## 社区模板

在主窗口中打开某个配置后，切换到 **Community** 标签。点击 **Refresh** 从网络加载最新目录，再在条目上选择下载，即可将对应模板保存到本地模板目录（与内置模板使用相同的 JSON 结构）。下载成功后，配置列表会刷新，可直接选用新模板。

模板内容与贡献规范见上述社区仓库；应用安装包内不包含这些 JSON，运行时按需拉取。短时间内过于频繁的刷新会被节流，以避免频繁请求目录接口。

## 环境要求

- Windows 10/11
- 用于构建的 [.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0) (`net9.0-windows`)
- 兼容 XInput 的手柄

## 快速开始

```powershell
dotnet restore "Gamepad Mapping.csproj"
dotnet build "Gamepad Mapping.csproj" -c Release
dotnet run --project "Gamepad Mapping.csproj"
```

或者在 Visual Studio 中打开 `GamepadMapping.sln` 并运行启动项目。

## 测试

```powershell
dotnet test "GamepadMapping.sln" -c Release
```

## 配置模型

  - `Assets/Config/default_settings.json`：出厂默认设置。
  - `Assets/Config/local_settings.json`：用户重写的全局设置（首次运行时自动创建）。
  - `Assets/Profiles/templates/*.json`：配置文件模板（映射、标签、组合键元数据、目标进程）。

## 开发者文档

  - [`docs/input-pipeline.md`](docs/input-pipeline.md)：输入帧如何经过中间件与映射引擎。
  - [`docs/emulation-parity.md`](docs/emulation-parity.md)：不同模拟后端之间的行为说明。

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
  - [Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput)（读取手柄）
  - 默认通过 Win32 **`SendInput`** 合成键鼠；在受支持的系统上可选用 **Windows Input Injection**

## CI/CD (持续集成/持续部署)

[GitHub Actions](.github/workflows/build.yml) 会在向 `main` 分支推送代码 (push) 和提交拉取请求 (PR) 时验证构建和测试。
打上版本标签（如 `v*`）会创建发布构建（包含单文件版本和依赖框架的 win-x64 压缩包）并发布一个 GitHub Release。

## 更新日志

有关发布历史，请参阅 [`CHANGELOG.md`](CHANGELOG.md)。

## 许可证

MIT — 详见 [`LICENSE`](LICENSE)。