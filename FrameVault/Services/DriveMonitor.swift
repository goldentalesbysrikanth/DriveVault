import Foundation
import DiskArbitration
import Combine
import os.log

final class DriveMonitor {

    private let driveConnectedSubject    = PassthroughSubject<URL, Never>()
    private let driveDisconnectedSubject = PassthroughSubject<String, Never>()

    var driveConnected: AnyPublisher<URL, Never>       { driveConnectedSubject.eraseToAnyPublisher() }
    var driveDisconnected: AnyPublisher<String, Never> { driveDisconnectedSubject.eraseToAnyPublisher() }

    private var session: DASession?
    private var autoScanTimer: Timer?
    private var lastKnownVolumes = Set<String>()
    private let log = Logger(subsystem: "com.drivevault", category: "DriveMonitor")

    private let systemVolumeNames: Set<String> = [
        "Macintosh HD", "Data", "Preboot", "Recovery",
        "VM", "Update", "com.apple.os.update",
        "Hardware", "iSCPreboot", "mnt1", "xarts", "home"
    ]

    // MARK: - Lifecycle

    func start() {
        print("🟢 DriveMonitor.start() called")

        // Initial scan
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) {
            self.scanMountedVolumes()
        }

        // Auto-scan every 5 seconds as backup — catches drives DA misses
        autoScanTimer = Timer.scheduledTimer(withTimeInterval: 5.0, repeats: true) { [weak self] _ in
            self?.scanMountedVolumes()
        }

        // DA session for real-time detection
        guard let session = DASessionCreate(kCFAllocatorDefault) else {
            print("❌ Failed to create DiskArbitration session")
            return
        }
        self.session = session
        DASessionSetDispatchQueue(session, DispatchQueue.main)

        DARegisterDiskAppearedCallback(session, nil, { disk, ctx in
            guard let ctx else { return }
            let monitor = Unmanaged<DriveMonitor>.fromOpaque(ctx).takeUnretainedValue()
            monitor.handleAppear(disk: disk)
        }, Unmanaged.passUnretained(self).toOpaque())

        DARegisterDiskDisappearedCallback(session, nil, { disk, ctx in
            guard let ctx else { return }
            let monitor = Unmanaged<DriveMonitor>.fromOpaque(ctx).takeUnretainedValue()
            monitor.handleDisappear(disk: disk)
        }, Unmanaged.passUnretained(self).toOpaque())

        print("✅ DriveMonitor ready — DA + auto-scan active")
    }

    func stop() {
        autoScanTimer?.invalidate()
        autoScanTimer = nil
        if let session {
            DASessionSetDispatchQueue(session, nil)
        }
        session = nil
    }

    // MARK: - Auto scan (backup mechanism)
    // Runs every 5s — detects drives that DA callbacks miss

    private func scanMountedVolumes() {
        guard let mounts = FileManager.default.mountedVolumeURLs(
            includingResourceValuesForKeys: nil,
            options: .skipHiddenVolumes
        ) else { return }

        let currentVolumes = Set(mounts.map { $0.lastPathComponent })

        // Detect new volumes
        for url in mounts {
            let name = url.lastPathComponent
            guard shouldIndex(url: url) else { continue }
            guard !lastKnownVolumes.contains(name) else { continue }
            print("📡 Auto-scan detected: \(name)")
            driveConnectedSubject.send(url)
        }

        // Detect removed volumes
        for name in lastKnownVolumes {
            if !currentVolumes.contains(name) {
                print("📡 Auto-scan lost: \(name)")
                driveDisconnectedSubject.send(name)
            }
        }

        lastKnownVolumes = Set(mounts.compactMap { url -> String? in
            let name = url.lastPathComponent
            return shouldIndex(url: url) ? name : nil
        })
    }

    // MARK: - DA Callbacks

    private func handleAppear(disk: DADisk) {
        guard let desc = DADiskCopyDescription(disk) as? [String: Any] else { return }

        // Case 1: volume already fully mounted — DAVolumePath present
        if let volumeURL = desc[kDADiskDescriptionVolumePathKey as String] as? URL {
            let name = volumeURL.lastPathComponent
            guard shouldIndex(url: volumeURL) else { return }
            print("🔌 DA detected: \(name)")
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                self.driveConnectedSubject.send(volumeURL)
            }
            return
        }

        // Case 2: volume still mounting — DAVolumePath missing, use DAVolumeName
        guard let volumeName = desc[kDADiskDescriptionVolumeNameKey as String] as? String,
              !volumeName.isEmpty,
              shouldIndexByName(volumeName) else { return }

        print("🔌 DA detected (mounting): \(volumeName) — waiting 2.5s")
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) {
            guard let url = DriveMonitor.mountedURL(for: volumeName) else {
                print("⚠️ \(volumeName) not found after delay — auto-scan will catch it")
                return
            }
            guard self.shouldIndex(url: url) else { return }
            print("✅ DA delayed: \(volumeName)")
            self.driveConnectedSubject.send(url)
        }
    }

    private func handleDisappear(disk: DADisk) {
        guard let desc = DADiskCopyDescription(disk) as? [String: Any] else { return }
        let name = (desc[kDADiskDescriptionVolumeNameKey as String] as? String)
            ?? (desc[kDADiskDescriptionMediaBSDNameKey as String] as? String)
            ?? "unknown"
        guard shouldIndexByName(name) else { return }
        print("🔌 DA lost: \(name)")
        driveDisconnectedSubject.send(name)
    }

    // MARK: - Filtering

    private func shouldIndex(url: URL) -> Bool {
        let name = url.lastPathComponent
        return shouldIndexByName(name)
    }

    private func shouldIndexByName(_ name: String) -> Bool {
        if systemVolumeNames.contains(name) { return false }
        if name.hasPrefix("com.apple") { return false }
        if name.hasPrefix(".") { return false }
        if name == "/" || name.isEmpty { return false }
        return true
    }

    // MARK: - Static helpers

    static func mountedURL(for volumeName: String) -> URL? {
        FileManager.default.mountedVolumeURLs(
            includingResourceValuesForKeys: nil,
            options: .skipHiddenVolumes
        )?.first { $0.lastPathComponent == volumeName }
    }

    static func driveInfo(for volumeURL: URL) -> (connectionType: String?, driveType: String?) {
        guard let session = DASessionCreate(kCFAllocatorDefault),
              let disk = DADiskCreateFromVolumePath(kCFAllocatorDefault, session, volumeURL as CFURL),
              let desc = DADiskCopyDescription(disk) as? [String: Any] else {
            return (connectionType: "USB", driveType: "HDD")
        }
        let bus = desc[kDADiskDescriptionDeviceProtocolKey as String] as? String ?? "USB"
        let isRemovable = desc[kDADiskDescriptionMediaRemovableKey as String] as? Bool ?? false
        let driveType = isRemovable ? "Flash" : "HDD"
        return (connectionType: bus, driveType: driveType)
    }
}
