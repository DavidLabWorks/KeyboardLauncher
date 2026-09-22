# Keyboard Launcher

[English](README.md) · [简体中文](README.zh-CN.md)

以键盘面板为入口的启动工具。唤起面板后，按下已绑定的键位或点击图标，即可打开应用、访问网址、执行命令或发送快捷键。

仓库包含两个独立的原生实现：

| 平台 | 技术栈 | 文档 |
| --- | --- | --- |
| macOS | Swift、SwiftUI、AppKit | 本文 |
| Windows | C#、WinUI 3、Windows App SDK、Win32 | [Windows 使用与构建说明](windows/README.md) |

下面介绍的是 **macOS 版已实现的功能**。Windows 的行为和构建方式以独立文档为准，两个版本尚未完全对齐。

## macOS 功能

- **38 个物理键位**：四行键盘布局，删除绑定不会导致其他绑定移位。配置跨越多页时显示翻页按钮。
- **四类动作**：打开应用并传入可选参数、打开网址、执行 Shell 命令、发送录入的键盘快捷键。
- **快捷绑定**：点击空键添加绑定，右键点击已绑定按键进行编辑；悬浮时点击右上角的 × 可快速解绑。
- **拖拽换绑**：将已绑定的键帽拖到空键可移动绑定，拖到已绑定按键可交换绑定。目标键位高亮，图标即时更新，配置在后台保存。
- **独立编辑窗口**：编辑绑定时启动器保持显示，应用选择器提供可搜索的图标网格。
- **自定义图标**：使用应用图标、分类与搜索的 SF Symbols、输入完整系统符号名称，或选择本地图片。可用系统符号取决于 macOS 版本。
- **可配置唤起方式**：录入组合快捷键，或设置某个按键的双击，包括左、右两侧的修饰键。两种方式均可单独清除，长按、重复和组合按键不会计为双击。
- **多屏与拖动**：在鼠标所在屏幕的可用区域居中显示；面板已显示时，将鼠标移到另一块屏幕再次唤起，会把面板移过去。可拖动顶部区域调整位置。
- **主题与语言**：跟随系统、浅色、深色三种外观；支持英语和简体中文，在常规设置中即时切换。已有绑定名称不随语言变化。
- **原生视觉**：macOS 26 及以上使用 Liquid Glass，旧版系统使用视觉效果背景。可点击控件和可拖动标题区域具有相应的鼠标指针反馈。
- **菜单栏与登录启动**：从菜单栏显示启动器、打开设置或退出；可选登录时自动启动。
- **快捷键设置**：辅助功能授权状态、可选的系统快捷键冲突覆盖，以及独立的 Spotlight 快捷键设置。

## macOS 使用方式

1. 从“应用程序”打开 `KeyboardLauncher.app`。
2. 使用默认的 **⌘⇧Space** 显示或隐藏面板。双击唤起需要先到 **设置 → 快捷键** 中录入。
3. 点击空键，选择动作、填写名称并保存。
4. 唤起面板后，按对应的物理键位，或直接点击图标执行动作。执行后面板关闭。

按 **Esc** 关闭面板。点击面板空白处不会关闭；点击面板外的其他应用会关闭。点击齿轮进入设置。

如需执行 `⌘⇧X`，在编辑窗口中选择“键盘快捷键”并录入。录入期间会拦截按键，避免同时触发其他应用的快捷键。执行时会尽可能恢复到之前的应用，再通过系统事件流发送组合键，因此也能触发截图工具等应用的全局快捷键。

双击检测、快捷键录入与拦截、发送快捷键需要按提示授予**辅助功能**权限。更新时保持应用标识和签名身份一致，有助于延续已有授权；改变这些身份可能需要重新授权。

### 共享键盘和鼠标

日常双击唤起在会话事件阶段处理，位于 HID 输入过滤之后；只有快捷键录入保留提前拦截，避免在共享软件过滤输入前就唤起面板。

已通过 **Deskflow** 实际验证：切换到其他电脑后双击，不再误唤起 Mac 启动器。此实现不读取 Deskflow 日志，也不依赖专用适配。其他共享工具尚未逐一验证。

