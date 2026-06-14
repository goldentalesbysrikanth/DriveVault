import SwiftUI

struct SidebarView: View {
    @EnvironmentObject var store: AppStore
    @Binding var selection: SidebarItem

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // Logo
            HStack(spacing: 8) {
                Image("AppIconImage")
                    .resizable()
                    .frame(width: 24, height: 24)
                    .clipShape(RoundedRectangle(cornerRadius: 5))
                VStack(alignment: .leading, spacing: 1) {
                    Text("Drive Vault")
                        .font(.system(size: 15, weight: .medium))
                    Text("v1.3.0 — Test Version")
                        .font(.system(size: 10))
                        .foregroundStyle(.tertiary)
                }
            }
            .padding(.horizontal, 16)
            .padding(.top, 16)
            .padding(.bottom, 20)

            sectionLabel("Library")
            navButton(.overview)
            navButton(.drives)
            navButton(.library)
            navButton(.clients)
            navButton(.activity)

            Spacer().frame(height: 16)

            sectionLabel("Preferences")
            navButton(.settings)

            Spacer()

            if let driveID = store.indexingState.driveID {
                indexingBanner(for: driveID)
            }
        }
        .frame(minWidth: 180, idealWidth: 200, maxHeight: .infinity, alignment: .topLeading)
        .background(Color(NSColor.controlBackgroundColor))
    }

    private func sectionLabel(_ title: String) -> some View {
        Text(title)
            .font(.system(size: 11, weight: .medium))
            .foregroundStyle(.secondary)
            .textCase(.uppercase)
            .tracking(0.5)
            .padding(.horizontal, 16)
            .padding(.bottom, 4)
    }

    private func navButton(_ item: SidebarItem) -> some View {
        Button {
            selection = item
        } label: {
            HStack(spacing: 8) {
                Image(systemName: item.icon)
                    .font(.system(size: 14))
                    .frame(width: 20)
                Text(item.rawValue)
                    .font(.system(size: 13))
                Spacer()
                if item == .overview && store.alerts.count > 0 {
                    Text("\(store.alerts.count)")
                        .font(.system(size: 11, weight: .medium))
                        .padding(.horizontal, 6)
                        .padding(.vertical, 2)
                        .background(Color.red.opacity(0.85))
                        .foregroundStyle(.white)
                        .clipShape(Capsule())
                        .accessibilityLabel("\(store.alerts.count) alerts")
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 7)
            .background(selection == item ? Color.accentColor.opacity(0.15) : Color.clear)
            .foregroundStyle(selection == item ? Color.accentColor : Color.primary)
            .clipShape(RoundedRectangle(cornerRadius: 6))
            .padding(.horizontal, 8)
            .animation(.easeInOut, value: selection)
        }
        .buttonStyle(.plain)
        .accessibilityLabel("Navigate to \(item.rawValue)")
    }

    private func indexingBanner(for driveID: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 8) {
                ProgressView().scaleEffect(0.7)
                Text("Indexing \(driveID)…")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Spacer()
                if store.indexingState.progress > 0 {
                    Text("\(Int(store.indexingState.progress * 100))%")
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(.secondary)
                }
            }
            if store.indexingState.progress > 0 {
                GeometryReader { geo in
                    ZStack(alignment: .leading) {
                        RoundedRectangle(cornerRadius: 2)
                            .fill(Color.secondary.opacity(0.2))
                            .frame(height: 3)
                        RoundedRectangle(cornerRadius: 2)
                            .fill(Color.accentColor)
                            .frame(width: geo.size.width * store.indexingState.progress, height: 3)
                    }
                }
                .frame(height: 3)
                Text(store.indexingState.progressText)
                    .font(.system(size: 10))
                    .foregroundStyle(.tertiary)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, 16)
        .padding(.vertical, 8)
        .background(.ultraThinMaterial)
        .clipShape(RoundedRectangle(cornerRadius: 6))
        .padding(.horizontal, 8)
    }
}
