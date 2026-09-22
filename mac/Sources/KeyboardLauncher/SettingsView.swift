import SwiftUI
import UniformTypeIdentifiers

// MARK: - Models

enum ActionType: String, CaseIterable, Equatable {
    case openApp = "Open App"
    case openURL = "Open URL"
    case shellCommand = "Shell Command"
    case keyboardShortcut = "Keyboard Shortcut"
}

enum IconMode: String, CaseIterable, Equatable {
    case auto = "Auto-detect"
    case sfSymbol = "Symbol"
    case appIcon = "App Icon"
    case custom = "Custom Image"
}

struct EditableLauncher: Identifiable, Equatable {
    var id = UUID()
    var name: String = "New Item"
    var actionType: ActionType = .openApp
    var appName: String = ""
    var appArgs: String = ""
    var url: String = ""
    var shellCommand: String = ""
    var keyboardShortcut: String = ""
    var iconMode: IconMode = .auto
    var iconValue: String = ""

    static func from(_ config: ConfigLauncher) -> EditableLauncher {
        var l = EditableLauncher()
        l.name = config.name

        let exec = config.exec
        if let shortcut = config.keyboardShortcut {
            l.actionType = .keyboardShortcut
            l.keyboardShortcut = shortcut
        } else if let appName = extractAppName(from: exec) {
            l.actionType = .openApp
            l.appName = appName
            l.appArgs = extractAppArgs(from: exec, appName: appName)
        } else if exec.hasPrefix("open ") && (exec.contains("http://") || exec.contains("https://")) {
            l.actionType = .openURL
            l.url = extractURL(from: exec)
        } else {
            l.actionType = .shellCommand
            l.shellCommand = exec
        }

        if let icon = config.icon {
            if icon.hasPrefix("sf:") {
                l.iconMode = .sfSymbol
                l.iconValue = String(icon.dropFirst(3))
            } else if icon.hasSuffix(".app") {
                l.iconMode = .appIcon
                l.iconValue = icon
            } else if !icon.isEmpty {
                l.iconMode = .custom
                l.iconValue = icon
            }
        }

        return l
    }

    func toConfig() -> ConfigLauncher {
        let exec: String
        switch actionType {
        case .openApp:
            if appName.isEmpty {
                exec = ""
            } else if appArgs.isEmpty {
                exec = "open -a '\(appName)'"
            } else {
                exec = "open -a '\(appName)' '\(appArgs)'"
            }
        case .openURL:
            exec = url.isEmpty ? "" : "open '\(url)'"
        case .shellCommand:
            exec = shellCommand
        case .keyboardShortcut:
            exec = ""
        }

        let icon: String?
        switch iconMode {
        case .auto: icon = nil
        case .sfSymbol: icon = iconValue.isEmpty ? nil : "sf:\(iconValue)"
        case .appIcon: icon = iconValue.isEmpty ? nil : iconValue
        case .custom: icon = iconValue.isEmpty ? nil : iconValue
        }

        return ConfigLauncher(name: name, exec: exec, icon: icon,
                              keyboardShortcut: actionType == .keyboardShortcut ? keyboardShortcut : nil)
    }

    func resolvedIcon() -> NSImage {
        switch iconMode {
        case .auto:
            if actionType == .keyboardShortcut { return NSImage(systemSymbolName: "keyboard", accessibilityDescription: nil)! }
            return IconResolver.resolve(icon: nil, exec: toConfig().exec)
        case .sfSymbol:
            if !iconValue.isEmpty,
               let img = NSImage(systemSymbolName: iconValue, accessibilityDescription: nil) {
                return img
            }
        case .appIcon:
            if !iconValue.isEmpty {
                return NSWorkspace.shared.icon(forFile: iconValue)
            }
        case .custom:
            if let img = NSImage(contentsOfFile: iconValue) {
                return img
            }
        }
        return NSImage(systemSymbolName: "app.fill", accessibilityDescription: nil)!
    }

    // MARK: Parsing

    private static func extractAppName(from exec: String) -> String? {
        let patterns = [
            #"open\s+(?:-\w\s+)*-a\s+'([^']+)'"#,
            #"open\s+(?:-\w\s+)*-a\s+"([^"]+)""#,
            #"open\s+(?:-\w\s+)*-a\s+(\S+)"#,
        ]
        for pattern in patterns {
            guard let regex = try? NSRegularExpression(pattern: pattern),
                  let match = regex.firstMatch(in: exec, range: NSRange(exec.startIndex..., in: exec)),
                  let range = Range(match.range(at: 1), in: exec) else { continue }
            return String(exec[range])
        }
        return nil
    }

