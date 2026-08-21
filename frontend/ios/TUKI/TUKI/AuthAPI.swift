//
//  AuthAPI.swift
//  TUKI
//

import Foundation

protocol AuthAPI {
    func login(userName: String, password: String) async -> AuthResult
    func loginWithGoogle(idToken: String) async -> AuthResult
    func loginWithFacebook(accessToken: String) async -> AuthResult
    func loginWithFacebookOidc(idToken: String, nonce: String) async -> AuthResult
}

final class TukiAuthAPI: AuthAPI {
    private let baseURL: URL
    private let credentialStore: TukiCredentialStore
    private let session: URLSession
    private let jsonEncoder = JSONEncoder()
    private let jsonDecoder = JSONDecoder()

    init(
        baseURL: URL,
        credentialStore: TukiCredentialStore,
        session: URLSession = .shared
    ) {
        self.baseURL = baseURL
        self.credentialStore = credentialStore
        self.session = session
    }

    func login(userName: String, password: String) async -> AuthResult {
        await authenticate(
            path: "api/auth/login",
            body: LoginRequest(userName: userName, password: password),
            unauthorizedMessage: "Invalid username or password."
        )
    }

    func loginWithGoogle(idToken: String) async -> AuthResult {
        await authenticate(
            path: "api/auth/google",
            body: GoogleLoginRequest(idToken: idToken),
            unauthorizedMessage: "Google login was rejected. Try again."
        )
    }

    func loginWithFacebook(accessToken: String) async -> AuthResult {
        await authenticate(
            path: "api/auth/facebook",
            body: FacebookLoginRequest(accessToken: accessToken),
            unauthorizedMessage: "Facebook login was rejected. Try again."
        )
    }

    func loginWithFacebookOidc(idToken: String, nonce: String) async -> AuthResult {
        await authenticate(
            path: "api/auth/facebook/oidc",
            body: FacebookOidcLoginRequest(idToken: idToken, nonce: nonce),
            unauthorizedMessage: "Facebook login was rejected. Try again."
        )
    }

    #if DEBUG
    func loginWithFacebook(
        accessToken: String,
        diagnostic initialDiagnostic: FacebookLoginDiagnosticReport
    ) async -> FacebookAuthDiagnosticResult {
        await authenticateWithFacebookDiagnostic(
            path: "api/auth/facebook",
            body: FacebookLoginRequest(accessToken: accessToken),
            unauthorizedMessage: "Facebook login was rejected. Try again.",
            initialDiagnostic: initialDiagnostic
        )
    }

    func loginWithFacebookOidc(
        idToken: String,
        nonce: String,
        diagnostic initialDiagnostic: FacebookLoginDiagnosticReport
    ) async -> FacebookAuthDiagnosticResult {
        await authenticateWithFacebookDiagnostic(
            path: "api/auth/facebook/oidc",
            body: FacebookOidcLoginRequest(idToken: idToken, nonce: nonce),
            unauthorizedMessage: "Facebook login was rejected. Try again.",
            initialDiagnostic: initialDiagnostic
        )
    }
    #endif

    func authenticatedRequest(path: String) -> URLRequest {
        let url = baseURL.appendingBackendPath(path)
        var request = URLRequest(url: url)

        if let credential = credentialStore.credential {
            request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
        }

        return request
    }

    private func authenticate<RequestBody: Encodable>(
        path: String,
        body: RequestBody,
        unauthorizedMessage: String
    ) async -> AuthResult {
        do {
            let request = try makeAuthenticationRequest(path: path, body: body)

            let (data, response) = try await session.data(for: request)
            guard let httpResponse = response as? HTTPURLResponse else {
                return .failure("The server returned an invalid login response.")
            }

            switch httpResponse.statusCode {
            case 200..<300:
                let loginResponse = try jsonDecoder.decode(LoginResponse.self, from: data)
                guard let credential = TukiCredential(loginResponse: loginResponse) else {
                    return .failure("The server returned an invalid login response.")
                }

                do {
                    try credentialStore.save(credential)
                    return .success
                } catch {
                    return .failure("TUKI could not securely save your login.")
                }

            case 401:
                return .failure(unauthorizedMessage)

            default:
                return .failure("Login is unavailable. Try again later.")
            }
        } catch is DecodingError {
            return .failure("The server returned an invalid login response.")
        } catch let error as URLError {
            switch error.code {
            case .timedOut:
                return .failure("Network timeout. Check your connection and try again.")
            case .cancelled:
                return .failure("Login was canceled.")
            default:
                return .failure("Network error. Check your connection and try again.")
            }
        } catch {
            return .failure("Login failed. Try again.")
        }
    }

