import Combine
import Foundation
import SwiftUI

@MainActor
private final class TukiFareTrackingModel: ObservableObject {
    @Published var snapshot: TukiNavigationSnapshot?

    private let api: TukiPlatformAPI?

    init() {
        let store = KeychainTukiCredentialStore()
        if let configuration = try? AppConfiguration.load(), store.credential != nil {
            api = TukiPlatformAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
        } else {
            api = nil
        }
    }

    func monitor() async {
        while !Task.isCancelled {
            await refresh()
            try? await Task.sleep(for: .seconds(5))
        }
    }

    private func refresh() async {
        guard let api else {
            snapshot = nil
            return
        }
        switch await api.activeNavigation() {
        case .success(let value):
            snapshot = value
        case .failure:
            snapshot = nil
        }
    }
}

struct TukiFareTrackingOverlay: View {
    @StateObject private var model = TukiFareTrackingModel()

    var body: some View {
        Group {
            if let snapshot = model.snapshot,
               snapshot.state.caseInsensitiveCompare("Arrived") != .orderedSame,
               snapshot.state.caseInsensitiveCompare("Cancelled") != .orderedSame {
                VStack(alignment: .leading, spacing: 8) {
                    HStack(spacing: 18) {
                        fareValue("Approx. fare spent", snapshot.approxFareSpent)
                        fareValue("Estimated remaining", snapshot.estimatedRemainingFare)
                    }

                    if snapshot.state.caseInsensitiveCompare("ApproachingAlightPoint") == .orderedSame,
                       !snapshot.requiresAlightingConfirmation {
                        Text("Prepare to alight. Confirm Alight unlocks within 75 m of your stop.")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundStyle(TukiPalette.orange)
                    }
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 11)
                .background(.regularMaterial)
                .clipShape(RoundedRectangle(cornerRadius: 14))
                .shadow(radius: 4, y: 2)
                .padding(.top, 112)
                .padding(.leading, 18)
                .frame(maxWidth: 330, alignment: .leading)
            }
        }
        .task { await model.monitor() }
        .allowsHitTesting(false)
    }

    private func fareValue(_ label: String, _ value: Double) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(label)
                .font(.system(size: 10, weight: .bold))
                .foregroundStyle(.secondary)
            Text(peso(value))
                .font(.system(size: 15, weight: .heavy))
                .foregroundStyle(TukiPalette.dark)
        }
    }

    private func peso(_ value: Double) -> String {
        let rounded = value.rounded()
        if abs(value - rounded) < 0.005 {
            return "₱\(Int(rounded))"
        }
        return String(format: "₱%.2f", value)
    }
}
