# Keyboard Launcher for Windows

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

发布结果位于 `windows/artifacts/<架构>/`，运行其中的 `KeyboardLauncher.exe`。请分发整个目录，不要只复制 EXE。登录启动引用当前 EXE 的绝对路径；移动目录后需重新开启登录启动。

单独运行验证：

```powershell
dotnet run --project windows/KeyboardLauncher.Checks
dotnet build windows/KeyboardLauncher/KeyboardLauncher.csproj -p:Platform=ARM64
```

## 使用

- 默认 `Ctrl+Shift+Space` 或同一侧 Control 双击显示/隐藏面板。
- 按 macOS 版本的 38 个 ANSI 物理键位启动动作；使用扫描码定位，切换输入语言不会改变键位。
- 点击空键添加绑定，右键编辑；编辑对话框可移除绑定。删除不会改变其他绑定的位置。
- `←` / `→` 翻页，最后一页后可进入新页添加绑定；最多 100 页。
- `Esc` 收起并返回原应用；点击其他应用会收起面板。
- 标题栏（设置按钮以外）可拖动；外窗按 mac 的 26px 半径随缩放裁剪，独立透明阴影层随窗口移动和隐藏，不接收点击或焦点。
- 支持应用/文件/文件夹/Windows URI、HTTP(S) 网址、Windows cmd 命令，以及发送快捷键。
- 编辑时可搜索开始菜单 `.lnk` 应用或使用文件选择器；图标可输入 emoji 或本地图片路径。
- 托盘左键打开面板，右键打开设置或退出；设置沿用 mac 的常规、快捷键、系统三页与卡片分组，支持系统/浅色/深色主题与登录启动。
- `KeyboardLauncher.exe --background` 启动到托盘，登录启动使用此选项。

快捷键输入示例：`ctrl+shift+space`、`alt+f2`、`win+e`。发送动作也支持单键，例如 `f5`。不把 macOS 的 `cmd` 静默映射为 Windows 键。

## 界面语言

Windows 客户端默认使用 English。在 General → Language 可通过分段切换条选择 English 或简体中文，立即生效并自动保存。语言影响面板、设置、绑定编辑器、托盘菜单及应用提示，不改写已有绑定的名称。外观也使用分段切换条，提供 System / Light / Dark。

配置字段 `language` 支持 `en` 和 `zh-CN`；旧配置未包含该字段时默认英文。英文资源集中在 `KeyboardLauncher.Core/Strings.en.json`。

## 配置

Windows 配置：`%LOCALAPPDATA%\KeyboardLauncher\config.json`。

首次启动会从旧 `%LOCALAPPDATA%\Launchpick\config.json` 复制配置（如果新路径不存在），保留旧文件。旧名称只用于兼容迁移。

沿用字段 `shortcut`、`doubleTapKey`、`suppressSystemShortcut`、`launchers`，以及绑定的 `keyIndex`、`name`、`exec`、`icon`、`keyboardShortcut`。Windows 的 `doubleTapKey` 支持 `control`、`alt`、`shift`，也支持 `leftControl` / `rightControl`、`leftAlt` / `rightAlt`、`leftShift` / `rightShift`；`null` 表示关闭；扩展 `theme`、`actionType`（`application` / `url` / `command`）和 `arguments`。

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
| `PanelWindow` | Acrylic 键盘面板、分页、焦点恢复 |
| `SettingsWindow` / `BindingEditor` | Mica 设置、绑定编辑、开始菜单搜索 |
| `DesktopIntegration` / `Native` | 托盘、RegisterHotKey、WH_KEYBOARD_LL / WH_MOUSE_LL、Win32 互操作 |
| `ActionRunner` | Shell 启动、cmd 命令、前台校验与 SendInput |
| `KeyboardLauncher.Checks` | 可独立运行的核心回归检查，不需要 WinUI |

普通快捷键模式报告占用冲突并保留原配置；拦截模式通过钩子吞掉匹配的按下/抬起事件。Ctrl+Alt+Delete 等 Windows 保留组合不能覆盖。双击检测排除长按、重复、组合键、鼠标点击和本程序发送的按键；允许虚拟机或远程键盘标记的输入。钩子回调只检测状态并投递 UI 工作，不执行文件或进程操作。

发送快捷键前等待实体按键释放，激活原窗口，并再次确认焦点。Windows UIPI 不允许普通权限应用向管理员权限应用发送输入，失败会提示。不会自动提升客户端权限。

当前使用非 MSIX、自包含目录部署。尚未包含安装器、代码签名、自动更新或商店应用完整目录扫描。快捷键支持在设置和动作编辑中点击录入，录入期间拦截按键以避免触发其他应用；设置失焦后自动保存。应用文件图标使用 Windows 缩略图接口读取。Windows 材质在系统不支持或关闭透明效果时采用系统回退。

## 视觉与主题回归

程序、托盘、面板与设置使用从 `mac/AppIcon.icns` 原样转换的 `Assets/AppIcon.png` / 多尺寸 `AppIcon.ico`。键帽结构和间距以 mac 的 `ContentView.swift` 与参考截图为准：70px 方形键帽、16px 圆角、名称在键帽下方，空键字母居中。浅色键面为白色、黑色 12% 边框；深色键面为 44% 灰、72% 不透明度。设置背景与卡片使用 mac 的中性灰。窗口材质、外框圆角及字体渲染由 Windows 提供，与 macOS 不会逐像素相同。

键帽内图标沿用 mac 的缩放规则：应用图片四周留 7px（70px 键帽内为 56×56）；单色符号四周留键宽的 24%（36.4×36.4），均保持比例居中。不同平台的图标素材和字体本身仍会影响可见轮廓。

运行 `KeyboardLauncher.exe --theme-smoke-test` 可验证六次主题切换及面板显示/隐藏，报告写入程序目录的 `theme-smoke-test.txt`，完成后退出，不保存测试主题。请先退出正在运行的客户端。`--settings` 直接打开设置。

已在 Windows ARM64 验证主题切换、面板与设置窗口，并通过 11 项核心回归检查；ARM64 / x64 均编译通过。x64 尚未在独立 x64 设备运行验收。

`--control-smoke-test` 使用带测试标记的输入验证左右 Control 双击经过全局键盘钩子后各唤起一次，报告写入程序目录的 `control-smoke-test.txt`；不会保存测试配置。需要先退出已运行实例。

`--panel-smoke-test` 验证窗口圆角区域、Esc 按键经过实际钩子关闭面板，以及前台窗口改变后的自动隐藏。失焦检测在面板显示时运行，编辑绑定期间暂停；隐藏后停止检测和阴影绘制。

官方参考：[WinUI 与 Windows App SDK](https://learn.microsoft.com/windows/apps/get-started/windows-developer-faq)、[Mica / Acrylic](https://learn.microsoft.com/windows/apps/develop/ui/system-backdrops)。
