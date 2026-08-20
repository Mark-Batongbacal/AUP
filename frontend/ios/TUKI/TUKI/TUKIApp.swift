//
//  TUKIApp.swift
//  TUKI
//
//  Created by Stephen Kurl Pinacate on 8/18/26.
//

import SwiftUI
import FacebookCore
import GoogleSignIn

@main
struct TUKIApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        WindowGroup {
            TukiParityRootView()
                .onOpenURL { url in
                    AuthCallbackURLHandler.handle(url)
                }
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
