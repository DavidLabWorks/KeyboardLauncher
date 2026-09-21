import Cocoa

let app = NSApplication.shared
app.applicationIconImage = IconResolver.appIcon
app.setActivationPolicy(.accessory)
(AppAppearance(rawValue: UserDefaults.standard.string(forKey: "appearanceMode") ?? "") ?? .system).apply()

let delegate = AppDelegate()
app.delegate = delegate
app.run()
