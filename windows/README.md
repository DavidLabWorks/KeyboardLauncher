# Keyboard Launcher for Windows — Microsoft Store

基于 `mac/` 的交互和配置模型，使用 C#、WinUI 3、Windows App SDK 与 Win32 实现。项目完全独立，不改动 macOS 源码。

## 构建与运行

要求 Windows 10 2004（19041）或以上、.NET 8 SDK 或以上，以及首次构建时可访问 NuGet。推荐 Windows 11，以使用 Acrylic / Mica 材质。支持 ARM64 和 x64。

```powershell
# 在仓库根目录运行，默认选择本机架构
.\windows\build.ps1 -Run

# 分发目录包含 .NET 和 Windows App SDK 运行时，无需另装运行时
.\windows\build.ps1 -Architecture x64 -Publish
.\windows\build.ps1 -Architecture ARM64 -Publish
```

以上命令仅用于本地开发，显式关闭商店授权检查，不可作为付费商店版分发。开发输出位于 `windows/artifacts/<架构>/`。正式分发使用下文的 MSIX 打包命令；项目默认开启商店授权检查，商店构建在包外运行不会解锁功能。

单独运行验证：

```powershell
dotnet run --project windows/KeyboardLauncher.Checks
dotnet build windows/KeyboardLauncher/KeyboardLauncher.csproj -p:Platform=ARM64 -p:StoreBuild=false
```

## 使用

- 动作卡片分别提供“打开应用”“打开文件”和“打开文件夹”。文件使用默认关联应用打开，文件夹使用资源管理器打开；各自通过专用选择器选取，保存后再次编辑会保留动作类型。应用浏览只显示 `.exe`、`.lnk` 和 `.appref-ms`。

- 默认 `Ctrl+Shift+Space` 或同一侧 Control 双击显示/隐藏面板。
- 按 macOS 版本的 38 个 ANSI 物理键位启动动作；使用扫描码定位，切换输入语言不会改变键位。
- 点击空键添加绑定，右键编辑；编辑对话框可移除绑定。删除不会改变其他绑定的位置。
- `←` / `→` 翻页，最后一页后可进入新页添加绑定；最多 100 页。
- `Esc` 收起并返回原应用；点击其他应用会收起面板。
- 标题栏（设置按钮以外）可拖动；外窗按 mac 的 26px 半径随缩放裁剪，不额外绘制外阴影。
- 支持应用/文件/文件夹/Windows URI、HTTP(S) 网址、Windows cmd 命令，以及发送快捷键。
- 编辑时可搜索开始菜单 `.lnk` 应用或使用文件选择器；图标可输入 emoji 或本地图片路径。
- 托盘左键打开面板，右键打开设置或退出；设置沿用 mac 的常规、快捷键、系统、关于四页与卡片分组，支持系统/浅色/深色主题与登录启动。
- `KeyboardLauncher.exe --background` 启动到托盘，登录启动使用此选项。

快捷键输入示例：`ctrl+shift+space`、`alt+f2`、`win+e`。发送动作也支持单键，例如 `f5`。不把 macOS 的 `cmd` 静默映射为 Windows 键。

## 脚本文件

绑定编辑器的 Shell 命令页通过文件选择器选择 `.cmd`、`.bat` 或 `.ps1`，显示文件名和路径，无需手填路径。新绑定保存为 `actionType: "script"`，以脚本所在目录运行；PowerShell 使用 `-NoProfile -File`，遵守系统执行策略。旧 `command` 绑定保持兼容，重新选择文件后转换为脚本绑定。

## 界面语言

Windows 客户端默认使用 English。在 General → Language 可通过分段切换条选择 English 或简体中文，立即生效并自动保存。语言影响面板、设置、绑定编辑器、托盘菜单及应用提示，不改写已有绑定的名称。外观也使用分段切换条，提供 System / Light / Dark。

配置字段 `language` 支持 `en` 和 `zh-CN`；旧配置未包含该字段时默认英文。英文资源集中在 `KeyboardLauncher.Core/Strings.en.json`。

## 配置

商店版配置：`%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalState\config.json`。首次运行会复制旧 EXE 版 `%LOCALAPPDATA%\KeyboardLauncher\config.json`，不修改原文件。卸载 MSIX 前如需保留设置，请备份 LocalState；Windows 可在卸载时删除包数据。

开发版配置：`%LOCALAPPDATA%\KeyboardLauncher\config.json`。

首次启动会从旧 `%LOCALAPPDATA%\Launchpick\config.json` 复制配置（如果新路径不存在），保留旧文件。旧名称只用于兼容迁移。