    private func makeAuthenticationRequest<RequestBody: Encodable>(
        path: String,
        body: RequestBody
    ) throws -> URLRequest {
        var request = URLRequest(url: baseURL.appendingBackendPath(path))
        request.httpMethod = "POST"
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.httpBody = try jsonEncoder.encode(body)

        return request
    }

    #if DEBUG
    private func authenticateWithFacebookDiagnostic<RequestBody: Encodable>(
        path: String,
        body: RequestBody,
        unauthorizedMessage: String,
        initialDiagnostic: FacebookLoginDiagnosticReport
    ) async -> FacebookAuthDiagnosticResult {
        var diagnostic = initialDiagnostic
        diagnostic.backendPath = "/\(path)"

        do {
            let request = try makeAuthenticationRequest(path: path, body: body)
            diagnostic.backendRequestSent = true
            let (data, response) = try await session.data(for: request)
            guard let httpResponse = response as? HTTPURLResponse else {
                diagnostic.failureDetail = "Malformed backend response: missing HTTP status"
                return FacebookAuthDiagnosticResult(
                    authResult: .failure("The server returned an invalid login response."),
                    diagnostic: diagnostic
                )
            }

            diagnostic.backendStatusCode = httpResponse.statusCode
            switch httpResponse.statusCode {
            case 200..<300:
                let loginResponse: LoginResponse
                do {
                    loginResponse = try jsonDecoder.decode(LoginResponse.self, from: data)
                } catch is DecodingError {
                    diagnostic.failureDetail = "Malformed backend response: decoding failed"
                    return FacebookAuthDiagnosticResult(
                        authResult: .failure("The server returned an invalid login response."),
                        diagnostic: diagnostic
                    )
                }

                guard let credential = TukiCredential(loginResponse: loginResponse) else {
                    diagnostic.failureDetail = "Malformed backend response: missing TUKI credential"
                    return FacebookAuthDiagnosticResult(
                        authResult: .failure("The server returned an invalid login response."),
                        diagnostic: diagnostic
                    )
                }

                diagnostic.tukiCredentialReceived = true
                do {
                    try credentialStore.save(credential)
                    diagnostic.tukiCredentialStored = true
                    diagnostic.authenticationCompleted = true
                    return FacebookAuthDiagnosticResult(
                        authResult: .success,
                        diagnostic: diagnostic
                    )
                } catch {
                    diagnostic.failureDetail = safeKeychainStorageFailureDetail(error)
                    return FacebookAuthDiagnosticResult(
                        authResult: .failure("TUKI could not securely save your login."),
                        diagnostic: diagnostic
                    )
                }

            case 401:
                diagnostic.failureDetail = "HTTP 401: backend rejected Facebook token"
                return FacebookAuthDiagnosticResult(
                    authResult: .failure(unauthorizedMessage),
                    diagnostic: diagnostic
                )

            case 502:
                diagnostic.failureDetail = "HTTP 502: backend gateway/upstream failure"
                return FacebookAuthDiagnosticResult(
                    authResult: .failure("Login is unavailable. Try again later."),
                    diagnostic: diagnostic
                )

            default:
                diagnostic.failureDetail = "HTTP \(httpResponse.statusCode): backend login request failed"
                return FacebookAuthDiagnosticResult(
                    authResult: .failure("Login is unavailable. Try again later."),
                    diagnostic: diagnostic
                )
            }
        } catch let error as URLError {
            diagnostic.failureDetail = "Backend connection failure: URLError code \(error.code.rawValue)"
            switch error.code {
            case .timedOut:
                return FacebookAuthDiagnosticResult(
                    authResult: .failure("Network timeout. Check your connection and try again."),
                    diagnostic: diagnostic
                )
            case .cancelled:
                return FacebookAuthDiagnosticResult(
                    authResult: .failure("Login was canceled."),
                    diagnostic: diagnostic
                )
            default:
                return FacebookAuthDiagnosticResult(
                    authResult: .failure("Network error. Check your connection and try again."),
                    diagnostic: diagnostic
                )
            }
        } catch {
            diagnostic.failureDetail = "Backend request encoding failure"
            return FacebookAuthDiagnosticResult(
                authResult: .failure("Login failed. Try again."),
                diagnostic: diagnostic
            )
        }
    }

    private func safeKeychainStorageFailureDetail(_ error: Error) -> String {
        if let keychainError = error as? KeychainCredentialStoreError {
            switch keychainError {
            case .unhandled(let status):
                return "Keychain storage failure: OSStatus \(status)"
            }
        }

        return "Keychain storage failure: \(String(reflecting: type(of: error)))"
    }
    #endif

}
