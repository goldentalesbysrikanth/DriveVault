import SwiftUI

struct DriveVaultCommands: Commands {
    var body: some Commands {
        CommandGroup(after: .appInfo) {
            Button("Re-index All Connected Drives") {
                NotificationCenter.default.post(name: .reindexAll, object: nil)
            }
            .keyboardShortcut("r", modifiers: [.command, .shift])
        }
    }
}

extension Notification.Name {
    static let reindexAll = Notification.Name("fv.reindexAll")
}
