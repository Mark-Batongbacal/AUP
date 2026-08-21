import Combine
import CoreLocation
import SwiftUI

private struct IOSNavigationRerouteRequest: Encodable {
    let reason: String
    let preference: String?
    let budget: Double?
    let clearBudget: Bool
    let destinationName: String?
    let destinationLatitude: Double?
    let destinationLongitude: Double?
}

private struct IOSNavigationErrorEnvelope: Decodable {
    let error: String?
    let message: String?
}

private final class TukiTripOptionsClient {
    private let baseURL: URL
    private let credentialStore: TukiCredentialStore
    private let session: URLSession
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    init(baseURL: URL, credentialStore: TukiCredentialStore, session: URLSession = .shared) {
        self.baseURL = baseURL
        self.credentialStore = credentialStore
        self.session = session
    }

    func reroute(sessionId: String, request body: IOSNavigationRerouteRequest) async -> Result<TukiNavigationSnapshot, TukiPlatformError> {
        guard let credential = credentialStore.credential else { return .failure(.notAuthenticated) }
        let url = baseURL.appendingPathComponent("api/navigation/\(sessionId)/reroute")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
        do {
            request.httpBody = try encoder.encode(body)
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else {
                return .failure(.message("The server returned an invalid response."))
            }
            guard (200..<300).contains(http.statusCode) else {
                if http.statusCode == 401 { return .failure(.notAuthenticated) }
                if let envelope = try? decoder.decode(IOSNavigationErrorEnvelope.self, from: data),
                   let message = envelope.message ?? envelope.error {
                    return .failure(.message(message.replacingOccurrences(of: "_", with: " ").capitalized))
                }
                return .failure(.message("Reroute failed (HTTP \(http.statusCode))."))
            }
            return .success(try decoder.decode(TukiNavigationSnapshot.self, from: data))
        } catch let error as TukiPlatformError {
            return .failure(error)
        } catch {
            return .failure(.message("Network error. Check your connection and try again."))
        }
    }
}

@MainActor
final class TukiTripOptionsModel: ObservableObject {
    @Published var activeSnapshot: TukiNavigationSnapshot?
    @Published var isWorking = false
    @Published var errorMessage: String?
    @Published var sheetPresented = false

    let location = TukiLocationService()
    private let platform: TukiPlatformAPI?
    private let client: TukiTripOptionsClient?

    init() {
        let store = KeychainTukiCredentialStore()
        if let configuration = try? AppConfiguration.load() {
            platform = TukiPlatformAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
            client = TukiTripOptionsClient(baseURL: configuration.backendBaseURL, credentialStore: store)
        } else {
            platform = nil
            client = nil
        }
    }

    func monitor() async {
        while !Task.isCancelled {
            await refreshActive()
            try? await Task.sleep(for: .seconds(5))
        }
    }

    func refreshActive() async {
        guard !isWorking, let platform else { return }
        switch await platform.activeNavigation() {
        case .success(let snapshot): activeSnapshot = snapshot
        case .failure: activeSnapshot = nil
        }
    }

    func searchDestinations(_ query: String) async -> [TukiPlace] {
        guard let platform else { return [] }
        let coordinate = location.currentLocation?.coordinate
        switch await platform.searchPlaces(query, focusLat: coordinate?.latitude, focusLon: coordinate?.longitude) {
        case .success(let places): return Array(places.prefix(5))
        case .failure(let error): errorMessage = error.message; return []
        }
    }

    func rerouteNow() async { await reroute(reason: "MANUAL") }
    func changePreference(_ preference: String) async { await reroute(reason: "PREFERENCE_CHANGED", preference: preference) }
    func changeBudget(_ budget: Double?, clear: Bool) async { await reroute(reason: "BUDGET_CHANGED", budget: budget, clearBudget: clear) }
    func changeDestination(_ place: TukiPlace) async {
        await reroute(reason: "DESTINATION_CHANGED", destination: place)
    }

    func endTrip() async {
        guard let platform, let sessionId = activeSnapshot?.sessionId else { return }
        isWorking = true
        errorMessage = nil
        switch await platform.cancel(sessionId: sessionId) {
        case .success:
            activeSnapshot = nil
            sheetPresented = false
            NotificationCenter.default.post(name: .tukiTripEnded, object: nil)
        case .failure(let error):
            errorMessage = error.message
        }
        isWorking = false
    }

