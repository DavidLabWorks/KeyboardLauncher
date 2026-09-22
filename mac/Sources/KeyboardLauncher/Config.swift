import Carbon
import Cocoa

struct LaunchpickConfig: Codable {
    var shortcut: String
    var doubleTapKey: String?
    var suppressSystemShortcut: Bool?
    var spotlightShortcut: String?
    var columns: Int?
    var launchers: [ConfigLauncher]

    private static let accessLock = NSRecursiveLock()

    // Keep read/modify/write indivisible across background moves and settings edits.
    @discardableResult
    static func update(_ change: (inout LaunchpickConfig) -> Bool) -> Bool {
        accessLock.lock()
        defer { accessLock.unlock() }
        var config = load()
        guard change(&config) else { return false }
        return save(config)
    }

    static var configDir: String {
        "\(NSHomeDirectory())/.config/launchpick"
    }

    static var configPath: String {
        "\(configDir)/config.json"
    }

    static func load() -> LaunchpickConfig {
        accessLock.lock()
        defer { accessLock.unlock() }
        let path = configPath

        guard FileManager.default.fileExists(atPath: path) else {
            let config = createDefault()
            save(config)
            return config
        }

        do {
            let data = try Data(contentsOf: URL(fileURLWithPath: path))
            return try JSONDecoder().decode(LaunchpickConfig.self, from: data)
        } catch {
            NSLog("Launchpick: Failed to load config: \(error)")
            return createDefault()
        }
    }

    @discardableResult
    static func save(_ config: LaunchpickConfig) -> Bool {
        accessLock.lock()
        defer { accessLock.unlock() }
        do {
            try FileManager.default.createDirectory(atPath: configDir, withIntermediateDirectories: true)
            let encoder = JSONEncoder()
            encoder.outputFormatting = [.prettyPrinted, .withoutEscapingSlashes]
            try encoder.encode(config).write(to: URL(fileURLWithPath: configPath), options: .atomic)
            return true
        } catch {
            NSLog("KeyboardLauncher: Failed to save config: \(error)")
            return false
        }
    }

    // Freeze legacy positional bindings before editing so other keys never shift.
    mutating func bind(_ launcher: ConfigLauncher?, to slot: Int) {
        for index in launchers.indices where launchers[index].keyIndex == nil {
            launchers[index].keyIndex = index
        }
        launchers.removeAll { $0.keyIndex == slot }
        if var launcher {
            launcher.keyIndex = slot
            launchers.append(launcher)
        }
    }

    // Freeze legacy positions before moving; occupied destinations swap atomically.
    @discardableResult
    mutating func moveBinding(from source: Int, to destination: Int) -> Bool {
        let count = max(1, ((launchers.enumerated().map { $0.element.keyIndex ?? $0.offset }.max() ?? 0)
            / KeyboardLayout.keys.count) + 1) * KeyboardLayout.keys.count
        guard source >= 0, destination >= 0, destination < count, source != destination,
              let origin = launchers.indices.first(where: { (launchers[$0].keyIndex ?? $0) == source }) else { return false }
        for index in launchers.indices where launchers[index].keyIndex == nil {
            launchers[index].keyIndex = index
        }
        if let target = launchers.firstIndex(where: { $0.keyIndex == destination }) {
            launchers[target].keyIndex = source
        }
        launchers[origin].keyIndex = destination
        return true
    }

    static func createDefault() -> LaunchpickConfig {
        var launchers: [ConfigLauncher] = []

        let apps: [(String, String)] = [
            ("Finder", "/System/Library/CoreServices/Finder.app"),
            ("Safari", "/Applications/Safari.app"),
            ("Terminal", "/System/Applications/Utilities/Terminal.app"),
            ("Notes", "/System/Applications/Notes.app"),
            ("Calculator", "/System/Applications/Calculator.app"),
            ("System Settings", "/System/Applications/System Settings.app"),
            ("Visual Studio Code", "/Applications/Visual Studio Code.app"),
            ("Firefox", "/Applications/Firefox.app"),
            ("Google Chrome", "/Applications/Google Chrome.app"),
            ("Slack", "/Applications/Slack.app"),
            ("Spotify", "/Applications/Spotify.app"),
            ("iTerm", "/Applications/iTerm.app"),
        ]

        for (name, path) in apps {
            if FileManager.default.fileExists(atPath: path) {
                launchers.append(ConfigLauncher(name: name, exec: "open -a '\(name)'", icon: nil))
            }
            if launchers.count >= 8 { break }
        }

        if launchers.isEmpty {
            launchers = [
                ConfigLauncher(name: "Finder", exec: "open -a Finder", icon: nil),
                ConfigLauncher(name: "Terminal", exec: "open -a Terminal", icon: nil),
            ]
        }

        return LaunchpickConfig(shortcut: "cmd+shift+space", suppressSystemShortcut: false, spotlightShortcut: nil, columns: 4, launchers: launchers)
    }

