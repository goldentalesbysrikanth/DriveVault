import Foundation
import AppKit

/// Manages security-scoped bookmarks for external drives.
/// Required under App Sandbox to read external volume contents.
final class DriveAccessManager {

    private let defaults = UserDefaults.standard
    private let bookmarkKeyPrefix = "fv.bookmark."

    // ── Request access ─────────────────────────────────────────────────

    /// Asks the user to grant access to a volume via NSOpenPanel.
    /// Returns the URL with access granted, or nil if cancelled.
    @MainActor
    func requestAccess(for volumeURL: URL) async -> URL? {
        let panel = NSOpenPanel()
        panel.message = "Drive Vault needs access to \"\(volumeURL.lastPathComponent)\" to index your shoots."
        panel.prompt = "Grant Access"
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.canCreateDirectories = false
        panel.allowsMultipleSelection = false
        panel.directoryURL = volumeURL

        let result = await panel.beginSheetModal(for: NSApp.keyWindow ?? NSApp.windows.first ?? NSWindow())
        guard result == .OK, let url = panel.url else { return nil }

        // Save bookmark so we never need to ask again
        saveBookmark(for: url)
        return url
    }

    // ── Bookmark management ────────────────────────────────────────────

    func saveBookmark(for url: URL) {
        do {
            let bookmark = try url.bookmarkData(
                options: .withSecurityScope,
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
            defaults.set(bookmark, forKey: bookmarkKeyPrefix + url.lastPathComponent)
        } catch {
            
        }
    }

    /// Resolves a saved bookmark and starts accessing the security-scoped resource.
    /// Returns the resolved URL if successful.
    func resolveBookmark(for volumeName: String) -> URL? {
        guard let data = defaults.data(forKey: bookmarkKeyPrefix + volumeName) else { return nil }
        var isStale = false
        do {
            let url = try URL(
                resolvingBookmarkData: data,
                options: .withSecurityScope,
                relativeTo: nil,
                bookmarkDataIsStale: &isStale
            )
            if isStale { saveBookmark(for: url) }
            url.startAccessingSecurityScopedResource()
            return url
        } catch {
           
            return nil
        }
    }

    func hasBookmark(for volumeName: String) -> Bool {
        defaults.data(forKey: bookmarkKeyPrefix + volumeName) != nil
    }

    func stopAccessing(_ url: URL) {
        url.stopAccessingSecurityScopedResource()
    }
}
