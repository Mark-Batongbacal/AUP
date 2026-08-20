import CoreLocation
import Foundation

@MainActor
final class TukiLocationService: NSObject, ObservableObject, CLLocationManagerDelegate {
    @Published private(set) var currentLocation: CLLocation?
    @Published private(set) var authorizationStatus: CLAuthorizationStatus
    @Published private(set) var errorMessage: String?

    private let manager = CLLocationManager()
    private var continuation: CheckedContinuation<CLLocation?, Never>?

    override init() {
        self.authorizationStatus = manager.authorizationStatus
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyBest
    }

    func requestCurrentLocation() async -> CLLocation? {
        errorMessage = nil
        authorizationStatus = manager.authorizationStatus

        switch authorizationStatus {
        case .notDetermined:
            manager.requestWhenInUseAuthorization()
        case .denied, .restricted:
            errorMessage = TukiServiceArea.locationFailureMessage
            return nil
        case .authorizedAlways, .authorizedWhenInUse:
            break
        @unknown default:
            errorMessage = TukiServiceArea.locationFailureMessage
            return nil
        }

        if let currentLocation, Date().timeIntervalSince(currentLocation.timestamp) < 60 {
            return currentLocation
        }

        return await withCheckedContinuation { continuation in
            self.continuation?.resume(returning: nil)
            self.continuation = continuation
            manager.requestLocation()
        }
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        authorizationStatus = manager.authorizationStatus
        if manager.authorizationStatus == .authorizedWhenInUse || manager.authorizationStatus == .authorizedAlways {
            manager.requestLocation()
        } else if manager.authorizationStatus == .denied || manager.authorizationStatus == .restricted {
            errorMessage = TukiServiceArea.locationFailureMessage
            continuation?.resume(returning: nil)
            continuation = nil
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.max(by: { $0.timestamp < $1.timestamp }) else { return }
        currentLocation = location
        errorMessage = nil
        continuation?.resume(returning: location)
        continuation = nil
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        errorMessage = TukiServiceArea.locationFailureMessage
        continuation?.resume(returning: nil)
        continuation = nil
    }
}
