import Foundation

/// Two short, isolated press/release pairs. A chord or held key cancels the sequence.
struct DoubleTapShortcut {
    var keyCode: UInt16?
    private var pressedAt: TimeInterval?
    private var releasedAt: TimeInterval?

    init(keyCode: UInt16? = nil) { self.keyCode = keyCode }

    mutating func reset() {
        pressedAt = nil
        releasedAt = nil
    }

    mutating func handle(keyCode: UInt16, isDown: Bool, time: TimeInterval,
                         isRepeat: Bool = false, hasOtherModifiers: Bool = false) -> Bool {
        guard keyCode == self.keyCode, !isRepeat, !hasOtherModifiers else {
            reset()
            return false
        }
        if isDown {
            guard pressedAt == nil else { reset(); return false }
            pressedAt = time
            return false
        }
        guard let pressedAt, time - pressedAt <= 0.3 else { reset(); return false }
        self.pressedAt = nil
        if let releasedAt, time - releasedAt <= 0.4 {
            reset()
            return true
        }
        releasedAt = time
        return false
    }
}
