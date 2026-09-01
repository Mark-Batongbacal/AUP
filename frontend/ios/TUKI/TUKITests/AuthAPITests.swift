//
//  AuthAPITests.swift
//  TUKITests
//

import XCTest
import Security
@testable import TUKI

final class AuthAPITests: XCTestCase {
    override func tearDown() {
        MockURLProtocol.requestHandler = nil
        super.tearDown()
    }

    func testGoogleLoginPostsIDTokenAndSavesReturnedTukiCredential() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.httpMethod, "POST")
            try Self.assertAbsoluteHTTPURL(
                request.url,
                expected: "https://example.test/api/auth/google"
            )

            let body = try XCTUnwrap(request.bodyData)
            let json = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: String])
            XCTAssertEqual(json, ["idToken": "GOOGLE_ID_TOKEN"])

            let data = try XCTUnwrap(
                """
                {
                  "apiKey": "TUKI_API_KEY",
                  "expiresAt": "2026-08-18T00:00:00Z",
                  "authenticationScheme": "ApiKey",
                  "headerName": "X-API-Key"
                }
                """.data(using: .utf8)
            )
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.loginWithGoogle(idToken: "GOOGLE_ID_TOKEN")

        XCTAssertEqual(result, .success)
        XCTAssertEqual(store.savedCredential?.apiKey, "TUKI_API_KEY")
        XCTAssertEqual(store.savedCredential?.headerName, "X-API-Key")
    }

    func testPasswordLoginPostsCredentialsAndSavesReturnedTukiCredential() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.httpMethod, "POST")
            XCTAssertEqual(request.url?.absoluteString, "https://example.test/api/auth/login")

            let body = try XCTUnwrap(request.bodyData)
            let json = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: String])
            XCTAssertEqual(json, [
                "userName": "admin",
                "password": "correct-password"
            ])

            let data = try XCTUnwrap(Self.validLoginResponseData)
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.login(userName: "admin", password: "correct-password")

        XCTAssertEqual(result, .success)
        XCTAssertEqual(store.savedCredential?.apiKey, "TUKI_API_KEY")
        XCTAssertEqual(store.savedCredential?.headerName, "X-API-Key")
    }

    func testGuestLoginPostsNoBodyAndSavesReturnedTukiCredential() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.httpMethod, "POST")
            try Self.assertAbsoluteHTTPURL(request.url, expected: "https://example.test/api/auth/guest")
            XCTAssertNil(request.bodyData)

            let data = try XCTUnwrap(Self.validLoginResponseData)
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )
            return (response, data)
        }

        let result = await api.loginAsGuest()

        XCTAssertEqual(result, .success)
        XCTAssertEqual(store.savedCredential?.apiKey, "TUKI_API_KEY")
        XCTAssertEqual(store.savedCredential?.headerName, "X-API-Key")
    }

    func testGuestLoginReturns401MessageAndDoesNotSaveCredential() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let response = try XCTUnwrap(
                HTTPURLResponse(url: try XCTUnwrap(request.url), statusCode: 401, httpVersion: nil, headerFields: nil)
            )
            return (response, Data())
        }

        let result = await api.loginAsGuest()

        XCTAssertEqual(result, .failure("Guest access could not be started. Please try again."))
        XCTAssertNil(store.savedCredential)
    }

    func testGuestLoginReturns404MessageWhenEndpointUnavailable() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let response = try XCTUnwrap(
                HTTPURLResponse(url: try XCTUnwrap(request.url), statusCode: 404, httpVersion: nil, headerFields: nil)
            )
            return (response, Data())
        }

        let result = await api.loginAsGuest()

        XCTAssertEqual(result, .failure("Guest access is not available on this server version."))
        XCTAssertNil(store.savedCredential)
    }

    func testGuestLoginWhenNetworkFailsReturnsUserFacingError() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { _ in
            throw URLError(.cannotConnectToHost)
        }

        let result = await api.loginAsGuest()

        XCTAssertEqual(result, .failure("Network error. Check your connection and try again."))
        XCTAssertNil(store.savedCredential)
    }

    func testGoogleLoginDoesNotSaveCredentialWhenBackendRejectsToken() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 401,
                    httpVersion: nil,
                    headerFields: nil
                )
            )

            return (response, Data())
        }

        let result = await api.loginWithGoogle(idToken: "GOOGLE_ID_TOKEN")

        XCTAssertEqual(result, .failure("Google login was rejected. Try again."))
        XCTAssertNil(store.savedCredential)
    }

    func testGoogleLoginWhenNetworkFailsReturnsUserFacingError() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { _ in
            throw URLError(.cannotConnectToHost)
        }

        let result = await api.loginWithGoogle(idToken: "GOOGLE_ID_TOKEN")

        XCTAssertEqual(result, .failure("Network error. Check your connection and try again."))
        XCTAssertNil(store.savedCredential)
    }

    func testFacebookLoginPostsAccessTokenAndSavesReturnedTukiCredential() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.httpMethod, "POST")
            XCTAssertEqual(request.url?.absoluteString, "https://example.test/api/auth/facebook")

            let body = try XCTUnwrap(request.bodyData)
            let json = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: String])
            XCTAssertEqual(json, ["accessToken": "FACEBOOK_ACCESS_TOKEN"])

            let data = try XCTUnwrap(Self.validLoginResponseData)
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.loginWithFacebook(accessToken: "FACEBOOK_ACCESS_TOKEN")

        XCTAssertEqual(result, .success)
        XCTAssertEqual(store.savedCredential?.apiKey, "TUKI_API_KEY")
        XCTAssertEqual(store.savedCredential?.headerName, "X-API-Key")
    }

    func testFacebookOidcLoginPostsAuthenticationTokenNonceAndSavesReturnedTukiCredential() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.httpMethod, "POST")
            try Self.assertAbsoluteHTTPURL(
                request.url,
                expected: "https://example.test/api/auth/facebook/oidc"
            )

            let body = try XCTUnwrap(request.bodyData)
            let json = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: String])
            XCTAssertEqual(json, [
                "idToken": "FACEBOOK_AUTHENTICATION_TOKEN",
                "nonce": "FACEBOOK_LOGIN_NONCE"
            ])

            let data = try XCTUnwrap(Self.validLoginResponseData)
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.loginWithFacebookOidc(
            idToken: "FACEBOOK_AUTHENTICATION_TOKEN",
            nonce: "FACEBOOK_LOGIN_NONCE"
        )

        XCTAssertEqual(result, .success)
        XCTAssertEqual(store.savedCredential?.apiKey, "TUKI_API_KEY")
        XCTAssertEqual(store.savedCredential?.headerName, "X-API-Key")
    }

    func testFacebookLoginDoesNotSaveCredentialWhenBackendRejectsToken() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 401,
                    httpVersion: nil,
                    headerFields: nil
                )
            )

            return (response, Data())
        }

        let result = await api.loginWithFacebook(accessToken: "FACEBOOK_ACCESS_TOKEN")

        XCTAssertEqual(result, .failure("Facebook login was rejected. Try again."))
        XCTAssertNil(store.savedCredential)
    }

    func testFacebookOidcLoginDoesNotSaveCredentialWhenBackendRejectsToken() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.absoluteString, "https://example.test/api/auth/facebook/oidc")
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 401,
                    httpVersion: nil,
                    headerFields: nil
                )
            )

            return (response, Data())
        }

        let result = await api.loginWithFacebookOidc(
            idToken: "FACEBOOK_AUTHENTICATION_TOKEN",
            nonce: "FACEBOOK_LOGIN_NONCE"
        )

        XCTAssertEqual(result, .failure("Facebook login was rejected. Try again."))
        XCTAssertNil(store.savedCredential)
    }

    func testRecentJourneysUsesCredentialAndMapsCompletedCancelledTrips() async throws {
        let store = RecordingCredentialStore()
        store.savedCredential = TukiCredential(
            loginResponse: LoginResponse(
                apiKey: "TUKI_API_KEY",
                expiresAt: nil,
                authenticationScheme: "ApiKey",
                headerName: "X-Api-Key"
            )
        )
        let api = TukiJourneyAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            try Self.assertAbsoluteHTTPURL(
                request.url,
                expected: "https://example.test/api/trips/recent"
            )
            XCTAssertEqual(request.value(forHTTPHeaderField: "X-Api-Key"), "TUKI_API_KEY")

            let data = try XCTUnwrap(
                """
                [
                  {
                    "passengerTripId": "trip-1",
                    "status": "COMPLETED",
                    "originName": "Sta. Rita",
                    "destinationName": "Guagua Town",
                    "startedAt": "2026-08-20T01:00:00Z",
                    "completedAt": "2026-08-20T01:30:00Z",
                    "createdAt": "2026-08-20T00:55:00Z",
                    "rerouted": true,
                    "rerouteCount": 1,
                    "recommendation": {
                      "totalMinutes": 22,
                      "legs": [
                        {
                          "legOrder": 0,
                          "transportMode": { "name": "Jeepney" },
                          "route": null,
                          "fromStop": null,
                          "toStop": null,
                          "fromName": "Sta. Rita",
                          "toName": "Guagua Plaza",
                          "estimatedMinutes": 14,
                          "estimatedFare": 15
                        }
                      ]
                    }
                  },
                  {
                    "passengerTripId": "trip-2",
                    "status": "CANCELLED",
                    "originName": "Porac",
                    "destinationName": "Dau Terminal",
                    "startedAt": null,
                    "completedAt": "2026-08-20T02:00:00Z",
                    "createdAt": "2026-08-20T01:50:00Z",
                    "rerouted": false,
                    "rerouteCount": 0,
                    "recommendation": null
                  }
                ]
                """.data(using: .utf8)
            )
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.recentJourneys()

        switch result {
        case .success(let journeys):
            XCTAssertEqual(journeys.count, 2)
            XCTAssertEqual(journeys[0].status, "Completed")
            XCTAssertTrue(journeys[0].wasRerouted)
            XCTAssertEqual(journeys[0].steps.first?.mode, "Jeepney")
            XCTAssertEqual(journeys[1].status, "Cancelled")
        case .failure(let error):
            XCTFail("Expected recent journeys, got \(error)")
        }
    }

    func testRecentJourneysWithoutCredentialDoesNotSendRequest() async throws {
        let store = RecordingCredentialStore()
        let api = TukiJourneyAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )
        var requestSent = false
        MockURLProtocol.requestHandler = { request in
            requestSent = true
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: nil
                )
            )
            return (response, Data())
        }

        let result = await api.recentJourneys()

        XCTAssertEqual(result, .failure(.notAuthenticated))
        XCTAssertFalse(requestSent)
    }

    func testFavoritesUsesCredentialAndMapsSavedRoutes() async throws {
        let store = RecordingCredentialStore()
        store.savedCredential = TukiCredential(
            loginResponse: LoginResponse(
                apiKey: "TUKI_API_KEY",
                expiresAt: nil,
                authenticationScheme: "ApiKey",
                headerName: "X-Api-Key"
            )
        )
        let api = TukiJourneyAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            try Self.assertAbsoluteHTTPURL(
                request.url,
                expected: "https://example.test/api/favorite-trips"
            )
            XCTAssertEqual(request.value(forHTTPHeaderField: "X-Api-Key"), "TUKI_API_KEY")

            let data = try XCTUnwrap(
                """
                [
                  {
                    "favoriteTripId": "favorite-1",
                    "userId": "user-1",
                    "recommendationId": "recommendation-1",
                    "origin": "Porac",
                    "destination": "Angeles",
                    "timesUsed": 4,
                    "note": "work",
                    "createdAt": "2026-08-20T01:00:00Z"
                  }
                ]
                """.data(using: .utf8)
            )
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.favorites()

        switch result {
        case .success(let favorites):
            XCTAssertEqual(favorites.first?.origin, "Porac")
            XCTAssertEqual(favorites.first?.destination, "Angeles")
            XCTAssertEqual(favorites.first?.timesUsed, 4)
        case .failure(let error):
            XCTFail("Expected favorites, got \(error)")
        }
    }

    func testFacebookLoginDoesNotSaveCredentialWhenBackendResponseIsMalformed() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let data = try XCTUnwrap(
                """
                {
                  "expiresAt": "2026-08-18T00:00:00Z",
                  "authenticationScheme": "ApiKey",
                  "headerName": "X-API-Key"
                }
                """.data(using: .utf8)
            )
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.loginWithFacebook(accessToken: "FACEBOOK_ACCESS_TOKEN")

        XCTAssertEqual(result, .failure("The server returned an invalid login response."))
        XCTAssertNil(store.savedCredential)
    }

    #if DEBUG
    func testFacebookLoginDiagnosticCompletesOnlyAfterCredentialIsStored() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.absoluteString, "https://example.test/api/auth/facebook/oidc")
            let data = try XCTUnwrap(Self.validLoginResponseData)
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.loginWithFacebookOidc(
            idToken: "FACEBOOK_AUTHENTICATION_TOKEN",
            nonce: "FACEBOOK_LOGIN_NONCE",
            diagnostic: .sdkSuccess(
                authenticationTokenAvailable: true,
                tokenDiagnostic: FacebookLoginTokenDiagnostic(
                    classicAccessTokenAvailable: false,
                    authenticationTokenAvailable: true,
                    selectedTokenType: .oidcAuthenticationToken
                ),
                backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
            )
        )

        XCTAssertEqual(result.authResult, .success)
        XCTAssertEqual(result.diagnostic.backendStatusCode, 200)
        XCTAssertTrue(result.diagnostic.tukiCredentialReceived)
        XCTAssertTrue(result.diagnostic.tukiCredentialStored)
        XCTAssertTrue(result.diagnostic.authenticationCompleted)
        XCTAssertTrue(result.diagnostic.lines.contains("Authentication completed: YES"))
        XCTAssertTrue(result.diagnostic.lines.contains("Selected token type: OIDC_AUTHENTICATION_TOKEN"))
        XCTAssertTrue(result.diagnostic.lines.contains("Backend /api/auth/facebook/oidc: HTTP 200"))
        XCTAssertFalse(result.diagnostic.logDescription.contains("FACEBOOK_AUTHENTICATION_TOKEN"))
        XCTAssertEqual(store.savedCredential?.apiKey, "TUKI_API_KEY")
    }

    func testFacebookLoginDiagnosticCapturesHTTP401BeforeParsingResponse() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let data = try XCTUnwrap("not-json".data(using: .utf8))
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 401,
                    httpVersion: nil,
                    headerFields: nil
                )
            )

            return (response, data)
        }

        let result = await api.loginWithFacebookOidc(
            idToken: "FACEBOOK_AUTHENTICATION_TOKEN",
            nonce: "FACEBOOK_LOGIN_NONCE",
            diagnostic: .sdkSuccess(
                authenticationTokenAvailable: true,
                tokenDiagnostic: FacebookLoginTokenDiagnostic(
                    classicAccessTokenAvailable: false,
                    authenticationTokenAvailable: true,
                    selectedTokenType: .oidcAuthenticationToken
                ),
                backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
            )
        )

        XCTAssertEqual(result.authResult, .failure("Facebook login was rejected. Try again."))
        XCTAssertEqual(result.diagnostic.backendStatusCode, 401)
        XCTAssertTrue(result.diagnostic.backendRequestSent)
        XCTAssertFalse(result.diagnostic.tukiCredentialReceived)
        XCTAssertEqual(result.diagnostic.failureDetail, "HTTP 401: backend rejected Facebook token")
        XCTAssertTrue(result.diagnostic.lines.contains("Backend /api/auth/facebook/oidc: HTTP 401"))
        XCTAssertFalse(result.diagnostic.logDescription.contains("FACEBOOK_AUTHENTICATION_TOKEN"))
    }

    func testFacebookLoginDiagnosticCapturesHTTP502() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 502,
                    httpVersion: nil,
                    headerFields: nil
                )
            )

            return (response, Data())
        }

        let result = await api.loginWithFacebookOidc(
            idToken: "FACEBOOK_AUTHENTICATION_TOKEN",
            nonce: "FACEBOOK_LOGIN_NONCE",
            diagnostic: .sdkSuccess(
                authenticationTokenAvailable: true,
                tokenDiagnostic: FacebookLoginTokenDiagnostic(
                    classicAccessTokenAvailable: false,
                    authenticationTokenAvailable: true,
                    selectedTokenType: .oidcAuthenticationToken
                ),
                backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
            )
        )

        XCTAssertEqual(result.authResult, .failure("Login is unavailable. Try again later."))
        XCTAssertEqual(result.diagnostic.backendStatusCode, 502)
        XCTAssertEqual(result.diagnostic.failureDetail, "HTTP 502: backend gateway/upstream failure")
        XCTAssertTrue(result.diagnostic.lines.contains("Backend /api/auth/facebook/oidc: HTTP 502"))
        XCTAssertFalse(result.diagnostic.authenticationCompleted)
    }

    func testFacebookLoginDiagnosticCapturesBackendConnectionFailure() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { _ in
            throw URLError(.cannotConnectToHost)
        }

        let result = await api.loginWithFacebookOidc(
            idToken: "FACEBOOK_AUTHENTICATION_TOKEN",
            nonce: "FACEBOOK_LOGIN_NONCE",
            diagnostic: .sdkSuccess(
                authenticationTokenAvailable: true,
                tokenDiagnostic: FacebookLoginTokenDiagnostic(
                    classicAccessTokenAvailable: false,
                    authenticationTokenAvailable: true,
                    selectedTokenType: .oidcAuthenticationToken
                ),
                backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
            )
        )

        XCTAssertEqual(result.authResult, .failure("Network error. Check your connection and try again."))
        XCTAssertTrue(result.diagnostic.backendRequestSent)
        XCTAssertNil(result.diagnostic.backendStatusCode)
        XCTAssertEqual(result.diagnostic.failureDetail, "Backend connection failure: URLError code \(URLError.Code.cannotConnectToHost.rawValue)")
        XCTAssertTrue(result.diagnostic.lines.contains("Backend /api/auth/facebook/oidc: NO HTTP RESPONSE"))
        XCTAssertFalse(result.diagnostic.tukiCredentialStored)
    }

    func testFacebookLoginDiagnosticCapturesMalformedBackendResponse() async throws {
        let store = RecordingCredentialStore()
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let data = try XCTUnwrap("not-json".data(using: .utf8))
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.loginWithFacebookOidc(
            idToken: "FACEBOOK_AUTHENTICATION_TOKEN",
            nonce: "FACEBOOK_LOGIN_NONCE",
            diagnostic: .sdkSuccess(
                authenticationTokenAvailable: true,
                tokenDiagnostic: FacebookLoginTokenDiagnostic(
                    classicAccessTokenAvailable: false,
                    authenticationTokenAvailable: true,
                    selectedTokenType: .oidcAuthenticationToken
                ),
                backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
            )
        )

        XCTAssertEqual(result.authResult, .failure("The server returned an invalid login response."))
        XCTAssertEqual(result.diagnostic.backendStatusCode, 200)
        XCTAssertEqual(result.diagnostic.failureDetail, "Malformed backend response: decoding failed")
        XCTAssertFalse(result.diagnostic.tukiCredentialReceived)
        XCTAssertFalse(result.diagnostic.authenticationCompleted)
    }

    func testFacebookLoginDiagnosticCapturesKeychainStorageFailure() async throws {
        let store = RecordingCredentialStore()
        store.saveError = KeychainCredentialStoreError.unhandled(errSecAuthFailed)
        let api = TukiAuthAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: store,
            session: makeSession()
        )

        MockURLProtocol.requestHandler = { request in
            let data = try XCTUnwrap(Self.validLoginResponseData)
            let response = try XCTUnwrap(
                HTTPURLResponse(
                    url: try XCTUnwrap(request.url),
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Content-Type": "application/json"]
                )
            )

            return (response, data)
        }

        let result = await api.loginWithFacebookOidc(
            idToken: "FACEBOOK_AUTHENTICATION_TOKEN",
            nonce: "FACEBOOK_LOGIN_NONCE",
            diagnostic: .sdkSuccess(
                authenticationTokenAvailable: true,
                tokenDiagnostic: FacebookLoginTokenDiagnostic(
                    classicAccessTokenAvailable: false,
                    authenticationTokenAvailable: true,
                    selectedTokenType: .oidcAuthenticationToken
                ),
                backendPath: FacebookLoginDiagnosticReport.oidcBackendPath
            )
        )

        XCTAssertEqual(result.authResult, .failure("TUKI could not securely save your login."))
        XCTAssertEqual(result.diagnostic.backendStatusCode, 200)
        XCTAssertTrue(result.diagnostic.tukiCredentialReceived)
        XCTAssertFalse(result.diagnostic.tukiCredentialStored)
        XCTAssertFalse(result.diagnostic.authenticationCompleted)
        XCTAssertEqual(result.diagnostic.failureDetail, "Keychain storage failure: OSStatus \(errSecAuthFailed)")
    }
    #endif

    private func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [MockURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    private static func assertAbsoluteHTTPURL(
        _ url: URL?,
        expected: String,
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        let url = try XCTUnwrap(url, file: file, line: line)
        XCTAssertEqual(url.absoluteString, expected, file: file, line: line)
        XCTAssertTrue(url.isAbsoluteHTTPURL, file: file, line: line)
    }

    private static var validLoginResponseData: Data? {
        """
        {
          "apiKey": "TUKI_API_KEY",
          "expiresAt": "2026-08-18T00:00:00Z",
          "authenticationScheme": "ApiKey",
          "headerName": "X-API-Key"
        }
        """.data(using: .utf8)
    }
}

