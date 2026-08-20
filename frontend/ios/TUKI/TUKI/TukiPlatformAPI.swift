import Foundation

struct TukiUserProfile: Codable, Equatable {
    let userId: String
    var firstName: String?
    var lastName: String?
    var phoneNumber: String?
    let role: String
    var profileImageUrl: String?
    let createdAt: String
    var updatedAt: String?
    var email: String?
    var tripsTaken: Int = 0
    var favoritesCount: Int = 0

    var displayName: String {
        let value = [firstName, lastName]
            .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
            .joined(separator: " ")
        return value.isEmpty ? "User" : value
    }

    var greetingName: String {
        let first = firstName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return first.isEmpty ? String(displayName.split(separator: " ").first ?? "User") : first
    }
}

struct TukiPlace: Codable, Identifiable, Hashable {
    let id: String
    let name: String
    let latitude: Double
    let longitude: Double
    let category: String
    let source: String
    let address: String?
}

struct TukiCoordinate: Codable, Hashable {
    let latitude: Double
    let longitude: Double
}

struct TukiRouteChoice: Identifiable, Hashable {
    let id: String
    let label: String
    let totalMinutes: Int
    let totalFare: Double
    let walkMeters: Int
    let transfers: Int
    let generalCost: Double
    let isRecommended: Bool
    let steps: [CommuteStep]
    let legRoutePoints: [[TukiCoordinate]]
    let legEndPoints: [TukiCoordinate]
}

enum TukiServiceArea {
    static let title = "Location Not Yet Supported"
    static let message = "TUKI is currently available only within Porac, Angeles City, Dau, and Mabalacat. Support for additional locations will be available in the future."
    static let shortMessage = "TUKI is currently available only within Porac, Angeles City, Dau, and Mabalacat."
    static let locationFailureMessage = "Unable to detect your current location. Please check your device's location settings and try again."

    static func contains(latitude: Double, longitude: Double) -> Bool {
        (15.00...15.30).contains(latitude) && (120.43...120.68).contains(longitude)
    }
}

struct TukiNavigationLocationUpdate: Encodable {
    let latitude: Double
    let longitude: Double
    let accuracyMeters: Double
    let timestamp: String
    let speedMetersPerSecond: Double?
    let bearingDegrees: Double?
}

struct TukiNavigationLeg: Codable, Equatable {
    let legIndex: Int
    let transportMode: String
    let routeName: String?
    let fromName: String?
    let toName: String?
    let startLatitude: Double?
    let startLongitude: Double?
    let endLatitude: Double?
    let endLongitude: Double?
    let distanceMeters: Double?
    let fare: Double
}

struct TukiNavigationInstruction: Codable, Equatable {
    let type: String
    let routeName: String?
    let transportMode: String?
    let distanceMeters: Double?
    let requiresConfirmation: Bool
}

struct TukiNavigationLandmark: Codable, Equatable {
    let name: String
    let category: String
    let role: String
    let relation: String
    let latitude: Double
    let longitude: Double
    let distanceFromTargetMeters: Double
}

struct TukiNavigationStopInfo: Codable, Equatable {
    let routeName: String?
    let latitude: Double?
    let longitude: Double?
    let landmark: TukiNavigationLandmark?
}

struct TukiNavigationEvent: Codable, Equatable {
    let type: String
    let landmarkName: String?
}

struct TukiNavigationSnapshot: Codable, Equatable {
    let sessionId: String
    let state: String
    let currentLegIndex: Int
    let currentLeg: TukiNavigationLeg?
    let nextInstruction: TukiNavigationInstruction?
    let spokenInstruction: String?
    let remainingDistanceMeters: Double?
    let progressMeters: Double
    let boardInfo: TukiNavigationStopInfo?
    let alightInfo: TukiNavigationStopInfo?
    let landmark: TukiNavigationLandmark?
    let requiresBoardingConfirmation: Bool
    let requiresAlightingConfirmation: Bool
    let rerouteRequired: Bool
    let status: String
    let triggeredEvents: [TukiNavigationEvent]

    var displayInstruction: String {
        if let spokenInstruction, !spokenInstruction.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return spokenInstruction
        }
        guard let nextInstruction else { return "Waiting for navigation guidance…" }
        let mode = nextInstruction.transportMode?.lowercased().capitalized
        return [nextInstruction.type, mode, nextInstruction.routeName]
            .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
            .joined(separator: " · ")
    }
}

