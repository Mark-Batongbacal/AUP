import CoreLocation
import MapKit
import XCTest
@testable import TUKI

final class TukiMapKitSupportTests: XCTestCase {
    func testEmptyCoordinatesFallsBackToDefaultCenter() {
        let region = TukiMapCameraFraming.region(for: [])
        XCTAssertEqual(region.center.latitude, TukiMapCameraFraming.defaultCenter.latitude, accuracy: 0.0001)
        XCTAssertEqual(region.center.longitude, TukiMapCameraFraming.defaultCenter.longitude, accuracy: 0.0001)
    }

    func testSinglePointCentersOnThatPointWithFallbackSpan() {
        let point = CLLocationCoordinate2D(latitude: 15.2, longitude: 120.6)
        let region = TukiMapCameraFraming.region(for: [point], fallbackSpanDegrees: 0.05)
        XCTAssertEqual(region.center.latitude, 15.2, accuracy: 0.0001)
        XCTAssertEqual(region.center.longitude, 120.6, accuracy: 0.0001)
        XCTAssertEqual(region.span.latitudeDelta, 0.05, accuracy: 0.0001)
    }

    func testDuplicatePointsAreTreatedAsASinglePoint() {
        let point = CLLocationCoordinate2D(latitude: 15.2, longitude: 120.6)
        let region = TukiMapCameraFraming.region(for: [point, point, point], fallbackSpanDegrees: 0.05)
        XCTAssertEqual(region.span.latitudeDelta, 0.05, accuracy: 0.0001)
    }

    func testInvalidCoordinatesAreFilteredOut() {
        let valid = CLLocationCoordinate2D(latitude: 15.2, longitude: 120.6)
        let invalid = CLLocationCoordinate2D(latitude: 999, longitude: 999)
        let region = TukiMapCameraFraming.region(for: [invalid, valid], fallbackSpanDegrees: 0.05)
        XCTAssertEqual(region.center.latitude, 15.2, accuracy: 0.0001)
        XCTAssertEqual(region.center.longitude, 120.6, accuracy: 0.0001)
    }

    func testBoundingBoxFitsAllPointsWithPadding() {
        let points = [
            CLLocationCoordinate2D(latitude: 15.10, longitude: 120.50),
            CLLocationCoordinate2D(latitude: 15.20, longitude: 120.60)
        ]
        let region = TukiMapCameraFraming.region(for: points, paddingFraction: 0.1, minimumSpanDegrees: 0.001)

        // Bounding box must be fully contained within the returned region.
        let minLat = region.center.latitude - region.span.latitudeDelta / 2
        let maxLat = region.center.latitude + region.span.latitudeDelta / 2
        let minLon = region.center.longitude - region.span.longitudeDelta / 2
        let maxLon = region.center.longitude + region.span.longitudeDelta / 2

        XCTAssertLessThanOrEqual(minLat, 15.10)
        XCTAssertGreaterThanOrEqual(maxLat, 15.20)
        XCTAssertLessThanOrEqual(minLon, 120.50)
        XCTAssertGreaterThanOrEqual(maxLon, 120.60)
    }

    func testMinimumSpanIsRespectedForNearlyIdenticalPoints() {
        let points = [
            CLLocationCoordinate2D(latitude: 15.1000, longitude: 120.5000),
            CLLocationCoordinate2D(latitude: 15.1001, longitude: 120.5001)
        ]
        let region = TukiMapCameraFraming.region(for: points, minimumSpanDegrees: 0.02)
        XCTAssertGreaterThanOrEqual(region.span.latitudeDelta, 0.02)
        XCTAssertGreaterThanOrEqual(region.span.longitudeDelta, 0.02)
    }

    func testRouteLineEmphasisWidthsDescendFromPrimaryToFaint() {
        XCTAssertGreaterThan(TukiRouteLineEmphasis.primary.fillWidth, TukiRouteLineEmphasis.secondary.fillWidth)
        XCTAssertGreaterThan(TukiRouteLineEmphasis.secondary.fillWidth, TukiRouteLineEmphasis.faint.fillWidth)
    }
}
