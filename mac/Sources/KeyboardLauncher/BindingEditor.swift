import SwiftUI

struct BindingEditor: View {
    @Environment(\.locale) private var locale
    let slot: Int
    let onSave: (ConfigLauncher?) -> Bool
    let onCancel: () -> Void
    @State private var draft: EditableLauncher
    @State private var error = false
    @Environment(\.colorScheme) private var colorScheme
    private let hasBinding: Bool

    init(slot: Int, onCancel: @escaping () -> Void, onSave: @escaping (ConfigLauncher?) -> Bool) {
        self.slot = slot
        self.onCancel = onCancel
        self.onSave = onSave
        let existing = LaunchpickConfig.load().launchers.enumerated().first { ($0.element.keyIndex ?? $0.offset) == slot }?.element
        hasBinding = existing != nil
        _draft = State(initialValue: existing.map(EditableLauncher.from) ?? EditableLauncher(name: L("New Item")))
    }

    func present(over parent: NSWindow) -> NSWindow {
        let screenFrame = parent.screen?.visibleFrame ?? parent.frame.insetBy(dx: -20, dy: -80)
        let width = min(CGFloat(580), screenFrame.width - 40)
        let height = min(CGFloat(600), screenFrame.height - 40)
        let frame = NSRect(
            x: min(max(parent.frame.midX - width / 2, screenFrame.minX + 20), screenFrame.maxX - width - 20),
            y: min(max(parent.frame.midY - height / 2, screenFrame.minY + 20), screenFrame.maxY - height - 20),
            width: width, height: height)
        let editor = NSWindow(contentRect: frame,
                              styleMask: [.titled, .closable, .miniaturizable, .resizable],
                              backing: .buffered, defer: false)
        editor.title = L("Bind Key %@", String(KeyboardLayout.keys[slot % KeyboardLayout.keys.count]))
        editor.titlebarAppearsTransparent = true
        editor.toolbarStyle = .unified
        editor.minSize = NSSize(width: 520, height: 520)
        editor.isReleasedWhenClosed = false
        editor.contentView = NSHostingView(rootView: LocalizedView(content: self))
        NSApp.activate(ignoringOtherApps: true)
        editor.makeKeyAndOrderFront(nil)
        return editor
    }

    var body: some View {
        let _ = locale
        VStack(spacing: 0) {
            LauncherDetailView(launcher: $draft)
            Divider()
            HStack {
                if hasBinding {
                    Button("Remove Binding", role: .destructive) { error = !onSave(nil) }
                }
                Spacer()
                Button("Cancel", action: onCancel).keyboardShortcut(.cancelAction)
                Button("Save") { error = !onSave(draft.toConfig()) }
                    .keyboardShortcut(.defaultAction)
                    .disabled(draft.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ||
                              (draft.actionType == .keyboardShortcut
                               ? KeyboardShortcutAction.events(for: draft.keyboardShortcut) == nil
                               : draft.toConfig().exec.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty))
            }.padding(24)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(colorScheme == .dark ? Color(white: 0.115) : Color(white: 0.96))
        .alert("Could not save. Check that the configuration folder is writable.", isPresented: $error) {
            Button("OK", role: .cancel) {}
        }
    }
}
