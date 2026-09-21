import SwiftUI
import UniformTypeIdentifiers

struct IconPickerView: View {
    @Environment(\.locale) private var locale
    @Binding var launcher: EditableLauncher
    @Binding var isPresented: Bool
    @State private var selectedTab = 0
    @State private var symbolSearch = ""
    @State private var symbolCategory = "All"
    @State private var hoveredSymbol: String?

    var body: some View {
        let _ = locale
        VStack(spacing: 0) {
            Picker("", selection: $selectedTab) {
                Text("Apps").tag(0)
                Text("Symbols").tag(1)
                Text("File").tag(2)
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .padding(12)

            Divider()

            // Auto-detect option
            Button(action: {
                launcher.iconMode = .auto
                launcher.iconValue = ""
                isPresented = false
            }) {
                HStack {
                    Image(systemName: "wand.and.stars")
                        .frame(width: 20)
                    Text("Auto-detect from command")
                    Spacer()
                    if launcher.iconMode == .auto {
                        Image(systemName: "checkmark")
                            .foregroundColor(.accentColor)
                    }
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)

            Divider()

            Group {
                switch selectedTab {
                case 0: appIconGrid
                case 1: sfSymbolGrid
                case 2: customFileView
                default: EmptyView()
                }
            }
        }
        .frame(width: 380, height: 440)
    }

    // MARK: - Apps Tab

    var appIconGrid: some View {
        AppSelectionGrid(selectedPath: launcher.iconMode == .appIcon ? launcher.iconValue : nil) { app in
            launcher.iconMode = .appIcon
            launcher.iconValue = app.path
            isPresented = false
        }
    }

    // MARK: - Symbols Tab

    var filteredSymbols: [String] {
        SFSymbols.symbols(matching: symbolSearch, category: symbolCategory)
    }

    private func selectSymbol(_ symbol: String) {
        launcher.iconMode = .sfSymbol
        launcher.iconValue = symbol
        isPresented = false
    }

    var sfSymbolGrid: some View {
        VStack(spacing: 8) {
            TextField("Search or enter a symbol name…", text: $symbolSearch)
                .textFieldStyle(.roundedBorder)
                .padding(.horizontal, 12)
                .padding(.top, 8)
            HStack {
                Picker("Category", selection: $symbolCategory) {
                    Text("All").tag("All")
                    ForEach(SFSymbols.categories, id: \.name) { category in
                        Text(LocalizedStringKey(category.name)).tag(category.name)
                    }
                }.labelsHidden()
                Spacer()
                Text("\(filteredSymbols.count) symbols")
                    .font(.caption).foregroundStyle(.secondary)
            }.padding(.horizontal, 12)

            if let symbol = SFSymbols.exactMatch(symbolSearch) {
                HStack(spacing: 10) {
                    Image(systemName: symbol).font(.system(size: 24)).frame(width: 32)
                    Text(symbol).font(.caption).lineLimit(1).truncationMode(.middle)
                    Spacer(minLength: 0)
                    Button("Use Symbol") { selectSymbol(symbol) }
                }
                .padding(10)
                .background(Color.accentColor.opacity(0.1), in: RoundedRectangle(cornerRadius: 8))
                .padding(.horizontal, 12)
            }

            ScrollView {
                if filteredSymbols.isEmpty && SFSymbols.exactMatch(symbolSearch) == nil {
                    VStack(spacing: 6) {
                        Text("Symbol unavailable").font(.headline)
                        Text("Try another search or a full SF Symbol name.")
                            .font(.caption).foregroundStyle(.secondary)
                    }.padding(.top, 28)
                } else {
                    LazyVGrid(columns: Array(repeating: GridItem(.fixed(44), spacing: 6), count: 7), spacing: 6) {
                        ForEach(filteredSymbols, id: \.self) { symbol in
                            Button { selectSymbol(symbol) } label: {
                                Image(systemName: symbol)
                                    .font(.system(size: 18))
                                    .frame(width: 38, height: 38)
                                    .background(
                                        launcher.iconMode == .sfSymbol && launcher.iconValue == symbol
                                            ? Color.accentColor.opacity(0.2)
                                            : (hoveredSymbol == symbol ? Color.primary.opacity(0.08) : .clear),
                                        in: RoundedRectangle(cornerRadius: 6))
                                    .contentShape(Rectangle())
                            }
                            .buttonStyle(.plain)
                            .onHover { hoveredSymbol = $0 ? symbol : nil }
                            .interactionPointer()
                            .help(symbol)
                            .accessibilityLabel(symbol)
                        }
                    }.padding(12)
                }
            }
            if !symbolSearch.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                Text("Search includes all categories").font(.caption2)
                    .foregroundStyle(.secondary).padding(.bottom, 6)
            }
        }
    }

    // MARK: - Custom File Tab

    var customFileView: some View {
        VStack(spacing: 16) {
            Spacer()

            if launcher.iconMode == .custom, let img = NSImage(contentsOfFile: launcher.iconValue) {
                Image(nsImage: img)
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(width: 64, height: 64)
                    .cornerRadius(12)

                Text(URL(fileURLWithPath: launcher.iconValue).lastPathComponent)
                    .font(.caption)
                    .foregroundColor(.secondary)
            } else {
                Image(systemName: "photo")
                    .font(.system(size: 48))
                    .foregroundColor(.secondary.opacity(0.5))
            }

            Button("Choose Image...") {
                let panel = NSOpenPanel()
                panel.allowedContentTypes = [.image]
                panel.canChooseFiles = true
                panel.canChooseDirectories = false
                panel.message = L("Select an icon image")
                if panel.runModal() == .OK, let url = panel.url {
                    launcher.iconMode = .custom
                    launcher.iconValue = url.path
                    isPresented = false
                }
            }

            Spacer()
        }
        .frame(maxWidth: .infinity)
    }
}

struct AppSelectionGrid: View {
    @Environment(\.locale) private var locale
    let selectedPath: String?
    let onSelect: (AppScanner.App) -> Void
    @State private var search = ""
    @State private var hoveredPath: String?

