// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "KeyboardLauncher",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "KeyboardLauncher", targets: ["KeyboardLauncher"])],
    targets: [
        .executableTarget(
            name: "KeyboardLauncher",
            path: "Sources/KeyboardLauncher",
            linkerSettings: [
                .linkedFramework("Carbon")
            ]
        )
    ]
)
