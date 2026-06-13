import SwiftUI
import UniformTypeIdentifiers

// MARK: - AppEvent Kind

enum AppEventKind: String, CaseIterable {
    case appInstalled      = "app_installed"
    case appReset          = "app_reset"
    case databaseReset     = "database_reset"
    case licenseActivated  = "license_activated"
    case driveConnected    = "drive_connected"
    case driveDisconnected = "drive_disconnected"
    case driveRemoved      = "drive_removed"
    case reindexTriggered  = "reindex_triggered"
    case exportDone        = "export_done"
    case settingsChanged   = "settings_changed"
    case cloudSync         = "cloud_sync"
    case tokenSync         = "token_sync"
    case appOpened         = "app_opened"
    case appClosed         = "app_closed"
    case indexComplete     = "index_complete"
    case indexSkipped      = "index_skipped"
    case activityLogReset  = "activity_log_reset"
    case databaseRestored  = "database_restored"
    case passcodeChanged   = "passcode_changed"

    var label: String {
        switch self {
        case .appInstalled:     return "App installed"
        case .appReset:         return "App reset"
        case .databaseReset:    return "Database reset"
        case .licenseActivated: return "License activated"
        case .driveConnected:   return "Drive connected"
        case .driveDisconnected:return "Drive disconnected"
        case .driveRemoved:     return "Drive removed"
        case .reindexTriggered: return "Re-index triggered"
        case .exportDone:       return "Export done"
        case .settingsChanged:  return "Settings changed"
        case .cloudSync:        return "Cloud sync"
        case .tokenSync:        return "Token sync"
        case .appOpened:        return "App opened"
        case .appClosed:        return "App closed"
        case .indexComplete:    return "Index complete"
        case .indexSkipped:     return "Index skipped"
        case .activityLogReset: return "Activity log reset"
        case .databaseRestored: return "Database restored"
        case .passcodeChanged:  return "Passcode changed"
        }
    }

    var icon: String {
        switch self {
        case .appInstalled:     return "app.badge.checkmark"
        case .appReset:         return "arrow.counterclockwise.circle"
        case .databaseReset:    return "cylinder.split.1x2"
        case .licenseActivated: return "checkmark.seal.fill"
        case .driveConnected:   return "externaldrive.fill.badge.plus"
        case .driveDisconnected:return "externaldrive.badge.minus"
        case .driveRemoved:     return "trash"
        case .reindexTriggered: return "arrow.clockwise"
        case .exportDone:       return "square.and.arrow.up"
        case .settingsChanged:  return "gearshape"
        case .cloudSync:        return "icloud.and.arrow.up"
        case .tokenSync:        return "key.horizontal"
        case .appOpened:        return "power"
        case .appClosed:        return "power.dotted"
        case .indexComplete:    return "checkmark.circle.fill"
        case .indexSkipped:     return "forward.fill"
        case .activityLogReset: return "clock.badge.xmark"
        case .databaseRestored: return "arrow.counterclockwise.circle"
        case .passcodeChanged:  return "lock.rotation"
        }
    }

    var color: Color {
        switch self {
        case .appInstalled:     return .purple
        case .appReset:         return .red
        case .databaseReset:    return .orange
        case .licenseActivated: return .green
        case .driveConnected:   return .green
        case .driveDisconnected:return .gray
        case .driveRemoved:     return .red
        case .reindexTriggered: return .blue
        case .exportDone:       return .teal
        case .settingsChanged:  return .gray
        case .cloudSync:        return .cyan
        case .tokenSync:        return .indigo
        case .appOpened:        return .green
        case .appClosed:        return .secondary
        case .indexComplete:    return .green
        case .indexSkipped:     return .orange
        case .activityLogReset: return .red
        case .databaseRestored: return .purple
        case .passcodeChanged:  return .indigo
        }
    }
}

// MARK: - AppEvent Model

struct AppEvent: Identifiable {
    let id: Int64
    let kind: AppEventKind
    let detail: String
    let occurredAt: Date
}

// MARK: - Time Filter

enum ActivityTimeFilter: String, CaseIterable {
    case last7   = "Last 7 days"
    case last30  = "Last 30 days"
    case last90  = "Last 90 days"
    case allTime = "All time"

