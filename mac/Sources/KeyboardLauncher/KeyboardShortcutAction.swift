import Cocoa

enum KeyboardShortcutAction {
    static let eventMarker: Int64 = 0x4B424C53

    // Build events separately so validation and tests never send real keystrokes.
    static func events(for shortcut: String) -> [CGEvent]? {
        let parts = shortcut.lowercased().split(separator: "+", omittingEmptySubsequences: false).map(String.init)
        guard let key = parts.last, let code = LaunchpickConfig.keyCodeMap[key] else { return nil }
        let modifiers: [String: CGEventFlags] = ["cmd": .maskCommand, "command": .maskCommand,
            "ctrl": .maskControl, "control": .maskControl, "alt": .maskAlternate,
            "opt": .maskAlternate, "option": .maskAlternate, "shift": .maskShift]
        var flags: CGEventFlags = []
        for part in parts.dropLast() {
            guard let modifier = modifiers[part] else { return nil }
            flags.formUnion(modifier)
        }
        let source = CGEventSource(stateID: .privateState)
        guard let down = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(code), keyDown: true),
              let up = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(code), keyDown: false) else { return nil }
        for event in [down, up] {
            event.flags = flags
            event.setIntegerValueField(.eventSourceUserData, value: eventMarker)
        }
        return [down, up]
    }

    static func send(_ shortcut: String, to target: NSRunningApplication?) {
        guard AccessibilityHelper.isTrusted else {
            AccessibilityHelper.showAccessibilityAlert()
            return
        }
        guard let events = events(for: shortcut) else {
            showError("The shortcut is invalid. Please record it again.")
            return
        }
        guard let target = externalApp(target) else {
            // Global shortcuts do not require a particular foreground app.
            NSApp.deactivate()
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.1) {
                for event in events { event.post(tap: .cghidEventTap) }
            }
            return
        }
        target.activate(options: [])
        var attempts = 0
        Timer.scheduledTimer(withTimeInterval: 0.05, repeats: true) { timer in
            attempts += 1
            guard !target.isTerminated else { timer.invalidate(); return }
            if NSWorkspace.shared.frontmostApplication?.processIdentifier == target.processIdentifier {
                timer.invalidate()
                // Use the system stream so global shortcuts (for example screenshot
                // tools) see the chord as well as the restored foreground app.
                for event in events { event.post(tap: .cghidEventTap) }
            } else if attempts >= 20 {
                timer.invalidate()
                showError("The target app could not be activated. Please try again.")
            }
        }
    }

    static func externalApp(_ app: NSRunningApplication?) -> NSRunningApplication? {
        guard let app, !app.isTerminated, app != .current else { return nil }
        return app
    }

    private static func showError(_ message: String) {
        let alert = NSAlert()
        alert.messageText = L("Could Not Send Shortcut")
        alert.informativeText = L(message)
        alert.addButton(withTitle: L("OK"))
        alert.runModal()
    }
}
