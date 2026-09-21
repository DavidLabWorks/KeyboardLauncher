import AppKit

struct SymbolPickerCheck {
    static func main() {
        assert(SFSymbols.categories.count >= 10)
        assert(SFSymbols.all.count > 400)
        assert(Set(SFSymbols.all).count == SFSymbols.all.count)
        assert(SFSymbols.all.allSatisfy { NSImage(systemSymbolName: $0, accessibilityDescription: nil) != nil })
        assert(SFSymbols.symbols(matching: "", category: "Media") == SFSymbols.categories.first { $0.name == "Media" }!.symbols)
        assert(SFSymbols.symbols(matching: "  KEYBOARD  ", category: "Media").contains("keyboard"))
        assert(SFSymbols.exactMatch("  BOLT.FILL\n") == "bolt.fill")
        assert(SFSymbols.exactMatch("not.a.real.symbol.123") == nil)
        assert(SFSymbols.exactMatch("  ") == nil)
        // Exact names work even when absent from the curated catalog.
        let name = "square.and.arrow.up.circle.fill"
        assert(!SFSymbols.all.contains(name))
        assert(SFSymbols.exactMatch(name) == name)
        var draft = EditableLauncher()
        draft.iconMode = .sfSymbol
        draft.iconValue = name
        assert(EditableLauncher.from(draft.toConfig()).iconValue == name)
        print("Symbol categories, search, exact-name selection and persistence passed (\(SFSymbols.all.count) available symbols)")
    }
}
