import Cocoa

class LaunchpickPanel: NSPanel {
    var allowsHeaderDragging = false
    private var dragOffset: NSPoint?

    init() {
        super.init(
            contentRect: NSRect(x: 0, y: 0, width: 720, height: 400),
            styleMask: [.borderless],
            backing: .buffered,
            defer: false
        )

        level = .floating
        isOpaque = false
        backgroundColor = .clear
        hasShadow = true
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient]
        isMovableByWindowBackground = false
        hidesOnDeactivate = false
    }

    func dragHeader(with event: NSEvent) -> Bool {
        switch event.type {
        case .leftMouseDown:
            let header = NSRect(x: 0, y: frame.height - 76,
                                width: max(0, frame.width - 84), height: 76)
            guard allowsHeaderDragging, header.contains(event.locationInWindow) else { return false }
            dragOffset = event.locationInWindow
            NSCursor.closedHand.push()
            return true
        case .leftMouseDragged:
            guard let offset = dragOffset else { return false }
            let pointer = convertPoint(toScreen: event.locationInWindow)
            setFrameOrigin(NSPoint(x: pointer.x - offset.x, y: pointer.y - offset.y))
            return true
        case .leftMouseUp:
            guard dragOffset != nil else { return false }
            cancelHeaderDrag()
            return true
        default:
            return false
        }
    }


    func cancelHeaderDrag() {
        guard dragOffset != nil else { return }
        dragOffset = nil
        NSCursor.pop()
    }

    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { true }
}
