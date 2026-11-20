import AppKit
import SwiftUI

struct LocalApplication: Identifiable {
    let url: URL
    let name: String
    let added: Date?
    var id: URL { url }

    private static let icons = NSCache<NSURL, NSImage>()
    var icon: NSImage {
        if let cached = Self.icons.object(forKey: url as NSURL) { return cached }
        let image = NSWorkspace.shared.icon(forFile: url.path)
        Self.icons.setObject(image, forKey: url as NSURL)
        return image
    }

    static func installed() -> [Self] {
        let fm = FileManager.default
        let roots = fm.urls(for: .applicationDirectory, in: [.localDomainMask, .systemDomainMask, .userDomainMask])
            + [URL(fileURLWithPath: "/System/Library/CoreServices/Applications")]
        let keys: Set<URLResourceKey> = [.addedToDirectoryDateKey, .localizedNameKey, .isDirectoryKey]
        var found = [URL: Self]()
        for root in roots {
            guard let entries = fm.enumerator(at: root, includingPropertiesForKeys: Array(keys),
                                              options: [.skipsPackageDescendants, .skipsHiddenFiles]) else { continue }
            for case let url as URL in entries where url.pathExtension.lowercased() == "app" {
                let values = try? url.resourceValues(forKeys: keys)
                guard values?.isDirectory == true else { continue }
                let path = url.resolvingSymlinksInPath().standardizedFileURL
                let name = values?.localizedName ?? url.lastPathComponent
                found[path] = Self(url: path, name: (name as NSString).pathExtension.lowercased() == "app"
                                   ? (name as NSString).deletingPathExtension : name, added: values?.addedToDirectoryDate)
            }
        }
        return found.values.sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
    }
}

final class ApplicationSearch: ObservableObject {
    @Published private(set) var isOpen = false
    @Published private(set) var focusRequest = 0
    @Published var query = "" { didSet { rebuild() } }
    @Published private(set) var suggestions: [LocalApplication] = []
    @Published private(set) var results: [LocalApplication] = []
    @Published var selected: URL?
    @Published private(set) var loading = false
    private(set) var addedCount = 0
    var onOpen: ((LocalApplication) -> Void)?
    private var apps: [LocalApplication] = []
    private var refreshed: Date?
    private var history = UserDefaults.standard.stringArray(forKey: "applicationSearchHistory") ?? []

    func open() {
        if !isOpen { query = ""; isOpen = true }
        focusRequest += 1
        guard !loading, refreshed.map({ Date().timeIntervalSince($0) < 60 }) != true else { return }
        loading = true
        DispatchQueue.global(qos: .userInitiated).async {
            let apps = LocalApplication.installed()
            DispatchQueue.main.async {
                self.apps = apps
                self.loading = false
                self.refreshed = Date()
                self.rebuild()
            }
        }
    }

    func close() { isOpen = false }

    func record(_ app: LocalApplication) {
        history.removeAll { $0 == app.url.path }
        history.insert(app.url.path, at: 0)
        history = Array(history.prefix(30))
        UserDefaults.standard.set(history, forKey: "applicationSearchHistory")
        rebuild()
    }

    private func rebuild() {
        let text = query.trimmingCharacters(in: .whitespacesAndNewlines)
        if text.isEmpty {
            let added = apps.filter { $0.added != nil }.sorted { $0.added! > $1.added! }.prefix(2)
            addedCount = added.count
            let byPath = Dictionary(uniqueKeysWithValues: apps.map { ($0.url.path, $0) })
            let addedIDs = Set(added.map(\.id))
            let recent = history.compactMap { byPath[$0] }.filter { !addedIDs.contains($0.id) }.prefix(2)
            suggestions = Array(added) + recent
            let ids = Set(suggestions.map(\.id))
            results = apps.filter { !ids.contains($0.id) }
        } else {
            suggestions = []
            var prefix: [LocalApplication] = []
            var other: [LocalApplication] = []
            for app in apps {
                if app.name.range(of: text, options: [.anchored, .caseInsensitive, .diacriticInsensitive], locale: .current) != nil {
                    prefix.append(app)
                } else if app.name.localizedStandardContains(text) || app.url.lastPathComponent.localizedStandardContains(text) {
                    other.append(app)
                }
            }
            results = prefix + other
        }
        selected = (suggestions.first ?? results.first)?.id
    }

    func handle(_ code: UInt16) -> Bool {
        switch code {
        case 53: close()
        case 36, 76:
            if let app = (suggestions + results).first(where: { $0.id == selected }) { onOpen?(app) }
        case 125, 126:
            let all = suggestions + results
            guard !all.isEmpty else { return true }
            let i = all.firstIndex { $0.id == selected } ?? 0
            selected = all[min(all.count - 1, max(0, i + (code == 125 ? 1 : -1)))].id
        default: return false
        }
        return true
    }
}