private final class RecordingCredentialStore: TukiCredentialStore {
    var savedCredential: TukiCredential?
    var saveError: Error?

    var credential: TukiCredential? {
        savedCredential
    }

    func save(_ credential: TukiCredential) throws {
        if let saveError {
            throw saveError
        }

        savedCredential = credential
    }

    func clear() throws {
        savedCredential = nil
    }
}

private final class MockURLProtocol: URLProtocol {
    static var requestHandler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let requestHandler = Self.requestHandler else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }

        do {
            let (response, data) = try requestHandler(request)
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: data)
            client?.urlProtocolDidFinishLoading(self)
        } catch {
            client?.urlProtocol(self, didFailWithError: error)
        }
    }

    override func stopLoading() {}
}

private extension URLRequest {
    var bodyData: Data? {
        if let httpBody {
            return httpBody
        }

        guard let httpBodyStream else {
            return nil
        }

        httpBodyStream.open()
        defer { httpBodyStream.close() }

        var data = Data()
        var buffer = [UInt8](repeating: 0, count: 1024)
        while httpBodyStream.hasBytesAvailable {
            let bytesRead = httpBodyStream.read(&buffer, maxLength: buffer.count)
            if bytesRead < 0 {
                return nil
            }
            if bytesRead == 0 {
                break
            }
            data.append(buffer, count: bytesRead)
        }

        return data
    }
}
