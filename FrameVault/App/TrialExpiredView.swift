import SwiftUI

struct TrialExpiredView: View {
    var body: some View {
        VStack(spacing: 24) {
            Spacer()

            Image(systemName: "lock.circle.fill")
                .font(.system(size: 64))
                .foregroundStyle(.blue)

            VStack(spacing: 8) {
                Text("Your trial has ended")
                    .font(.system(size: 24, weight: .semibold))
                Text("Thank you for trying Drive Vault.\nPurchase a license to continue using the app.")
                    .font(.system(size: 15))
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }

            VStack(spacing: 12) {
                Button {
                    // Replace with your actual purchase URL
                    NSWorkspace.shared.open(URL(string: "https://drivevault.app/buy")!)
                } label: {
                    Text("Purchase Drive Vault")
                        .font(.system(size: 15, weight: .medium))
                        .frame(width: 240)
                        .padding(.vertical, 12)
                        .background(.blue)
                        .foregroundStyle(.white)
                        .clipShape(RoundedRectangle(cornerRadius: 10))
                }
                .buttonStyle(.plain)

                Button {
                    NSWorkspace.shared.open(URL(string: "mailto:support@drivevault.app")!)
                } label: {
                    Text("Contact support")
                        .font(.system(size: 13))
                        .foregroundStyle(.secondary)
                }
                .buttonStyle(.plain)
            }

            Spacer()

            Text("Drive Vault v1.0")
                .font(.caption)
                .foregroundStyle(.tertiary)
                .padding(.bottom, 20)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(.background)
    }
}