沿用字段 `shortcut`、`doubleTapKey`、`suppressSystemShortcut`、`launchers`，以及绑定的 `keyIndex`、`name`、`exec`、`icon`、`keyboardShortcut`。Windows 的 `doubleTapKey` 支持 `control`、`alt`、`shift`，也支持 `leftControl` / `rightControl`、`leftAlt` / `rightAlt`、`leftShift` / `rightShift`，以及 `space`；`null` 表示关闭；扩展 `theme`、`actionType`（`application` / `url` / `command`）和 `arguments`。

```json
{
  "shortcut": "ctrl+shift+space",
  "doubleTapKey": "control",
  "suppressSystemShortcut": false,
  "theme": "system",
  "launchers": [
    { "keyIndex": 12, "name": "记事本", "exec": "notepad.exe", "actionType": "application", "arguments": "", "icon": "📝" },
    { "keyIndex": 13, "name": "复制", "exec": "", "keyboardShortcut": "ctrl+c" }
  ]
}
```

保存先写临时文件再替换，并保留上一版本为 `config.json.bak`。无法读取配置时保留原文件、显示提示，并禁用保存，避免默认值覆盖原数据。手动编辑后重启生效。

配置结构相近不代表命令跨平台兼容：macOS `open -a`、`.app` 路径、Spotlight 设置、SF Symbols、登录启动机制均不能直接搬到 Windows。请在 Windows 重新选择应用和图标。旧配置未填写 `keyIndex` 时按原数组位置读取，首次编辑固定所有键位。

## 实现与边界

| 模块 | 职责 |
| --- | --- |
| `KeyboardLauncher.Core` | JSON 配置、固定键位、快捷键解析、双击 Control 状态机 |
| `PanelWindow` | 大圆角键盘面板、分页、焦点恢复 |
| `SettingsWindow` / `BindingEditor` | Mica 设置、绑定编辑、开始菜单搜索 |
| `TrayMenuWindow` | WinUI 托盘菜单、主题与语言跟随、关闭后隐藏宿主 |
| `DesktopIntegration` / `Native` | 托盘、RegisterHotKey、WH_KEYBOARD_LL / WH_MOUSE_LL、Win32 互操作 |
| `ActionRunner` | Shell 启动、cmd 命令、前台校验与 SendInput |
| `KeyboardLauncher.Checks` | 可独立运行的核心回归检查，不需要 WinUI |

普通快捷键模式报告占用冲突并保留原配置；拦截模式通过钩子吞掉匹配的按下/抬起事件。Ctrl+Alt+Delete 等 Windows 保留组合不能覆盖。双击检测排除长按、重复、组合键、鼠标点击和本程序发送的按键；允许虚拟机或远程键盘标记的输入。钩子回调只检测状态并投递 UI 工作，不执行文件或进程操作。

发送快捷键前等待实体按键释放，激活原窗口，并再次确认焦点。Windows UIPI 不允许普通权限应用向管理员权限应用发送输入，失败会提示。不会自动提升客户端权限。

正式版使用自包含 MSIX，由 Microsoft Store 分发、签名和管理更新。关于页只查询商店更新，不再查询 GitHub Releases。尚未实现商店应用完整目录扫描。快捷键支持在设置和动作编辑中点击录入，录入期间拦截按键以避免触发其他应用；设置失焦后自动保存。应用文件图标使用 Windows 缩略图接口读取。Windows 材质在系统不支持或关闭透明效果时采用系统回退。

## 视觉与主题回归

程序、托盘、面板与设置使用从 `mac/AppIcon.icns` 原样转换的 `Assets/AppIcon.png` / 多尺寸 `AppIcon.ico`。键帽结构和间距以 mac 的 `ContentView.swift` 与参考截图为准：70px 方形键帽、16px 圆角、名称在键帽下方，空键字母居中。浅色键面为白色、黑色 12% 边框；深色键面为 44% 灰、72% 不透明度。设置背景与卡片使用 mac 的中性灰。启动面板使用 26px 大圆角与桌面 Acrylic，显示后清除系统恢复的边框和小圆角，避免底层露出；设置窗口保留系统材质。字体渲染与 macOS 不会逐像素相同。

键帽内图标沿用 mac 的缩放规则：应用图片四周留 7px（70px 键帽内为 56×56）；单色符号四周留键宽的 24%（36.4×36.4），均保持比例居中。不同平台的图标素材和字体本身仍会影响可见轮廓。

运行 `KeyboardLauncher.exe --theme-smoke-test` 可验证六次主题切换及面板显示/隐藏，报告写入程序目录的 `theme-smoke-test.txt`，完成后退出，不保存测试主题。请先退出正在运行的客户端。`--settings` 直接打开设置。