    private static func extractAppArgs(from exec: String, appName: String) -> String {
        let escaped = NSRegularExpression.escapedPattern(for: appName)
        let patterns = [
            "open\\s+(?:-\\w\\s+)*-a\\s+'\(escaped)'\\s*",
            "open\\s+(?:-\\w\\s+)*-a\\s+\"\(escaped)\"\\s*",
            "open\\s+(?:-\\w\\s+)*-a\\s+\(escaped)\\s*",
        ]
        for pattern in patterns {
            guard let regex = try? NSRegularExpression(pattern: pattern),
                  let match = regex.firstMatch(in: exec, range: NSRange(exec.startIndex..., in: exec)),
                  let range = Range(match.range, in: exec) else { continue }
            var args = String(exec[range.upperBound...]).trimmingCharacters(in: .whitespaces)
            // Strip surrounding quotes so the UI shows clean paths
            if (args.hasPrefix("'") && args.hasSuffix("'")) ||
               (args.hasPrefix("\"") && args.hasSuffix("\"")) {
                args = String(args.dropFirst().dropLast())
            }
            return args
        }
        return ""
    }

    private static func extractURL(from exec: String) -> String {
        let patterns = [
            #"open\s+'([^']+)'"#,
            #"open\s+"([^"]+)""#,
            #"open\s+(\S+)"#,
        ]
        for pattern in patterns {
            guard let regex = try? NSRegularExpression(pattern: pattern),
                  let match = regex.firstMatch(in: exec, range: NSRange(exec.startIndex..., in: exec)),
                  let range = Range(match.range(at: 1), in: exec) else { continue }
            return String(exec[range])
        }
        return ""
    }
}

// MARK: - App Scanner

class AppScanner {
    static let shared = AppScanner()

    struct App: Identifiable {
        let id: String
        let name: String
        let path: String
        let icon: NSImage
    }

    lazy var apps: [App] = {
        var result: [App] = []
        var seen = Set<String>()
        let fm = FileManager.default
        let searchPaths = [
            "/Applications",
            "/System/Applications",
            "/System/Applications/Utilities",
            "\(NSHomeDirectory())/Applications",
        ]

        for basePath in searchPaths {
            guard let items = try? fm.contentsOfDirectory(atPath: basePath) else { continue }
            for item in items.sorted() where item.hasSuffix(".app") {
                let fullPath = "\(basePath)/\(item)"
                let name = String(item.dropLast(4))
                guard seen.insert(name).inserted else { continue }
                let icon = NSWorkspace.shared.icon(forFile: fullPath)
                result.append(App(id: fullPath, name: name, path: fullPath, icon: icon))
            }
        }

        return result.sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }()
}

// MARK: - Settings State

class SettingsState: ObservableObject {
    @Published var shortcut: String = "cmd+shift+space"
    @Published var suppressSystemShortcut: Bool = false
    @Published var doubleTapKey = ""
    @Published var spotlightShortcut: String = ""
    var columns: Int = 4

    func load() {
        let config = LaunchpickConfig.load()
        shortcut = config.shortcut
        doubleTapKey = config.doubleTapKey ?? ""
        suppressSystemShortcut = config.suppressSystemShortcut ?? false
        spotlightShortcut = config.spotlightShortcut ?? ""
        columns = config.columns ?? 4
    }

    func save() {
        if !spotlightShortcut.isEmpty {
            suppressSystemShortcut = false
        }
        LaunchpickConfig.update { config in
            config.shortcut = shortcut
            config.doubleTapKey = doubleTapKey.isEmpty ? nil : doubleTapKey
            config.suppressSystemShortcut = suppressSystemShortcut
            config.spotlightShortcut = spotlightShortcut.isEmpty ? nil : spotlightShortcut
            config.columns = columns
            return true
        }
        NotificationCenter.default.post(name: Notification.Name("ReloadHotKey"), object: nil)
    }

}

// MARK: - Launch at Login

enum LaunchAtLogin {
    private static let plistPath = NSHomeDirectory() + "/Library/LaunchAgents/com.custom.launchpick.plist"

    static var isEnabled: Bool {
        FileManager.default.fileExists(atPath: plistPath)
    }

