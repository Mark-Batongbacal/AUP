import Foundation

struct TukiAssistantRequest: Encodable {
    let message: String
    let originLatitude: Double?
    let originLongitude: Double?
    let tripSessionId: String?
    let destinationId: String?

    init(
        message: String,
        originLatitude: Double? = nil,
        originLongitude: Double? = nil,
        tripSessionId: String? = nil,
        destinationId: String? = nil
    ) {
        self.message = message
        self.originLatitude = originLatitude
        self.originLongitude = originLongitude
        self.tripSessionId = tripSessionId
        self.destinationId = destinationId
    }
}

struct TukiAssistantRoute: Identifiable, Equatable {
    let id: String
    let recommendationType: String
    let farePesos: Double
    let durationSeconds: Double
    let walkingMeters: Double
    let routeNames: [String]
    let choice: TukiRouteChoice
}

struct TukiAssistantResponse: Equatable {
    let status: String
    let message: String
    let journeys: [TukiAssistantRoute]
    let destinations: [TukiPlace]
    let destination: TukiPlace?
}

private struct AssistantResponseDTO: Decodable {
    let status: String
    let message: String
    let journeys: [AssistantJourneyDTO]?
    let destinations: [TukiPlace]?
    let destination: TukiPlace?
}

private struct AssistantJourneyDTO: Decodable {
    let journeyId: String
    let recommendationType: String
    let farePesos: Double
    let durationSeconds: Double
    let walkingMeters: Double
    let legs: [AssistantJourneyLegDTO]
    let plan: AssistantJourneyPlanDTO
}

private struct AssistantJourneyLegDTO: Decodable {
    let mode: String
    let routeName: String?
}

private struct AssistantJourneyPlanDTO: Decodable {
    let recommendationType: String
    let legs: [AssistantJourneyPlanLegDTO]
    let originAccess: AssistantAccessSegmentDTO
    let destinationAccess: AssistantAccessSegmentDTO
    let transferWalkDistancesMeters: [Double]
    let transferWalkTimesSeconds: [Double]?
    let totalTimeSeconds: Double
    let totalFarePesos: Double
    let generalizedCostPesos: Double
    let transferCount: Int
}

private struct AssistantAccessSegmentDTO: Decodable {
    let walkDistanceMeters: Double
}

private struct AssistantGeometryDTO: Decodable {
    let latitude: Double
    let longitude: Double
}

private struct AssistantJourneyPlanLegDTO: Decodable {
    let mode: Int
    let routeName: String?
    let routeId: String?
    let destinationLatitude: Double
    let destinationLongitude: Double
    let durationSeconds: Double
    let farePesos: Double
    let geometry: [AssistantGeometryDTO]?
}

private struct AssistantErrorEnvelope: Decodable {
    let message: String?
    let title: String?
    let errors: [String: [String]]?
}

final class TukiAssistantAPI {
    private let baseURL: URL
    private let credentialStore: TukiCredentialStore
    private let session: URLSession
    private let encoder = JSONEncoder()
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

    func ask(_ body: TukiAssistantRequest) async -> Result<TukiAssistantResponse, TukiPlatformError> {
        guard let credential = credentialStore.credential else {
            return .failure(.notAuthenticated)
        }

        do {
            let url = baseURL.appendingBackendPath("api/AI/ask")
            var request = URLRequest(url: url)
            request.httpMethod = "POST"
            request.timeoutInterval = 30
            request.setValue("application/json", forHTTPHeaderField: "Accept")
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
            request.httpBody = try encoder.encode(body)

            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else {
                return .failure(.message("The server returned an invalid response."))
            }
            guard (200..<300).contains(http.statusCode) else {
                if http.statusCode == 401 { return .failure(.notAuthenticated) }
                return .failure(parseError(status: http.statusCode, data: data))
            }

            let dto = try decoder.decode(AssistantResponseDTO.self, from: data)
            return .success(TukiAssistantResponse(
                status: dto.status,
                message: dto.message,
                journeys: dto.journeys?.map { $0.route() } ?? [],
                destinations: dto.destinations ?? [],
                destination: dto.destination
            ))
        } catch let error as URLError {
            return .failure(.message(
                error.code == .timedOut
                    ? "Network timeout. Check your connection and try again."
                    : "Network error. Check your connection and try again."
            ))
        } catch {
            return .failure(.message("The server returned data TUKI could not read."))
        }
    }

    private func parseError(status: Int, data: Data) -> TukiPlatformError {
        if let envelope = try? decoder.decode(AssistantErrorEnvelope.self, from: data) {
            if let value = envelope.message, !value.isEmpty { return .message(value) }
            if let value = envelope.errors?.values.flatMap({ $0 }).first, !value.isEmpty {
                return .message(value)
            }
            if let value = envelope.title, !value.isEmpty { return .message(value) }
        }
        return .message("Request failed (HTTP \(status)).")
    }
}

private extension AssistantJourneyDTO {
    func route() -> TukiAssistantRoute {
        let tags = recommendationType
            .split(separator: ",")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }

        var labels: [String] = []
        if tags.contains("efficient") { labels.append("Balanced") }
        if tags.contains("cheapest") { labels.append("Cheapest") }
        if tags.contains("fastest") { labels.append("Fastest") }
        let label = labels.isEmpty ? "Alternative" : labels.joined(separator: " · ")

        let steps = plan.legs.enumerated().map { index, leg in
            let mode = leg.mode == 0 ? "Walk" : leg.mode == 1 ? "Tricycle" : leg.mode == 2 ? "Jeepney" : "Transit"
            return CommuteStep(
                mode: mode,
                from: index == 0 ? "Current location" : (leg.routeName ?? "Transfer point"),
                to: index == plan.legs.count - 1 ? "Destination" : (leg.routeName ?? "Transfer point"),
                minutes: Int((leg.durationSeconds / 60).rounded()),
                fare: leg.farePesos
            )
        }

        let choice = TukiRouteChoice(
            id: journeyId,
            label: label,
            totalMinutes: Int((plan.totalTimeSeconds / 60).rounded()),
            totalFare: plan.totalFarePesos,
            walkMeters: Int((
                plan.originAccess.walkDistanceMeters +
                plan.destinationAccess.walkDistanceMeters +
                plan.transferWalkDistancesMeters.reduce(0, +)
            ).rounded()),
            transfers: plan.transferCount,
            generalCost: plan.generalizedCostPesos,
            isRecommended: tags.contains("efficient"),
            steps: steps,
            legRoutePoints: plan.legs.map { leg in
                (leg.geometry ?? []).map {
                    TukiCoordinate(latitude: $0.latitude, longitude: $0.longitude)
                }
            },
            legEndPoints: plan.legs.map {
                TukiCoordinate(latitude: $0.destinationLatitude, longitude: $0.destinationLongitude)
            },
            legRouteIds: plan.legs.map(\.routeId)
        )

        return TukiAssistantRoute(
            id: journeyId,
            recommendationType: recommendationType,
            farePesos: farePesos,
            durationSeconds: durationSeconds,
            walkingMeters: walkingMeters,
            routeNames: legs.map { leg in
                let mode = leg.mode.uppercased()
                if mode == "TRIKE" { return "Tricycle" }
                if mode == "WALK" { return "Walk" }
                if mode == "JEEPNEY" { return leg.routeName?.isEmpty == false ? leg.routeName! : "Jeepney" }
                return leg.routeName?.isEmpty == false ? leg.routeName! : leg.mode.capitalized
            },
            choice: choice
        )
    }
}
