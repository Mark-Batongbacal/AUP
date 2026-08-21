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
            TukiParityRootView()
                .id(mainFlowId)

            TukiFareTrackingOverlay()
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                .allowsHitTesting(false)

            // Keep this overlay at its intrinsic size. Stretching it to the
            // full window creates a transparent hit-testing layer above the
            // login screen and can block Google/Facebook sign-in buttons.
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