    var days: Int? {
        switch self {
        case .last7:   return 7
        case .last30:  return 30
        case .last90:  return 90
        case .allTime: return nil
        }
    }
}

// MARK: - ActivityLogView

struct ActivityLogView: View {
    @EnvironmentObject var store: AppStore
    @State private var timeFilter: ActivityTimeFilter = .last30
    @State private var exportCSV: ExportFile? = nil
    @State private var exportPDF: ExportFile? = nil
    @State private var showCSVExporter = false
    @State private var showPDFExporter = false

    // Pull from store — app-level events only
    private var allEvents: [ActivityEvent] { store.recentActivity }

    private var filtered: [ActivityEvent] {
        guard let days = timeFilter.days else { return allEvents }
        let cutoff = Calendar.current.date(byAdding: .day, value: -days, to: Date()) ?? .distantPast
        return allEvents.filter { $0.occurredAt >= cutoff }
    }

    // MARK: Stats
    private var totalEvents: Int    { filtered.count }
    private var reindexCount: Int   { filtered.filter { $0.kind == .reindexed }.count }
    private var connectedCount: Int { filtered.filter { $0.kind == .driveConnected }.count }
    private var removedCount: Int   { filtered.filter { $0.kind == .folderRemoved }.count }

    var body: some View {
        VStack(spacing: 0) {

            // ── Top bar ──────────────────────────────────────────────
            HStack(spacing: 10) {
                // Install date
                if let date = store.appInstallDate {
                    Text("Installed \(date.formatted(date: .abbreviated, time: .omitted))")
                        .font(.system(size: 11))
                        .foregroundStyle(.tertiary)
                }
                Spacer()

                // Time filter picker
                Picker("", selection: $timeFilter) {
                    ForEach(ActivityTimeFilter.allCases, id: \.self) { f in
                        Text(f.rawValue).tag(f)
                    }
                }
                .pickerStyle(.menu)
                .frame(width: 130)
                .font(.system(size: 12))

                // Export
                Menu {
                    Button {
                        exportCSV = makeCSVExport()
                        showCSVExporter = true
                        store.logAppEvent(.exportDone, detail: "Activity log exported as CSV")
                    } label: { Label("Export as CSV", systemImage: "tablecells") }
                    Button {
                        exportPDF = makePDFExport()
                        showPDFExporter = true
                        store.logAppEvent(.exportDone, detail: "Activity log exported as PDF")
                    } label: { Label("Export as PDF", systemImage: "doc.richtext") }
                } label: {
                    HStack(spacing: 4) {
                        Image(systemName: "square.and.arrow.up")
                        Text("Export")
                    }
                    .font(.system(size: 12))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 5)
                    .background(.background.secondary)
                    .clipShape(RoundedRectangle(cornerRadius: 6))
                    .overlay(RoundedRectangle(cornerRadius: 6).stroke(.separator, lineWidth: 0.5))
                }
                .menuStyle(.borderlessButton)
                .fixedSize()


            }
            .padding(.horizontal, 20)
            .padding(.vertical, 12)
            .background(.background)

            Divider()

            // ── Stat cards ───────────────────────────────────────────
            HStack(spacing: 12) {
                statCard("Total Events",     value: "\(totalEvents)")
                statCard("Re-indexed",       value: "\(reindexCount)")
                statCard("Drives Connected", value: "\(connectedCount)")
                statCard("Drives Removed",   value: "\(removedCount)")
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 14)
            .background(.background)

            Divider()

            // ── Event list ───────────────────────────────────────────
            if filtered.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: "clock.badge.xmark")
                        .font(.system(size: 36))
                        .foregroundStyle(.tertiary)
                    Text("No activity in this period")
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 0) {
                        ForEach(filtered) { event in
                            eventRow(event)
                            Divider().padding(.leading, 44)
                        }
                    }
                    .padding(.vertical, 8)
                }
            }
        }
        .navigationTitle("Activity Log")
        .toolbar {
            ToolbarItem(placement: .automatic) {
                Button {
                    store.reload()
                } label: {
                    Image(systemName: "arrow.clockwise")
                }
                .help("Refresh activity log")
            }
        }
        .fileExporter(
            isPresented: $showCSVExporter,
            document: exportCSV,
            contentType: .commaSeparatedText,
            defaultFilename: "DriveVault_Activity_\(dateStamp()).csv"
        ) { _ in }
        .fileExporter(
            isPresented: $showPDFExporter,
            document: exportPDF,
            contentType: .pdf,
            defaultFilename: "DriveVault_Activity_\(dateStamp()).pdf"
        ) { _ in }

    }

    // MARK: Stat Card

    private func statCard(_ label: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(label)
                .font(.system(size: 12))
                .foregroundStyle(.secondary)
            Text(value)
                .font(.system(size: 26, weight: .medium))
                .foregroundStyle(.primary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .background(.background.secondary)
        .clipShape(RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(.separator, lineWidth: 0.5))
    }

    // MARK: Event Row

    private func eventRow(_ event: ActivityEvent) -> some View {
        HStack(alignment: .top, spacing: 12) {
            // Colored dot
            Circle()
                .fill(event.kind.color)
                .frame(width: 10, height: 10)
                .padding(.top, 4)

            // Icon
            Image(systemName: event.kind.icon)
                .font(.system(size: 13))
                .foregroundStyle(event.kind.color)
                .frame(width: 16)
                .padding(.top, 1)

            // Text
            VStack(alignment: .leading, spacing: 3) {
                Text(event.title)
                    .font(.system(size: 13, weight: .medium))
                    .lineLimit(1)
                if !event.subtitle.isEmpty {
                    Text(event.subtitle)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(2)
                }
            }

            Spacer()

            // Timestamp
            Text(event.occurredAt.formatted(date: .abbreviated, time: .shortened))
                .font(.system(size: 11))
                .foregroundStyle(.tertiary)
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 10)
    }

    // MARK: Export

    private func makeCSVExport() -> ExportFile {
        var lines = ["Date,Time,Event,Detail"]
        for event in filtered {
            let date   = event.occurredAt.formatted(date: .abbreviated, time: .omitted)
            let time   = event.occurredAt.formatted(date: .omitted, time: .shortened)
            let detail = event.subtitle.replacingOccurrences(of: ",", with: ";")
            lines.append("\(date),\(time),\(event.title),\(detail)")
        }
        return ExportFile(csvContent: lines.joined(separator: "\n"))
    }

    private func makePDFExport() -> ExportFile {
        let subtitle = "Exported \(Date().formatted(date: .long, time: .shortened)) · \(filtered.count) events"
        let rows = filtered.map { event -> (date: String, time: String, event: String, detail: String) in
            (
                date:   event.occurredAt.formatted(date: .abbreviated, time: .omitted),
                time:   event.occurredAt.formatted(date: .omitted, time: .shortened),
                event:  event.title,
                detail: event.subtitle
            )
        }
        let data = ExportFile.drawPDF(events: rows, subtitle: subtitle)
        return ExportFile(rawData: data)
    }

    private func dateStamp() -> String {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd"
        return f.string(from: Date())
    }
}

