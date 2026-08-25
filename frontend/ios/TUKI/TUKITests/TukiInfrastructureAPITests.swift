import CoreLocation
import XCTest
@testable import TUKI

final class TukiInfrastructureAPITests: XCTestCase {
    override func tearDown() {
        InfraMockURLProtocol.requestHandler = nil
        super.tearDown()
    }

    private func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [InfraMockURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    func testTodaPointDecodesFromBackendShapedJSON() throws {
        let json = """
        {
          "tricyclePointId": 7,
          "stopId": null,
          "pointCode": "DAU-01",
          "pointName": "Dau Terminal TODA",
          "description": null,
          "address": null,
          "operatorName": "Dau TODA",
          "centerLatitude": 15.1900,
          "centerLongitude": 120.5400,
          "radiusMeters": 50,
          "baseFare": 15.0,
          "farePerKilometer": 3.0,
          "averageWaitingTimeSeconds": 120,
          "serviceStartTime": null,
          "serviceEndTime": null,
          "isActive": true
        }
        """.data(using: .utf8)!

        let point = try JSONDecoder().decode(TukiTodaPoint.self, from: json)
        XCTAssertEqual(point.id, 7)
        XCTAssertEqual(point.name, "Dau Terminal TODA")
        XCTAssertEqual(point.latitude, 15.1900, accuracy: 0.0001)
        XCTAssertEqual(point.longitude, 120.5400, accuracy: 0.0001)
        XCTAssertTrue(point.isActive)
    }

    func testActiveTodaPointsFetchesFromExpectedEndpoint() async throws {
        let api = TukiInfrastructureAPI(baseURL: try XCTUnwrap(URL(string: "https://example.test/")), session: makeSession())

        InfraMockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.httpMethod, "GET")
            XCTAssertEqual(request.url?.absoluteString, "https://example.test/api/tricycle-points")
            let data = """
            [{"tricyclePointId":1,"stopId":null,"pointCode":"A","pointName":"Point A","description":null,"address":null,"operatorName":null,"centerLatitude":15.1,"centerLongitude":120.5,"radiusMeters":40,"baseFare":null,"farePerKilometer":null,"averageWaitingTimeSeconds":null,"serviceStartTime":null,"serviceEndTime":null,"isActive":true}]
            """.data(using: .utf8)!
            let response = try XCTUnwrap(HTTPURLResponse(url: try XCTUnwrap(request.url), statusCode: 200, httpVersion: nil, headerFields: nil))
            return (response, data)
        }

        let result = await api.activeTodaPoints()
        guard case .success(let points) = result else { return XCTFail("expected success") }
        XCTAssertEqual(points.map(\.name), ["Point A"])
    }

    func testRoutePointsFetchesFromExpectedEndpointAndMapsCoordinates() async throws {
        let api = TukiInfrastructureAPI(baseURL: try XCTUnwrap(URL(string: "https://example.test/")), session: makeSession())

        InfraMockURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.absoluteString, "https://example.test/api/transport-routes/42/points")
            let data = """
            {"routeId":42,"points":[{"routePointId":1,"pointOrder":0,"latitude":15.10,"longitude":120.50},{"routePointId":2,"pointOrder":1,"latitude":15.11,"longitude":120.51}]}
            """.data(using: .utf8)!
            let response = try XCTUnwrap(HTTPURLResponse(url: try XCTUnwrap(request.url), statusCode: 200, httpVersion: nil, headerFields: nil))
            return (response, data)
        }

        let result = await api.routePoints(routeId: "42")
        guard case .success(let points) = result else { return XCTFail("expected success") }
        XCTAssertEqual(points.count, 2)
        XCTAssertEqual(points[0].latitude, 15.10, accuracy: 0.0001)
        XCTAssertEqual(points[1].longitude, 120.51, accuracy: 0.0001)
    }

    func testRoutePointsReturnsFailureOnServerError() async throws {
        let api = TukiInfrastructureAPI(baseURL: try XCTUnwrap(URL(string: "https://example.test/")), session: makeSession())

        InfraMockURLProtocol.requestHandler = { request in
            let response = try XCTUnwrap(HTTPURLResponse(url: try XCTUnwrap(request.url), statusCode: 500, httpVersion: nil, headerFields: nil))
            return (response, Data())
        }

        let result = await api.routePoints(routeId: "42")
        guard case .failure = result else { return XCTFail("expected failure") }
    }
}

private final class InfraMockURLProtocol: URLProtocol {
    static var requestHandler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let handler = InfraMockURLProtocol.requestHandler else {
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
