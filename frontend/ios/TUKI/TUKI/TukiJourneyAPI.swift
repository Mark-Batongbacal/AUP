import Foundation

enum TukiJourneyAPIError: Error, Equatable {
    case notAuthenticated
    case requestFailed
    case decodingFailed

    var message: String {
        switch self {
        case .notAuthenticated:
            return "Sign in to view your saved journeys."
        case .requestFailed:
            return "Saved journeys are unavailable right now."
        case .decodingFailed:
            return "The server returned journey data TUKI could not read."
        }
    }
}

final class TukiJourneyAPI {
    private let baseURL: URL
    private let credentialStore: TukiCredentialStore
    private let session: URLSession
    private let decoder = JSONDecoder()

    init(
        baseURL: URL,
        credentialStore: TukiCredentialStore,
        session: URLSession = .shared
    ) {
        self.baseURL = baseURL
        self.credentialStore = credentialStore
        self.session = session
    }

    func recentJourneys() async -> Result<[RecentCommute], TukiJourneyAPIError> {
        do {
            let request = try authenticatedRequest(path: "api/trips/recent")
            let data = try await data(for: request)
            let journeys = try decoder.decode([RecentJourneyDTO].self, from: data)
            return .success(journeys.map(\.commute))
        } catch let error as TukiJourneyAPIError {
            return .failure(error)
        } catch is DecodingError {
            return .failure(.decodingFailed)
        } catch {
            return .failure(.requestFailed)
        }
    }

    func favorites() async -> Result<[FavoriteRoute], TukiJourneyAPIError> {
        do {
            let request = try authenticatedRequest(path: "api/favorite-trips")
            let data = try await data(for: request)
            let favorites = try decoder.decode([FavoriteTripDTO].self, from: data)
            return .success(favorites.map(\.route))
        } catch let error as TukiJourneyAPIError {
            return .failure(error)
        } catch is DecodingError {
            return .failure(.decodingFailed)
        } catch {
            return .failure(.requestFailed)
        }
    }

    private func authenticatedRequest(path: String) throws -> URLRequest {
        guard let credential = credentialStore.credential else {
            throw TukiJourneyAPIError.notAuthenticated
        }

        var request = URLRequest(url: baseURL.appendingBackendPath(path))
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
        return request
    }

    private func data(for request: URLRequest) async throws -> Data {
        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw TukiJourneyAPIError.requestFailed
        }

        switch httpResponse.statusCode {
        case 200..<300:
            return data
        case 401:
            throw TukiJourneyAPIError.notAuthenticated
        default:
            throw TukiJourneyAPIError.requestFailed
        }
    }
}

private struct RecentJourneyDTO: Decodable {
    let passengerTripId: String
    let status: String
    let originName: String
    let destinationName: String
    let completedAt: String?
    let startedAt: String?
    let createdAt: String
    let recommendation: RecommendationDTO?
    let rerouted: Bool?
    let rerouteCount: Int?

    var commute: RecentCommute {
        let orderedLegs = (recommendation?.legs ?? []).sorted { $0.legOrder < $1.legOrder }
        return RecentCommute(
            id: passengerTripId,
            origin: originName,
            destination: destinationName,
            legs: orderedLegs.count,
            minutes: Int(recommendation?.totalMinutes ?? 0),
            status: displayStatus,
            wasRerouted: rerouted ?? false,
            rerouteCount: rerouteCount ?? 0,
            dateGroup: Self.dateGroup(completedAt ?? startedAt ?? createdAt),
            steps: orderedLegs.map { leg in
                CommuteStep(
                    mode: leg.transportMode?.name ?? leg.route?.routeName ?? "Transit",
                    from: leg.fromName ?? leg.fromStop?.name ?? originName,
                    to: leg.toName ?? leg.toStop?.name ?? destinationName,
                    minutes: Int(leg.estimatedMinutes),
                    fare: leg.estimatedFare
                )
            }
        )
    }

    private var displayStatus: String {
        switch status.uppercased() {
        case "ARRIVED", "COMPLETED":
            return "Completed"
        case "CANCELLED":
            return "Cancelled"
        default:
            return status.replacingOccurrences(of: "_", with: " ").capitalized
        }
    }

    private static func dateGroup(_ timestamp: String) -> String {
        let formatter = ISO8601DateFormatter()
        let date = formatter.date(from: timestamp) ?? Date()
        let calendar = Calendar.current
        if calendar.isDateInToday(date) {
            return "Today"
        }
        if calendar.isDateInYesterday(date) {
            return "Yesterday"
        }
        return "Earlier"
    }
}

private struct RecommendationDTO: Decodable {
    let totalMinutes: Double
    let legs: [RecommendationLegDTO]
}

private struct RecommendationLegDTO: Decodable {
    let legOrder: Int
    let transportMode: TransportModeDTO?
    let route: TransportRouteDTO?
    let fromStop: TransportStopDTO?
    let toStop: TransportStopDTO?
    let fromName: String?
    let toName: String?
    let estimatedMinutes: Double
    let estimatedFare: Double
}

private struct TransportModeDTO: Decodable {
    let name: String
}

private struct TransportRouteDTO: Decodable {
    let routeName: String
}

private struct TransportStopDTO: Decodable {
    let name: String
}

private struct FavoriteTripDTO: Decodable {
    let favoriteTripId: String
    let origin: String?
    let destination: String?
    let timesUsed: Int
    let note: String?

    var route: FavoriteRoute {
        FavoriteRoute(
            id: favoriteTripId,
            origin: origin ?? "Unknown origin",
            destination: destination ?? "Unknown destination",
            timesUsed: timesUsed,
            note: note ?? ""
        )
    }
}
