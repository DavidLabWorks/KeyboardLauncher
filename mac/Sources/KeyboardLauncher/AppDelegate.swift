import Carbon
import Cocoa
import SwiftUI

class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem!
    private(set) var panel: LaunchpickPanel!
    private var state = LaunchpickState()
    private var clickMonitor: Any?
    private var keyMonitor: Any?
    private var previousApp: NSRunningApplication?
    private var lastExternalApp: NSRunningApplication?
    private var bindingSlot: Int?
    private var bindingEditorPanel: NSWindow?
    private var bindingCloseObserver: NSObjectProtocol?
    private(set) var settingsWindow: NSWindow?
    private var settingsCloseObserver: NSObjectProtocol?
    private var doubleTap = DoubleTapShortcut()

    private var recordedKeyAwaitingRelease: Int64?
    private var eventTap: CFMachPort?
    private var eventTapSource: CFRunLoopSource?
    private var recordingTap: CFMachPort?
    private var recordingTapSource: CFRunLoopSource?

    // Hotkey IDs
    private let launchpickHotKeyID: UInt32 = 1

    // Launcher shortcut (parsed for event tap suppression of system shortcuts)
    private var launcherKeyCode: Int64 = 49
    private var launcherModifiers = CGEventFlags.maskCommand
    private var suppressSystemShortcut = false

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard state.isMoving else { return .terminateNow }
        // Do not abandon an accepted drag while its atomic save is in flight.
        Timer.scheduledTimer(withTimeInterval: 0.05, repeats: true) { [weak self] timer in
            guard self?.state.isMoving != true else { return }
            timer.invalidate()
            sender.reply(toApplicationShouldTerminate: true)
        }
        return .terminateLater
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return false
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        lastExternalApp = KeyboardShortcutAction.externalApp(NSWorkspace.shared.frontmostApplication)
        NSWorkspace.shared.notificationCenter.addObserver(
            self, selector: #selector(trackExternalApp(_:)),
            name: NSWorkspace.didActivateApplicationNotification, object: nil)
        NotificationCenter.default.addObserver(self, selector: #selector(refreshLanguage),
                                               name: AppLanguage.changed, object: nil)
        setupMainMenu()
        setupStatusItem()
        setupPanel()
        reloadLaunchers()

        state.onLaunch = { [weak self] item in
            self?.launch(item)
        }
        state.onDismiss = { [weak self] in
            self?.hidePanel()
        }

        state.onSettings = { [weak self] in self?.openSettings() }

        state.onBind = { [weak self] launcher, slot in
            guard self?.state.isMoving == false else { return false }
            guard LaunchpickConfig.update({ config in
                config.bind(launcher, to: slot)
                return true
            }) else { return false }
            self?.reloadLaunchers()
            return true
        }

        state.onMove = { source, destination, completion in
            DispatchQueue.global(qos: .userInitiated).async {
                let success = LaunchpickConfig.update { $0.moveBinding(from: source, to: destination) }
                DispatchQueue.main.async { completion(success) }
            }
        }

        // Load shortcuts from config
        let config = LaunchpickConfig.load()
        loadLauncherShortcut(from: config)
        applySpotlightShortcut(from: config)

        reloadLaunchpickHotKey(from: config)

        // Reload hotkeys when shortcuts change in settings
        NotificationCenter.default.addObserver(
            forName: Notification.Name("ReloadHotKey"),
            object: nil,
            queue: .main
        ) { [weak self] _ in
            let config = LaunchpickConfig.load()
            self?.reloadLaunchpickHotKey(from: config)
            self?.loadLauncherShortcut(from: config)
            self?.applySpotlightShortcut(from: config)
        }

        // Pre-load system apps in background so first search doesn't lag
        DispatchQueue.global(qos: .utility).async {
            _ = AppScanner.shared.apps
        }

        // Request accessibility permission and install event tap
        if !AccessibilityHelper.isTrusted {
            // Try the system prompt first
            AccessibilityHelper.checkAndRequestPermission()

            // If still not trusted, show our own alert with a direct link
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                if !AccessibilityHelper.isTrusted {
                    AccessibilityHelper.showAccessibilityAlert()
                }
            }
        }
        setupEventTap()
    }

    private func reloadLaunchpickHotKey(from config: LaunchpickConfig) {
        HotKeyManager.shared.unregister(id: launchpickHotKeyID)
        guard let (keyCode, modifiers) = LaunchpickConfig.parseShortcut(config.shortcut) else { return }
        HotKeyManager.shared.register(id: launchpickHotKeyID, keyCode: keyCode, modifiers: modifiers) { [weak self] in
            DispatchQueue.main.async {
                self?.handleLaunchpickHotKey()
            }
        }
    }

    private func loadLauncherShortcut(from config: LaunchpickConfig) {
        let parsed = LaunchpickConfig.parseShortcutForSymbolicHotKey(config.shortcut)
        doubleTap = DoubleTapShortcut(keyCode: config.doubleTapKey.flatMap { UInt16($0.split(separator: ":").first ?? "") })
        launcherKeyCode = Int64(parsed.keyCode)
        launcherModifiers = CGEventFlags(rawValue: UInt64(parsed.modifiers))
        suppressSystemShortcut = LaunchpickConfig.parseShortcut(config.shortcut) != nil && (config.suppressSystemShortcut ?? false)
    }

    private func applySpotlightShortcut(from config: LaunchpickConfig) {
        if let shortcut = config.spotlightShortcut, !shortcut.isEmpty {
            SpotlightShortcutManager.applyShortcut(shortcut)
        }
    }

    // MARK: - Launchpick hotkey handler

    private func handleLaunchpickHotKey() {
        togglePanel()
    }

    // MARK: - Menu setup

    @objc private func refreshLanguage() {
        setupMainMenu()
        setupStatusItem()
        settingsWindow?.title = L("Keyboard Launcher Settings")
        if let slot = bindingSlot {
            bindingEditorPanel?.title = L("Bind Key %@", String(KeyboardLayout.keys[slot % KeyboardLayout.keys.count]))
        }
    }

    private func setupMainMenu() {
        let mainMenu = NSMenu()

        let editMenu = NSMenu(title: L("Edit"))
        editMenu.addItem(withTitle: L("Undo"), action: Selector(("undo:")), keyEquivalent: "z")
        let redoItem = editMenu.addItem(withTitle: L("Redo"), action: Selector(("redo:")), keyEquivalent: "z")
        redoItem.keyEquivalentModifierMask = [.command, .shift]
        editMenu.addItem(.separator())
        editMenu.addItem(withTitle: L("Cut"), action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        editMenu.addItem(withTitle: L("Copy"), action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: L("Paste"), action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: L("Select All"), action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")

        let editMenuItem = NSMenuItem(title: L("Edit"), action: nil, keyEquivalent: "")
        editMenuItem.submenu = editMenu
        mainMenu.addItem(editMenuItem)

        NSApp.mainMenu = mainMenu
    }

    private func setupStatusItem() {
        if statusItem == nil { statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength) }
        if let button = statusItem.button {
            let icon = NSImage(named: "MenuBarTemplate")
            icon?.isTemplate = true
            icon?.size = NSSize(width: 20, height: 20)
            button.image = icon
            button.imagePosition = .imageOnly
            button.setAccessibilityLabel("Keyboard Launcher")
        }

        let menu = NSMenu()

        let settingsItem = NSMenuItem(title: L("Settings..."), action: #selector(openSettings), keyEquivalent: ",")
        settingsItem.target = self
        menu.addItem(settingsItem)

        menu.addItem(.separator())

        let showItem = NSMenuItem(title: L("Show Keyboard Launcher"), action: #selector(togglePanel), keyEquivalent: "")
        showItem.target = self
        menu.addItem(showItem)

        menu.addItem(.separator())
        menu.addItem(NSMenuItem(title: L("Quit Keyboard Launcher"), action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"))

        statusItem.menu = menu
    }

    @objc func openSettings() {
        // Finish the launcher's mouse event before changing key windows.
        DispatchQueue.main.async { [weak self] in self?.presentSettings() }
    }

    private func presentSettings() {
        if let window = settingsWindow {
            if window.isMiniaturized { window.deminiaturize(nil) }
            bringSettingsForward(window)
            return
        }

        let settingsState = SettingsState()
        settingsState.load()

        let settingsView = SettingsView(state: settingsState)
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 880, height: 620),
            styleMask: [.titled, .closable, .resizable, .miniaturizable],
            backing: .buffered,
            defer: false
        )
        window.title = L("Keyboard Launcher Settings")
        window.titlebarAppearsTransparent = true
        window.toolbarStyle = .unified
        window.minSize = NSSize(width: 800, height: 520)
        window.isReleasedWhenClosed = false
        window.contentView = NSHostingView(rootView: LocalizedView(content: settingsView))
        window.center()

        if let old = settingsCloseObserver {
            NotificationCenter.default.removeObserver(old)
        }
        settingsCloseObserver = NotificationCenter.default.addObserver(
            forName: NSWindow.willCloseNotification,
            object: window,
            queue: .main
        ) { [weak self] _ in
            self?.settingsWindow = nil
        }

        settingsWindow = window
        bringSettingsForward(window)
    }

    private func bringSettingsForward(_ window: NSWindow) {
        NSApp.activate(ignoringOtherApps: true)
        hidePanel(restorePreviousApp: false)
        window.makeKeyAndOrderFront(nil)
        // App activation can reorder floating panels after the click callback.
        DispatchQueue.main.async { [weak self, weak window] in
            guard let self, let window, self.settingsWindow === window, window.isVisible else { return }
            self.hidePanel(restorePreviousApp: false)
            window.makeKeyAndOrderFront(nil)
        }
    }

    // MARK: - Launchpick panel

    func setupPanel() {
        panel = LaunchpickPanel()
        panel.allowsHeaderDragging = true

        let hostingView = NSHostingView(rootView: LocalizedView(content: ContentView(state: state)))
        // Liquid Glass is composited separately. Give WindowServer a nonzero-alpha
        // backing surface so blank areas cannot click through to another app.
        let surface = NSView(frame: panel.contentRect(forFrameRect: panel.frame))
        surface.wantsLayer = true
        surface.layer?.backgroundColor = NSColor.black.withAlphaComponent(0.02).cgColor
        surface.layer?.cornerRadius = 26
        surface.layer?.masksToBounds = true
        hostingView.frame = surface.bounds
        hostingView.autoresizingMask = [.width, .height]
        surface.addSubview(hostingView)
        panel.contentView = surface
        state.onEditingChanged = { [weak self] in
            DispatchQueue.main.async { self?.updateBindingEditor() }
        }

        // Dismiss on explicit outside clicks, not transient key-window changes.
    }

    private func updateBindingEditor() {
        guard let slot = state.editingSlot else { return }
        state.editingSlot = nil
        bindingEditorPanel?.close()
        panel.level = .normal
        let editor = BindingEditor(slot: slot, onCancel: { [weak self] in
            self?.bindingEditorPanel?.close()
        }, onSave: { [weak self] launcher in
            guard let self, self.state.onBind?(launcher, slot) == true else { return false }
            self.bindingEditorPanel?.close()
            return true
        })
        let window = editor.present(over: panel)
        bindingEditorPanel = window
        bindingSlot = slot
        if let observer = bindingCloseObserver { NotificationCenter.default.removeObserver(observer) }
        bindingCloseObserver = NotificationCenter.default.addObserver(
            forName: NSWindow.willCloseNotification, object: window, queue: .main
        ) { [weak self] _ in
            self?.bindingEditorPanel = nil
            self?.bindingSlot = nil
            self?.panel.level = .floating
        }
    }

    private func reloadLaunchers() {
        guard !state.isMoving else { return }
        let config = LaunchpickConfig.load()
        state.launchers = config.launchers.enumerated().map { index, configItem in
            LaunchpickItem(
                slot: configItem.keyIndex ?? index,
                name: configItem.name,
                exec: configItem.exec,
                icon: IconResolver.resolve(icon: configItem.icon ?? (configItem.keyboardShortcut == nil ? nil : "sf:keyboard"), exec: configItem.exec),
                keyboardShortcut: configItem.keyboardShortcut
            )
        }
        state.keyboardPage = min(state.keyboardPage, state.pageCount - 1)
    }

    @objc func togglePanel() {
        if panel.isVisible {
            if let screen = panel.screen, !screen.frame.contains(NSEvent.mouseLocation) {
                // Move the existing panel, preserving its state and event monitors.
                resizePanelToFit()
                NSApp.activate(ignoringOtherApps: true)
                panel.makeKeyAndOrderFront(nil)
            } else {
                hidePanel()
            }
        } else {
            showPanel()
        }
    }

    @objc private func trackExternalApp(_ notification: Notification) {
        if let app = KeyboardShortcutAction.externalApp(
            notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication) {
            lastExternalApp = app
        }
    }

    private func showPanel() {

        previousApp = KeyboardShortcutAction.externalApp(NSWorkspace.shared.frontmostApplication)
            ?? KeyboardShortcutAction.externalApp(lastExternalApp)

        reloadLaunchers()
        state.keyboardPage = 0

        resizePanelToFit()
        panel.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)

        clickMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] _ in
            guard let self, self.state.editingSlot == nil, !self.panel.frame.contains(NSEvent.mouseLocation) else { return }
            self.hidePanel()
        }

        keyMonitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown, .leftMouseDown, .leftMouseDragged, .leftMouseUp]) { [weak self] event in
            guard let self = self else { return event }
            guard event.window === self.panel, self.state.editingSlot == nil else { return event }
            if event.type != .keyDown {
                return self.panel.dragHeader(with: event) ? nil : event
            }
            if event.keyCode == 53 {
                self.hidePanel()
                return nil
            }

            guard event.modifierFlags.intersection([.command, .control, .option, .shift]).isEmpty,
                  !event.isARepeat else { return event }
            if let index = KeyboardLayout.index(for: event.keyCode,
                                                page: self.state.keyboardPage,
                                                count: self.state.pageCount * KeyboardLayout.keys.count),
               let item = self.state.launchers.first(where: { $0.slot == index }) {
                self.state.onLaunch?(item)
                return nil
            }
            return event
        }
    }

    private func resizePanelToFit() {
        let mouseLocation = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { $0.frame.contains(mouseLocation) } ?? NSScreen.main
        let screenFrame = screen?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1000, height: 800)
        let panelWidth = min(CGFloat(1040), screenFrame.width - 40)
        let panelHeight = min(CGFloat(560), screenFrame.height - 40)
        let x = screenFrame.midX - panelWidth / 2
        let y = screenFrame.midY - panelHeight / 2
        panel.setFrame(NSRect(x: x, y: y, width: panelWidth, height: panelHeight), display: true)
    }

    private func hidePanel(restorePreviousApp: Bool = true) {
        guard panel.isVisible else { return }
        let appToRestore = previousApp
        previousApp = nil
        panel.cancelHeaderDrag()
        state.draggingSlot = nil
        panel.orderOut(nil)
        state.editingSlot = nil

        if let monitor = clickMonitor {
            NSEvent.removeMonitor(monitor)
            clickMonitor = nil
        }

        if let monitor = keyMonitor {
            NSEvent.removeMonitor(monitor)
            keyMonitor = nil
        }

        if restorePreviousApp, let prev = appToRestore, prev != NSRunningApplication.current {
            prev.activate(options: [])
        }
        previousApp = nil
    }

    private func launch(_ item: LaunchpickItem) {
        let target = previousApp
        hidePanel()
        if let shortcut = item.keyboardShortcut {
            KeyboardShortcutAction.send(shortcut, to: target)
            return
        }
        let home = NSHomeDirectory()
        let exec = item.exec
            .replacingOccurrences(of: "'~/", with: "'\(home)/")
            .replacingOccurrences(of: "\"~/", with: "\"\(home)/")
            .replacingOccurrences(of: " ~/", with: " \(home)/")
        DispatchQueue.global(qos: .userInitiated).async {
            let process = Process()
            process.executableURL = URL(fileURLWithPath: "/bin/sh")
            process.arguments = ["-c", exec]
            process.environment = ProcessInfo.processInfo.environment
            try? process.run()
        }
    }

    // MARK: - CGEventTap (runs on main run loop)

    private var eventTapRetryTimer: Timer?

    private func setupEventTap() {
        let eventTypes: [CGEventType] = [.keyDown, .keyUp, .flagsChanged, .leftMouseDown, .rightMouseDown, .otherMouseDown]
        let eventMask: CGEventMask = eventTypes.reduce(0) { $0 | (1 << $1.rawValue) }

        let selfPtr = Unmanaged.passUnretained(self).toOpaque()

        // Daily activation runs after HID filters (including keyboard-sharing tools).
        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .tailAppendEventTap,
            options: .defaultTap,
            eventsOfInterest: eventMask,
            callback: { (proxy, type, event, refcon) -> Unmanaged<CGEvent>? in
                guard let refcon = refcon else { return Unmanaged.passUnretained(event) }
                let delegate = Unmanaged<AppDelegate>.fromOpaque(refcon).takeUnretainedValue()
                return delegate.handleEventTap(proxy: proxy, type: type, event: event)
            },
            userInfo: selfPtr
        ) else {
            NSLog("Launchpick: Failed to create event tap. Waiting for Accessibility permission...")
            if eventTapRetryTimer == nil {
                eventTapRetryTimer = Timer.scheduledTimer(withTimeInterval: 2.0, repeats: true) { [weak self] _ in
                    if AXIsProcessTrusted() {
                        self?.eventTapRetryTimer?.invalidate()
                        self?.eventTapRetryTimer = nil
                        self?.setupEventTap()
                    }
                }
            }
            return
        }

        // Only recording needs to consume input before system/global shortcuts.
        guard let capture = CGEvent.tapCreate(
            tap: .cghidEventTap,
            place: .headInsertEventTap,
            options: .defaultTap,
            eventsOfInterest: eventMask,
            callback: { (_, type, event, refcon) in
                guard let refcon else { return Unmanaged.passUnretained(event) }
                let delegate = Unmanaged<AppDelegate>.fromOpaque(refcon).takeUnretainedValue()
                return delegate.handleRecordingTap(type: type, event: event)
            }, userInfo: selfPtr
        ) else {
            CFMachPortInvalidate(tap)
            NSLog("KeyboardLauncher: Failed to create shortcut recording tap")
            return
        }
        recordingTap = capture
        let captureSource = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, capture, 0)
        recordingTapSource = captureSource
        CFRunLoopAddSource(CFRunLoopGetMain(), captureSource, .commonModes)
        CGEvent.tapEnable(tap: capture, enable: true)

        eventTap = tap
        let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        eventTapSource = source
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        NSLog("Launchpick: Event tap installed")
    }

    // Intercept before app shortcuts and session-level hotkey listeners can act.
    func interceptShortcutRecording(type: CGEventType, event: CGEvent, recorder: ShortcutRecorderNSView?) -> Bool {
        let code = event.getIntegerValueField(.keyboardEventKeycode)
        if let pending = recordedKeyAwaitingRelease, code == pending,
           type == .keyDown || type == .keyUp {
            if type == .keyUp { recordedKeyAwaitingRelease = nil }
            return true
        }
        guard [.keyDown, .keyUp, .flagsChanged].contains(type),
              let recorder, recorder.isRecording else { return false }
        if type != .keyUp, let nsEvent = NSEvent(cgEvent: event) {
            _ = recorder.record(nsEvent)
            if !recorder.isRecording && type == .keyDown { recordedKeyAwaitingRelease = code }
        }
        return true
    }

    func handleRecordingTap(type: CGEventType, event: CGEvent) -> Unmanaged<CGEvent>? {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            recordedKeyAwaitingRelease = nil
            doubleTap.reset()
            if let recordingTap { CGEvent.tapEnable(tap: recordingTap, enable: true) }
            return Unmanaged.passUnretained(event)
        }
        guard event.getIntegerValueField(.eventSourceUserData) != KeyboardShortcutAction.eventMarker else {
            return Unmanaged.passUnretained(event)
        }
        if interceptShortcutRecording(type: type, event: event,
                                      recorder: NSApp.keyWindow?.firstResponder as? ShortcutRecorderNSView) {
            doubleTap.reset()
            return nil
        }
        return Unmanaged.passUnretained(event)
    }

    // Event tap callback — runs on main run loop.
    // Keep it minimal. All heavy work (AX operations) dispatched to background.
    private func handleEventTap(proxy: CGEventTapProxy, type: CGEventType, event: CGEvent) -> Unmanaged<CGEvent>? {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            doubleTap.reset()
            if let tap = eventTap {
                CGEvent.tapEnable(tap: tap, enable: true)
            }
            return Unmanaged.passUnretained(event)
        }

        if event.getIntegerValueField(.eventSourceUserData) == KeyboardShortcutAction.eventMarker {
            return Unmanaged.passUnretained(event)
        }
        let flags = event.flags
        let keyCode = event.getIntegerValueField(.keyboardEventKeycode)


        let modifierFlags: [UInt16: CGEventFlags] = [58: .maskAlternate, 61: .maskAlternate,
            55: .maskCommand, 54: .maskCommand, 59: .maskControl, 62: .maskControl,
            56: .maskShift, 60: .maskShift, 63: .maskSecondaryFn, 57: .maskAlphaShift]
        if state.editingSlot != nil || (NSApp.keyWindow?.firstResponder as? ShortcutRecorderNSView)?.isRecording == true ||
            [.leftMouseDown, .rightMouseDown, .otherMouseDown].contains(type) {
            doubleTap.reset()
        } else if [.keyDown, .keyUp, .flagsChanged].contains(type) {
            let code = UInt16(keyCode)
            let allowed = doubleTap.keyCode.flatMap { modifierFlags[$0] } ?? []
            let otherModifiers = flags.intersection([.maskCommand, .maskControl, .maskAlternate, .maskShift]).subtracting(allowed)
            let time = Double(event.timestamp) / 1_000_000_000
            // Caps Lock reports its toggled state, rather than a physical release.
            let capsLockPress = type == .flagsChanged && code == 57
            if capsLockPress {
                _ = doubleTap.handle(keyCode: code, isDown: true, time: time,
                                     hasOtherModifiers: !otherModifiers.isEmpty)
            }
            let isDown = capsLockPress ? false : (type == .flagsChanged ? flags.contains(modifierFlags[code] ?? []) : type == .keyDown)
            if doubleTap.handle(keyCode: code, isDown: isDown,
                                time: time,
                                isRepeat: event.getIntegerValueField(.keyboardEventAutorepeat) != 0,
                                hasOtherModifiers: !otherModifiers.isEmpty) {
                DispatchQueue.main.async { [weak self] in self?.handleLaunchpickHotKey() }
            }
        }

        if type == .keyDown {
            // Launcher shortcut — suppress to prevent system shortcut (e.g. Spotlight)
            let fullMods = flags.intersection([.maskCommand, .maskAlternate, .maskControl, .maskShift])
            let targetLauncherMods = launcherModifiers.intersection([.maskCommand, .maskAlternate, .maskControl, .maskShift])
            if suppressSystemShortcut && keyCode == launcherKeyCode && fullMods == targetLauncherMods {
                DispatchQueue.main.async { [weak self] in
                    self?.handleLaunchpickHotKey()
                }
                return nil
            }
        }

        return Unmanaged.passUnretained(event)
    }

}
