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
    @Published private(set) var currentUserProfile: TukiUserProfile?
    #if DEBUG
    @Published private(set) var facebookLoginDiagnostic: FacebookLoginDiagnosticReport?
    #endif

    private let authAPI: AuthAPI?
    private let platformAPI: TukiPlatformAPI?
    private let credentialStore: TukiCredentialStore
    private let googleSignInCoordinator: GoogleSignInCoordinator?
    private let facebookSignInCoordinator: FacebookSignInCoordinator?

    var canEnterApp: Bool { isAuthenticated || isGuest }

    init() {
        let credentialStore = KeychainTukiCredentialStore()
        self.credentialStore = credentialStore
        let hasCredential = credentialStore.credential != nil
        self.isAuthenticated = hasCredential

        do {
            let configuration = try AppConfiguration.load()
            self.authAPI = TukiAuthAPI(baseURL: configuration.backendBaseURL, credentialStore: credentialStore)
            self.platformAPI = TukiPlatformAPI(baseURL: configuration.backendBaseURL, credentialStore: credentialStore)
            self.googleSignInCoordinator = configuration.googleOAuth.map { GoogleSignInCoordinator(configuration: $0) }
            self.facebookSignInCoordinator = configuration.facebookOAuth.map { FacebookSignInCoordinator(configuration: $0) }
        } catch {
            self.authAPI = nil
            self.platformAPI = nil
            self.googleSignInCoordinator = nil
            self.facebookSignInCoordinator = nil
            self.errorMessage = "TUKI login is not configured."
        }

        if hasCredential {
            Task { await restoreAuthenticatedProfile() }
        }
    }

    func loginWithPassword() {
        guard !isAuthenticating else { return }
        let trimmedUserName = userName.trimmingCharacters(in: .whitespacesAndNewlines)
        let currentPassword = password
        guard !trimmedUserName.isEmpty else { errorMessage = "Enter your email address."; return }
        guard !currentPassword.isEmpty else { errorMessage = "Enter your password."; return }
        guard currentPassword.count >= 8 else { errorMessage = "Password must be at least 8 characters."; return }
        guard let authAPI else { errorMessage = "TUKI login is not configured."; return }

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
                await finishAuthenticationResult(await authAPI.loginWithGoogle(idToken: idToken))
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
            publishFacebookDiagnostic(.sdkFailure(failureDetail: "Facebook login is not configured"))
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
                    await finishAuthenticationResult(result.authResult)
                } else {
                    await finishAuthenticationResult(await authAPI.loginWithFacebookOidc(idToken: credential.idToken, nonce: credential.nonce))
                }
            #else
            case .success(let credential):
                await finishAuthenticationResult(await authAPI.loginWithFacebookOidc(idToken: credential.idToken, nonce: credential.nonce))
            #endif
            #if DEBUG
            case .missingAuthenticationToken(let tokenDiagnostic):
                publishFacebookDiagnostic(.sdkSuccess(authenticationTokenAvailable: false, tokenDiagnostic: tokenDiagnostic, backendPath: FacebookLoginDiagnosticReport.oidcBackendPath))
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
                publishFacebookDiagnostic(.sdkFailure(failureDetail: sdkError == nil ? message : "Facebook SDK error", sdkError: sdkError))
                errorMessage = message
            #else
            case .failure(let message):
                errorMessage = message
            #endif
            }
            isAuthenticating = false
        }
    }

    func register(fullName: String, email: String, password: String) async -> Bool {
        guard let platformAPI else { errorMessage = "TUKI sign up is not configured."; return false }
        isAuthenticating = true
        errorMessage = nil
        defer { isAuthenticating = false }
        switch await platformAPI.register(fullName: fullName, email: email, password: password) {
        case .success(let profile):
            currentUserProfile = profile
            isAuthenticated = true
            isGuest = false
            self.password = ""
            return true
        case .failure(let error):
            errorMessage = error.message
            return false
        }
    }

    func refreshProfile() async -> Bool {
        guard isAuthenticated, let platformAPI else { return false }
        switch await platformAPI.currentUser() {
        case .success(let profile):
            currentUserProfile = profile
            return true
        case .failure(.notAuthenticated):
            forceSessionExpired()
            return false
        case .failure(let error):
            errorMessage = error.message
            return false
        }
    }

    func updateProfile(fullName: String, phone: String) async -> Result<TukiUserProfile, TukiPlatformError> {
        guard let platformAPI else { return .failure(.message("Profile updates are not configured.")) }
        let result = await platformAPI.updateProfile(fullName: fullName, phone: phone)
        if case .success(let profile) = result { currentUserProfile = profile }
        if case .failure(.notAuthenticated) = result { forceSessionExpired() }
        return result
    }

    func changePassword(current: String, new: String) async -> Result<Void, TukiPlatformError> {
        guard let platformAPI else { return .failure(.message("Changing your password isn't configured.")) }
        let result = await platformAPI.changePassword(current: current, new: new)
        if case .failure(.notAuthenticated) = result { forceSessionExpired() }
        return result
    }

    func deleteAccount() async -> Result<Void, TukiPlatformError> {
        guard let platformAPI else { return .failure(.message("Account deletion isn't configured.")) }
        let result = await platformAPI.deleteAccount()
        if case .success = result { signOut() }
        if case .failure(.notAuthenticated) = result { forceSessionExpired() }
        return result
    }

    func signOut() {
        GIDSignIn.sharedInstance.signOut()
        facebookSignInCoordinator?.signOut()
        try? credentialStore.clear()
        currentUserProfile = nil
        isAuthenticated = false
        isGuest = false
        password = ""
        #if DEBUG
        facebookLoginDiagnostic = nil
        #endif
    }

    func continueAsGuest() {
        currentUserProfile = nil
        isGuest = true
        isAuthenticated = false
        errorMessage = nil
        password = ""
    }

    private func authenticate(_ operation: @escaping () async -> AuthResult) async {
        isAuthenticating = true
        errorMessage = nil
        await finishAuthenticationResult(await operation())
        isAuthenticating = false
    }

    private func finishAuthenticationResult(_ result: AuthResult) async {
        switch result {
        case .success:
            guard let platformAPI else {
                errorMessage = "TUKI profile loading is not configured."
                try? credentialStore.clear()
                return
            }
            switch await platformAPI.currentUser() {
            case .success(let profile):
                currentUserProfile = profile
                isAuthenticated = true
                isGuest = false
                password = ""
            case .failure(let error):
                try? credentialStore.clear()
                currentUserProfile = nil
                isAuthenticated = false
                errorMessage = error.message
            }
        case .failure(let message):
            errorMessage = message
        }
    }

    private func restoreAuthenticatedProfile() async {
        guard let platformAPI else { return }
        switch await platformAPI.currentUser() {
        case .success(let profile):
            currentUserProfile = profile
            isAuthenticated = true
        case .failure:
            forceSessionExpired()
        }
    }

    private func forceSessionExpired() {
        try? credentialStore.clear()
        currentUserProfile = nil
        isAuthenticated = false
        isGuest = false
        password = ""
    }

    #if DEBUG
    private func publishFacebookDiagnostic(_ diagnostic: FacebookLoginDiagnosticReport) {
        facebookLoginDiagnostic = diagnostic
        print("[Facebook Login Debug]\n\(diagnostic.logDescription)")
    }
    #endif
}
