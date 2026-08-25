import CoreLocation
import MapKit
import SwiftUI

/// Shared MapKit building blocks so every screen draws route geometry and fits the
/// camera the same way. Mirrors Android's casing-under-fill route line styling
/// (`LiveTripRouteCasing`/`LiveTripRouteLayer` in `LiveTripMapScreen.kt`) and its
/// bounds-fitting behavior (`MapCameraFraming.kt` / `NavigationCameraGeometry.kt`).
/// Kept MapKit-native (not a MapLibre port) per the parity task's own allowance to
/// preserve behavior with a platform-appropriate implementation.
enum TukiRouteLineEmphasis {
    /// The active/current leg of the trip being tracked.
    case primary
    /// Other legs of the same selected route.
    case secondary
    /// Future/other-route legs, dimmed per Android's `updateFutureLegLayers`.
    case faint

    var fillColor: Color {
        switch self {
        case .primary: TukiPalette.dark
        case .secondary: TukiPalette.dark.opacity(0.55)
        case .faint: TukiPalette.gray.opacity(0.45)
        }
    }

    var casingColor: Color {
        switch self {
        case .primary: TukiPalette.cream
        case .secondary: TukiPalette.cream.opacity(0.7)
        case .faint: .clear
        }
    }

    var fillWidth: CGFloat {
        switch self {
        case .primary: 5
        case .secondary: 4
        case .faint: 3
        }
    }

    var casingWidth: CGFloat { fillWidth + 3 }
}

/// Renders a route line as a cream casing under a dark fill (or a plain dimmed line for
/// `.faint`), matching Android's two-layer route rendering. No-ops for fewer than 2 points.
@MapContentBuilder
func tukiRoutePolyline(
    _ coordinates: [CLLocationCoordinate2D],
    emphasis: TukiRouteLineEmphasis = .primary
) -> some MapContent {
    if coordinates.count >= 2 {
        if emphasis.casingWidth > 0 && emphasis != .faint {
            MapPolyline(coordinates: coordinates)
                .stroke(
                    emphasis.casingColor,
                    style: StrokeStyle(lineWidth: emphasis.casingWidth, lineCap: .round, lineJoin: .round)
                )
        }
        MapPolyline(coordinates: coordinates)
            .stroke(
                emphasis.fillColor,
                style: StrokeStyle(lineWidth: emphasis.fillWidth, lineCap: .round, lineJoin: .round)
            )
    }
}

extension TukiRouteLineEmphasis: Equatable {}

enum TukiMapCameraFraming {
    /// Porac/Angeles City/Dau/Mabalacat area center, matching Android's `DefaultMapCenter`.
    static let defaultCenter = CLLocationCoordinate2D(latitude: 15.1453, longitude: 120.5887)

    /// Fits a region around every valid coordinate given. Mirrors Android's
    /// `navigationCameraFrame()` degenerate-bounds handling: falls back to a centered
    /// default span when zero or one distinct point is available, otherwise expands a
    /// bounding box with proportional padding so the whole route/leg stays on-screen.
    static func region(
        for coordinates: [CLLocationCoordinate2D],
        paddingFraction: Double = 0.18,
        minimumSpanDegrees: Double = 0.01,
        fallbackSpanDegrees: Double = 0.02
    ) -> MKCoordinateRegion {
        let valid = coordinates.filter { CLLocationCoordinate2DIsValid($0) }

        guard let first = valid.first else {
            return MKCoordinateRegion(
                center: defaultCenter,
                span: MKCoordinateSpan(latitudeDelta: fallbackSpanDegrees, longitudeDelta: fallbackSpanDegrees)
            )
        }

        var minLat = first.latitude, maxLat = first.latitude
        var minLon = first.longitude, maxLon = first.longitude
        for coordinate in valid {
            minLat = min(minLat, coordinate.latitude)
            maxLat = max(maxLat, coordinate.latitude)
            minLon = min(minLon, coordinate.longitude)
            maxLon = max(maxLon, coordinate.longitude)
        }

        guard maxLat - minLat > 0 || maxLon - minLon > 0 else {
            return MKCoordinateRegion(
                center: first,
                span: MKCoordinateSpan(latitudeDelta: fallbackSpanDegrees, longitudeDelta: fallbackSpanDegrees)
            )
        }

        let latSpan = max(maxLat - minLat, minimumSpanDegrees) * (1 + paddingFraction * 2)
        let lonSpan = max(maxLon - minLon, minimumSpanDegrees) * (1 + paddingFraction * 2)
        let center = CLLocationCoordinate2D(latitude: (minLat + maxLat) / 2, longitude: (minLon + maxLon) / 2)

        return MKCoordinateRegion(
            center: center,
            span: MKCoordinateSpan(latitudeDelta: latSpan, longitudeDelta: lonSpan)
        )
    }
}
