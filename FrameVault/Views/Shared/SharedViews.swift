import SwiftUI

// ── FolderRow ──────────────────────────────────────────────────────────
// Shared component used in DrivesView, OverviewView (SearchShootDetailSheet)

struct FolderRow: View {
    let folder: DriveFolder
    let totalBytes: Int64

    private var fraction: Double {
        totalBytes > 0 ? Double(folder.sizeBytes) / Double(totalBytes) : 0
    }

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "folder")
                .font(.system(size: 15))
                .foregroundStyle(Color.orange)

            Text(folder.name)
                .font(.system(size: 13))
                .lineLimit(1)

            Spacer()

            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule().fill(.secondary.opacity(0.15)).frame(height: 3)
                    Capsule().fill(.purple.opacity(0.6))
                        .frame(width: geo.size.width * fraction, height: 3)
                }
                .frame(height: 3)
            }
            .frame(width: 80, height: 3)

            Text(folder.formattedSize)
                .font(.system(size: 12))
                .foregroundStyle(.secondary)
                .frame(minWidth: 52, alignment: .trailing)

            Text(folder.scannedAt.formatted(date: .abbreviated, time: .omitted))
                .font(.system(size: 11))
                .foregroundStyle(.tertiary)
                .frame(minWidth: 80, alignment: .trailing)
        }
        .padding(.horizontal, 14)
        .padding(.leading, 30)
        .padding(.vertical, 8)
    }
}