## macOS 构建与安装

部署目标为 **macOS 13 及以上**。当前源码包含经过运行时版本判断的 Liquid Glass API，因此构建需要带有 **macOS 26 SDK** 的 Xcode 工具链。

在仓库根目录执行：

```sh
# 仅编译可执行文件，不打包、不签名。
(cd mac && swift build -c release)

# 使用指定签名身份，构建当前电脑架构的 App 和 DMG。
SIGNING_IDENTITY="你的代码签名身份" bash mac/build.sh
# 指定 ARCH=x86_64 构建 Intel 版，ARCH=arm64 构建 Apple Silicon 版。

# 构建、备份已安装版本、替换并启动。
SIGNING_IDENTITY="你的代码签名身份" bash mac/deploy.sh
```

钥匙串中需要有可用的签名证书及私钥。脚本当前默认使用维护者本机的开发签名身份，其他电脑需通过 `SIGNING_IDENTITY` 覆盖。

产物位置：

- 应用：`mac/build/KeyboardLauncher.app`
- 安装镜像：`mac/dist/KeyboardLauncher-1.0.1-<架构>.dmg`
- 安装位置：`/Applications/KeyboardLauncher.app`
- 安装备份：`mac/dist/backup/`

打包脚本会签名，但**不执行公证**。使用开发证书签名的本地版本不等同于经过公证的公开发行版。脚本按构建电脑的架构生成产物，不生成通用二进制。

## 配置

绑定及唤起快捷键保存在：

```text
~/.config/launchpick/config.json
```

为延续已有安装，保留旧配置目录名称和应用标识 `com.custom.launchpick`。主题与语言另存于 macOS 偏好设置。登录启动使用 `~/Library/LaunchAgents/com.custom.launchpick.plist`。

日常修改建议使用界面。手动修改 JSON 前先备份，修改后重启应用。最小示例：

```json
{
  "shortcut": "cmd+shift+space",
  "doubleTapKey": "59:Left Control",
  "suppressSystemShortcut": false,
  "launchers": [
    {
      "keyIndex": 0,
      "name": "终端",
      "exec": "open -a Terminal"
    },
    {
      "keyIndex": 1,
      "name": "截图",
      "exec": "",
      "icon": "sf:camera",
      "keyboardShortcut": "cmd+shift+x"
    }
  ]
}
```

`keyIndex` 从 0 开始，跨页连续编号。示例绑定前两个按键 `1` 和 `2`。快捷键动作执行的是你 Mac 上该组合键对应的功能，不会安装截图工具。将 `shortcut` 设为空字符串可关闭组合键唤起；省略 `doubleTapKey` 或设为 `null` 可关闭双击唤起。

当前没有云同步或自动更新。macOS 与 Windows 的命令、应用路径、图标和系统设置具有平台差异；JSON 字段相近不代表配置可以直接跨平台使用。

## 检查与源码结构

```sh
# 键位、绑定保存、双击、窗口行为、快捷键录入、图标及本地化检查。
bash mac/test.sh

# 安装并启动后，检查是否只有一个应用进程。
bash mac/Tests/InstalledAppCheck.sh
```

| 路径 | 内容 |
| --- | --- |
| `mac/Sources/KeyboardLauncher/` | macOS 原生应用源码 |
| `mac/Resources/` | 英语与简体中文文案 |
| `mac/Tests/` | 可执行的回归检查 |
| `mac/assets/branding/` | 应用及菜单栏图标源素材 |
| `windows/` | 独立的 Windows 实现 |

自动检查不能替代权限、多屏、共享键鼠，以及其他应用全局快捷键的实际操作验证。

macOS 实现起源于 [Launchpick](https://github.com/scorredoira/launchpick)，并逐步调整为本文描述的键盘面板交互。

## 发布版本号

统一修改项目根目录 `app.json` 中的 `name` 和 `version`，macOS 和 Windows 构建都会读取它；Windows 设置页和安装包信息也使用同一版本号。