    static func setEnabled(_ enabled: Bool) {
        if enabled {
            let plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.custom.launchpick</string>
                <key>ProgramArguments</key>
                <array>
                    <string>/Applications/KeyboardLauncher.app/Contents/MacOS/KeyboardLauncher</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
                <key>KeepAlive</key>
                <false/>
            </dict>
            </plist>
            """
            let dir = (plistPath as NSString).deletingLastPathComponent
            try? FileManager.default.createDirectory(atPath: dir, withIntermediateDirectories: true)
            try? plist.write(toFile: plistPath, atomically: true, encoding: .utf8)
            DispatchQueue.global(qos: .utility).async {
                let process = Process()
                process.executableURL = URL(fileURLWithPath: "/bin/launchctl")
                process.arguments = ["load", plistPath]
                try? process.run()
                process.waitUntilExit()
            }
        } else {
            DispatchQueue.global(qos: .utility).async {
                let process = Process()
                process.executableURL = URL(fileURLWithPath: "/bin/launchctl")
                process.arguments = ["unload", plistPath]
                try? process.run()
                process.waitUntilExit()
                try? FileManager.default.removeItem(atPath: plistPath)
            }
        }
    }
}

// MARK: - Settings View

enum AppAppearance: String, CaseIterable {
    case system = "System", light = "Light", dark = "Dark"

    func apply() {
        switch self {
        case .system: NSApp.appearance = nil
        case .light: NSApp.appearance = NSAppearance(named: .aqua)
        case .dark: NSApp.appearance = NSAppearance(named: .darkAqua)
        }
    }
}

enum SettingsPage: String, CaseIterable, Identifiable {
    case general = "General", shortcuts = "Shortcuts", system = "System"
    var id: String { rawValue }
    var icon: String {
        switch self {
        case .general: return "gearshape.fill"
        case .shortcuts: return "command"
        case .system: return "macwindow"
        }
    }
    var color: Color {
        switch self {
        case .general: return .gray
        case .shortcuts: return .blue
        case .system: return .purple
        }
    }
}

struct SettingsView: View {
    @Environment(\.locale) private var locale
    @ObservedObject var state: SettingsState
    @State private var launchAtLogin = LaunchAtLogin.isEnabled
    @State private var selection: SettingsPage? = .general

    var body: some View {
        let _ = locale
        NavigationSplitView {
            List(SettingsPage.allCases, selection: $selection) { page in
                Label {
                    Text(LocalizedStringKey(page == .system ? "System Settings Page" : page.rawValue)).font(.system(size: 13, weight: .medium))
                } icon: {
                    Image(systemName: page.icon)
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(.white)
                        .frame(width: 26, height: 26)
                        .background(page.color.gradient, in: RoundedRectangle(cornerRadius: 7))
                }
                .padding(.vertical, 5)
                .tag(page)
            }
            .listStyle(.sidebar)
            .navigationSplitViewColumnWidth(min: 180, ideal: 200, max: 240)
            .safeAreaInset(edge: .bottom) {
                Text(verbatim: Bundle.main.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String ?? "Keyboard Launcher").font(.caption).foregroundStyle(.secondary)
                    .padding(18).frame(maxWidth: .infinity, alignment: .leading)
            }
        } detail: {
            SettingsDetailView(state: state, launchAtLogin: $launchAtLogin, page: selection ?? .general)
                .navigationTitle(LocalizedStringKey(selection == .system ? "System Settings Page" : (selection ?? .general).rawValue))
        }
        .navigationSplitViewStyle(.balanced)
        .frame(minWidth: 800, minHeight: 520)
    }
}

// MARK: - Shortcut Recorder

struct ShortcutRecorderView: NSViewRepresentable {
    @Environment(\.locale) private var locale
    @Binding var shortcut: String
    var singleKeyOnly = false
    var onSave: () -> Void

    func makeNSView(context: Context) -> ShortcutRecorderNSView {
        let view = ShortcutRecorderNSView()
        view.shortcut = shortcut
        view.singleKeyOnly = singleKeyOnly
        view.setAccessibilityElement(true)
        view.setAccessibilityRole(.button)
        view.setAccessibilityLabel(L(singleKeyOnly ? "Record Double-Tap Key" : "Record Shortcut"))
        view.onChange = { newShortcut in
            shortcut = newShortcut
            onSave()
        }
        return view
    }

    func updateNSView(_ nsView: ShortcutRecorderNSView, context: Context) {
        _ = locale
        nsView.setAccessibilityLabel(L(singleKeyOnly ? "Record Double-Tap Key" : "Record Shortcut"))
        nsView.shortcut = shortcut
        nsView.setAccessibilityValue(shortcut.isEmpty ? L("Not set") : shortcut)
        nsView.needsDisplay = true
    }
}

class ShortcutRecorderNSView: NSView {
    var shortcut: String = ""
    var singleKeyOnly = false
    var onChange: ((String) -> Void)?
    private(set) var isRecording = false
    private var shortcutBeforeRecording: String = ""
    private var trackingArea: NSTrackingArea?
    private var isHovered = false
    private var keyMonitor: Any?

    override var acceptsFirstResponder: Bool { true }

    override var intrinsicContentSize: NSSize {
        NSSize(width: 200, height: 28)
    }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let existing = trackingArea { removeTrackingArea(existing) }
        trackingArea = NSTrackingArea(rect: bounds, options: [.mouseEnteredAndExited, .activeAlways], owner: self)
        addTrackingArea(trackingArea!)
    }

    override func mouseEntered(with event: NSEvent) { isHovered = true; needsDisplay = true }
    override func mouseExited(with event: NSEvent) { isHovered = false; needsDisplay = true }

    override func draw(_ dirtyRect: NSRect) {
        let rect = bounds.insetBy(dx: 0.5, dy: 0.5)
        let path = NSBezierPath(roundedRect: rect, xRadius: 6, yRadius: 6)

        if isRecording {
            NSColor.controlAccentColor.withAlphaComponent(0.08).setFill()
        } else if isHovered {
            NSColor.launcherControlBackground.setFill()
        } else {
            NSColor.launcherControlBackground.setFill()
        }
        path.fill()

        let borderColor = isRecording ? NSColor.controlAccentColor : NSColor.separatorColor
        borderColor.setStroke()
        path.lineWidth = isRecording ? 2 : 1
        path.stroke()

        let text: String
        let color: NSColor
        if isRecording {
            text = L("Press shortcut…")
            color = .controlAccentColor
        } else if shortcut.isEmpty {
            text = L("Not set")
            color = .secondaryLabelColor
        } else {
            text = displayString(for: shortcut)
            color = .labelColor
        }

        let attrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 13, weight: .medium),
            .foregroundColor: color,
        ]
        let attrStr = NSAttributedString(string: text, attributes: attrs)
        let size = attrStr.size()
        let point = NSPoint(x: (bounds.width - size.width) / 2, y: (bounds.height - size.height) / 2)
        attrStr.draw(at: point)
    }

    override func mouseDown(with event: NSEvent) {
        toggleRecording()
    }

    override func accessibilityPerformPress() -> Bool {
        toggleRecording()
        return true
    }

    private func toggleRecording() {
        if isRecording {
            cancelRecording()
            return
        }
        isRecording = true
        shortcutBeforeRecording = shortcut
        window?.makeFirstResponder(self)
        needsDisplay = true
        installKeyMonitor()
    }

    override func keyDown(with event: NSEvent) {
        guard !isRecording else { return }
        super.keyDown(with: event)
    }

    override func resignFirstResponder() -> Bool {
        if isRecording { cancelRecording() }
        return super.resignFirstResponder()
    }

    private func installKeyMonitor() {
        removeKeyMonitor()
        keyMonitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown, .flagsChanged]) { [weak self] event in
            guard let self = self, self.isRecording else { return event }

            return self.record(event)
        }
    }

    // Shared by the early system event tap and the local fallback.
    func record(_ event: NSEvent) -> NSEvent? {
        guard isRecording else { return event }
        // Escape cancels recording
        if event.keyCode == 53 {
            self.cancelRecording()
            return nil
        }

        if self.singleKeyOnly {
            let modifiers: [UInt16: String] = [58: "Left Option", 61: "Right Option", 55: "Left Command", 54: "Right Command", 59: "Left Control", 62: "Right Control", 56: "Left Shift", 60: "Right Shift", 63: "Fn", 57: "Caps Lock"]
            let label = modifiers[event.keyCode] ?? self.keyName(for: event.keyCode) ?? event.charactersIgnoringModifiers?.uppercased()
            guard let label, !label.isEmpty else { return nil }
            self.isRecording = false
            self.shortcut = "\(event.keyCode):\(label)"
            self.onChange?(self.shortcut)
            self.needsDisplay = true
            self.removeKeyMonitor()
            return nil
        }
        guard event.type == .keyDown else { return event }

        let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        let isFunctionKey = flags.contains(.function) || self.keyName(for: event.keyCode)?.hasPrefix("f") == true
        let hasModifier = !flags.intersection([.command, .control, .option, .shift]).isEmpty

        guard hasModifier || isFunctionKey else { return nil }

        var parts: [String] = []
        if flags.contains(.command) { parts.append("cmd") }
        if flags.contains(.control) { parts.append("ctrl") }
        if flags.contains(.option) { parts.append("alt") }
        if flags.contains(.shift) { parts.append("shift") }

        if let keyName = self.keyName(for: event.keyCode) {
            parts.append(keyName)
        } else if let chars = event.charactersIgnoringModifiers?.lowercased(), !chars.isEmpty {
            parts.append(chars)
        } else {
            return nil
        }

        self.isRecording = false
        self.shortcut = parts.joined(separator: "+")
        self.onChange?(self.shortcut)
        self.needsDisplay = true
        self.removeKeyMonitor()
        return nil
    }

    private func removeKeyMonitor() {
        if let monitor = keyMonitor {
            NSEvent.removeMonitor(monitor)
            keyMonitor = nil
        }
    }

    private func cancelRecording() {
        isRecording = false
        shortcut = shortcutBeforeRecording
        needsDisplay = true
        removeKeyMonitor()
    }

    private func keyName(for keyCode: UInt16) -> String? {
        let map: [UInt16: String] = [
            49: "space", 36: "return", 48: "tab", 51: "delete", 53: "escape",
            123: "left", 124: "right", 125: "down", 126: "up", 50: "`",
            122: "f1", 120: "f2", 99: "f3", 118: "f4", 96: "f5", 97: "f6",
            98: "f7", 100: "f8", 101: "f9", 109: "f10", 103: "f11", 111: "f12",
            105: "f13", 107: "f14", 113: "f15", 106: "f16", 64: "f17", 79: "f18", 80: "f19",
        ]
        return map[keyCode] ?? LaunchpickConfig.keyCodeMap.first { $0.value == UInt32(keyCode) }?.key
    }

    private func displayString(for shortcut: String) -> String {
        if singleKeyOnly { return L(shortcut.split(separator: ":", maxSplits: 1).last.map(String.init) ?? shortcut) }
        return shortcut.split(separator: "+").map { part in
            switch part.lowercased() {
            case "cmd", "command": return "\u{2318}"
            case "ctrl", "control": return "\u{2303}"
            case "alt", "opt", "option": return "\u{2325}"
            case "shift": return "\u{21E7}"
            case "space": return L("Space")
            case "tab": return "\u{21E5}"
            case "return", "enter": return "\u{21A9}"
            case "delete": return "\u{232B}"
            case "escape": return "\u{238B}"
            case "`", "~": return "`"
            case "up": return "\u{2191}"
            case "down": return "\u{2193}"
            case "left": return "\u{2190}"
            case "right": return "\u{2192}"
            default: return part.uppercased()
            }
        }.joined(separator: " ")
    }
}

// MARK: - Settings Detail

struct SettingsDetailView: View {
    @Environment(\.locale) private var locale
    @ObservedObject var state: SettingsState
    @Binding var launchAtLogin: Bool
    let page: SettingsPage
    @AppStorage("appLanguage") private var language = AppLanguage.defaultCode
    @AppStorage("appearanceMode") private var appearanceMode: AppAppearance = .system
    @State private var accessibilityGranted = AccessibilityHelper.isTrusted
    @Environment(\.colorScheme) private var colorScheme

    var body: some View {
        let _ = locale
        ScrollView {
            VStack(alignment: .leading, spacing: 28) {
                if page == .general {
                    HStack(spacing: 20) {
                        Image(nsImage: IconResolver.appIcon)
                            .resizable().scaledToFit().frame(width: 76, height: 76)
                        VStack(alignment: .leading, spacing: 7) {
                            Text(verbatim: Bundle.main.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String ?? "Keyboard Launcher").font(.system(size: 24, weight: .semibold))
                            Text("Your apps, one keystroke away.")
                                .font(.system(size: 13)).foregroundStyle(.secondary)
                            Text(String(format: L("Version %@"), Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0"))
                                .font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer(minLength: 0)
                    }
                    .padding(24)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(groupBackground, in: RoundedRectangle(cornerRadius: 18))
                settingsSection("General") {
                    HStack {
                        settingLabel("Language", detail: "Choose the app language.")
                        Spacer()
                        Picker("Language", selection: $language) {
                            Text(verbatim: "English").tag("en")
                            Text(verbatim: "简体中文").tag("zh-Hans")
                        }
                        .pickerStyle(.segmented)
                        .labelsHidden()
                        .frame(width: 210)
                        .onChange(of: language) { _ in
                            NotificationCenter.default.post(name: AppLanguage.changed, object: nil)
                        }
                    }
                    Divider()
                    HStack(spacing: 20) {
                        settingLabel("Appearance", detail: "Choose a theme or follow your Mac.")
                        Spacer(minLength: 8)
                        Picker("Appearance", selection: $appearanceMode) {
                            ForEach(AppAppearance.allCases, id: \.self) { mode in
                                Text(LocalizedStringKey(mode.rawValue)).tag(mode)
                            }
                        }
                        .pickerStyle(.segmented)
                        .labelsHidden()
                        .frame(width: 210)
                    }
                    Divider()
                    HStack {
                        settingLabel("Launch at login", detail: "Start automatically when you sign in.")
                        Spacer()
                        Toggle("Launch at login", isOn: $launchAtLogin)
                            .labelsHidden()
                            .toggleStyle(.switch)
                            .onChange(of: launchAtLogin) { LaunchAtLogin.setEnabled($0) }
                    }
                }

                    settingsSection("Permissions") {
                        HStack(spacing: 12) {
                            Image(systemName: "hand.raised.fill")
                                .font(.system(size: 16))
                                .foregroundStyle(.white)
                                .frame(width: 32, height: 32)
                                .background(Color.purple.gradient, in: RoundedRectangle(cornerRadius: 8))
                            settingLabel("Accessibility", detail: accessibilityGranted ? "Enabled for double-tap shortcuts." : "Required for double-tap shortcuts.")
                            Spacer(minLength: 8)
                            Button("Open Settings") {
                                NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
                            }
                            .controlSize(.small)
                        }
                    }
                } else if page == .shortcuts {
                settingsSection("Shortcuts") {
                    HStack(spacing: 20) {
                        settingLabel("Keyboard shortcut", detail: "Record a key combination.")
                        Spacer(minLength: 12)
                        VStack(alignment: .trailing, spacing: 6) {
                            ShortcutRecorderView(shortcut: $state.shortcut) { state.save() }
                                .frame(width: 200, height: 32)
                            if !state.shortcut.isEmpty {
                                Button("Clear") { state.shortcut = ""; state.save() }
                                    .buttonStyle(.borderless)
                                    .foregroundStyle(.secondary)
                                    .font(.caption)
                                    .help("Disable the keyboard shortcut")
                            }
                        }
                    }
                    Divider().padding(.vertical, 2)
                    HStack(spacing: 20) {
                        settingLabel("Double-tap key", detail: "Tap twice to show or hide.")
                        Spacer(minLength: 12)
                        VStack(alignment: .trailing, spacing: 6) {
                            ShortcutRecorderView(shortcut: $state.doubleTapKey, singleKeyOnly: true) { state.save() }
                                .frame(width: 200, height: 32)
                            if !state.doubleTapKey.isEmpty {
                                Button("Clear") { state.doubleTapKey = ""; state.save() }
                                    .buttonStyle(.borderless)
                                    .foregroundStyle(.secondary)
                                    .font(.caption)
                            }
                        }
                    }
                    Text("Supports modifier keys. Letters still type in the active app.")
                        .font(.system(size: 11)).foregroundStyle(.secondary)
                    if !accessibilityGranted {
                        HStack(spacing: 10) {
                            Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                            Text("Allow Accessibility access to use double-tap.")
                                .font(.callout)
                            Spacer(minLength: 0)
                            Button("Open System Settings") {
                                NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
                            }
                        }
                        .padding(12)
                        .background(Color.orange.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
                    }
                }

                } else {
                settingsSection("System Settings Page") {
                    HStack {
                        settingLabel("Override conflicts", detail: "Prioritize the launcher over system shortcuts.")
                        Spacer()
                        Toggle("Override conflicts", isOn: $state.suppressSystemShortcut)
                            .labelsHidden()
                            .toggleStyle(.switch)
                            .onChange(of: state.suppressSystemShortcut) { _ in state.save() }
                            .disabled(!state.spotlightShortcut.isEmpty)
                    }
                    Divider().padding(.vertical, 2)
                    HStack(spacing: 20) {
                        settingLabel("Spotlight shortcut", detail: "Use a separate shortcut for Spotlight.")
                        Spacer(minLength: 12)
                        VStack(alignment: .trailing, spacing: 6) {
                            ShortcutRecorderView(shortcut: $state.spotlightShortcut) { state.save() }
                                .frame(width: 200, height: 32)
                            if !state.spotlightShortcut.isEmpty {
                                Button("Clear") {
                                    SpotlightShortcutManager.restoreDefault()
                                    state.spotlightShortcut = ""
                                    state.save()
                                }
                                .buttonStyle(.borderless)
                                    .foregroundStyle(.secondary)
                                .font(.caption)
                                .help("Clear the custom shortcut and restore Spotlight’s default")
                            }
                        }
                    }
                    if !state.spotlightShortcut.isEmpty {
                        Text("Override is unavailable while Spotlight has a custom shortcut.")
                            .font(.caption).foregroundStyle(.secondary)
                    }
                }
                }
                Text("Changes save automatically.")
                    .font(.caption).foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .padding(28)
            .frame(maxWidth: 740)
            .frame(maxWidth: .infinity)
        }
        .background(colorScheme == .dark ? Color(white: 0.115) : Color(white: 0.96))
        .onChange(of: appearanceMode) { $0.apply() }
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in
            accessibilityGranted = AccessibilityHelper.isTrusted
        }
    }

    private var groupBackground: Color {
        colorScheme == .dark ? Color(white: 0.155) : Color(nsColor: .controlBackgroundColor)
    }

    private func settingLabel(_ title: String, detail: String) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            Text(LocalizedStringKey(title)).font(.system(size: 13, weight: .medium))
            Text(LocalizedStringKey(detail)).font(.caption).foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private func settingsSection<Content: View>(_ title: String,
                                               @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(LocalizedStringKey(title)).font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary).padding(.leading, 6)
            VStack(alignment: .leading, spacing: 18) {
                content()
            }
            .padding(20)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(groupBackground, in: RoundedRectangle(cornerRadius: 14))
        }
    }
}

// MARK: - Detail View

struct LauncherDetailView: View {
    @Environment(\.locale) private var locale
    @Binding var launcher: EditableLauncher
    @State private var showIconPicker = false
    @State private var showAppPicker = false
    @State private var appHovered = false
    @FocusState private var nameFocused: Bool
    @Environment(\.colorScheme) private var colorScheme

    private var groupBackground: Color {
        colorScheme == .dark ? Color(white: 0.155) : Color(nsColor: .controlBackgroundColor)
    }

    var body: some View {
        let _ = locale
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                HStack(spacing: 18) {
                    Button { showIconPicker.toggle() } label: {
                        Image(nsImage: launcher.resolvedIcon())
                            .resizable().scaledToFit().frame(width: 60, height: 60)
                            .padding(8)
                            .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 16))
                            .overlay(alignment: .bottomTrailing) {
                                Image(systemName: "pencil.circle.fill").font(.system(size: 20))
                                    .foregroundStyle(Color.accentColor)
                                    .background(.background, in: Circle()).offset(x: 3, y: 3)
                            }
                    }
                    .buttonStyle(.plain)
                    .interactionPointer()
                    .help("Change icon")
                    .accessibilityLabel("Change icon")
                    .popover(isPresented: $showIconPicker) {
                        IconPickerView(launcher: $launcher, isPresented: $showIconPicker)
                    }
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Name").font(.system(size: 12, weight: .medium)).foregroundStyle(.secondary)
                        TextField("Name", text: $launcher.name)
                            .textFieldStyle(.plain).font(.system(size: 19, weight: .semibold))
                            .focused($nameFocused)
                            .padding(.horizontal, 12)
                            .padding(.vertical, 9)
                            .background(colorScheme == .dark ? Color(white: 0.22) : Color(white: 0.96),
                                        in: RoundedRectangle(cornerRadius: 8))
                            .overlay(RoundedRectangle(cornerRadius: 8)
                                .strokeBorder(nameFocused ? Color.accentColor : Color.primary.opacity(0.2),
                                              lineWidth: nameFocused ? 2 : 1))
                        Text("Customize the name and icon shown on your keyboard.")
                            .font(.caption).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                    }
                }
                .padding(20).frame(maxWidth: .infinity, alignment: .leading)
                .background(groupBackground, in: RoundedRectangle(cornerRadius: 18))

                VStack(alignment: .leading, spacing: 10) {
                    Text("Action").font(.system(size: 12, weight: .semibold))
                        .foregroundStyle(.secondary).padding(.leading, 6)
                    VStack(alignment: .leading, spacing: 18) {
                        Picker("Action", selection: $launcher.actionType) {
                            ForEach(ActionType.allCases, id: \.self) { Text(LocalizedStringKey($0.rawValue)).tag($0) }
                        }.pickerStyle(.segmented).labelsHidden()
                        .controlSize(.small)
                        Divider()
                        switch launcher.actionType {
                        case .openApp:
                            Button { showAppPicker.toggle() } label: {
                                HStack(spacing: 12) {
                                    if let app = AppScanner.shared.apps.first(where: { $0.name == launcher.appName }) {
                                        Image(nsImage: app.icon).resizable().scaledToFit().frame(width: 36, height: 36)
                                    } else {
                                        Image(systemName: "app.dashed").font(.system(size: 27))
                                            .foregroundStyle(.secondary).frame(width: 36, height: 36)
                                    }
                                    VStack(alignment: .leading, spacing: 4) {
                                        Text("Application").font(.caption).foregroundStyle(.secondary)
                                        Text(launcher.appName.isEmpty ? L("Choose an app…") : launcher.appName)
                                            .font(.system(size: 13, weight: .medium))
                                    }
                                    Spacer()
                                    Image(systemName: "chevron.up.chevron.down").font(.caption).foregroundStyle(.secondary)
                                }
                                .padding(10)
                                .background(Color.primary.opacity(appHovered ? 0.07 : 0.025), in: RoundedRectangle(cornerRadius: 10))
                                .contentShape(Rectangle())
                            }
                            .buttonStyle(.plain)
                            .onHover { appHovered = $0 }
                            .interactionPointer()
                            .animation(.easeOut(duration: 0.12), value: appHovered)
                            .popover(isPresented: $showAppPicker) {
                                AppSelectionGrid(selectedPath: AppScanner.shared.apps.first { $0.name == launcher.appName }?.path) { app in
                                    launcher.appName = app.name
                                    showAppPicker = false
                                }.frame(width: 380, height: 400)
                            }
                            field("Arguments", hint: "Optional", placeholder: "e.g. ~/projects/my-app", text: $launcher.appArgs)
                        case .openURL:
                            field("URL", placeholder: "https://example.com", text: $launcher.url)
                        case .keyboardShortcut:
                            HStack {
                                ShortcutRecorderView(shortcut: $launcher.keyboardShortcut, onSave: {})
                                    .frame(height: 32)
                                Button("Clear") { launcher.keyboardShortcut = "" }
                                    .disabled(launcher.keyboardShortcut.isEmpty)
                            }
                            Text("Returns to your previous app, then presses the shortcut. Supports global shortcuts.")
                                .font(.caption).foregroundStyle(.secondary)
                        case .shellCommand:
                            field("Shell Command", placeholder: "/path/to/script.sh", text: $launcher.shellCommand)
                        }
                    }
                    .padding(20).frame(maxWidth: .infinity, alignment: .leading)
                    .background(groupBackground, in: RoundedRectangle(cornerRadius: 14))
                }
                Label(iconModeDescription, systemImage: "info.circle")
                    .font(.caption).foregroundStyle(.secondary).padding(.horizontal, 6)
            }.padding(24)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .onChange(of: launcher.appName) { newName in
            if (launcher.name == "New Item" || launcher.name == L("New Item") || launcher.name.isEmpty) && !newName.isEmpty {
                launcher.name = newName
            }
        }
    }

    private func field(_ title: String, hint: String = "", placeholder: String, text: Binding<String>) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(LocalizedStringKey(title)).font(.system(size: 12, weight: .medium))
                if !hint.isEmpty { Text(LocalizedStringKey(hint)).font(.caption).foregroundStyle(.tertiary) }
            }.foregroundStyle(.secondary)
            TextField(LocalizedStringKey(placeholder), text: text).textFieldStyle(.roundedBorder)
        }
    }

    var iconModeDescription: String {
        switch launcher.iconMode {
        case .auto: return L("Icon auto-detected from command")
        case .sfSymbol: return L("Using SF Symbol: %@", launcher.iconValue)
        case .appIcon: return L("Using app icon")
        case .custom: return L("Using custom image")
        }
    }
}
