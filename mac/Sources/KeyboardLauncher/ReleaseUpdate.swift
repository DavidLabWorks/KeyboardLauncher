import Foundation

enum ReleaseUpdate {
    static let repository = URL(string: "https://github.com/DavidLabWorks/KeyboardLauncher")!
    static let releases = repository.appendingPathComponent("releases/latest")

    static func version(_ tag: String) throws -> [Int] {
        let value = tag.hasPrefix("v") ? String(tag.dropFirst()) : tag
        let parts = value.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 3,
              parts.allSatisfy({ !$0.isEmpty && $0.allSatisfy({ $0.isASCII && $0.isNumber }) }),
              parts.allSatisfy({ Int($0) != nil }) else { throw URLError(.cannotParseResponse) }
        return parts.map { Int($0)! }
    }

    static func isNewer(_ tag: String, than current: String) throws -> Bool {
        try version(current).lexicographicallyPrecedes(version(tag))
    }

    static func latestVersion() async throws -> String? {
        var request = URLRequest(url: URL(string: "https://api.github.com/repos/DavidLabWorks/KeyboardLauncher/releases/latest")!,
                                 cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: 15)
        request.setValue("KeyboardLauncher", forHTTPHeaderField: "User-Agent")
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        let (data, response) = try await URLSession.shared.data(for: request)
        guard let response = response as? HTTPURLResponse else { throw URLError(.badServerResponse) }
        if response.statusCode == 404 { return nil }
        guard response.statusCode == 200 else { throw URLError(.badServerResponse) }
        struct Release: Decodable { let tag_name: String }
        let tag = try JSONDecoder().decode(Release.self, from: data).tag_name
        _ = try version(tag)
        return tag
    }
}
