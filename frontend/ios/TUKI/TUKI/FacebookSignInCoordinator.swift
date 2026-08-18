//
//  FacebookSignInCoordinator.swift
//  TUKI
//

import FacebookCore
import FacebookLogin
import Foundation
import Security
import UIKit

@MainActor
final class FacebookSignInCoordinator {
    private static let permissions = ["public_profile", "email"]

    private let configuration: FacebookOAuthConfiguration
    private let loginManager = LoginManager()

    init(configuration: FacebookOAuthConfiguration) {
        self.configuration = configuration
    }

    func getAuthenticationToken() async -> FacebookSignInResult {
        guard !configuration.appID.isEmpty, !configuration.clientToken.isEmpty else {
            return makeFailure("Facebook login is not configured.")
        }

        guard let presentingViewController = UIApplication.shared.facebookTopMostViewController else {
            return makeFailure("Facebook sign-in is unavailable right now.")
        }

        guard let nonce = Self.makeNonce() else {
            return makeFailure("Facebook sign-in is unavailable right now.")
        }

        guard let loginConfiguration = LoginConfiguration(
            permissions: Self.permissions,
            tracking: .limited,
            nonce: nonce
        ) else {
            return makeFailure("Facebook sign-in returned an invalid credential.")
        }

        loginManager.logOut()

        return await withCheckedContinuation { continuation in
            loginManager.logIn(
                viewController: presentingViewController,
                configuration: loginConfiguration
            ) { result in
                switch result {
                case .success:
                    let authenticationTokenString = AuthenticationToken.current?.tokenString
                        .trimmingCharacters(in: .whitespacesAndNewlines)
                    let hasAuthenticationToken = authenticationTokenString?.isEmpty == false
                    #if DEBUG
                    let tokenDiagnostic = self.makeTokenDiagnostic(
                        hasAuthenticationToken: hasAuthenticationToken
                    )
                    #endif

                    guard let authenticationTokenString, hasAuthenticationToken else {
                        #if DEBUG
                        continuation.resume(returning: .missingAuthenticationToken(tokenDiagnostic: tokenDiagnostic))
                        #else
                        continuation.resume(returning: .missingAuthenticationToken)
                        #endif
                        return
                    }

                    let credential = FacebookAuthenticationTokenCredential(
                        idToken: authenticationTokenString,
                        nonce: nonce
                    )
                    #if DEBUG
                    continuation.resume(returning: .success(credential, tokenDiagnostic: tokenDiagnostic))
                    #else
                    continuation.resume(returning: .success(credential))
                    #endif
                case .cancelled:
                    continuation.resume(returning: .cancelled)
                case .failed(let error):
                    continuation.resume(returning: self.makeFailure("Facebook sign-in failed. Try again.", error: error))
                }
            }
        }
    }

    private static func makeNonce(byteCount: Int = 32) -> String? {
        var bytes = [UInt8](repeating: 0, count: byteCount)
        let status = SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes)
        guard status == errSecSuccess else {
            return nil
        }

        return Data(bytes)
            .base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }

    func signOut() {
        loginManager.logOut()
    }

    private func makeFailure(_ message: String, error: Error? = nil) -> FacebookSignInResult {
        #if DEBUG
        return .failure(message, sdkError: error.map { FacebookSDKErrorDiagnostic(error: $0) })
        #else
        return .failure(message)
        #endif
    }

    #if DEBUG
    private func makeTokenDiagnostic(hasAuthenticationToken: Bool) -> FacebookLoginTokenDiagnostic {
        let classicAccessTokenString = AccessToken.current?.tokenString
            .trimmingCharacters(in: .whitespacesAndNewlines)
        let hasClassicAccessToken = classicAccessTokenString?.isEmpty == false
        let selectedTokenType: FacebookSelectedTokenDiagnosticType

        if hasAuthenticationToken {
            selectedTokenType = .oidcAuthenticationToken
        } else {
            selectedTokenType = .none
        }

        return FacebookLoginTokenDiagnostic(
            classicAccessTokenAvailable: hasClassicAccessToken,
            authenticationTokenAvailable: hasAuthenticationToken,
            selectedTokenType: selectedTokenType
        )
    }
    #endif
}

struct FacebookAuthenticationTokenCredential: Equatable {
    let idToken: String
    let nonce: String
}

enum FacebookSignInResult: Equatable {
    #if DEBUG
    case success(FacebookAuthenticationTokenCredential, tokenDiagnostic: FacebookLoginTokenDiagnostic)
    case missingAuthenticationToken(tokenDiagnostic: FacebookLoginTokenDiagnostic?)
    #else
    case success(FacebookAuthenticationTokenCredential)
    case missingAuthenticationToken
    #endif
    case cancelled
    #if DEBUG
    case failure(String, sdkError: FacebookSDKErrorDiagnostic?)
    #else
    case failure(String)
    #endif
}

private extension UIApplication {
    @MainActor
    var facebookTopMostViewController: UIViewController? {
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
