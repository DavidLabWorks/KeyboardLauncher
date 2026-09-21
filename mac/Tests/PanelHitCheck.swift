import AppKit
import SwiftUI

struct PanelHitCheck {
    static func main() {
        _ = NSApplication.shared
        let originalAppearance = NSApp.appearance
        for mode in AppAppearance.allCases {
            assert(AppAppearance(rawValue: mode.rawValue) == mode)
            mode.apply()
            switch mode {
            case .system: assert(NSApp.appearance == nil, "System must remove the app override")
            case .light: assert(NSApp.appearance?.name == .aqua)
            case .dark: assert(NSApp.appearance?.name == .darkAqua)
            }
        }
        NSApp.appearance = originalAppearance
        let panel = LaunchpickPanel()
        panel.setContentSize(NSSize(width: 880, height: 460))
        let host = NSHostingView(rootView: ContentView(state: LaunchpickState()))
        panel.contentView = host
        host.layoutSubtreeIfNeeded()

        // Header, gap above keyboard, side gutter, and footer gap.
        for point in [NSPoint(x: 440, y: 420), NSPoint(x: 440, y: 360),
                      NSPoint(x: 80, y: 220), NSPoint(x: 440, y: 50)] {
            let hit = host.hitTest(point)
            assert(hit != nil && !(hit is NSVisualEffectView),
                   "Blank clicks must be handled by the panel content, not the backdrop")
        }
        let dragPanel = LaunchpickPanel()
        dragPanel.setFrame(NSRect(x: 100, y: 100, width: 1040, height: 560), display: false)
        dragPanel.allowsHeaderDragging = true
        func mouse(_ type: NSEvent.EventType, _ x: CGFloat, _ y: CGFloat) -> Bool {
            dragPanel.dragHeader(with: NSEvent.mouseEvent(with: type,
                location: NSPoint(x: x, y: y), modifierFlags: [], timestamp: 0,
                windowNumber: dragPanel.windowNumber, context: nil,
                eventNumber: 0, clickCount: 1, pressure: 1)!)
        }
        assert(mouse(.leftMouseDown, 300, 530))
        assert(mouse(.leftMouseDragged, 380, 550))
        assert(dragPanel.frame.origin == NSPoint(x: 180, y: 120), "Dragging must actually move the window")
        assert(mouse(.leftMouseDragged, 320, 540))
        assert(dragPanel.frame.origin == NSPoint(x: 200, y: 130), "Dragging must continue without jumping")
        assert(mouse(.leftMouseUp, 300, 530))
        assert(!mouse(.leftMouseDragged, 400, 550), "Releasing must end the drag")
        assert(!mouse(.leftMouseDown, 990, 522), "Settings must stay clickable")
        assert(!mouse(.leftMouseDown, 300, 300), "Keys must stay clickable")
        panel.orderFront(nil)
        let originalFrame = panel.frame
        let editor = BindingEditor(slot: 9, onCancel: {}, onSave: { _ in true }).present(over: panel)
        assert(panel.frame == originalFrame, "Opening the editor must not resize the launcher")
        assert(editor.frame.height > panel.frame.height, "Only the editor should be taller")
        assert(panel.isVisible, "Opening an editor must keep the launcher visible")
        assert(editor.parent == nil, "Binding editor must be an independent window")
        assert(editor.styleMask.contains(.titled) && editor.styleMask.contains(.closable))
        assert(editor.level == .normal, "Binding editor must behave like Settings")
        editor.close()
        assert(panel.frame == originalFrame, "Closing the editor must not resize the launcher")
        // A delayed focus notification must not dismiss an inside click when
        // NSApp.currentEvent is nil or still refers to the shortcut event.
        let delegate = AppDelegate()
        delegate.setupPanel()
        let surface = delegate.panel.contentView!
        assert((surface.layer?.backgroundColor?.alpha ?? 0) > 0,
               "Glass must have a nontransparent backing surface to prevent WindowServer click-through")
        assert(surface.layer?.cornerRadius == 26 && surface.layer?.masksToBounds == true,
               "The mouse-catching surface must preserve rounded corners")
        delegate.togglePanel()
        let screenFrame = delegate.panel.screen!.visibleFrame
        assert(abs(delegate.panel.frame.midX - screenFrame.midX) < 1)
        assert(abs(delegate.panel.frame.midY - screenFrame.midY) < 1, "Launcher must open at the screen center")
        assert(delegate.panel.allowsHeaderDragging)
        let mouse = NSEvent.mouseLocation
        delegate.panel.setFrameOrigin(NSPoint(x: mouse.x - 100, y: mouse.y - 100))
        RunLoop.main.run(until: Date().addingTimeInterval(0.35))
        NotificationCenter.default.post(name: NSWindow.didResignKeyNotification, object: delegate.panel)
        assert(delegate.panel.isVisible, "Inside-panel focus changes must not dismiss the launcher")
        delegate.togglePanel()
        assert(!delegate.panel.isVisible, "Explicit shortcut dismissal must still work")
        for _ in 0..<2 {
            delegate.togglePanel()
            assert(delegate.panel.isVisible)
            delegate.openSettings()
            RunLoop.main.run(until: Date().addingTimeInterval(0.1))
            assert(!delegate.panel.isVisible, "The floating launcher must not cover settings")
            assert(delegate.settingsWindow?.isVisible == true, "New and reused settings windows must be visible")
        }
        delegate.settingsWindow?.close()
        print("Panel hit, editor size, focus-dismissal, and settings presentation checks passed")
    }
}
