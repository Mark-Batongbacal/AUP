import XCTest
@testable import TUKI

final class TukiHistoryAPITests: XCTestCase {
    override func tearDown() {
        HistoryMockURLProtocol.requestHandler = nil
        super.tearDown()
    }

    private func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [HistoryMockURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    private func makeAPI(session: URLSession) throws -> TukiHistoryAPI {
        TukiHistoryAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: StubCredentialStore(),
            session: session
        )
    }

    // MARK: - recentDateText

    func testRecentDateTextFallsBackWhenValueMissing() {
        XCTAssertEqual(recentDateText(nil), "Recent trip")
        XCTAssertEqual(recentDateText(""), "Recent trip")
    }

    func testRecentDateTextFallsBackWhenUnparseable() {
        XCTAssertEqual(recentDateText("not-a-date"), "Recent trip")
    }

    func testRecentDateTextFormatsValidISO8601Date() {
        XCTAssertEqual(recentDateText("2026-03-14T08:00:00Z"), "Mar 14, 2026")
    }

    func testRecentDateTextParsesFractionalSecondsDate() {
        XCTAssertEqual(recentDateText("2026-03-14T08:00:00.1234567Z"), "Mar 14, 2026")
    }

    // MARK: - Favorites drop invalid rows (matches Android's toFavoriteRouteOrNull)

    func testFavoritesDropsRowsMissingFavoriteTripIdOrRecommendationId() async throws {
        let api = try makeAPI(session: makeSession())
        HistoryMockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.absoluteString, "https://example.test/api/favorite-trips")
            let data = """
            [
              {"favoriteTripId":"fav-1","recommendationId":"rec-1","origin":"A","destination":"B","totalMinutes":20,"totalFare":15,"transferCount":1,"timesUsed":3,"note":"daily"},
              {"favoriteTripId":null,"recommendationId":"rec-2","origin":"C","destination":"D","totalMinutes":10,"totalFare":10,"transferCount":0,"timesUsed":1,"note":null},
              {"favoriteTripId":"fav-3","recommendationId":"","origin":"E","destination":"F","totalMinutes":10,"totalFare":10,"transferCount":0,"timesUsed":1,"note":null}
            ]
            """.data(using: .utf8)!
            let response = try XCTUnwrap(HTTPURLResponse(url: try XCTUnwrap(request.url), statusCode: 200, httpVersion: nil, headerFields: nil))
            return (response, data)
        }

        let result = await api.favorites()
        guard case .success(let routes) = result else { return XCTFail("expected success") }
        XCTAssertEqual(routes.count, 1)
        XCTAssertEqual(routes.first?.id, "fav-1")
        XCTAssertEqual(routes.first?.recommendationId, "rec-1")
        XCTAssertEqual(routes.first?.totalMinutes, 20)
    }

    func testHistoryReturnsNotAuthenticatedWithoutCredential() async throws {
        let api = TukiHistoryAPI(
            baseURL: try XCTUnwrap(URL(string: "https://example.test/")),
            credentialStore: StubCredentialStore(credential: nil),
            session: makeSession()
        )
        let result = await api.history()
        guard case .failure(.notAuthenticated) = result else { return XCTFail("expected notAuthenticated") }
    }
}

private struct StubCredentialStore: TukiCredentialStore {
    var credential: TukiCredential? = TukiCredential(loginResponse: LoginResponse(
        apiKey: "test-key", expiresAt: nil, authenticationScheme: nil, headerName: "X-API-Key"
    ))
    func save(_ credential: TukiCredential) throws {}
    func clear() throws {}
}

private final class HistoryMockURLProtocol: URLProtocol {
    static var requestHandler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let handler = HistoryMockURLProtocol.requestHandler else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        do {
            let (response, data) = try handler(request)
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: data)
            client?.urlProtocolDidFinishLoading(self)
        } catch {
            client?.urlProtocol(self, didFailWithError: error)
        }
    }

    override func stopLoading() {}
}
