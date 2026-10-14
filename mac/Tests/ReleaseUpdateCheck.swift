import Foundation

struct ReleaseUpdateCheck {
    static func main() throws {
        for (tag, current, expected) in [
            ("v1.0.2", "1.0.1", true), ("v1.10.0", "1.9.0", true),
            ("2.0.0", "1.99.99", true), ("v1.0.1", "1.0.1", false), ("v1.0.0", "1.0.1", false)
        ] {
            let newer = try ReleaseUpdate.isNewer(tag, than: current)
            assert(newer == expected)
        }
        for tag in ["", "latest", "1.0", "1..0", "1.0.0.1", "1.0.2-beta", "1.0.-1", "１.0.2"] {
            do {
                _ = try ReleaseUpdate.isNewer(tag, than: "1.0.1")
                assertionFailure("Accepted malformed tag: \(tag)")
            } catch { }
        }
        print("Release version comparison and malformed tag checks passed")
    }
}
