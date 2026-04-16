# Gamepad Mapping

**English:** [README.md](README.md)

Gamepad Mapping 是一款 Windows 桌面应用（WPF），通过可编辑的 JSON 配置模板，将 **兼容 XInput 的手柄输入**映射为**键盘和鼠标输出**。面向缺少原生手柄支持的游戏：在同一工作区内提供基于 SVG 的**可视化编辑器**、映射列表、**键盘动作**与**轮盘菜单**目录、可选的**前台进程**目标、**Community** 社区模板的下载与上传，以及基于 GitHub 的**应用内更新**。

## 网络与安全

> [!TIP]
> **本软件如何使用网络**
>
> - **社区模板（下载）：** 打开 **Community** 标签并 **Refresh** 时，会通过 HTTPS 拉取公开目录索引（GitHub Raw，失败时可经 CDN 回退）。对条目执行 **Download** 时，会将 **JSON 配置文件**下载到本地模板目录；应用在本地解析 JSON，**不会**执行来自网络的代码。
> - **社区模板（上传）：** 使用 **Upload…** 时，在上传对话框中完成 **Cloudflare Turnstile** 校验后，通过 HTTPS 将模板包与元数据提交到已配置的 **Cloudflare Worker**；由 Worker 在 GitHub 上创建 **Pull Request**。应用内**不保存**你的 GitHub 个人访问令牌。
> - **应用更新：** **Updates** 区域会访问 **GitHub**（发布信息与安装包资源）以检查新版本、可选地下载官方发布的安装包，并在安装前进行**完整性校验**。

