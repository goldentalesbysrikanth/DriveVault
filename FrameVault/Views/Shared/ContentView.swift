import SwiftUI

enum SidebarItem: String, CaseIterable, Hashable {
    case overview  = "Overview"
    case drives    = "Drives"
    case library   = "Library"
    case clients   = "Clients"
    case activity  = "Activity Log"
    case settings  = "Settings"

    var icon: String {
        switch self {
        case .overview:  return "square.grid.2x2"
        case .drives:    return "externaldrive"
        case .library:   return "photo.on.rectangle.angled"
        case .clients:   return "person.2"
        case .activity:  return "clock.arrow.trianglehead.counterclockwise.rotate.90"
        case .settings:  return "gearshape"
        }
    }
}

struct ContentView: View {
    @EnvironmentObject var store: AppStore
    @AppStorage("lastSidebarSelection") private var lastSelection: String = SidebarItem.overview.rawValue
    @State private var selection: SidebarItem = .overview

    var body: some View {
        NavigationSplitView {
            SidebarView(selection: $selection)
                .navigationSplitViewColumnWidth(min: 180, ideal: 200)
        } detail: {
            switch selection {
            case .overview:  OverviewView(selection: $selection)
            case .drives:    DrivesView()
            case .library:   LibraryView()
            case .clients:   ClientsView()
            case .activity:  PasscodeGate(actionTitle: "Activity Log") { ActivityLogView() }
            case .settings:  SettingsView()
            }
        }
        .navigationSplitViewStyle(.balanced)
        .onAppear {
            if let restored = SidebarItem(rawValue: lastSelection) {
                selection = restored
            }
        }
        .onChange(of: selection) { newValue in
            lastSelection = newValue.rawValue
        }
        .alert("Index \(store.pendingIndexURL?.lastPathComponent ?? "drive")?",
               isPresented: $store.showIndexPrompt) {
            Button("Index Now") { store.confirmIndexPrompt() }
            Button("Skip", role: .cancel) { store.declineIndexPrompt() }
        } message: {
            Text("Drive Vault detected a new drive. Would you like to index it now for offline browsing?")
        }
    }
}
