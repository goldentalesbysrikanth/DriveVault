import Foundation

/// Represents a point-in-time backup of drivevault.sqlite
struct DatabaseSnapshot: Identifiable {
    let id = UUID()
    let url: URL
    let createdAt: Date

    var displayName: String {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        return f.string(from: createdAt)
    }
}
