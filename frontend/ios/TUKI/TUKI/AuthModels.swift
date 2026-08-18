//
//  AuthModels.swift
//  TUKI
//

import Foundation

struct LoginRequest: Encodable {
    let userName: String
    let password: String
}

struct GoogleLoginRequest: Encodable {
    let idToken: String
}

struct FacebookLoginRequest: Encodable {
    let accessToken: String
}

struct FacebookOidcLoginRequest: Encodable {
    let idToken: String
    let nonce: String
}

struct LoginResponse: Codable, Equatable {
    let apiKey: String?
    let expiresAt: String?
    let authenticationScheme: String?
    let headerName: String?
}

struct TukiCredential: Codable, Equatable {
    let apiKey: String
    let expiresAt: String?
    let authenticationScheme: String?
    let headerName: String

    init?(loginResponse: LoginResponse) {
        guard let apiKey = loginResponse.apiKey, !apiKey.isEmpty else {
            return nil
        }

        self.apiKey = apiKey
        self.expiresAt = loginResponse.expiresAt
        self.authenticationScheme = loginResponse.authenticationScheme
        self.headerName = loginResponse.headerName ?? "X-API-Key"
    }
}

enum AuthResult: Equatable {
    case success
    case failure(String)
}

#if DEBUG
struct FacebookSDKErrorDiagnostic: Equatable {
    let type: String
    let domain: String
    let code: Int

    init(error: Error) {
        let nsError = error as NSError
        self.type = String(reflecting: Swift.type(of: error))
        self.domain = nsError.domain
        self.code = nsError.code
    }

    var displayValue: String {
        "type=\(type), domain=\(domain), code=\(code)"
    }
}

enum FacebookSDKLoginDiagnosticStatus: String, Equatable {
    case success = "SUCCESS"
    case failed = "FAILED"
    case cancelled = "CANCELLED"
}

enum FacebookSelectedTokenDiagnosticType: String, Equatable {
    case oidcAuthenticationToken = "OIDC_AUTHENTICATION_TOKEN"
    case classicAccessToken = "CLASSIC_ACCESS_TOKEN"
    case none = "NONE"
}

struct FacebookLoginTokenDiagnostic: Equatable {
    let classicAccessTokenAvailable: Bool
    let authenticationTokenAvailable: Bool
    let selectedTokenType: FacebookSelectedTokenDiagnosticType
}

struct FacebookLoginDiagnosticReport: Equatable {
    static let graphBackendPath = "/api/auth/facebook"
    static let oidcBackendPath = "/api/auth/facebook/oidc"

    var sdkLogin: FacebookSDKLoginDiagnosticStatus
    var authenticationTokenAvailable: Bool
    var tokenDiagnostic: FacebookLoginTokenDiagnostic?
    var backendPath: String
    var backendRequestSent: Bool
    var backendStatusCode: Int?
    var tukiCredentialReceived: Bool
    var tukiCredentialStored: Bool
    var authenticationCompleted: Bool
    var failureDetail: String?
    var sdkError: FacebookSDKErrorDiagnostic?

    static func sdkSuccess(
        authenticationTokenAvailable: Bool,
        tokenDiagnostic: FacebookLoginTokenDiagnostic? = nil,
        backendPath: String = oidcBackendPath,
        missingTokenFailureDetail: String = "Missing Facebook authentication token"
    ) -> FacebookLoginDiagnosticReport {
        FacebookLoginDiagnosticReport(
            sdkLogin: .success,
            authenticationTokenAvailable: authenticationTokenAvailable,
            tokenDiagnostic: tokenDiagnostic,
            backendPath: backendPath,
            backendRequestSent: false,
            backendStatusCode: nil,
            tukiCredentialReceived: false,
            tukiCredentialStored: false,
            authenticationCompleted: false,
            failureDetail: authenticationTokenAvailable ? nil : missingTokenFailureDetail,
            sdkError: nil
        )
    }

    static func sdkFailure(
        failureDetail: String,
        sdkError: FacebookSDKErrorDiagnostic? = nil
    ) -> FacebookLoginDiagnosticReport {
        FacebookLoginDiagnosticReport(
            sdkLogin: .failed,
            authenticationTokenAvailable: false,
            tokenDiagnostic: nil,
            backendPath: oidcBackendPath,
            backendRequestSent: false,
            backendStatusCode: nil,
            tukiCredentialReceived: false,
            tukiCredentialStored: false,
            authenticationCompleted: false,
            failureDetail: failureDetail,
            sdkError: sdkError
        )
    }

    static func cancelled() -> FacebookLoginDiagnosticReport {
        FacebookLoginDiagnosticReport(
            sdkLogin: .cancelled,
            authenticationTokenAvailable: false,
            tokenDiagnostic: nil,
            backendPath: oidcBackendPath,
            backendRequestSent: false,
            backendStatusCode: nil,
            tukiCredentialReceived: false,
            tukiCredentialStored: false,
            authenticationCompleted: false,
            failureDetail: "User cancelled Facebook login",
            sdkError: nil
        )
    }

    var lines: [String] {
        var diagnosticLines = [
            "Facebook SDK login: \(sdkLogin.rawValue)",
            "AuthenticationToken available: \(yesNo(authenticationTokenAvailable))",
            "Selected token type: \(selectedTokenType.rawValue)",
            "Backend request: \(backendRequestSent ? "SENT" : "NOT SENT")",
            "Backend \(backendPath): \(backendStatusLine)",
            "TUKI credential received: \(yesNo(tukiCredentialReceived))",
            "TUKI credential stored: \(yesNo(tukiCredentialStored))",
            "Authentication completed: \(yesNo(authenticationCompleted))"
        ]

        if let failureDetail {
            diagnosticLines.append("Failure: \(failureDetail)")
        }

        if let sdkError {
            diagnosticLines.append("Facebook SDK error: \(sdkError.displayValue)")
        }

        return diagnosticLines
    }

    var logDescription: String {
        lines.joined(separator: "\n")
    }

    private var backendStatusLine: String {
        if let backendStatusCode {
            return "HTTP \(backendStatusCode)"
        }

        return backendRequestSent ? "NO HTTP RESPONSE" : "NOT SENT"
    }

    private var selectedTokenType: FacebookSelectedTokenDiagnosticType {
        tokenDiagnostic?.selectedTokenType ?? .none
    }

    private func yesNo(_ value: Bool) -> String {
        value ? "YES" : "NO"
    }
}

struct FacebookAuthDiagnosticResult: Equatable {
    let authResult: AuthResult
    let diagnostic: FacebookLoginDiagnosticReport
}
#endif
