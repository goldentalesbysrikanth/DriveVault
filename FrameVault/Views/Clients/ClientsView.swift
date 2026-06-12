import SwiftUI

struct ClientsView: View {
    @EnvironmentObject var store: AppStore
    @State private var searchText = ""
    @State private var expandedClientKey: String? = nil
    @State private var expandedShootID: Int64? = nil

    var body: some View {
        ScrollView {
            LazyVStack(spacing: 0) {
                ForEach(filteredGroups) { group in
                    ClientGroupRow(
                        group: group,
                        drives: store.drives,
                        isExpanded: expandedClientKey == group.key,
                        expandedShootID: $expandedShootID,
                        onToggleClient: {
                            withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) {
                                expandedClientKey = expandedClientKey == group.key ? nil : group.key
                                expandedShootID = nil
                            }
                        }
                    )
                    Divider()
                }
            }
            .background(.background)
            .clipShape(RoundedRectangle(cornerRadius: 10))
            .overlay(RoundedRectangle(cornerRadius: 10).stroke(.separator, lineWidth: 0.5))
            .padding(16)
        }
        .navigationTitle("Clients")
        .searchable(text: $searchText, prompt: "Search clients or shoots…")
        .overlay {
            if filteredGroups.isEmpty {
                ContentUnavailableView(
                    "No clients yet",
                    systemImage: "person.2",
                    description: Text(store.drives.isEmpty
                        ? "Connect a drive to auto-discover shoots"
                        : "No shoots found for connected drives")
                )
            }
        }
    }

    private var filteredGroups: [ClientGroup] {
        guard !searchText.isEmpty else { return store.clientGroups }
        let q = searchText.lowercased()
        return store.clientGroups.compactMap { group in
            let matchesGroup = group.displayName.lowercased().contains(q)
            let matchedShoots = group.shoots.filter { $0.displayName.lowercased().contains(q) }
            if matchesGroup { return group }
            if !matchedShoots.isEmpty { return ClientGroup(key: group.key, shoots: matchedShoots) }
            return nil
        }
    }
}

// MARK: - Client group row

struct ClientGroupRow: View {
    let group: ClientGroup
    let drives: [Drive]
    let isExpanded: Bool
    @Binding var expandedShootID: Int64?
    let onToggleClient: () -> Void
    @EnvironmentObject var store: AppStore

    var body: some View {
        VStack(spacing: 0) {
            Button(action: onToggleClient) {
                HStack(spacing: 12) {
                    ZStack {
                        Circle().fill(.purple.opacity(0.12)).frame(width: 36, height: 36)
                        Text(group.initials)
                            .font(.system(size: 12, weight: .medium))
                            .foregroundStyle(.purple)
                    }

                    VStack(alignment: .leading, spacing: 2) {
                        Text(group.displayName)
                            .font(.system(size: 13, weight: .medium))
                        Text("\(group.shoots.count) shoot\(group.shoots.count != 1 ? "s" : "") · \(group.formattedTotalSize)")
                            .font(.system(size: 12))
                            .foregroundStyle(.secondary)
                    }

                    Spacer()

                    LazyHStack(spacing: 6) {
                        ForEach(group.uniqueDriveIDs, id: \.self) { driveID in
                            if let drive = drives.first(where: { $0.id == driveID }) {
                                Label(drive.name, systemImage: "externaldrive")
                                    .font(.system(size: 11))
                                    .padding(.horizontal, 7).padding(.vertical, 2)
                                    .background(.secondary.opacity(0.1))
                                    .clipShape(Capsule())
                            }
                        }
                    }

                    Image(systemName: "chevron.right")
                        .font(.system(size: 13)).foregroundStyle(.tertiary)
                        .rotationEffect(.degrees(isExpanded ? 90 : 0))
                        .animation(.spring(response: 0.3, dampingFraction: 0.8), value: isExpanded)
                }
                .padding(.horizontal, 16).padding(.vertical, 11).contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .background(isExpanded ? Color(.systemGray).opacity(0.04) : .clear)

            if isExpanded {
                VStack(spacing: 0) {
                    Divider()
                    ForEach(group.shoots) { shoot in
                        let shootFolders = store.folders(for: shoot)
                        let drive = drives.first { $0.id == shoot.driveID }
                        ClientShootRow(
                            shoot: shoot,
                            drive: drive,
                            folders: shootFolders,
                            isExpanded: expandedShootID == shoot.id,
                            onToggle: {
                                withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) {
                                    expandedShootID = expandedShootID == shoot.id ? nil : shoot.id
                                }
                            }
                        )
                        if shoot.id != group.shoots.last?.id {
                            Divider().padding(.leading, 62)
                        }
                    }
                }
                .transition(.opacity.combined(with: .move(edge: .top)))
            }
        }
    }
}

// MARK: - Client shoot row

