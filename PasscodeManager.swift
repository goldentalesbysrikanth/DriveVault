import SwiftUI
import Combine
import LocalAuthentication
import Security

// MARK: - PasscodeManager

/// Manages passcode storage in Keychain, biometric auth, and app lock state.
@MainActor
final class PasscodeManager: ObservableObject {

    static let shared = PasscodeManager()

    // MARK: Published State

    @Published var isLocked: Bool = false
    @Published var isPasscodeEnabled: Bool = false
    @Published var isBiometricsEnabled: Bool = false
    @Published var passcodeLength: Int = 6  // 4 or 6

    // MARK: Keys

    private let keychainService = "app.drivevault.passcode"
    private let keychainAccount = "passcode"

    private let enabledKey       = "fv.passcodeEnabled"
    private let biometricsKey    = "fv.biometricsEnabled"
    private let lengthKey        = "fv.passcodeLength"
    private let appLockKey       = "fv.appLockEnabled"

    // MARK: Init

    private init() {
        isPasscodeEnabled  = UserDefaults.standard.bool(forKey: enabledKey)
        isBiometricsEnabled = UserDefaults.standard.bool(forKey: biometricsKey)
        passcodeLength     = UserDefaults.standard.integer(forKey: lengthKey)
        if passcodeLength == 0 { passcodeLength = 6 }
        // Lock on launch if passcode is set
        if isPasscodeEnabled {
            isLocked = true
        }
    }

    // MARK: Passcode Setup

    func setPasscode(_ code: String) {
        saveToKeychain(code)
        isPasscodeEnabled = true
        UserDefaults.standard.set(true,          forKey: enabledKey)
        UserDefaults.standard.set(code.count,    forKey: lengthKey)
        passcodeLength = code.count
    }

    func removePasscode() {
        deleteFromKeychain()
        isPasscodeEnabled   = false
        isBiometricsEnabled = false
        isLocked            = false
        UserDefaults.standard.set(false, forKey: enabledKey)
        UserDefaults.standard.set(false, forKey: biometricsKey)
    }

    func enableBiometrics(_ enabled: Bool) {
        isBiometricsEnabled = enabled
        UserDefaults.standard.set(enabled, forKey: biometricsKey)
    }

    // MARK: Verification

    func verify(_ code: String) -> Bool {
        guard let stored = loadFromKeychain() else { return false }
        return code == stored
    }

    func unlock() {
        isLocked = false
    }

    func lock() {
        if isPasscodeEnabled {
            isLocked = true
        }
    }

    // MARK: Biometrics

    var biometricsAvailable: Bool {
        let ctx = LAContext()
        var error: NSError?
        return ctx.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &error)
    }

    var biometricType: String {
        let ctx = LAContext()
        _ = ctx.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: nil)
        switch ctx.biometryType {
        case .touchID:  return "Touch ID"
        case .faceID:   return "Face ID"
        default:        return "Biometrics"
        }
    }

    func authenticateWithBiometrics(reason: String, completion: @escaping (Bool) -> Void) {
        guard isBiometricsEnabled && biometricsAvailable else {
            completion(false)
            return
        }
        let ctx = LAContext()
        ctx.evaluatePolicy(
            .deviceOwnerAuthenticationWithBiometrics,
            localizedReason: reason
        ) { success, _ in
            DispatchQueue.main.async {
                completion(success)
            }
        }
    }

    // MARK: Keychain

    private func saveToKeychain(_ code: String) {
        let data = code.data(using: .utf8)!
        let query: [String: Any] = [
            kSecClass as String:       kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: keychainAccount,
            kSecValueData as String:   data
        ]
        SecItemDelete(query as CFDictionary)
        SecItemAdd(query as CFDictionary, nil)
    }

    private func loadFromKeychain() -> String? {
        let query: [String: Any] = [
            kSecClass as String:       kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: keychainAccount,
            kSecReturnData as String:  true,
            kSecMatchLimit as String:  kSecMatchLimitOne
        ]
        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        guard status == errSecSuccess,
              let data = result as? Data,
              let code = String(data: data, encoding: .utf8) else { return nil }
        return code
    }

    private func deleteFromKeychain() {
        let query: [String: Any] = [
            kSecClass as String:       kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: keychainAccount
        ]
        SecItemDelete(query as CFDictionary)
    }
}