enum TukiPlatformError: Error, Equatable {
    case notAuthenticated
    case message(String)

    var message: String {
        switch self {
        case .notAuthenticated: return "Your session has expired. Please log in again."
        case .message(let message): return message
        }
    }
}

private struct UpdateProfileBody: Encodable {
    let firstName: String?
    let lastName: String?
    let phoneNumber: String?
    let profileImageUrl: String?
}

private struct RegisterBody: Encodable {
    let userName: String
    let password: String
    let firstName: String
    let lastName: String
    let phoneNumber: String?
}

private struct RegisterResponse: Decodable {
    let apiKey: String
    let expiresAt: String?
    let authenticationScheme: String?
    let headerName: String?
}

private struct ChangePasswordBody: Encodable {
    let currentPassword: String
    let newPassword: String
}

private struct JourneyPlanBody: Encodable {
    let originLatitude: Double
    let originLongitude: Double
    let destinationName: String
    let destinationLatitude: Double
    let destinationLongitude: Double
    let budget: Double?
    let preference: String?
}

private struct JourneyRecommendationDTO: Decodable {
    let recommendationId: String
    let plan: JourneyPlanDTO
}

private struct JourneyPlanDTO: Decodable {
    let recommendationType: String
    let legs: [JourneyLegDTO]
    let originAccess: AccessSegmentDTO
    let destinationAccess: AccessSegmentDTO
    let transferWalkDistancesMeters: [Double]
    let totalTimeSeconds: Double
    let totalFarePesos: Double
    let generalizedCostPesos: Double
    let transferCount: Int
}

private struct AccessSegmentDTO: Decodable { let walkDistanceMeters: Double }
private struct GeometryDTO: Decodable { let latitude: Double; let longitude: Double }
private struct JourneyLegDTO: Decodable {
    let mode: Int
    let routeName: String?
    let destinationLatitude: Double
    let destinationLongitude: Double
    let durationSeconds: Double
    let farePesos: Double
    let geometry: [GeometryDTO]?
}

private struct StartNavigationBody: Encodable { let recommendationId: String }
private struct APIErrorEnvelope: Decodable {
    let message: String?
    let title: String?
    let errors: [String: [String]]?
}

final class TukiPlatformAPI {
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

    func currentUser() async -> Result<TukiUserProfile, TukiPlatformError> {
        await request(TukiUserProfile.self, path: "api/users/me", auth: true)
    }

    func register(fullName: String, email: String, password: String) async -> Result<TukiUserProfile, TukiPlatformError> {
        let parts = fullName.trimmingCharacters(in: .whitespacesAndNewlines)
            .split(maxSplits: 1, whereSeparator: { $0.isWhitespace })
            .map(String.init)
        guard parts.count == 2 else { return .failure(.message("Enter both your first and last name.")) }
        let body = RegisterBody(userName: email, password: password, firstName: parts[0], lastName: parts[1], phoneNumber: nil)
        switch await request(RegisterResponse.self, path: "api/auth/register", method: "POST", body: body, auth: false) {
        case .success(let response):
            guard let credential = TukiCredential(loginResponse: LoginResponse(
                apiKey: response.apiKey,
                expiresAt: response.expiresAt,
                authenticationScheme: response.authenticationScheme,
                headerName: response.headerName
            )) else { return .failure(.message("The server returned an invalid login response.")) }
            do { try credentialStore.save(credential) }
            catch { return .failure(.message("TUKI could not securely save your login.")) }
            return await currentUser()
        case .failure(let error): return .failure(error)
        }
    }

    func updateProfile(fullName: String, phone: String) async -> Result<TukiUserProfile, TukiPlatformError> {
        let parts = fullName.trimmingCharacters(in: .whitespacesAndNewlines)
            .split(maxSplits: 1, whereSeparator: { $0.isWhitespace }).map(String.init)
        let body = UpdateProfileBody(firstName: parts.first, lastName: parts.count > 1 ? parts[1] : "", phoneNumber: phone, profileImageUrl: nil)
        return await request(TukiUserProfile.self, path: "api/users/me", method: "PUT", body: body, auth: true)
    }

    func deleteAccount() async -> Result<Void, TukiPlatformError> {
        await noContent(path: "api/users/me", method: "DELETE", auth: true)
    }

