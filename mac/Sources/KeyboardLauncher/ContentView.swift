import SwiftUI

extension NSColor {
    static let launcherControlBackground = NSColor(name: "LauncherControlBackground") { appearance in
        appearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua
            ? NSColor(white: 0.44, alpha: 0.72)
            : .controlBackgroundColor
    }
}

class LaunchpickState: ObservableObject {
    @Published var launchers: [LaunchpickItem] = []
    @Published var keyboardPage = 0
    @Published var editingSlot: Int? {
        didSet {
            if oldValue != editingSlot { onEditingChanged?() }
        }
    }
    var onEditingChanged: (() -> Void)?

    var onLaunch: ((LaunchpickItem) -> Void)?
    var onDismiss: (() -> Void)?
    var onSettings: (() -> Void)?
    var onBind: ((ConfigLauncher?, Int) -> Bool)?

    var pageCount: Int { max(1, ((launchers.map(\.slot).max() ?? 0) / KeyboardLayout.keys.count) + 1) }
}

struct LaunchpickItem: Identifiable {
    let id = UUID()
    let slot: Int
    let name: String
    let exec: String
    let icon: NSImage
    var keyboardShortcut: String? = nil
}

struct ContentView: View {
    @Environment(\.locale) private var locale
    @ObservedObject var state: LaunchpickState
    @State private var unbindFailed = false
    @State private var settingsHovered = false
    @Environment(\.colorScheme) private var colorScheme

