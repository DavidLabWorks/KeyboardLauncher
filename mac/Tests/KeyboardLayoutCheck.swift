struct KeyboardLayoutCheck {
    static func main() {
        assert(KeyboardLayout.rows.map(\.count) == [12, 10, 9, 7])
        assert(Set(KeyboardLayout.keys).count == 38)
        assert(Set(KeyboardLayout.keyCodes).count == 38)
        for (index, code) in KeyboardLayout.keyCodes.enumerated() {
            assert(KeyboardLayout.index(for: code, page: 0, count: 38) == index)
        }
        assert(KeyboardLayout.index(for: 12, page: 0, count: 13) == 12) // Q
        assert(KeyboardLayout.index(for: 12, page: 0, count: 12) == nil)
        assert(KeyboardLayout.index(for: 18, page: 1, count: 39) == 38)
        assert(KeyboardLayout.index(for: 19, page: 1, count: 39) == nil)
        assert(KeyboardLayout.index(for: 53, page: 0, count: 38) == nil)
        assert(KeyboardLayout.index(for: 18, page: 0, count: 0) == nil)
        print("Keyboard layout checks passed")
    }
}