struct ClientShootRow: View {
    let shoot: Shoot
    let drive: Drive?
    let folders: [DriveFolder]
    let isExpanded: Bool
    let onToggle: () -> Void

    // Only depth-0 for file count display
    private var rootFolders: [DriveFolder] {
        folders.filter { $0.depth == 0 }.sorted { $0.sizeBytes > $1.sizeBytes }
    }

    private var totalFileCount: Int64 {
        rootFolders.reduce(0) { $0 + $1.fileCount }
    }

    var body: some View {
        VStack(spacing: 0) {
            Button(action: onToggle) {
                HStack(spacing: 12) {
                    Color.clear.frame(width: 46)

                    Image(systemName: "camera")
                        .font(.system(size: 14)).foregroundStyle(.secondary)

                    Text(shoot.displayName)
                        .font(.system(size: 13, weight: .medium)).lineLimit(1)

                    Spacer()

                    if let drive {
                        Label(drive.name, systemImage: "externaldrive")
                            .font(.system(size: 11))
                            .padding(.horizontal, 7).padding(.vertical, 2)
                            .background(.secondary.opacity(0.1))
                            .clipShape(Capsule())
                    }

                    if totalFileCount > 0 {
                        Text("\(totalFileCount) files")
                            .font(.system(size: 11)).foregroundStyle(.tertiary)
                    }

                    Text(shoot.formattedSize)
                        .font(.system(size: 12)).foregroundStyle(.secondary)
                        .frame(minWidth: 60, alignment: .trailing)

                    Text(shoot.createdAt.formatted(date: .abbreviated, time: .omitted))
                        .font(.system(size: 11)).foregroundStyle(.tertiary)
                        .frame(minWidth: 88, alignment: .trailing)

                    Image(systemName: "chevron.right")
                        .font(.system(size: 12)).foregroundStyle(.tertiary)
                        .rotationEffect(.degrees(isExpanded ? 90 : 0))
                        .animation(.spring(response: 0.3, dampingFraction: 0.8), value: isExpanded)
                }
                .padding(.horizontal, 16).padding(.vertical, 9).contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .background(isExpanded ? Color(.systemGray).opacity(0.05) : .clear)

            if isExpanded {
                VStack(spacing: 0) {
                    Divider()
                    // Single-level flat display for clients — no nested tree
                    ForEach(rootFolders) { folder in
                        ClientFolderRow(folder: folder, shootTotalBytes: shoot.totalBytes)
                        if folder.id != rootFolders.last?.id {
                            Divider().padding(.leading, 80)
                        }
                    }
                    if rootFolders.isEmpty {
                        Text("No subfolders found")
                            .font(.callout).foregroundStyle(.tertiary).padding()
                    }
                    HStack(spacing: 6) {
                        Image(systemName: "checkmark.icloud")
                            .font(.system(size: 12)).foregroundStyle(.tertiary)
                        Text("Auto-scanned · available offline")
                            .font(.system(size: 11)).foregroundStyle(.tertiary)
                    }
                    .padding(.horizontal, 80).padding(.vertical, 7)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(Color(.systemGray).opacity(0.03))
                }
                .transition(.opacity.combined(with: .slide))
            }
        }
    }
}

// MARK: - Client folder row (flat single-level, no nesting)

struct ClientFolderRow: View {
    let folder: DriveFolder
    let shootTotalBytes: Int64

    private var fraction: Double {
        shootTotalBytes > 0 ? Double(folder.sizeBytes) / Double(shootTotalBytes) : 0
    }

    var body: some View {
        HStack(spacing: 8) {
            Color.clear.frame(width: 80)
            Image(systemName: "folder").font(.system(size: 13)).foregroundStyle(.orange)
            VStack(alignment: .leading, spacing: 2) {
                Text(folder.name).font(.system(size: 13)).lineLimit(1)
                if let types = folder.fileTypes, !types.isEmpty {
                    Text(types).font(.system(size: 11)).foregroundStyle(.secondary)
                }
            }
            Spacer()
            if folder.fileCount > 0 {
                Text(folder.formattedFileCount)
                    .font(.system(size: 10))
                    .padding(.horizontal, 6).padding(.vertical, 2)
                    .background(Color.secondary.opacity(0.1))
                    .foregroundStyle(.secondary).clipShape(Capsule())
            }
            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule().fill(.secondary.opacity(0.12)).frame(height: 3)
                    Capsule().fill(.purple.opacity(0.5))
                        .frame(width: geo.size.width * fraction, height: 3)
                }
            }
            .frame(width: 60, height: 3)
            Text(folder.formattedSize)
                .font(.system(size: 11)).foregroundStyle(.secondary)
                .frame(minWidth: 52, alignment: .trailing)
        }
        .padding(.horizontal, 16).padding(.vertical, 7)
    }
}