    var body: some View {
        let _ = locale
        VStack(spacing: 8) {
            TextField("Search apps…", text: $search)
                .textFieldStyle(.roundedBorder)
                .padding(.horizontal, 12)
                .padding(.top, 12)
            ScrollView {
                let apps = AppScanner.shared.apps.filter {
                    search.isEmpty || $0.name.localizedCaseInsensitiveContains(search)
                }
                if apps.isEmpty {
                    Text("No matching apps").foregroundStyle(.secondary).padding(.top, 32)
                }
                LazyVGrid(columns: Array(repeating: GridItem(.fixed(64), spacing: 8), count: 5), spacing: 8) {
                    ForEach(apps) { app in
                        Button { onSelect(app) } label: {
                            VStack(spacing: 4) {
                                Image(nsImage: app.icon).resizable().scaledToFit()
                                    .frame(width: 36, height: 36)
                                Text(app.name).font(.system(size: 9)).lineLimit(1).frame(width: 56)
                            }
                            .padding(4)
                            .background(selectedPath == app.path ? Color.accentColor.opacity(0.2) : (hoveredPath == app.path ? Color.primary.opacity(0.07) : .clear),
                                        in: RoundedRectangle(cornerRadius: 8))
                            .contentShape(Rectangle())
                        }
                        .buttonStyle(.plain)
                        .onHover { hovering in
                            hoveredPath = hovering ? app.path : nil
                        }
                        .interactionPointer()
                        .animation(.easeOut(duration: 0.12), value: hoveredPath)
                        .help(app.name)
                        .accessibilityLabel(app.name)
                    }
                }.padding(12)
            }
        }
    }
}

// MARK: - SF Symbols Library

enum SFSymbols {
    struct Category {
        let name: String
        let symbols: [String]

        init(_ name: String, _ names: String) {
            self.name = name
            var seen = Set<String>()
            symbols = names.split(whereSeparator: { $0.isWhitespace }).map(String.init).filter {
                seen.insert($0).inserted && NSImage(systemSymbolName: $0, accessibilityDescription: nil) != nil
            }
        }
    }

