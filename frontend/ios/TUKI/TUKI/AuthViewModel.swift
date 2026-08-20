//
//  AuthViewModel.swift
//  TUKI
//

import Foundation
import Combine
import GoogleSignIn

@MainActor
final class AuthViewModel: ObservableObject {
    @Published var userName = ""
    @Published var password = ""
    @Published var isAuthenticating = false
    @Published var errorMessage: String?
    @Published private(set) var isAuthenticated: Bool
    @Published private(set) var isGuest = false
    #if DEBUG
    @Published private(set) var facebookLoginDiagnostic: FacebookLoginDiagnosticReport?
    #endif

    private let authAPI: AuthAPI?
    private let credentialStore: TukiCredentialStore
    private let googleSignInCoordinator: GoogleSignInCoordinator?
    private let facebookSignInCoordinator: FacebookSignInCoordinator?

    var canEnterApp: Bool {
        isAuthenticated || isGuest
    }

    init() {
        let credentialStore = KeychainTukiCredentialStore()
        self.credentialStore = credentialStore
        self.isAuthenticated = credentialStore.credential != nil

        do {
            let configuration = try AppConfiguration.load()
            self.authAPI = TukiAuthAPI(
                baseURL: configuration.backendBaseURL,
                credentialStore: credentialStore
            )
            self.googleSignInCoordinator = configuration.googleOAuth.map {
                GoogleSignInCoordinator(configuration: $0)
            }
            self.facebookSignInCoordinator = configuration.facebookOAuth.map {
                FacebookSignInCoordinator(configuration: $0)
            }
        } catch {
            self.authAPI = nil
            self.googleSignInCoordinator = nil
            self.facebookSignInCoordinator = nil
            self.errorMessage = "TUKI login is not configured."
        }
    }

    func loginWithPassword() {
        guard !isAuthenticating else { return }

        let trimmedUserName = userName.trimmingCharacters(in: .whitespacesAndNewlines)
        let currentPassword = password
        guard !trimmedUserName.isEmpty, !currentPassword.isEmpty else {
            errorMessage = "Enter your username and password."
            return
        }

        guard let authAPI else {
            errorMessage = "TUKI login is not configured."
            return
        }

        Task {
            await authenticate {
                await authAPI.login(userName: trimmedUserName, password: currentPassword)
            }
        }
    }

    func loginWithGoogle() {
        guard !isAuthenticating else { return }

        guard let googleSignInCoordinator, let authAPI else {
            errorMessage = "Google login is not configured."
            return
        }

        Task {
            isAuthenticating = true
            errorMessage = nil

            let signInResult = await googleSignInCoordinator.getIDToken()
            switch signInResult {
            case .success(let idToken):
                let result = await authAPI.loginWithGoogle(idToken: idToken)
                apply(result)
            case .missingIDToken:
                errorMessage = "Google sign-in returned an invalid credential."
            case .failure(let message):
                errorMessage = message
            }

            isAuthenticating = false
        }
    }

    func loginWithFacebook() {
        guard !isAuthenticating else { return }
        #if DEBUG
        facebookLoginDiagnostic = nil
        #endif

        guard let facebookSignInCoordinator, let authAPI else {
            errorMessage = "Facebook login is not configured."
            #if DEBUG
            publishFacebookDiagnostic(
                .sdkFailure(failureDetail: "Facebook login is not configured")
            )
            #endif
            return
        }

        Task {
            isAuthenticating = true
            errorMessage = nil

            let signInResult = await facebookSignInCoordinator.getAuthenticationToken()
            switch signInResult {
            #if DEBUG
            case .success(let credential, let tokenDiagnostic):
                let initialDiagnostic = FacebookLoginDiagnosticReport.sdkSuccess(
                    authenticationTokenAvailable: true,
                    tokenDiagnostic: tokenDiagnostic,
                    backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
                )
                publishFacebookDiagnostic(initialDiagnostic)
                if let tukiAuthAPI = authAPI as? TukiAuthAPI {
                    let result = await tukiAuthAPI.loginWithFacebookOidc(
                        idToken: credential.idToken,
                        nonce: credential.nonce,
                        diagnostic: initialDiagnostic
                    )
                    publishFacebookDiagnostic(result.diagnostic)
                    apply(result.authResult)
                } else {
                    let result = await authAPI.loginWithFacebookOidc(
                        idToken: credential.idToken,
                        nonce: credential.nonce
                    )
                    apply(result)
                }
            #else
            case .success(let credential):
                let result = await authAPI.loginWithFacebookOidc(
                    idToken: credential.idToken,
                    nonce: credential.nonce
                )
                apply(result)
            #endif
            #if DEBUG
            case .missingAuthenticationToken(let tokenDiagnostic):
                publishFacebookDiagnostic(
                    .sdkSuccess(
                        authenticationTokenAvailable: false,
                        tokenDiagnostic: tokenDiagnostic,
                        backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
                    )
                )
                errorMessage = "Facebook sign-in returned an invalid credential."
            #else
            case .missingAuthenticationToken:
                errorMessage = "Facebook sign-in returned an invalid credential."
            #endif
            case .cancelled:
                #if DEBUG
                publishFacebookDiagnostic(.cancelled())
                #endif
                break
            #if DEBUG
            case .failure(let message, let sdkError):
                publishFacebookDiagnostic(
                    .sdkFailure(
                        failureDetail: sdkError == nil ? message : "Facebook SDK error",
                        sdkError: sdkError
                    )
                )
                errorMessage = message
            #else
            case .failure(let message):
                errorMessage = message
            #endif
            }

            isAuthenticating = false
        }
    }

    func signOut() {
        GIDSignIn.sharedInstance.signOut()
        facebookSignInCoordinator?.signOut()
        try? credentialStore.clear()
        isAuthenticated = false
        isGuest = false
        password = ""
        #if DEBUG
        facebookLoginDiagnostic = nil
        #endif
    }

    func continueAsGuest() {
        isGuest = true
        isAuthenticated = false
        errorMessage = nil
        password = ""
    }

    private func authenticate(_ operation: @escaping () async -> AuthResult) async {
        isAuthenticating = true
        errorMessage = nil

        let result = await operation()
        apply(result)

        isAuthenticating = false
    }

    private func apply(_ result: AuthResult) {
        switch result {
        case .success:
            isAuthenticated = true
            password = ""
        case .failure(let message):
            errorMessage = message
        }
    }

    #if DEBUG
    private func publishFacebookDiagnostic(_ diagnostic: FacebookLoginDiagnosticReport) {
        facebookLoginDiagnostic = diagnostic
        print("[Facebook Login Debug]\n\(diagnostic.logDescription)")
    }
    #endif

}