    func changePassword(current: String, new: String) async -> Result<Void, TukiPlatformError> {
        await noContent(path: "api/auth/change-password", method: "POST", body: ChangePasswordBody(currentPassword: current, newPassword: new), auth: true)
    }

    func searchPlaces(_ query: String, focusLat: Double? = nil, focusLon: Double? = nil) async -> Result<[TukiPlace], TukiPlatformError> {
        var items = [URLQueryItem(name: "q", value: query)]
        if let focusLat { items.append(URLQueryItem(name: "focusLat", value: String(focusLat))) }
        if let focusLon { items.append(URLQueryItem(name: "focusLon", value: String(focusLon))) }
        return await request([TukiPlace].self, path: "api/places/search", query: items, auth: false)
    }

    func reverseGeocode(lat: Double, lon: Double) async -> Result<TukiPlace, TukiPlatformError> {
        await request(TukiPlace.self, path: "api/places/reverse", query: [URLQueryItem(name: "lat", value: String(lat)), URLQueryItem(name: "lon", value: String(lon))], auth: false)
    }

    func plan(originName: String, originLat: Double, originLon: Double, destination: TukiPlace) async -> Result<[TukiRouteChoice], TukiPlatformError> {
        guard TukiServiceArea.contains(latitude: originLat, longitude: originLon),
              TukiServiceArea.contains(latitude: destination.latitude, longitude: destination.longitude) else {
            return .failure(.message(TukiServiceArea.shortMessage))
        }
        let body = JourneyPlanBody(originLatitude: originLat, originLongitude: originLon, destinationName: destination.name, destinationLatitude: destination.latitude, destinationLongitude: destination.longitude, budget: nil, preference: nil)
        switch await request([JourneyRecommendationDTO].self, path: "api/journeys/plan", method: "POST", body: body, auth: false) {
        case .success(let values): return .success(values.map { $0.choice(origin: originName, destination: destination.name) })
        case .failure(let error): return .failure(error)
        }
    }

    func startNavigation(recommendationId: String) async -> Result<TukiNavigationSnapshot, TukiPlatformError> {
        await request(TukiNavigationSnapshot.self, path: "api/navigation/start", method: "POST", body: StartNavigationBody(recommendationId: recommendationId), auth: true)
    }

    func activeNavigation() async -> Result<TukiNavigationSnapshot, TukiPlatformError> {
        await request(TukiNavigationSnapshot.self, path: "api/navigation/active", auth: true)
    }

    func updateLocation(sessionId: String, update: TukiNavigationLocationUpdate) async -> Result<TukiNavigationSnapshot, TukiPlatformError> {
        await request(TukiNavigationSnapshot.self, path: "api/navigation/\(sessionId)/location", method: "POST", body: update, auth: true)
    }

    func board(sessionId: String) async -> Result<TukiNavigationSnapshot, TukiPlatformError> {
        await request(TukiNavigationSnapshot.self, path: "api/navigation/\(sessionId)/boarding", method: "POST", auth: true)
    }

    func alight(sessionId: String) async -> Result<TukiNavigationSnapshot, TukiPlatformError> {
        await request(TukiNavigationSnapshot.self, path: "api/navigation/\(sessionId)/alighting", method: "POST", auth: true)
    }

    func cancel(sessionId: String) async -> Result<TukiNavigationSnapshot, TukiPlatformError> {
        await request(TukiNavigationSnapshot.self, path: "api/navigation/\(sessionId)/cancel", method: "POST", auth: true)
    }

    private func request<T: Decodable, B: Encodable>(_ type: T.Type, path: String, query: [URLQueryItem] = [], method: String = "GET", body: B? = nil, auth: Bool) async -> Result<T, TukiPlatformError> {
        do {
            var request = try makeRequest(path: path, query: query, method: method, auth: auth)
            if let body { request.httpBody = try encoder.encode(body) }
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else { return .failure(.message("The server returned an invalid response.")) }
            guard (200..<300).contains(http.statusCode) else { return .failure(parseError(http.statusCode, data)) }
            do { return .success(try decoder.decode(T.self, from: data)) }
            catch { return .failure(.message("The server returned data TUKI could not read.")) }
        } catch let error as TukiPlatformError { return .failure(error) }
        catch let error as URLError { return .failure(.message(error.code == .timedOut ? "Network timeout. Check your connection and try again." : "Network error. Check your connection and try again.")) }
        catch { return .failure(.message("The request could not be completed.")) }
    }