> [!WARNING]
> **非官方下载渠道或二次打包的安装包**
>
> 第三方镜像、二次打包或所谓「绿色版」可能**篡改自动更新逻辑**、**替换下载地址**或捆绑**恶意程序**（包括记录键盘等输入的软件）。开发者仅支持与本开源仓库一致的构建。**请从本仓库 [Releases](https://github.com/Maxim00191/Gamepad-Mapping/releases) 获取安装包，或自行从可信源码克隆后编译。**

## 付费说明与官方分发

Gamepad Mapping 是在 MIT 许可证下的**免费开源软件**。正式版本在 GitHub 上**免费**发布。开发者不会通过第三方应用商店、代理商或付费下载页销售本应用、激活码或所谓「高级版」。

**若有人要求你为下载、激活、授权码或「高级功能」付费，请视为垃圾信息或诈骗**，并仅从本仓库的 [Releases](https://github.com/Maxim00191/Gamepad-Mapping/releases) 获取安装包（或自行从源码构建）。

## 功能概览

### 映射与输出

- 读取兼容 XInput 的手柄输入；通过经典 Win32 **`SendInput`** 或在受支持系统上使用 **Windows Input Injection** 合成键鼠（可在设置中选择）。
- **按键、扳机、摇杆**绑定：按下、释放、轻触、长按、阈值与模拟轴激活规则。
- **组合键与连招：** 连招前导键行为、修饰键宽限窗口、可选 HUD 提示，以及**组合键形式**的快捷键输出。
- **轮盘菜单：** 通过手柄输入触发方向性操作，HUD 可自定义。
- **前台进程过滤：** 仅在指定进程处于焦点时输出映射。
- **手感调节：** 摇杆**死区形状**（轴向/径向）、**鼠标视角**灵敏度与平滑，以及可选的**拟人化**鼠标移动噪声（在设置中调节）。

### 配置工作区

- **基于模板的配置：** 每个配置均为经校验的 JSON 文件，位于 `Assets/Profiles/templates`。
- **可视化编辑器（SVG）：** 可交互手柄示意图、分区命中、**平移/缩放**、可选**示意图标签**（动作摘要或物理键名等）；侧栏列出与当前选中控制相关的全部映射。
- **Mappings** 标签页，以及内置的**键盘动作**与**轮盘菜单**目录。
- **手柄监视器：** 实时查看手柄状态，布局便于阅读。

### 社区模板

- 在应用内 **Community** 标签**浏览并下载**公开模板；目录数据从 [`GamepadMapping-CommunityProfiles`](https://github.com/Maxim00191/GamepadMapping-CommunityProfiles) 拉取（优先 GitHub Raw，不可用时回退到 jsDelivr CDN）。
- **上传**自己的布局：引导填写元数据、自动预检后通过 **Cloudflare Worker** 与 **Turnstile** 提交；成功后创建 **Pull Request** 供审核（客户端**无需**个人 GitHub Token）。详细步骤与进阶配置见下文 [社区模板使用说明](#社区模板使用说明)。

### 更新、设置与系统集成

- **应用内更新：** **Updates** 区域解析 GitHub 发布信息、带进度下载安装包、校验完整性后交接安装（网络异常时具备合理回退）。
- **提权：** 当目标进程以管理员运行时，应用可提示以同级权限重启，减轻 UIPI 对模拟输入的拦截。
- **全局设置**保存在首次运行时创建的 `Assets/Config/local_settings.json`；出厂默认见 `Assets/Config/default_settings.json`。

## 版本说明

各版本的详细变更见 [`CHANGELOG.md`](CHANGELOG.md)。安装包与发布资源见 **[GitHub Releases](https://github.com/Maxim00191/Gamepad-Mapping/releases)**。

## 社区模板使用说明

在主窗口中打开某个配置后，切换到 **Community** 标签。

**浏览与安装：** 点击 **Refresh** 从网络加载最新目录，在条目上选择 **Download**，即可将模板保存到本地模板目录（与内置模板使用相同的 JSON 结构）。下载成功后，配置列表会刷新，可直接选用新模板。

**分享你的布局：** 在顶部配置列表中选中要投稿的模板后，选择 **Upload…**。应用会收集关联模板、引导填写**游戏文件夹**、**作者**与**列表说明**，并执行**合规检查**（映射、ID、体积限制等），随后通过 **Cloudflare Worker** 与 **Cloudflare Turnstile** 校验提交；成功后会在 [`GamepadMapping-CommunityProfiles`](https://github.com/Maxim00191/GamepadMapping-CommunityProfiles) 上打开 **Pull Request** 供开发者审核——客户端**无需**配置个人 GitHub Token。

模板内容与贡献规范见上述社区仓库；应用安装包内不包含目录 JSON，运行时按需拉取。短时间内过于频繁的刷新会被节流，以避免频繁请求目录接口。

**进阶：** 默认上传接口与 Turnstile 行为名在 `Assets/Config/default_settings.json` 中（`communityProfilesUploadWorkerUrl`、`communityProfilesUploadTurnstileAction`）。若自建 Worker 基础设施，可在 `Assets/Config/local_settings.json` 中覆盖。

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

- `Assets/Profiles/templates/Elden Ring/default.json`
- `Assets/Profiles/templates/Flight Sim/flight_sim.json`
- `Assets/Profiles/templates/Roco Kingdom/roco-kingdom-world-radial.json`
- `Assets/Profiles/templates/Roco Kingdom/roco-kingdom-world-fight-radial.json`

## 提权与 UIPI

当目标应用程序以管理员身份运行时，Windows 可能会拦截模拟输入 (UIPI)。在需要时，Gamepad Mapping 可以提示以管理员权限重新启动，以便映射可以继续在具有管理员权限的目标程序上生效。

## 技术栈

- WPF (.NET 9)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [Vortice.XInput](https://www.nuget.org/packages/Vortice.XInput)（读取手柄）
- 默认通过 Win32 **`SendInput`** 合成键鼠；在受支持的系统上可选用 **Windows Input Injection**

## CI/CD（持续集成与发布）

[GitHub Actions](.github/workflows/build.yml) 会在向 `main` 分支推送代码与提交拉取请求时验证构建与测试。  
打上版本标签（如 `v*`）会创建发布构建（单文件与依赖框架的 win-x64 包）并发布 GitHub Release。

## 许可证

MIT — 详见 [`LICENSE`](LICENSE)。
