//
//  GoogleSignInCoordinator.swift
//  TUKI
//

import Foundation
import GoogleSignIn
import UIKit

struct GoogleSignInCoordinator {
    let configuration: GoogleOAuthConfiguration

    @MainActor
    func getIDToken() async -> GoogleSignInResult {
        guard let presentingViewController = UIApplication.shared.topMostViewController else {
            return .failure("Google sign-in is unavailable right now.")
        }

        GIDSignIn.sharedInstance.configuration = GIDConfiguration(
            clientID: configuration.clientID,
            serverClientID: configuration.serverClientID
        )

        do {
            let result = try await GIDSignIn.sharedInstance.signIn(withPresenting: presentingViewController)
            guard let idToken = result.user.idToken?.tokenString, !idToken.isEmpty else {
                return .missingIDToken
            }

            return .success(idToken)
        } catch {
            let nsError = error as NSError
            if nsError.domain == kGIDSignInErrorDomain &&
                nsError.code == GoogleSignInErrorCode.canceled {
                return .failure("Google sign-in was canceled.")
            }

            return .failure("Google sign-in failed. Try again.")
        }
    }
}

enum GoogleSignInResult: Equatable {
    case success(String)
    case missingIDToken
    case failure(String)
}

private enum GoogleSignInErrorCode {
    static let canceled = -5
}

private extension UIApplication {
    @MainActor
    var topMostViewController: UIViewController? {
        let activeScenes = connectedScenes.compactMap { $0 as? UIWindowScene }
        let window = activeScenes
            .flatMap(\.windows)
            .first { $0.isKeyWindow } ?? activeScenes.flatMap(\.windows).first

        var topController = window?.rootViewController
        while let presentedController = topController?.presentedViewController {
            topController = presentedController
        }

        return topController
    }
}
