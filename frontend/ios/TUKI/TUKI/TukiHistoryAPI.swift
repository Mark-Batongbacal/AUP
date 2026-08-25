import Foundation

final class TukiHistoryAPI {
    private let baseURL: URL
    private let credentialStore: TukiCredentialStore
    private let platformAPI: TukiPlatformAPI
    private let session: URLSession
    private let decoder = JSONDecoder()

    init(baseURL: URL, credentialStore: TukiCredentialStore, session: URLSession = .shared) {
        self.baseURL = baseURL
        self.credentialStore = credentialStore
        self.platformAPI = TukiPlatformAPI(baseURL: baseURL, credentialStore: credentialStore, session: session)
        self.session = session
    }

    func history() async -> Result<[RecentCommute], TukiPlatformError> {
        switch await get([HistoryDTO].self, path: "api/trips") {
        case .success(let values):
            var result: [RecentCommute] = []
            var seen = Set<String>()
            for value in values where seen.insert(value.passengerTripId).inserted {
                var origin = value.originName
                var destination = value.destinationName
                if origin.isGenericLocationLabel,
                   case .success(let place) = await platformAPI.reverseGeocode(lat: value.originLatitude, lon: value.originLongitude) {
                    origin = place.name
                }
                if destination.isGenericLocationLabel,
                   case .success(let place) = await platformAPI.reverseGeocode(lat: value.destinationLatitude, lon: value.destinationLongitude) {
                    destination = place.name
                }
                result.append(value.commute(origin: origin, destination: destination))
            }
            return .success(result)
        case .failure(let error): return .failure(error)
        }
    }

    func favorites() async -> Result<[FavoriteRoute], TukiPlatformError> {
        switch await get([FavoriteDTO].self, path: "api/favorite-trips") {
        case .success(let values): return .success(values.compactMap(\.route))
        case .failure(let error): return .failure(error)
        }
    }

    /// `POST api/favorite-trips`
    func addFavorite(recommendationId: String) async -> Result<FavoriteRoute, TukiPlatformError> {
        switch await send(FavoriteDTO.self, path: "api/favorite-trips", method: "POST", body: AddFavoriteBody(recommendationId: recommendationId)) {
        case .success(let value):
            guard let route = value.route else {
                return .failure(.message("The server returned an invalid favorite."))
            }
            return .success(route)
        case .failure(let error): return .failure(error)
        }
    }

    /// `DELETE api/favorite-trips/{favoriteTripId}`
    func removeFavorite(favoriteTripId: String) async -> Result<Void, TukiPlatformError> {
        await sendNoContent(path: "api/favorite-trips/\(favoriteTripId)", method: "DELETE")
    }

    private func get<T: Decodable>(_ type: T.Type, path: String) async -> Result<T, TukiPlatformError> {
        guard let credential = credentialStore.credential else { return .failure(.notAuthenticated) }
        var request = URLRequest(url: baseURL.appendingBackendPath(path))
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else { return .failure(.message("The server returned an invalid response.")) }
            if http.statusCode == 401 { return .failure(.notAuthenticated) }
            guard (200..<300).contains(http.statusCode) else { return .failure(.message("Saved journeys are unavailable right now.")) }
            do { return .success(try decoder.decode(T.self, from: data)) }
            catch { return .failure(.message("The server returned journey data TUKI could not read.")) }
        } catch { return .failure(.message("Saved journeys are unavailable right now.")) }
    }

    private func send<T: Decodable, B: Encodable>(_ type: T.Type, path: String, method: String, body: B) async -> Result<T, TukiPlatformError> {
        guard let credential = credentialStore.credential else { return .failure(.notAuthenticated) }
        var request = URLRequest(url: baseURL.appendingBackendPath(path))
        request.httpMethod = method
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
        do {
            request.httpBody = try JSONEncoder().encode(body)
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else { return .failure(.message("The server returned an invalid response.")) }
            if http.statusCode == 401 { return .failure(.notAuthenticated) }
            guard (200..<300).contains(http.statusCode) else { return .failure(.message("Saving this favorite didn't work. Try again.")) }
            do { return .success(try decoder.decode(T.self, from: data)) }
            catch { return .failure(.message("The server returned data TUKI could not read.")) }
        } catch { return .failure(.message("Network error. Check your connection and try again.")) }
    }

    private func sendNoContent(path: String, method: String) async -> Result<Void, TukiPlatformError> {
        guard let credential = credentialStore.credential else { return .failure(.notAuthenticated) }
        var request = URLRequest(url: baseURL.appendingBackendPath(path))
        request.httpMethod = method
        request.timeoutInterval = 30
        request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
        do {
            let (_, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else { return .failure(.message("The server returned an invalid response.")) }
            if http.statusCode == 401 { return .failure(.notAuthenticated) }
            guard (200..<300).contains(http.statusCode) else { return .failure(.message("Removing this favorite didn't work. Try again.")) }
            return .success(())
        } catch { return .failure(.message("Network error. Check your connection and try again.")) }
    }
}

