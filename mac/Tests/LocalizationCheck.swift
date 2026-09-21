import Foundation

struct LocalizationCheck {
    static func main() throws {
        let resources = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).appendingPathComponent("Resources")
        let bundle = Bundle(url: resources)!
        func strings(_ language: String) throws -> [String: String] {
            let data = try Data(contentsOf: resources.appendingPathComponent("\(language).lproj/Localizable.strings"))
            return try PropertyListSerialization.propertyList(from: data, format: nil) as! [String: String]
        }
        let english = try strings("en"), chinese = try strings("zh-Hans")
        assert(english.count > 100 && Set(english.keys) == Set(chinese.keys))
        let formats = try NSRegularExpression(pattern: "%[@a-z]+")
        for key in english.keys {
            func placeholders(_ value: String) -> [String] {
                formats.matches(in: value, range: NSRange(value.startIndex..., in: value)).map {
                    String(value[Range($0.range, in: value)!])
                }
            }
            assert(!chinese[key]!.isEmpty)
            assert(placeholders(english[key]!) == placeholders(chinese[key]!))
        }
        assert(AppLanguage.text("Settings", language: "en", bundle: bundle) == "Settings")
        assert(AppLanguage.text("Settings", language: "zh-Hans", bundle: bundle) == "设置")
        assert(AppLanguage.text("System Settings Page", language: "en", bundle: bundle) == "System")
        assert(AppLanguage.text("unknown-key", language: "zh-Hans", bundle: bundle) == "unknown-key")
        let format = AppLanguage.text("Bind Key %@", language: "zh-Hans", bundle: bundle)
        assert(String(format: format, "U").contains("U"))
        print("English/Chinese resources, lookup, fallback and format parity passed (\(english.count) strings)")
    }
}
