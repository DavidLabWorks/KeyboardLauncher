import SwiftUI

enum AppLanguage {
    static let defaultCode = Locale.preferredLanguages.first?.hasPrefix("zh-Hans") == true ||
        Locale.preferredLanguages.first?.hasPrefix("zh-CN") == true ? "zh-Hans" : "en"
    static var code: String { UserDefaults.standard.string(forKey: "appLanguage") == "zh-Hans" ? "zh-Hans" :
        (UserDefaults.standard.string(forKey: "appLanguage") == nil ? defaultCode : "en") }
    static let changed = Notification.Name("AppLanguageChanged")

    static func text(_ key: String, language: String, bundle: Bundle = .main) -> String {
        guard let path = bundle.path(forResource: language, ofType: "lproj"),
              let localized = Bundle(path: path) else { return key }
        return localized.localizedString(forKey: key, value: key, table: nil)
    }
}

func L(_ key: String, _ arguments: CVarArg...) -> String {
    let text = AppLanguage.text(key, language: AppLanguage.code)
    return arguments.isEmpty ? text : String(format: text, locale: Locale(identifier: AppLanguage.code), arguments: arguments)
}

struct LocalizedView<Content: View>: View {
    @AppStorage("appLanguage") private var language = AppLanguage.defaultCode
    let content: Content
    var body: some View {
        content.environment(\.locale, Locale(identifier: language == "zh-Hans" ? "zh-Hans" : "en"))
    }
}