private struct AddFavoriteBody: Encodable { let recommendationId: String }

private struct HistoryDTO: Decodable {
    let passengerTripId: String
    let status: String
    let originName: String
    let destinationName: String
    let originLatitude: Double
    let originLongitude: Double
    let destinationLatitude: Double
    let destinationLongitude: Double
    let startedAt: String?
    let completedAt: String?
    let createdAt: String
    let recommendation: HistoryRecommendationDTO?
    let rerouted: Bool?
    let rerouteCount: Int?

    func commute(origin: String, destination: String) -> RecentCommute {
        let ordered = (recommendation?.legs ?? []).sorted { $0.legOrder < $1.legOrder }
        return RecentCommute(
            id: passengerTripId,
            origin: origin,
            destination: destination,
            legs: ordered.count,
            minutes: Int((recommendation?.totalMinutes ?? 0).rounded()),
            status: displayStatus,
            wasRerouted: rerouted ?? false,
            rerouteCount: rerouteCount ?? 0,
            dateGroup: Self.dateGroup(completedAt ?? startedAt ?? createdAt),
            originLatitude: originLatitude,
            originLongitude: originLongitude,
            destinationLatitude: destinationLatitude,
            destinationLongitude: destinationLongitude,
            steps: ordered.map {
                CommuteStep(
                    mode: $0.transportMode?.name ?? $0.route?.routeName ?? "Transit",
                    from: $0.fromName ?? $0.fromStop?.name ?? origin,
                    to: $0.toName ?? $0.toStop?.name ?? destination,
                    minutes: Int($0.estimatedMinutes.rounded()),
                    fare: $0.estimatedFare
                )
            },
            recommendationId: recommendation?.recommendationId,
            totalFare: recommendation?.totalFare ?? 0,
            endedAt: completedAt ?? startedAt
        )
    }

    private var displayStatus: String {
        switch status.uppercased() {
        case "ARRIVED", "COMPLETED": return "Completed"
        case "CANCELLED": return "Cancelled"
        default: return status.replacingOccurrences(of: "_", with: " ").capitalized
        }
    }

    private static func dateGroup(_ value: String) -> String {
        let formatter = ISO8601DateFormatter()
        guard let date = formatter.date(from: value) else { return "Earlier" }
        if Calendar.current.isDateInToday(date) { return "Today" }
        if Calendar.current.isDateInYesterday(date) { return "Yesterday" }
        return "Earlier"
    }
}

private struct HistoryRecommendationDTO: Decodable {
    let recommendationId: String
    let totalMinutes: Double
    let totalFare: Double
    let legs: [HistoryLegDTO]
}
private struct HistoryLegDTO: Decodable {
    let legOrder: Int
    let transportMode: HistoryModeDTO?
    let route: HistoryRouteDTO?
    let fromStop: HistoryStopDTO?
    let toStop: HistoryStopDTO?
    let fromName: String?
    let toName: String?
    let estimatedMinutes: Double
    let estimatedFare: Double
}
private struct HistoryModeDTO: Decodable { let name: String }
private struct HistoryRouteDTO: Decodable { let routeName: String }
private struct HistoryStopDTO: Decodable { let name: String }

private struct FavoriteDTO: Decodable {
    let favoriteTripId: String?
    let recommendationId: String?
    let origin: String?
    let destination: String?
    let totalMinutes: Double?
    let totalFare: Double?
    let transferCount: Int?
    let timesUsed: Int?
    let note: String?

    /// Matches Android's `toFavoriteRouteOrNull()`: a favorite without both a real
    /// favoriteTripId and recommendationId can't be removed or reopened, so it's dropped
    /// rather than shown broken.
    var route: FavoriteRoute? {
        guard let favoriteTripId, !favoriteTripId.isEmpty,
              let recommendationId, !recommendationId.isEmpty else { return nil }
        return FavoriteRoute(
            id: favoriteTripId,
            origin: origin?.isEmpty == false ? origin! : "Unknown origin",
            destination: destination?.isEmpty == false ? destination! : "Unknown destination",
            timesUsed: timesUsed ?? 0,
            note: note ?? "",
            recommendationId: recommendationId,
            totalMinutes: max(0, Int((totalMinutes ?? 0).rounded())),
            totalFare: totalFare ?? 0,
            transferCount: transferCount ?? 0
        )
    }
}

private extension String {
    var isGenericLocationLabel: Bool {
        let value = trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return value.isEmpty || value == "current location" || value == "pinned destination" || value == "unknown origin" || value == "unknown destination"
    }
}
