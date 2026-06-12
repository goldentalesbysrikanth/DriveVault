import Foundation
import ServiceManagement

/// Manages launch at login using the modern ServiceManagement API (macOS 13+)
final class LaunchAtLoginManager {

    static let shared = LaunchAtLoginManager()
    private init() {}

    var isEnabled: Bool {
        get {
            if #available(macOS 13.0, *) {
                return SMAppService.mainApp.status == .enabled
            }
            return UserDefaults.standard.bool(forKey: "fv.launchAtLogin")
        }
        set {
            if #available(macOS 13.0, *) {
                do {
                    if newValue {
                        try SMAppService.mainApp.register()
                    } else {
                        try SMAppService.mainApp.unregister()
                    }
                } catch {
                    print("LaunchAtLogin error: \(error)")
                }
            }
            UserDefaults.standard.set(newValue, forKey: "fv.launchAtLogin")
        }
    }
}