已在 Windows ARM64 验证主题切换、面板与设置窗口，并通过 14 项核心回归检查；ARM64 / x64 均编译通过。x64 尚未在独立 x64 设备运行验收。

`--control-smoke-test` 使用带测试标记的输入验证左右 Control 双击经过全局键盘钩子后各唤起一次，报告写入程序目录的 `control-smoke-test.txt`；不会保存测试配置。需要先退出已运行实例。

`--panel-smoke-test` 验证窗口圆角区域、Esc 按键经过实际钩子关闭面板，以及前台窗口改变后的自动隐藏。失焦检测在面板显示时运行，编辑绑定期间暂停；隐藏后停止检测和阴影绘制。

官方参考：[WinUI 与 Windows App SDK](https://learn.microsoft.com/windows/apps/get-started/windows-developer-faq)、[Mica / Acrylic](https://learn.microsoft.com/windows/apps/develop/ui/system-backdrops)。

## 版本与安装包

根目录 `app.json` 的 `name` 和 `version` 是两端共用的显示名称和版本号。Windows 构建会读取它，设置页和安装器也使用相同信息。可执行文件名、配置目录和 macOS Bundle ID 保持稳定，不随显示名称变化。

使用 Windows SDK 的 MakeAppx 原生工具打包现有 WinUI 桌面程序；无需 NSIS。`.csproj` 中 `WindowsPackageType=None` 负责产生自包含的桌面程序文件，`package.ps1` 为这些文件加入包身份、清单、启动任务和图标，输出真正的 MSIX。

### 先验证打包（尚未创建商店应用）

```powershell
.\windows\package.ps1 -Architecture ARM64 -Preview
.\windows\package.ps1 -Architecture x64 -Preview
```

输出在 `windows/artifacts/store/`。预览包使用 `KeyboardLauncher.LocalPreview` 身份，文件名带 `-preview`，未签名，不能直接双击安装或提交商店，也不会伪造试用授权。MakeAppx 会执行清单和包内容验证；不关闭验证。

### 创建商店应用后

1. 在 Partner Center 预留应用名称；将 `windows/store/identity.example.json` 复制为 `identity.json`，填写产品标识页面的 **Package/Identity/Name**、**Package/Identity/Publisher**、**PublisherDisplayName**。不要自行编造 Publisher。该本地配置已加入忽略列表。
2. 在“定价和可用性”选择付费价格，以及 **Time-limited → 7 days**。采用应用本身的一次性购买，不是订阅或额外内购商品。试用起止时间、购买状态来自 Microsoft Store，不使用本地首次运行时间作为授权。
3. 分别运行 `package.ps1 -Architecture ARM64` 与 `package.ps1 -Architecture x64`，上传生成的两个 `.msix` 到同一提交。商店负责签名；无需把测试证书提交到仓库。第四段版本号为 0；升级前增加根目录 `app.json` 的版本号。
4. 清单声明 `runFullTrust`。审核说明中解释：这是用户主动配置的键盘启动器，需要全局快捷键、托盘和启动本机应用/脚本；不申请管理员权限。
5. 补齐商店中英文介绍、截图、支持信息、隐私政策和年龄评级。通过私有受众/测试发布验证真实商店安装、授权、购买及更新，再提交公开发布。

未配置真实身份时，正式打包会明确失败；`-Preview` 只用于本地验证。未创建/关联商店应用前，不能完成真实交易和商店版本更新的端到端验证。

### 授权与更新验收

- 新用户从商店获取 7 天试用：完整功能可用，关于页显示授权到期时间。
- 到期、无授权或授权验证失败：保留设置与购买/恢复入口，键盘、鼠标、托盘及已打开面板的启动动作均受授权检查约束。
- 一次性购买或恢复购买后刷新商店授权；取消或失败不会误解锁。离线授权由 Store API 的本机缓存处理，不自行保存可伪造的购买标记。
- 商店授权变化会触发重新查询；试用到期时间也在每次启动动作时检查。
- 登录启动由 `StartupTask` 管理，默认关闭；尊重用户或组织在 Windows 设置中的禁用状态，登录启动不会主动弹出面板。
- 更新按钮调用 `GetAppAndOptionalStorePackageUpdatesAsync`；有更新时打开 Microsoft Store 更新页，由商店安装。
- 原 EXE 版如已设置开机启动，迁移前先退出并关闭其开机启动，再卸载旧程序，避免两个发行渠道争用快捷键。旧配置会复制到商店版，原文件保留。

参考：[手动打包桌面应用](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-manual-conversion)、[试用授权](https://learn.microsoft.com/windows/uwp/monetize/implement-a-trial-version-of-your-app)、[商店更新](https://learn.microsoft.com/windows/apps/package-and-deploy/package-updates-from-store)。
