import Combine
import Foundation
import SwiftUI

@MainActor
private final class TukiNavigationEnhancementsModel: ObservableObject {
    @Published var snapshot: TukiNavigationSnapshot?

    private let baseURL: URL?
    private let credentialStore = KeychainTukiCredentialStore()
    private let session: URLSession
    private let decoder = JSONDecoder()
    private var lastSessionId: String?
    private var acknowledgedSessionId: String?

    init(session: URLSession = .shared) {
        self.session = session
        self.baseURL = try? AppConfiguration.load().backendBaseURL
    }

    var followingText: String? {
        guard let snapshot,
              snapshot.state.caseInsensitiveCompare("Arrived") != .orderedSame,
              snapshot.state.caseInsensitiveCompare("Cancelled") != .orderedSame else {
            return nil
        }
        return snapshot.followingDisplayInstruction
    }

    var arrivedSummary: TukiNavigationTripSummary? {
        guard let snapshot,
              snapshot.state.caseInsensitiveCompare("Arrived") == .orderedSame,
              snapshot.sessionId != acknowledgedSessionId else {
            return nil
        }
        return snapshot.tripSummary
    }

    func monitor() async {
        while !Task.isCancelled {
            await refresh()
            try? await Task.sleep(for: .seconds(3))
        }
    }

    func acknowledgeArrival() {
        acknowledgedSessionId = snapshot?.sessionId
        lastSessionId = nil
        snapshot = nil
        NotificationCenter.default.post(name: .tukiTripEnded, object: nil)
    }

    private func refresh() async {
        guard credentialStore.credential != nil else {
            snapshot = nil
            lastSessionId = nil
            return
        }

        if let active = await fetch(path: "api/navigation/active") {
            lastSessionId = active.sessionId
            snapshot = active
            return
        }

        guard let sessionId = lastSessionId,
              sessionId != acknowledgedSessionId,
              let completed = await fetch(path: "api/navigation/\(sessionId)") else {
            return
        }

        if completed.state.caseInsensitiveCompare("Arrived") == .orderedSame {
            snapshot = completed
        } else if completed.state.caseInsensitiveCompare("Cancelled") == .orderedSame {
            snapshot = nil
            lastSessionId = nil
        }
    }

    private func fetch(path: String) async -> TukiNavigationSnapshot? {
        guard let baseURL, let credential = credentialStore.credential else { return nil }
        var request = URLRequest(url: baseURL.appendingPathComponent(path))
        request.httpMethod = "GET"
        request.timeoutInterval = 15
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)

        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse,
                  (200..<300).contains(http.statusCode) else {
                return nil
            }
            return try decoder.decode(TukiNavigationSnapshot.self, from: data)
        } catch {
            return nil
        }
    }
}

struct TukiNavigationEnhancementsOverlay: View {
    @StateObject private var model = TukiNavigationEnhancementsModel()

    var body: some View {
        Group {
            if let followingText = model.followingText {
                VStack(alignment: .leading, spacing: 4) {
                    Text("THEN")
                        .font(.system(size: 10, weight: .heavy))
                        .foregroundStyle(TukiPalette.gray)
                    Text(followingText)
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(TukiPalette.dark.opacity(0.78))
                        .lineLimit(2)
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .frame(maxWidth: 300, alignment: .leading)
                .background(.regularMaterial)
                .clipShape(RoundedRectangle(cornerRadius: 14))
                .shadow(radius: 3, y: 1)
                .allowsHitTesting(false)
            }
        }
        .task { await model.monitor() }
        .sheet(
            isPresented: Binding(
                get: { model.arrivedSummary != nil },
                set: { _ in }
            )
        ) {
            if let summary = model.arrivedSummary {
                TukiArrivalSummaryView(summary: summary) {
                    model.acknowledgeArrival()
                }
                .interactiveDismissDisabled()
            }
        }
    }
}

private struct TukiArrivalSummaryView: View {
    let summary: TukiNavigationTripSummary
    let onDone: () -> Void

    var body: some View {
        VStack(spacing: 20) {
            Spacer(minLength: 8)
            Text("🎉")
                .font(.system(size: 52))
            Text("You have arrived!")
                .font(.system(size: 27, weight: .heavy))
                .foregroundStyle(TukiPalette.dark)
            Text(summary.destinationName)
                .font(.system(size: 17, weight: .bold))
                .foregroundStyle(TukiPalette.teal)
                .multilineTextAlignment(.center)

            VStack(spacing: 12) {
                if let duration = summary.durationMinutes {
                    summaryRow("Travel time", "\(duration) min")
                }
                summaryRow("Approx. fare spent", peso(summary.approxFareSpent))
                summaryRow("Transit legs", "\(summary.transitLegs)")
                summaryRow("Transfers", "\(summary.transfers)")
            }
            .padding(18)
            .background(TukiPalette.creamCard)
            .clipShape(RoundedRectangle(cornerRadius: 18))

            Button(action: onDone) {
                Text("Done")
                    .font(.system(size: 17, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity)
                    .frame(height: 52)
                    .background(TukiPalette.teal)
                    .clipShape(RoundedRectangle(cornerRadius: 16))
            }
            .buttonStyle(.plain)
            Spacer()
        }
        .padding(28)
        .presentationDetents([.medium])
        .background(TukiPalette.cream)
    }

    private func summaryRow(_ label: String, _ value: String) -> some View {
        HStack {
            Text(label).foregroundStyle(TukiPalette.gray).fontWeight(.semibold)
            Spacer()
            Text(value).foregroundStyle(TukiPalette.dark).fontWeight(.heavy)
        }
    }

    private func peso(_ value: Double) -> String {
        let rounded = value.rounded()
        return abs(value - rounded) < 0.005 ? "₱\(Int(rounded))" : String(format: "₱%.2f", value)
    }
}