    private func reroute(
        reason: String,
        preference: String? = nil,
        budget: Double? = nil,
        clearBudget: Bool = false,
        destination: TukiPlace? = nil
    ) async {
        guard let platform, let client, let sessionId = activeSnapshot?.sessionId else { return }
        isWorking = true
        errorMessage = nil

        guard let current = await location.requestCurrentLocation() else {
            errorMessage = location.errorMessage ?? TukiServiceArea.locationFailureMessage
            isWorking = false
            return
        }
        let update = TukiNavigationLocationUpdate(
            latitude: current.coordinate.latitude,
            longitude: current.coordinate.longitude,
            accuracyMeters: max(0, current.horizontalAccuracy),
            timestamp: ISO8601DateFormatter().string(from: current.timestamp),
            speedMetersPerSecond: current.speed >= 0 ? current.speed : nil,
            bearingDegrees: current.course >= 0 ? current.course : nil
        )
        if case .failure(let error) = await platform.updateLocation(sessionId: sessionId, update: update) {
            errorMessage = error.message
            isWorking = false
            return
        }

        let request = IOSNavigationRerouteRequest(
            reason: reason,
            preference: preference,
            budget: budget,
            clearBudget: clearBudget,
            destinationName: destination?.name,
            destinationLatitude: destination?.latitude,
            destinationLongitude: destination?.longitude
        )
        switch await client.reroute(sessionId: sessionId, request: request) {
        case .success(let snapshot):
            activeSnapshot = snapshot
            sheetPresented = false
        case .failure(let error):
            errorMessage = error.message
        }
        isWorking = false
    }
}

struct TukiTripOptionsOverlay: View {
    @StateObject private var model = TukiTripOptionsModel()

    var body: some View {
        ZStack(alignment: .topTrailing) {
            if let snapshot = model.activeSnapshot,
               snapshot.state.caseInsensitiveCompare("Arrived") != .orderedSame,
               snapshot.state.caseInsensitiveCompare("Cancelled") != .orderedSame {
                Button {
                    model.sheetPresented = true
                } label: {
                    Label("Trip options", systemImage: "ellipsis.circle.fill")
                        .font(.system(size: 14, weight: .bold))
                        .foregroundStyle(TukiPalette.dark)
                        .padding(.horizontal, 13)
                        .padding(.vertical, 9)
                        .background(.ultraThinMaterial)
                        .clipShape(Capsule())
                        .shadow(radius: 3, y: 1)
                }
                .buttonStyle(.plain)
                .disabled(model.isWorking)
                .padding(.top, 58)
                .padding(.trailing, 18)
            }
        }
        .task { await model.monitor() }
        .sheet(isPresented: $model.sheetPresented) {
            TukiTripOptionsPanel(model: model)
                .presentationDetents([.medium, .large])
                .presentationDragIndicator(.visible)
        }
    }
}

private enum TukiTripOptionsPage { case menu, preference, budget, destination }

private struct TukiTripOptionsPanel: View {
    @ObservedObject var model: TukiTripOptionsModel
    @Environment(\.dismiss) private var dismiss
    @State private var page: TukiTripOptionsPage = .menu
    @State private var preference = "efficient"
    @State private var budgetText = ""
    @State private var destinationQuery = ""
    @State private var destinationResults: [TukiPlace] = []
    @State private var destinationLoading = false