    var body: some View {
        let _ = locale
        VStack(spacing: 0) {
            HStack(spacing: 12) {
                HStack(spacing: 12) {
                    Image(nsImage: IconResolver.appIcon)
                        .resizable().scaledToFit().frame(width: 34, height: 34)
                    Text("Keyboard Launcher").font(.system(size: 15, weight: .semibold))
                    Spacer()
                }
                .frame(maxHeight: .infinity)
                .contentShape(Rectangle())
                .interactionPointer(draggable: true)
                Button { state.onSettings?() } label: {
                    Image(systemName: "gearshape")
                        .font(.system(size: 19, weight: .medium))
                        .foregroundStyle(settingsHovered ? Color.white : Color.primary)
                        .frame(width: 42, height: 38)
                        .background(settingsHovered ? Color.accentColor : Color(nsColor: .launcherControlBackground),
                                    in: RoundedRectangle(cornerRadius: 10))
                        .overlay(RoundedRectangle(cornerRadius: 10)
                            .strokeBorder(settingsHovered ? Color.white.opacity(0.25) : Color.primary.opacity(0.12), lineWidth: 1))
                        .contentShape(RoundedRectangle(cornerRadius: 10))
                }
                .buttonStyle(.plain)
                .onHover { settingsHovered = $0 }
                .interactionPointer()
                .help("Settings")
                .accessibilityLabel("Settings")
            }
            .padding(.horizontal, 30)
            .frame(height: 76)

            keyboardPanel

            HStack(spacing: 6) {
                Image(systemName: "keyboard")
                Text("Click an empty key to bind · Right-click to edit · Press or click to launch")
                Spacer()
                if state.pageCount > 1 {
                    Button { state.keyboardPage -= 1 } label: { Image(systemName: "chevron.left") }
                        .disabled(state.keyboardPage == 0)
                        .accessibilityLabel("Previous page")
                    Text("\(state.keyboardPage + 1) / \(state.pageCount)").monospacedDigit()
                    Button { state.keyboardPage += 1 } label: { Image(systemName: "chevron.right") }
                        .disabled(state.keyboardPage + 1 == state.pageCount)
                        .accessibilityLabel("Next page")
                }
                Text("Esc  Close").padding(.leading, 12)
            }
            .buttonStyle(.plain)
            .font(.system(size: 11))
            .foregroundStyle(.secondary)
            .padding(.horizontal, 30)
            .frame(height: 46)
        }
        // Consume blank clicks without interfering with the keycap buttons.
        .contentShape(Rectangle())
        .onTapGesture {}
        .background {
            if #available(macOS 26.0, *) {
                Color.clear
                    .glassEffect(.regular.tint(colorScheme == .dark ? Color.white.opacity(0.2) : .clear),
                                 in: RoundedRectangle(cornerRadius: 26, style: .continuous))
                    .allowsHitTesting(false)
            } else {
                VisualEffectBackground().allowsHitTesting(false)
            }
        }
        .clipShape(RoundedRectangle(cornerRadius: 26, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous)
            .strokeBorder(Color.primary.opacity(0.1), lineWidth: 0.5))
        .alert("Could not remove binding. Check that the configuration folder is writable.", isPresented: $unbindFailed) {
            Button("OK", role: .cancel) {}
        }
    }

    private var keyboardPanel: some View {
        GeometryReader { geometry in
            let width = min(70, (geometry.size.width - 110) / 12)
            VStack(spacing: 12) {
                Spacer(minLength: 0)
                ForEach(KeyboardLayout.rows.indices, id: \.self) { row in
                    HStack(spacing: 10) {
                        ForEach(KeyboardLayout.rows[row], id: \.self) { key in
                            let position = KeyboardLayout.keys.firstIndex(of: key)!
                            let index = state.keyboardPage * KeyboardLayout.keys.count + position
                            let item = state.launchers.first { $0.slot == index }
                            KeyCapView(key: String(key), item: item, width: width, action: {
                                if let item { state.onLaunch?(item) }
                                else { state.editingSlot = index }
                            }, onRemove: {
                                unbindFailed = state.onBind?(nil, index) != true
                            })
                            .contextMenu {
                                Button(LocalizedStringKey(item == nil ? "Bind Action…" : "Edit Binding…")) { state.editingSlot = index }
                            }
                        }
                    }
                    .offset(x: row == 2 ? 12 : (row == 3 ? 22 : 0))
                }
                Spacer(minLength: 0)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .padding(.horizontal, 30)
    }

}

private struct KeyCapView: View {
    @Environment(\.locale) private var locale
    let key: String
    let item: LaunchpickItem?
    let width: CGFloat
    let action: () -> Void
    let onRemove: () -> Void
    @State private var hovered = false
    @Environment(\.colorScheme) private var colorScheme

    var body: some View {
        let _ = locale
        ZStack(alignment: .topTrailing) {
            Button(action: action) {
                VStack(spacing: 5) {
                    ZStack(alignment: .bottomTrailing) {
                        RoundedRectangle(cornerRadius: 16, style: .continuous)
                            .fill(Color(nsColor: .launcherControlBackground))
                            .overlay(RoundedRectangle(cornerRadius: 16).fill(Color.white.opacity(hovered ? 0.08 : 0)))
                        if let item {
                            Image(nsImage: item.icon)
                                .resizable().scaledToFit()
                                .padding(item.icon.isTemplate ? width * 0.24 : 7)
                            Text(key).font(.system(size: 11, weight: .bold, design: .rounded))
                                .foregroundStyle(colorScheme == .dark ? Color.white : Color.primary)
                                .shadow(color: .black.opacity(colorScheme == .dark ? 0.35 : 0), radius: 1, y: 1)
                                .padding(5)
                        } else {
                            Text(key).font(.system(size: 18, weight: .semibold, design: .rounded))
                                .foregroundStyle(colorScheme == .dark ? Color.white.opacity(0.92) : Color.primary)
                                .frame(maxWidth: .infinity, maxHeight: .infinity)
                        }
                    }
                    .frame(width: width, height: width)
                    .overlay(RoundedRectangle(cornerRadius: 16)
                        .strokeBorder(hovered ? Color.accentColor.opacity(0.8) : Color.primary.opacity(0.12), lineWidth: 0.75))
                    .shadow(color: .black.opacity(0.08), radius: 2, y: 1)
                    Text(item?.name ?? "")
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(colorScheme == .dark ? Color.white.opacity(0.75) : Color.secondary)
                        .lineLimit(1)
                        .truncationMode(.tail)
                        .frame(width: width, height: 14)
                }
            }
            .buttonStyle(.plain)
            .accessibilityLabel(item.map { "\(key), \($0.name)" } ?? L("%@ · Click to bind", key))

            if item != nil && hovered {
                Button(action: onRemove) {
                    Image(systemName: "xmark")
                        .font(.system(size: 9, weight: .bold))
                        .foregroundStyle(colorScheme == .dark ? Color.white : Color.primary)
                        .frame(width: 18, height: 18)
                        .background(colorScheme == .dark ? Color(white: 0.22) : Color(nsColor: .controlBackgroundColor), in: Circle())
                        .overlay(Circle().strokeBorder(colorScheme == .dark ? Color.white.opacity(0.4) : Color.primary.opacity(0.12), lineWidth: 0.75))
                        .contentShape(Circle())
                }
                .buttonStyle(.plain)
                .help("Remove Binding")
                .accessibilityLabel(L("Remove binding for %@", key))
                .shadow(color: .black.opacity(0.14), radius: 2, y: 1)
                .offset(x: 6, y: -6)
            }
        }
        // Include the protruding button in the hover region without changing key spacing.
        .padding(.top, 6)
        .padding(.trailing, 6)
        .contentShape(Rectangle())
        .onHover { hovered = $0 }
        .interactionPointer()
        .padding(.top, -6)
        .padding(.trailing, -6)
        .animation(.easeOut(duration: 0.12), value: hovered)
        .zIndex(hovered ? 1 : 0)
    }
}

struct VisualEffectBackground: NSViewRepresentable {
    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = .hudWindow
        view.state = .active
        view.blendingMode = .behindWindow
        return view
    }

    func updateNSView(_ nsView: NSVisualEffectView, context: Context) {}
}


extension View {
    @ViewBuilder
    func interactionPointer(draggable: Bool = false) -> some View {
        if #available(macOS 15.0, *) {
            self.pointerStyle(draggable ? .grabIdle : .link)
        } else {
            self.onContinuousHover { phase in
                switch phase {
                case .active: (draggable ? NSCursor.openHand : NSCursor.pointingHand).set()
                case .ended: NSCursor.arrow.set()
                }
            }
        }
    }
}
