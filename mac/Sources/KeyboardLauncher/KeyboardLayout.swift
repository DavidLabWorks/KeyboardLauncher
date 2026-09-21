/// Physical ANSI key positions, shared by the keycaps and event handling.
enum KeyboardLayout {
    static let rows = [Array("1234567890-="), Array("QWERTYUIOP"), Array("ASDFGHJKL"), Array("ZXCVBNM")]
    static let keys = rows.flatMap { $0 }
    static let keyCodes: [UInt16] = [18,19,20,21,23,22,26,28,25,29,27,24,
                                   12,13,14,15,17,16,32,34,31,35,
                                   0,1,2,3,5,4,38,40,37,
                                   6,7,8,9,11,45,46]

    static func index(for keyCode: UInt16, page: Int, count: Int) -> Int? {
        guard page >= 0, let position = keyCodes.firstIndex(of: keyCode) else { return nil }
        let index = page * keys.count + position
        return index < count ? index : nil
    }
}