    static func parseShortcut(_ shortcut: String) -> (keyCode: UInt32, modifiers: UInt32)? {
        guard !shortcut.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
        let parts = shortcut.lowercased().split(separator: "+").map(String.init)

        var modifiers: UInt32 = 0
        var keyCode: UInt32 = 49 // default: space

        for part in parts {
            switch part {
            case "cmd", "command": modifiers |= UInt32(cmdKey)
            case "shift": modifiers |= UInt32(shiftKey)
            case "opt", "option", "alt": modifiers |= UInt32(optionKey)
            case "ctrl", "control": modifiers |= UInt32(controlKey)
            default:
                if let code = keyCodeMap[part] {
                    keyCode = code
                }
            }
        }

        return (keyCode, modifiers)
    }

    static func parseShortcutForSymbolicHotKey(_ shortcut: String) -> (keyCode: Int, modifiers: Int) {
        let parts = shortcut.lowercased().split(separator: "+").map(String.init)

        var modifiers = 0
        var keyCode = 49 // default: space

        for part in parts {
            switch part {
            case "cmd", "command": modifiers |= 1048576   // NSEvent.ModifierFlags.command
            case "shift": modifiers |= 131072              // NSEvent.ModifierFlags.shift
            case "opt", "option", "alt": modifiers |= 524288 // NSEvent.ModifierFlags.option
            case "ctrl", "control": modifiers |= 262144    // NSEvent.ModifierFlags.control
            default:
                if let code = keyCodeMap[part] {
                    keyCode = Int(code)
                }
            }
        }

        return (keyCode, modifiers)
    }

    static let keyCodeMap: [String: UInt32] = [
        "a": 0, "b": 11, "c": 8, "d": 2, "e": 14, "f": 3,
        "g": 5, "h": 4, "i": 34, "j": 38, "k": 40, "l": 37,
        "m": 46, "n": 45, "o": 31, "p": 35, "q": 12, "r": 15,
        "s": 1, "t": 17, "u": 32, "v": 9, "w": 13, "x": 7,
        "y": 16, "z": 6,
        "0": 29, "1": 18, "2": 19, "3": 20, "4": 21, "5": 23,
        "6": 22, "7": 26, "8": 28, "9": 25,
        "delete": 51, "left": 123, "right": 124, "down": 125, "up": 126,
        "-": 27, "=": 24, "[": 33, "]": 30, ";": 41, "\'": 39, ",": 43, ".": 47, "/": 44, "\\": 42,
        "space": 49, "return": 36, "enter": 36, "tab": 48,
        "escape": 53, "`": 50, "~": 50,
        "f1": 122, "f2": 120, "f3": 99, "f4": 118, "f5": 96, "f6": 97,
        "f7": 98, "f8": 100, "f9": 101, "f10": 109, "f11": 103, "f12": 111,
        "f13": 105, "f14": 107, "f15": 113, "f16": 106, "f17": 64, "f18": 79, "f19": 80,
    ]
}

enum SpotlightShortcutManager {
    static func applyShortcut(_ shortcut: String) {
        let (keyCode, modifiers) = LaunchpickConfig.parseShortcutForSymbolicHotKey(shortcut)
        writeSymbolicHotKey(keyCode: keyCode, modifiers: modifiers)
    }

    static func restoreDefault() {
        // Cmd+Space: keyCode 49, modifiers 1048576 (command)
        writeSymbolicHotKey(keyCode: 49, modifiers: 1048576)
    }

    private static func writeSymbolicHotKey(keyCode: Int, modifiers: Int) {
        DispatchQueue.global(qos: .utility).async {
            let plistXml = "<dict><key>enabled</key><true/><key>value</key><dict><key>parameters</key><array><integer>65535</integer><integer>\(keyCode)</integer><integer>\(modifiers)</integer></array><key>type</key><string>standard</string></dict></dict>"

            let writeProcess = Process()
            writeProcess.executableURL = URL(fileURLWithPath: "/usr/bin/defaults")
            writeProcess.arguments = ["write", "com.apple.symbolichotkeys", "AppleSymbolicHotKeys", "-dict-add", "64", plistXml]
            try? writeProcess.run()
            writeProcess.waitUntilExit()

            let reloadProcess = Process()
            reloadProcess.executableURL = URL(fileURLWithPath: "/System/Library/PrivateFrameworks/SystemAdministration.framework/Resources/activateSettings")
            reloadProcess.arguments = ["-u"]
            try? reloadProcess.run()
            reloadProcess.waitUntilExit()
        }
    }
}

struct ConfigLauncher: Codable {
    var keyIndex: Int? = nil
    let name: String
    let exec: String
    let icon: String?
    var keyboardShortcut: String? = nil
}