    private func request<T: Decodable>(_ type: T.Type, path: String, query: [URLQueryItem] = [], method: String = "GET", auth: Bool) async -> Result<T, TukiPlatformError> {
        await request(type, path: path, query: query, method: method, body: Optional<EmptyBody>.none, auth: auth)
    }

    private func noContent<B: Encodable>(path: String, method: String, body: B? = nil, auth: Bool) async -> Result<Void, TukiPlatformError> {
        do {
            var request = try makeRequest(path: path, method: method, auth: auth)
            if let body { request.httpBody = try encoder.encode(body) }
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else { return .failure(.message("The server returned an invalid response.")) }
            guard (200..<300).contains(http.statusCode) else { return .failure(parseError(http.statusCode, data)) }
            return .success(())
        } catch let error as TukiPlatformError { return .failure(error) }
        catch { return .failure(.message("The request could not be completed.")) }
    }

    private func noContent(path: String, method: String, auth: Bool) async -> Result<Void, TukiPlatformError> {
        await noContent(path: path, method: method, body: Optional<EmptyBody>.none, auth: auth)
    }

    private func makeRequest(path: String, query: [URLQueryItem] = [], method: String, auth: Bool) throws -> URLRequest {
        var components = URLComponents(url: baseURL.appendingBackendPath(path), resolvingAgainstBaseURL: false)
        if !query.isEmpty { components?.queryItems = query }
        guard let url = components?.url else { throw TukiPlatformError.message("The request URL is invalid.") }
        var request = URLRequest(url: url)
        request.httpMethod = method
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if auth {
            guard let credential = credentialStore.credential else { throw TukiPlatformError.notAuthenticated }
            request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
        }
        return request
    }

    private func parseError(_ status: Int, _ data: Data) -> TukiPlatformError {
        if status == 401 { return .notAuthenticated }
        if let envelope = try? decoder.decode(APIErrorEnvelope.self, from: data) {
            if let value = envelope.message, !value.isEmpty { return .message(value) }
            if let value = envelope.errors?.values.flatMap({ $0 }).first, !value.isEmpty { return .message(value) }
            if let value = envelope.title, !value.isEmpty { return .message(value) }
        }
        return .message("Request failed (HTTP \(status)).")
    }
}

private struct EmptyBody: Encodable {}

private extension JourneyRecommendationDTO {
    func choice(origin: String, destination: String) -> TukiRouteChoice {
        let tags = plan.recommendationType.split(separator: ",").map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
        let efficient = tags.contains("efficient"), fastest = tags.contains("fastest"), cheapest = tags.contains("cheapest")
        let label: String = efficient && fastest ? "Best Overall · Fastest" : efficient && cheapest ? "Best Overall · Cheapest" : efficient ? "Best Overall" : fastest ? "Fastest" : cheapest ? "Cheapest" : (tags.map(\.capitalized).joined(separator: " · ").isEmpty ? "Route option" : tags.map(\.capitalized).joined(separator: " · "))
        let steps = plan.legs.enumerated().map { index, leg in
            let mode = leg.mode == 0 ? "Walk" : leg.mode == 1 ? "Tricycle" : leg.mode == 2 ? "Jeepney" : "Transit"
            return CommuteStep(mode: mode, from: index == 0 ? origin : (leg.routeName ?? "Transfer point"), to: index == plan.legs.count - 1 ? destination : (leg.routeName ?? "Transfer point"), minutes: Int((leg.durationSeconds / 60).rounded()), fare: leg.farePesos)
        }
        return TukiRouteChoice(
            id: recommendationId,
            label: label,
            totalMinutes: Int((plan.totalTimeSeconds / 60).rounded()),
            totalFare: plan.totalFarePesos,
            walkMeters: Int((plan.originAccess.walkDistanceMeters + plan.destinationAccess.walkDistanceMeters + plan.transferWalkDistancesMeters.reduce(0,+)).rounded()),
            transfers: plan.transferCount,
            generalCost: plan.generalizedCostPesos,
            isRecommended: efficient,
            steps: steps,
            legRoutePoints: plan.legs.map { ($0.geometry ?? []).map { TukiCoordinate(latitude: $0.latitude, longitude: $0.longitude) } },
            legEndPoints: plan.legs.map { TukiCoordinate(latitude: $0.destinationLatitude, longitude: $0.destinationLongitude) }
        )
    }
}