// MARK: - ExportFile (FileDocument)

struct ExportFile: FileDocument {
    static var readableContentTypes: [UTType] { [.commaSeparatedText, .pdf, .plainText] }
    let data: Data
    let type: UTType

    init(csvContent: String) {
        self.data = csvContent.data(using: .utf8) ?? Data()
        self.type = .commaSeparatedText
    }

    init(rawData: Data) {
        self.data = rawData
        self.type = .pdf
    }

    init(configuration: ReadConfiguration) throws {
        data = Data(); type = .plainText
    }

    func fileWrapper(configuration: WriteConfiguration) throws -> FileWrapper {
        FileWrapper(regularFileWithContents: data)
    }

    // MARK: - Clean PDF generation with CoreGraphics

    static func drawPDF(events: [(date: String, time: String, event: String, detail: String)], subtitle: String) -> Data {
        let pageW: CGFloat = 612   // US Letter
        let pageH: CGFloat = 792
        let margin: CGFloat = 44
        let colW: [CGFloat] = [80, 52, 130, pageW - margin * 2 - 80 - 52 - 130]
        let rowH: CGFloat = 20
        let headerH: CGFloat = rowH

        let pdfData = NSMutableData()
        var mediaBox = CGRect(x: 0, y: 0, width: pageW, height: pageH)
        guard let ctx = CGContext(consumer: CGDataConsumer(data: pdfData as CFMutableData)!,
                                  mediaBox: &mediaBox, nil) else { return Data() }

        let titleFont  = CTFontCreateWithName("Helvetica-Bold" as CFString, 17, nil)
        let subFont    = CTFontCreateWithName("Helvetica" as CFString, 10, nil)
        let hdrFont    = CTFontCreateWithName("Helvetica-Bold" as CFString, 9, nil)
        let cellFont   = CTFontCreateWithName("Helvetica" as CFString, 9, nil)
        let grayColor  = CGColor(gray: 0.45, alpha: 1)
        let darkColor  = CGColor(gray: 0.1, alpha: 1)
        let lineColor  = CGColor(gray: 0.85, alpha: 1)
        let hdrBg      = CGColor(red: 0.94, green: 0.94, blue: 0.96, alpha: 1)
        let altBg      = CGColor(red: 0.975, green: 0.975, blue: 0.985, alpha: 1)

        func drawStr(_ text: String, font: CTFont, color: CGColor = CGColor(gray: 0.1, alpha: 1),
                     x: CGFloat, y: CGFloat, width: CGFloat) {
            let para = NSMutableParagraphStyle()
            para.lineBreakMode = .byTruncatingTail
            let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color, .paragraphStyle: para]
            let str = NSAttributedString(string: text, attributes: attrs)
            let fs  = CTFramesetterCreateWithAttributedString(str)
            let path = CGPath(rect: CGRect(x: x, y: y, width: width - 4, height: rowH), transform: nil)
            let frame = CTFramesetterCreateFrame(fs, CFRangeMake(0, 0), path, nil)
            ctx.saveGState(); CTFrameDraw(frame, ctx); ctx.restoreGState()
        }

