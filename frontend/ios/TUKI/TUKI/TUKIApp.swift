//
//  TUKIApp.swift
//  TUKI
//
//  Created by Stephen Kurl Pinacate on 8/18/26.
//

import SwiftUI
import FacebookCore
import GoogleSignIn

extension Notification.Name {
    static let tukiTripEnded = Notification.Name("tuki.trip.ended")
}

@main
struct TUKIApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        WindowGroup {
            TukiAppContent()
                .onOpenURL { url in
                    AuthCallbackURLHandler.handle(url)
                }
        }
    }
}

private struct TukiAppContent: View {
    @State private var mainFlowId = UUID()

    var body: some View {
        ZStack(alignment: .topTrailing) {
            TukiUnifiedParityRootView()
                .id(mainFlowId)

            TukiFareTrackingOverlay()
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                .allowsHitTesting(false)

            TukiNavigationEnhancementsOverlay()
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomLeading)
                .padding(.leading, 18)
                .padding(.bottom, 210)

            // TukiNavigationCameraOverlay() removed: it hijacked *any* MKMapView on screen
            // via userTrackingMode, which would now fight TukiLiveTripMapView's own
            // follow/recenter state (owned directly by the tracking screen, matching
            // Android's LiveTripMapScreen). Left in place, unreferenced, for the cleanup pass.

            // Keep interactive trip controls at their intrinsic size. A
            // full-window transparent overlay would block Google/Facebook
            // sign-in buttons on the login screen.
            TukiTripOptionsOverlay()
        }
        .onReceive(NotificationCenter.default.publisher(for: .tukiTripEnded)) { _ in
            mainFlowId = UUID()
        }
    }
}

final class AppDelegate: NSObject, UIApplicationDelegate {
    func application(
        _ application: UIApplication,
        didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil
    ) -> Bool {
        ApplicationDelegate.shared.application(
            application,
            didFinishLaunchingWithOptions: launchOptions
        )
        return true
    }
}

private enum AuthCallbackURLHandler {
    @discardableResult
    static func handle(_ url: URL) -> Bool {
        let handledByFacebook = ApplicationDelegate.shared.application(
            UIApplication.shared,
            open: url,
            sourceApplication: nil,
            annotation: nil
        )
        let handledByGoogle = GIDSignIn.sharedInstance.handle(url)
        return handledByFacebook || handledByGoogle
    }
}
