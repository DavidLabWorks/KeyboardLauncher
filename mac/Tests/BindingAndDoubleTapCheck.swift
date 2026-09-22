import Foundation
import CoreGraphics
import Cocoa

struct BindingAndDoubleTapCheck {
    static func main() throws {
        let frames = [0: CGRect(x: 0, y: 0, width: 70, height: 90),
                      1: CGRect(x: 80, y: 0, width: 70, height: 90)]
        assert(ContentView.dropSlot(at: CGPoint(x: 100, y: 35), frames: frames) == 1)
        assert(ContentView.dropSlot(at: CGPoint(x: 75, y: 35), frames: frames) == nil)
        assert(ContentView.dropSlot(at: CGPoint(x: -10, y: 35), frames: frames) == nil)
        let state = LaunchpickState()
        let first = LaunchpickItem(slot: 0, name: "First", exec: "first", icon: NSImage(size: NSSize(width: 16, height: 16)))
        let second = LaunchpickItem(slot: 1, name: "Second", exec: "", icon: NSImage(), keyboardShortcut: "cmd+shift+x")
        state.launchers = [first, second]
        var finishSave: ((Bool) -> Void)?
        state.onMove = { source, destination, completion in
            assert(source == 0 && destination == 1)
            assert(state.launchers.map(\.slot) == [1, 0], "UI must change before persistence starts")
            finishSave = completion
        }
        assert(state.moveBinding(from: 0, to: 1))
        assert(state.isMoving && state.launchers[0].id == first.id && state.launchers[0].icon === first.icon)
        assert(state.launchers[1].keyboardShortcut == "cmd+shift+x")
        assert(!state.moveBinding(from: 1, to: 2), "Do not overlap a pending write")
        finishSave?(false)
        assert(!state.isMoving && state.moveFailed && state.launchers.map(\.slot) == [0, 1])
        state.moveFailed = false
        assert(state.moveBinding(from: 0, to: 1))
        finishSave?(true)
        assert(!state.isMoving && !state.moveFailed && state.launchers.map(\.slot) == [1, 0])

        var shortcutAction = EditableLauncher()
        shortcutAction.actionType = .keyboardShortcut
        shortcutAction.keyboardShortcut = "cmd+shift+x"
        let savedAction = try JSONDecoder().decode(ConfigLauncher.self, from: JSONEncoder().encode(shortcutAction.toConfig()))
        let editable = EditableLauncher.from(savedAction)
        assert(editable.actionType == .keyboardShortcut && editable.keyboardShortcut == "cmd+shift+x")
        assert(savedAction.exec.isEmpty, "Shortcut actions must never become shell commands")
        let events = KeyboardShortcutAction.events(for: editable.keyboardShortcut)!
        assert(events.map(\.type) == [.keyDown, .keyUp])
        assert(events.allSatisfy { $0.getIntegerValueField(.keyboardEventKeycode) == 7 && $0.flags == [.maskCommand, .maskShift] })
        assert(events.allSatisfy { $0.getIntegerValueField(.eventSourceUserData) == KeyboardShortcutAction.eventMarker })
        for invalid in ["", "cmd", "cmd+bad", "bad+x", "cmd++x"] {
            assert(KeyboardShortcutAction.events(for: invalid) == nil)
        }
        let legacy = Data(#"{"shortcut":"cmd+shift+space","switcherShortcut":"alt+tab","sameAppSwitcherShortcut":"alt+cmd+p","groupByApp":true,"launchers":[{"name":"One","exec":"one"},{"name":"Two","exec":"two"}]}"#.utf8)
        var moving = try JSONDecoder().decode(LaunchpickConfig.self, from: legacy)
        assert(moving.moveBinding(from: 0, to: 12))
        assert(moving.launchers.map(\.keyIndex) == [12, 1])
        assert(moving.moveBinding(from: 12, to: 1))
        assert(moving.launchers.map(\.keyIndex) == [1, 12])
        let before = try JSONEncoder().encode(moving)
        for (source, target) in [(1, 1), (0, 2), (-1, 2), (1, -1), (1, 38)] {
            assert(!moving.moveBinding(from: source, to: target))
        }
        let after = try JSONEncoder().encode(moving)
        let beforeObject = try JSONSerialization.jsonObject(with: before) as! NSDictionary
        let afterObject = try JSONSerialization.jsonObject(with: after) as! NSDictionary
        assert(beforeObject == afterObject)
        let persisted = try JSONDecoder().decode(LaunchpickConfig.self, from: after)
        assert(persisted.launchers.map(\.name) == ["One", "Two"])
        assert(persisted.launchers.map(\.keyIndex) == [1, 12])

        var config = try JSONDecoder().decode(LaunchpickConfig.self, from: legacy)
        config.bind(ConfigLauncher(name: "Q", exec: "q", icon: nil), to: 12)
        config.bind(nil, to: 0)
        assert(config.launchers.map { $0.keyIndex! } == [1, 12])
        config.bind(ConfigLauncher(name: "New Q", exec: "new", icon: nil), to: 12)
        assert(config.launchers.count == 2 && config.launchers.last?.name == "New Q")
        config.doubleTapKey = "58:Left Option"
        let restored = try JSONDecoder().decode(LaunchpickConfig.self, from: JSONEncoder().encode(config))
        assert(restored.launchers.map(\.keyIndex) == [1, 12] && restored.doubleTapKey == "58:Left Option")

        let encoded = try JSONSerialization.jsonObject(with: JSONEncoder().encode(config)) as! [String: Any]
        assert(encoded["switcherShortcut"] == nil && encoded["sameAppSwitcherShortcut"] == nil && encoded["groupByApp"] == nil)
        let parsed = LaunchpickConfig.parseShortcutForSymbolicHotKey(config.shortcut)
        assert(parsed.keyCode == 49 && parsed.modifiers == 1179648)

        assert(LaunchpickConfig.parseShortcut("") == nil, "Cleared shortcuts must not become bare Space")
        assert(LaunchpickConfig.parseShortcut("  ") == nil)
        assert(LaunchpickConfig.parseShortcut("cmd+shift+space")?.keyCode == 49)
        config.shortcut = ""
        let cleared = try JSONDecoder().decode(LaunchpickConfig.self, from: JSONEncoder().encode(config))
        assert(cleared.shortcut.isEmpty && cleared.doubleTapKey == "58:Left Option")

        var tap = DoubleTapShortcut(keyCode: 58)
        func send(_ code: UInt16 = 58, _ down: Bool, _ time: Double, repeatKey: Bool = false, chord: Bool = false) -> Bool {
            tap.handle(keyCode: code, isDown: down, time: time, isRepeat: repeatKey, hasOtherModifiers: chord)
        }
        assert(!send(58, true, 0)); assert(!send(58, false, 0.1))
        assert(!send(58, true, 0.2)); assert(send(58, false, 0.3))
        // A triple tap only toggles once.
        assert(!send(58, true, 0.4)); assert(!send(58, false, 0.5))
        tap.reset()
        assert(!send(58, true, 1)); assert(!send(58, false, 1.5)) // held modifier
        assert(!send(58, true, 1.6)); assert(!send(58, false, 1.7))
        tap.reset()
        assert(!send(58, true, 2)); assert(!send(0, true, 2.1)) // interrupted by letter
        assert(!send(58, false, 2.2)); assert(!send(58, true, 2.3)); assert(!send(58, false, 2.4))
        tap.reset()
        assert(!send(58, true, 3)); assert(!send(58, false, 3.1))
        assert(!send(58, true, 3.2, chord: true)); assert(!send(58, false, 3.3))
        tap.reset()
        assert(!send(58, true, 4)); assert(!send(58, false, 4.1))
        assert(!send(58, true, 5)); assert(!send(58, false, 5.1)) // too slow
        tap = DoubleTapShortcut(keyCode: 0) // any ordinary key is supported
        assert(!send(0, true, 6)); assert(!send(0, false, 6.1))
        assert(!send(0, true, 6.2)); assert(send(0, false, 6.3))
        assert(!send(0, true, 7)); assert(!send(0, true, 7.1, repeatKey: true)); assert(!send(0, false, 7.2))
        // Replay the actual left-Control press/release timing captured during diagnosis.
        tap = DoubleTapShortcut(keyCode: 59)
        assert(!send(59, true, 0))
        assert(!send(59, false, 0.057009416))
        assert(!send(59, true, 0.123779708))
        assert(send(59, false, 0.274822916))
        print("Binding migration and double-tap checks passed")
    }
}