        func hline(y: CGFloat) {
            ctx.setStrokeColor(lineColor); ctx.setLineWidth(0.4)
            ctx.move(to: CGPoint(x: margin, y: y))
            ctx.addLine(to: CGPoint(x: pageW - margin, y: y))
            ctx.strokePath()
        }

        var y: CGFloat = 0
        var pageIdx = 0
        var rowIdx = 0

        func newPage() {
            ctx.beginPDFPage(nil)
            pageIdx += 1
            y = pageH - margin

            if pageIdx == 1 {
                // Title
                drawStr("Drive Vault — Activity Log", font: titleFont, x: margin, y: y - 20, width: pageW - margin * 2)
                y -= 26
                drawStr(subtitle, font: subFont, color: grayColor, x: margin, y: y - 14, width: pageW - margin * 2)
                y -= 22
                hline(y: y)
                y -= 4
            }

            // Column headers
            ctx.setFillColor(hdrBg)
            ctx.fill(CGRect(x: margin, y: y - headerH, width: pageW - margin * 2, height: headerH))
            var x = margin + 4
            for (i, h) in ["DATE", "TIME", "EVENT", "DETAIL"].enumerated() {
                drawStr(h, font: hdrFont, color: grayColor, x: x, y: y - headerH + 5, width: colW[i])
                x += colW[i]
            }
            y -= headerH
            hline(y: y)
            rowIdx = 0
        }

        newPage()

        for row in events {
            if y - rowH < margin + 20 { ctx.endPDFPage(); newPage() }

            if rowIdx % 2 == 1 {
                ctx.setFillColor(altBg)
                ctx.fill(CGRect(x: margin, y: y - rowH, width: pageW - margin * 2, height: rowH))
            }

            var x = margin + 4
            let cols = [row.date, row.time, row.event, row.detail]
            for (i, col) in cols.enumerated() {
                drawStr(col, font: cellFont, color: i == 2 ? darkColor : (i == 3 ? grayColor : darkColor),
                        x: x, y: y - rowH + 5, width: colW[i])
                x += colW[i]
            }
            hline(y: y - rowH)
            y -= rowH
            rowIdx += 1
        }

        // Footer
        drawStr("Drive Vault  ·  Page \(pageIdx)", font: subFont, color: grayColor,
                x: margin, y: margin - 16, width: pageW - margin * 2)
        ctx.endPDFPage()
        ctx.closePDF()
        return pdfData as Data
    }
}