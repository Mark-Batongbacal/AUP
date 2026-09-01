import Combine
import CoreLocation
import Foundation

/// A tricycle (TODA) stand, matching Android's `TricyclePointResponseDto`
/// (data/tricycle/TricycleData.kt). Infrastructure, not route-specific — loaded once
/// and shared across every map on screen, same as Android's `TukiMapOverlayState`.
struct TukiTodaPoint: Decodable, Identifiable, Hashable {
    let id: Int
    let name: String
    let latitude: Double
    let longitude: Double
    let isActive: Bool

    private enum CodingKeys: String, CodingKey {
        case id = "tricyclePointId"
        case name = "pointName"
        case latitude = "centerLatitude"
        case longitude = "centerLongitude"
        case isActive
    }

    var coordinate: CLLocationCoordinate2D { CLLocationCoordinate2D(latitude: latitude, longitude: longitude) }
}

private struct TukiRoutePointDTO: Decodable {
    let latitude: Double
    let longitude: Double
}

private struct TukiRoutePointsResponseDTO: Decodable {
    let points: [TukiRoutePointDTO]
}

/// Thin client for the two public infrastructure endpoints the live-trip map needs.
/// Mirrors Android's `TricycleRepository`/`TransportRouteRepository` — plain `GET`s, no
/// session required (Android calls these through `apiCall`, not `authenticatedApiCall`).
final class TukiInfrastructureAPI {
    private let baseURL: URL
    private let session: URLSession
    private let decoder = JSONDecoder()

    init(baseURL: URL, session: URLSession = .shared) {
        self.baseURL = baseURL
        self.session = session
    }

    /// `GET api/tricycle-points`
    func activeTodaPoints() async -> Result<[TukiTodaPoint], TukiPlatformError> {
        await get([TukiTodaPoint].self, path: "api/tricycle-points")
    }

    /// `GET api/transport-routes/{routeId}/points`
    func routePoints(routeId: String) async -> Result<[CLLocationCoordinate2D], TukiPlatformError> {
        switch await get(TukiRoutePointsResponseDTO.self, path: "api/transport-routes/\(routeId)/points") {
        case .success(let response):
            return .success(response.points.map { CLLocationCoordinate2D(latitude: $0.latitude, longitude: $0.longitude) })
        case .failure(let error):
            return .failure(error)
        }
    }

    private func get<T: Decodable>(_ type: T.Type, path: String) async -> Result<T, TukiPlatformError> {
        do {
            var request = URLRequest(url: baseURL.appendingBackendPath(path))
            request.httpMethod = "GET"
            request.timeoutInterval = 30
            request.setValue("application/json", forHTTPHeaderField: "Accept")
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else {
                return .failure(.message("The server returned an invalid response."))
            }
            guard (200..<300).contains(http.statusCode) else {
                return .failure(.message("Request failed (HTTP \(http.statusCode))."))
            }
            return .success(try decoder.decode(T.self, from: data))
        } catch is DecodingError {
            return .failure(.message("The server returned data TUKI could not read."))
        } catch {
            return .failure(.message("Network error. Check your connection and try again."))
        }
    }
}

/// Process-wide cache for TODA points and per-route polylines, matching Android's
/// `TukiMapOverlayState` (MapOverlayState.kt): TODA points load once and are shared by
/// every map instance; route points are cached per route id so switching legs/screens
/// doesn't refetch a route already seen this session.
@MainActor
final class TukiMapOverlayState: ObservableObject {
    static let shared = TukiMapOverlayState()

    @Published private(set) var todaPoints: [TukiTodaPoint] = []
    @Published private(set) var routePoints: [String: [CLLocationCoordinate2D]] = [:]

    private var todaPointsLoaded = false
    private var loadingRouteIds: Set<String> = []

    func ensureTodaPoints(api: TukiInfrastructureAPI?) async {
        guard !todaPointsLoaded, let api else { return }
        todaPointsLoaded = true
        if case .success(let points) = await api.activeTodaPoints() {
            todaPoints = points.filter(\.isActive)
        }
    }

    func ensureRoutePoints(routeId: String, api: TukiInfrastructureAPI?) async {
        guard routePoints[routeId] == nil, !loadingRouteIds.contains(routeId), let api else { return }
        loadingRouteIds.insert(routeId)
        if case .success(let points) = await api.routePoints(routeId: routeId) {
            routePoints[routeId] = points
        }
        loadingRouteIds.remove(routeId)
    }
}
