import Cocoa

struct ShortcutRecordingCheck {
    static func main() {
        _ = NSApplication.shared
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 300, height: 100),
                              styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        let recorder = ShortcutRecorderNSView()
        window.contentView = recorder
        assert(KeyboardShortcutAction.externalApp(nil) == nil)
        assert(KeyboardShortcutAction.externalApp(.current) == nil,
               "The launcher itself must never be restored as the target app")
        assert(KeyboardShortcutAction.events(for: "cmd+shift+x") != nil,
               "A global shortcut remains valid without a target app")
        let delegate = AppDelegate()
        let ordinary = CGEvent(keyboardEventSource: nil, virtualKey: 59, keyDown: true)!
        assert(delegate.handleRecordingTap(type: .flagsChanged, event: ordinary)?.takeUnretainedValue() === ordinary,
               "The early HID listener must pass normal input through without activation")
        recorder.shortcut = "cmd+a"
        _ = recorder.accessibilityPerformPress()
        assert(window.firstResponder === recorder && recorder.isRecording)
        let events = KeyboardShortcutAction.events(for: "cmd+shift+x")!
        assert(delegate.interceptShortcutRecording(type: .keyDown, event: events[0], recorder: recorder))
        assert(recorder.shortcut == "cmd+shift+x" && !recorder.isRecording)
        assert(delegate.interceptShortcutRecording(type: .keyDown, event: events[0], recorder: recorder), "Repeat must stay suppressed until release")
        assert(delegate.interceptShortcutRecording(type: .keyUp, event: events[1], recorder: recorder))
        assert(!delegate.interceptShortcutRecording(type: .keyDown, event: events[0], recorder: recorder), "Normal shortcuts must resume after recording")
        _ = recorder.accessibilityPerformPress()
        let escape = KeyboardShortcutAction.events(for: "escape")!
        assert(delegate.interceptShortcutRecording(type: .keyDown, event: escape[0], recorder: recorder))
        assert(recorder.shortcut == "cmd+shift+x" && !recorder.isRecording)
        assert(delegate.interceptShortcutRecording(type: .keyUp, event: escape[1], recorder: recorder))
        window.close()
        print("Shortcut capture, suppression, release, and cancellation checks passed")
    }
}
