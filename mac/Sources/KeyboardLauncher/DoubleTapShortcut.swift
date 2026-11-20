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

struct TapGestures {
    enum Decision { case none, double, wait, triple }
    private var twice: DoubleTapShortcut
    private var three: DoubleTapShortcut
    private var secondRelease: TimeInterval?
    private var thirdPress: TimeInterval?
    private var pendingDouble = false
    private(set) var generation = 0

    init(double: UInt16? = nil, triple: UInt16? = nil) {
        twice = DoubleTapShortcut(keyCode: double)
        three = DoubleTapShortcut(keyCode: triple)
    }

    mutating func reset() {
        twice.reset()
        three.reset()
        secondRelease = nil
        thirdPress = nil
        pendingDouble = false
        generation += 1
    }

    mutating func expire(_ token: Int) -> Bool {
        guard generation == token, pendingDouble, thirdPress == nil else { return false }
        reset()
        return true
    }

    mutating func handle(keyCode: UInt16, isDown: Bool, time: TimeInterval,
                         isRepeat: Bool = false, hasOtherModifiers: Bool = false) -> Decision {
        var expired = false
        if let secondRelease {
            if keyCode == three.keyCode, !isRepeat, !hasOtherModifiers {
                if let thirdPress, !isDown, time - thirdPress <= 0.3 {
                    reset()
                    return .triple
                }
                if thirdPress == nil, isDown, time - secondRelease <= 0.4 {
                    thirdPress = time
                    return .none
                }
            }
            expired = pendingDouble && thirdPress == nil && time - secondRelease > 0.4
            reset()
        }
        let double = twice.handle(keyCode: keyCode, isDown: isDown, time: time,
                                  isRepeat: isRepeat, hasOtherModifiers: hasOtherModifiers)
        if three.handle(keyCode: keyCode, isDown: isDown, time: time,
                        isRepeat: isRepeat, hasOtherModifiers: hasOtherModifiers) {
            secondRelease = time
            pendingDouble = double
            generation += 1
            return double ? .wait : .none
        }
        return double || expired ? .double : .none
    }
}