    static let categories: [Category] = [
        Category("General", """
            star.fill heart.fill house.fill gear gearshape.fill bell.fill bookmark.fill
            tag.fill flag.fill pin.fill bolt.fill eye.fill lock.fill lock.open.fill
            key.fill shield.fill crown.fill star star.circle star.circle.fill heart
            heart.circle.fill house house.circle.fill gearshape gearshape.2.fill bell bell.badge.fill
            bookmark tag flag pin bolt bolt.circle.fill bolt.shield.fill
            eye eye.slash.fill lock lock.shield.fill key key.horizontal.fill shield
            crown lightbulb lightbulb.fill power sleep button.programmable button.programmable.square.fill
            command option control shift capslock escape delete.left
            return arrow.up.to.line compact ellipse
            """),
        Category("Media", """
            play.fill pause.fill stop.fill forward.fill backward.fill speaker.wave.2.fill mic.fill
            music.note music.note.list film camera.fill video.fill photo.fill play
            play.circle.fill play.rectangle.fill pause pause.circle.fill stop stop.circle.fill record.circle
            record.circle.fill backward.end.fill forward.end.fill repeat repeat.1 shuffle speaker.fill
            speaker.slash.fill speaker.wave.1.fill speaker.wave.3.fill mic mic.slash.fill music.note.house.fill film.fill
            camera camera.circle.fill camera.aperture video video.slash.fill photo photo.on.rectangle
            photo.stack.fill play.tv.fill tv.fill airplay.audio airplay.video headphones waveform
            waveform.circle.fill hifispeaker.fill opticaldisc metronome.fill
            """),
        Category("Communication", """
            envelope.fill phone.fill message.fill bubble.left.fill bubble.left.and.bubble.right.fill paperplane.fill at
            link envelope envelope.badge.fill envelope.open.fill phone phone.circle.fill phone.arrow.up.right.fill
            message message.badge.fill bubble.left bubble.right.fill bubble.middle.bottom.fill bubble.left.and.text.bubble.right.fill paperplane
            at.circle.fill link.circle.fill quote.bubble.fill ellipsis.bubble.fill video.bubble.fill text.bubble.fill person.crop.circle.badge.checkmark
            """),
        Category("Editing & Documents", """
            pencil pencil.circle.fill scissors paintbrush.fill doc.fill doc.text.fill folder.fill
            folder.badge.plus tray.fill tray.2.fill archivebox.fill list.bullet list.number chart.bar.fill
            chart.pie.fill pencil.tip pencil.and.outline square.and.pencil pencil.and.scribble highlighter eraser.fill
            paintbrush.pointed.fill eyedropper scissors.badge.ellipsis doc doc.text doc.richtext doc.plaintext
            doc.on.doc.fill doc.badge.plus doc.text.magnifyingglass doc.append doc.zipper doc.viewfinder folder
            folder.circle.fill folder.fill.badge.plus folder.badge.minus folder.badge.gearshape folder.badge.person.crop folder.badge.questionmark tray
            tray.and.arrow.down.fill tray.and.arrow.up.fill externaldrive.badge.timemachine archivebox list.bullet.rectangle list.bullet.clipboard list.bullet.indent
            list.bullet.below.rectangle checklist chart.xyaxis.line chart.line.uptrend.xyaxis chart.bar.xaxis chart.dots.scatter chart.pie
            square.and.arrow.up square.and.arrow.down square.and.arrow.up.on.square text.alignleft text.aligncenter text.alignright text.justify
            bold italic underline strikethrough paragraph signature paperclip
            """),
        Category("Arrows", """
            arrow.clockwise arrow.counterclockwise arrow.up.circle.fill arrow.down.circle.fill arrow.left.circle.fill arrow.right.circle.fill arrow.triangle.2.circlepath
            arrowshape.turn.up.right.fill arrow.up arrow.down arrow.left arrow.right arrow.up.right arrow.down.right
            arrow.up.left arrow.down.left arrow.up.and.down arrow.left.and.right arrow.up.left.and.arrow.down.right arrow.down.right.and.arrow.up.left arrow.uturn.backward
            arrow.uturn.forward arrow.uturn.up arrow.uturn.down arrow.turn.down.right arrow.turn.up.right arrow.triangle.branch arrow.triangle.merge
            arrow.triangle.swap arrow.triangle.pull arrow.clockwise.circle.fill arrow.counterclockwise.circle.fill arrow.2.squarepath arrow.up.forward.app.fill arrow.down.app.fill
            arrow.right.to.line arrow.left.to.line arrowshape.turn.up.left.fill arrowshape.zigzag.right.fill chevron.left chevron.right chevron.up
            chevron.down chevron.left.2 chevron.right.2
            """),
        Category("Devices", """
            desktopcomputer laptopcomputer display keyboard printer.fill network wifi
            antenna.radiowaves.left.and.right server.rack externaldrive.fill cpu.fill memorychip.fill keyboard.fill keyboard.badge.ellipsis
            keyboard.chevron.compact.down computermouse.fill magicmouse.fill trackpad.fill display.2 display.trianglebadge.exclamationmark macwindow
            macwindow.on.rectangle rectangle.on.rectangle rectangle.split.2x1 rectangle.split.1x2 sidebar.left sidebar.right sidebar.squares.left
            macbook iphone ipad applewatch airpods airpodspro headphones.circle.fill
            webcam.fill printer scanner.fill wifi.slash wifi.circle.fill personalhotspot bluetooth
            externaldrive externaldrive.connected.to.line.below internaldrive.fill opticaldiscdrive.fill sdcard.fill simcard.fill cable.connector
            powerplug.fill battery.100.bolt bolt.horizontal.fill
            """),
        Category("Objects", """
            briefcase.fill cart.fill creditcard.fill bag.fill gift.fill wrench.fill hammer.fill
            screwdriver.fill briefcase suitcase.fill bag cart basket.fill shippingbox.fill
            shippingbox.and.arrow.backward.fill gift giftcard.fill creditcard wallet.pass.fill banknote.fill key.viewfinder
            wrench.and.screwdriver.fill hammer screwdriver ruler.fill level.fill flashlight.on.fill umbrella.fill
            cup.and.saucer.fill mug.fill waterbottle.fill fork.knife bed.double.fill lamp.desk.fill chair.fill
            sofa.fill clock.desk.fill fan.fill
            """),
        Category("Code & Developer", """
            terminal.fill chevron.left.forwardslash.chevron.right curlybraces number textformat terminal chevron.left.slash.chevron.right
            curlybraces.square.fill number.square.fill function fx scope target point.3.connected.trianglepath
            point.topleft.down.curvedto.point.bottomright.up point.3.filled.connected.trianglepath network.badge.shield lock.doc.fill doc.badge.gearshape puzzlepiece.extension.fill square.stack.3d.up.fill
            square.stack.3d.down.right.fill square.stack.3d.forward.dottedline cube.transparent.fill cylinder.split.1x2.fill externaldrive.fill.badge.checkmark memorychip cpu
            hammer.circle.fill wrench.adjustable.fill testtube.2 ant.fill ladybug.fill
            """),
        Category("Nature", """
            cloud.fill cloud.rain.fill sun.max.fill moon.fill snowflake flame.fill drop.fill
            leaf.fill sun.max sun.min.fill sunrise.fill sunset.fill moon moon.stars.fill
            moon.zzz.fill cloud cloud.sun.fill cloud.moon.fill cloud.drizzle.fill cloud.heavyrain.fill cloud.fog.fill
            cloud.snow.fill cloud.bolt.fill cloud.bolt.rain.fill wind tornado hurricane thermometer.medium
            thermometer.sun.fill drop drop.circle.fill humidity.fill snowflake.circle.fill leaf leaf.circle.fill
            tree.fill mountain.2.fill globe.americas globe.europe.africa.fill globe.asia.australia.fill pawprint.fill fish.fill
            bird.fill lizard.fill
            """),
        Category("Shapes", """
            circle.fill square.fill triangle.fill diamond.fill hexagon.fill app.fill cube.fill
            cylinder.fill square.grid.2x2.fill circle circle.dotted circle.dashed circle.lefthalf.filled circle.righthalf.filled
            smallcircle.filled.circle.fill square square.dashed square.on.square.fill rectangle.fill rectangle.portrait.fill capsule.fill
            oval.fill triangle diamond hexagon pentagon.fill octagon.fill seal.fill
            shield.lefthalf.filled app app.badge.fill square.grid.3x3.fill square.grid.4x3.fill circle.grid.2x2.fill circle.grid.3x3.fill
            circle.hexagongrid.fill square.stack.fill square.3.layers.3d
            """),
        Category("People", """
            person.fill person.2.fill person.3.fill figure.walk hand.raised.fill hand.thumbsup.fill person
            person.circle.fill person.crop.circle.fill person.crop.square.fill person.badge.plus person.badge.minus person.2 person.2.circle.fill
            person.3.sequence.fill person.text.rectangle.fill figure.stand figure.run figure.hiking figure.cycling figure.yoga
            figure.mind.and.body figure.strengthtraining.traditional figure.pool.swim hand.raised hand.point.up.left.fill hand.tap.fill hand.draw.fill
            hand.thumbsup hand.thumbsdown.fill hands.clap.fill hand.wave.fill accessibility human male
            female
            """),
        Category("Symbols", """
            checkmark.circle.fill xmark.circle.fill plus.circle.fill minus.circle.fill questionmark.circle.fill exclamationmark.circle.fill info.circle.fill
            checkmark xmark plus minus multiply divide equal
            percent checkmark.seal.fill xmark.seal.fill plus.app.fill minus.square.fill questionmark.diamond.fill exclamationmark.triangle.fill
            exclamationmark.octagon.fill info.circle questionmark.circle checkmark.square.fill checkmark.rectangle.fill checkmark.shield.fill checkmark.gobackward
            arrow.counterclockwise.circle infinity infinity.circle.fill a.circle.fill b.circle.fill c.circle.fill 1.circle.fill
            2.circle.fill 3.circle.fill
            """),
        Category("Other", """
            globe globe.americas.fill map.fill location.fill clock.fill calendar alarm.fill
            timer battery.100 power trash.fill magnifyingglass paintpalette.fill gamecontroller.fill
            headphones airplane car.fill bus.fill bicycle dollarsign.circle.fill eurosign.circle.fill
            building.fill building.2.fill book.fill books.vertical.fill newspaper.fill graduationcap.fill lightbulb.fill
            puzzlepiece.fill ticket.fill wand.and.stars sparkles map location location.circle.fill
            location.north.fill mappin.and.ellipse mappin.circle.fill compass.drawing safari safari.fill clock
            clock.arrow.circlepath stopwatch.fill alarm calendar.badge.plus calendar.badge.clock calendar.circle.fill timer.circle.fill
            hourglass trash trash.circle.fill magnifyingglass.circle.fill magnifyingglass.plus magnifyingglass.minus line.3.horizontal.decrease.circle.fill
            slider.horizontal.3 switch.2 checkmark.circle.badge.questionmark paintpalette gamecontroller airplane.circle.fill car
            car.side.fill bus doubledecker.bus.fill tram.fill train.side.front.car ferry.fill sailboat.fill
            bicycle.circle.fill scooter fuelpump.fill road.lanes building.2.crop.circle.fill building.columns.fill house.lodge.fill
            book books.vertical book.closed.fill bookmark.circle.fill newspaper graduationcap eyeglasses
            sunglasses.fill ticket puzzlepiece wand.and.rays wand.and.rays.inverse qrcode barcode
            qrcode.viewfinder viewfinder camera.viewfinder
            """),
    ]

    static let all: [String] = {
        var seen = Set<String>()
        return categories.flatMap(\.symbols).filter { seen.insert($0).inserted }
    }()

    static func exactMatch(_ search: String) -> String? {
        let name = search.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !name.isEmpty, NSImage(systemSymbolName: name, accessibilityDescription: nil) != nil else { return nil }
        return name
    }

    static func symbols(matching search: String, category: String) -> [String] {
        let query = search.trimmingCharacters(in: .whitespacesAndNewlines)
        if !query.isEmpty { return all.filter { $0.localizedCaseInsensitiveContains(query) } }
        return category == "All" ? all : categories.first { $0.name == category }?.symbols ?? []
    }
}
