import Combine
import MapKit
import SwiftUI
import UIKit

@MainActor
private final class TukiNavigationCameraModel: ObservableObject {
    @Published private(set) var isActive = false
    @Published private(set) var isFollowing = true

    private let baseURL: URL?
    private let credentialStore = KeychainTukiCredentialStore()
    private let session: URLSession
    private weak var trackedMap: MKMapView?
    private var appliedFollow = false
    private var suppressManualDetectionUntil = Date.distantPast

    init(session: URLSession = .shared) {
        self.session = session
        self.baseURL = try? AppConfiguration.load().backendBaseURL
    }

    func monitor() async {
        while !Task.isCancelled {
            let active = await hasActiveNavigation()
            if active != isActive {
                isActive = active
                isFollowing = true
                appliedFollow = false
                trackedMap = nil
            }

            if active {
                synchronizeMap()
            }
            try? await Task.sleep(for: .milliseconds(800))
        }
    }

    func recenter() {
        guard isActive, let map = currentMap() else { return }
        trackedMap = map
        isFollowing = true
        suppressManualDetectionUntil = Date().addingTimeInterval(1.2)
        map.showsUserLocation = true
        map.setUserTrackingMode(.follow, animated: true)
        appliedFollow = true
    }

    private func synchronizeMap() {
        guard let map = currentMap() else { return }
        map.showsUserLocation = true

        if trackedMap !== map {
            trackedMap = map
            isFollowing = true
            appliedFollow = false
        }

        if isFollowing && !appliedFollow {
            suppressManualDetectionUntil = Date().addingTimeInterval(1.2)
            map.setUserTrackingMode(.follow, animated: true)
            appliedFollow = true
            return
        }

        if isFollowing,
           appliedFollow,
           Date() > suppressManualDetectionUntil,
           map.userTrackingMode == .none {
            // MapKit switches userTrackingMode to .none when the passenger
            // manually pans/zooms. Do not fight that gesture; offer Recenter.
            isFollowing = false
        }
    }

    private func hasActiveNavigation() async -> Bool {
        guard let baseURL, let credential = credentialStore.credential else { return false }
        var request = URLRequest(url: baseURL.appendingPathComponent("api/navigation/active"))
        request.httpMethod = "GET"
        request.timeoutInterval = 10
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)

        do {
            let (_, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else { return false }
            return (200..<300).contains(http.statusCode)
        } catch {
            return isActive
        }
    }

    private func currentMap() -> MKMapView? {
        if let trackedMap, trackedMap.window != nil { return trackedMap }

        let windows = UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows)
            .filter { !$0.isHidden }

        for window in windows {
            if let map = findMap(in: window) { return map }
        }
        return nil
    }

    private func findMap(in view: UIView) -> MKMapView? {
        if let map = view as? MKMapView { return map }
        for child in view.subviews {
            if let map = findMap(in: child) { return map }
        }
        return nil
    }
}

struct TukiNavigationCameraOverlay: View {
    @StateObject private var model = TukiNavigationCameraModel()

    var body: some View {
        Group {
            if model.isActive && !model.isFollowing {
                Button(action: model.recenter) {
                    Label("Recenter", systemImage: "location.fill")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(TukiPalette.dark)
                        .padding(.horizontal, 13)
                        .padding(.vertical, 10)
                        .background(.regularMaterial)
                        .clipShape(Capsule())
                        .shadow(radius: 3, y: 1)
                }
                .buttonStyle(.plain)
            }
        }
        .task { await model.monitor() }
    }
}
