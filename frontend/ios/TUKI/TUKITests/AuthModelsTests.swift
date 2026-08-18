//
//  AuthModelsTests.swift
//  TUKITests
//

import XCTest
@testable import TUKI

final class AuthModelsTests: XCTestCase {
    func testCredentialUsesBackendApiKeyAndDefaultHeader() {
        let credential = TukiCredential(
            loginResponse: LoginResponse(
                apiKey: "TUKI_API_KEY",
                expiresAt: "2026-08-18T00:00:00Z",
                authenticationScheme: "ApiKey",
                headerName: nil
            )
        )

        XCTAssertEqual(credential?.apiKey, "TUKI_API_KEY")
        XCTAssertEqual(credential?.expiresAt, "2026-08-18T00:00:00Z")
        XCTAssertEqual(credential?.authenticationScheme, "ApiKey")
        XCTAssertEqual(credential?.headerName, "X-API-Key")
    }

    func testCredentialRejectsMissingApiKey() {
        let credential = TukiCredential(
            loginResponse: LoginResponse(
                apiKey: nil,
                expiresAt: nil,
                authenticationScheme: nil,
                headerName: "X-API-Key"
            )
        )

        XCTAssertNil(credential)
    }

    #if DEBUG
    func testFacebookLoginDiagnosticReportsSelectedTokenType() {
        let tokenDiagnostic = FacebookLoginTokenDiagnostic(
            classicAccessTokenAvailable: false,
            authenticationTokenAvailable: true,
            selectedTokenType: .oidcAuthenticationToken
        )

        let report = FacebookLoginDiagnosticReport.sdkSuccess(
            authenticationTokenAvailable: true,
            tokenDiagnostic: tokenDiagnostic,
            backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
        )

        XCTAssertTrue(report.lines.contains("AuthenticationToken available: YES"))
        XCTAssertTrue(report.lines.contains("Selected token type: OIDC_AUTHENTICATION_TOKEN"))
        XCTAssertTrue(report.lines.contains("Backend /api/auth/facebook/oidc: NOT SENT"))
    }
    #endif
}