    var body: some View {
        NavigationStack {
            Group {
                switch page {
                case .menu: menu
                case .preference: preferenceEditor
                case .budget: budgetEditor
                case .destination: destinationEditor
                }
            }
            .padding(22)
            .navigationTitle(page == .menu ? "Trip options" : "")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    if page == .menu { Button("Close") { dismiss() } }
                    else { Button("Back") { page = .menu } }
                }
            }
            .overlay {
                if model.isWorking {
                    ZStack {
                        Color.black.opacity(0.12).ignoresSafeArea()
                        VStack(spacing: 12) {
                            ProgressView()
                            Text("Updating your trip…").fontWeight(.semibold)
                        }
                        .padding(24)
                        .background(.regularMaterial)
                        .clipShape(RoundedRectangle(cornerRadius: 18))
                    }
                }
            }
            .alert("Unable to update trip", isPresented: Binding(
                get: { model.errorMessage != nil },
                set: { if !$0 { model.errorMessage = nil } }
            )) {
                Button("OK") { model.errorMessage = nil }
            } message: {
                Text(model.errorMessage ?? "Please try again.")
            }
        }
    }

    private var menu: some View {
        VStack(spacing: 10) {
            Text("Update your active trip without starting over.")
                .foregroundStyle(.secondary)
                .frame(maxWidth: .infinity, alignment: .leading)
            optionButton("arrow.trianglehead.2.clockwise.rotate.90", "Reroute now", "Find a new route from your current location.") {
                Task { await model.rerouteNow() }
            }
            optionButton("arrow.left.arrow.right", "Change route preference", "Choose fastest, cheapest, or balanced.") {
                page = .preference
            }
            optionButton("pesosign.circle", "Change budget", "Set or remove your maximum fare budget.") {
                page = .budget
            }
            optionButton("mappin.and.ellipse", "Change destination", "Search for a new destination and reroute.") {
                page = .destination
            }
            Spacer(minLength: 6)
            Button(role: .destructive) {
                Task { await model.endTrip() }
            } label: {
                Text("End trip").fontWeight(.bold).frame(maxWidth: .infinity).frame(height: 48)
            }
            .buttonStyle(.borderedProminent)
        }
    }

    private var preferenceEditor: some View {
        VStack(spacing: 18) {
            Text("Route preference").font(.title2.bold()).frame(maxWidth: .infinity, alignment: .leading)
            Picker("Preference", selection: $preference) {
                Text("Fastest").tag("fastest")
                Text("Cheapest").tag("cheapest")
                Text("Balanced").tag("efficient")
            }
            .pickerStyle(.segmented)
            Button("Apply preference") {
                Task { await model.changePreference(preference) }
            }
            .buttonStyle(.borderedProminent)
            .tint(TukiPalette.teal)
            .frame(maxWidth: .infinity)
            Spacer()
        }
    }

    private var budgetEditor: some View {
        VStack(spacing: 16) {
            Text("Change budget").font(.title2.bold()).frame(maxWidth: .infinity, alignment: .leading)
            TextField("Budget (₱)", text: $budgetText)
                .keyboardType(.decimalPad)
                .textFieldStyle(.roundedBorder)
            Button("Apply budget") {
                guard let value = Double(budgetText), value > 0 else {
                    model.errorMessage = "Enter a valid budget greater than ₱0."
                    return
                }
                Task { await model.changeBudget(value, clear: false) }
            }
            .buttonStyle(.borderedProminent)
            .tint(TukiPalette.teal)
            Button("Remove budget limit") {
                Task { await model.changeBudget(nil, clear: true) }
            }
            .foregroundStyle(TukiPalette.orange)
            Spacer()
        }
    }

    private var destinationEditor: some View {
        VStack(spacing: 12) {
            Text("Change destination").font(.title2.bold()).frame(maxWidth: .infinity, alignment: .leading)
            TextField("New destination", text: $destinationQuery)
                .textFieldStyle(.roundedBorder)
            if destinationLoading {
                ProgressView().frame(maxWidth: .infinity)
            }
            ScrollView {
                LazyVStack(spacing: 8) {
                    ForEach(destinationResults) { place in
                        Button {
                            Task { await model.changeDestination(place) }
                        } label: {
                            VStack(alignment: .leading, spacing: 3) {
                                Text(place.name).fontWeight(.bold).foregroundStyle(TukiPalette.dark)
                                if let address = place.address, !address.isEmpty {
                                    Text(address).font(.caption).foregroundStyle(.secondary)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(12)
                            .background(Color.secondary.opacity(0.08))
                            .clipShape(RoundedRectangle(cornerRadius: 12))
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            Spacer()
        }
        .task(id: destinationQuery) {
            let query = destinationQuery.trimmingCharacters(in: .whitespacesAndNewlines)
            guard query.count >= 2 else {
                destinationResults = []
                return
            }
            try? await Task.sleep(for: .milliseconds(300))
            guard !Task.isCancelled else { return }
            destinationLoading = true
            destinationResults = await model.searchDestinations(query)
            destinationLoading = false
        }
    }

    private func optionButton(
        _ icon: String,
        _ title: String,
        _ subtitle: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            HStack(spacing: 14) {
                Image(systemName: icon)
                    .font(.title3)
                    .foregroundStyle(TukiPalette.teal)
                    .frame(width: 28)
                VStack(alignment: .leading, spacing: 3) {
                    Text(title).fontWeight(.bold).foregroundStyle(TukiPalette.dark)
                    Text(subtitle).font(.caption).foregroundStyle(.secondary)
                }
                Spacer()
            }
            .padding(13)
            .background(Color.secondary.opacity(0.07))
            .clipShape(RoundedRectangle(cornerRadius: 14))
        }
        .buttonStyle(.plain)
    }
}
