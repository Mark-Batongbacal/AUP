import CoreLocation
import MapKit
import SwiftUI

/// The live-trip map: route polyline (current leg bold, upcoming legs dimmed), live GPS
/// position, destination pin, nearby TODA points, and the relevant jeepney route for
/// context. Ported from Android's `LiveTripMapScreen.kt` — kept as its own file/type there
/// too, deliberately separate from the general-purpose picker map, since trip tracking owns
/// a different camera model (follow-with-heading vs. static pick-a-point).
///
/// Camera has two modes, matching Android exactly: `followLocation` auto-pans/zooms to the
/// live GPS fix until the user touches the map, at which point a "Recenter" control (owned
/// by the parent, not this view) resumes it. Bumping `legOverviewRequestKey` fits the whole
/// current leg instead ("View Leg"), cancelling follow until the user recenters.
struct TukiLiveTripMapView: View {
    let legRoutePoints: [[CLLocationCoordinate2D]]
    let currentLegIndex: Int
    let destination: CLLocationCoordinate2D?
    let currentPosition: CLLocationCoordinate2D?
    let todaPoints: [TukiTodaPoint]
    let relevantRoutePoints: [CLLocationCoordinate2D]
    let recenterRequestKey: Int
    let legOverviewRequestKey: Int

    @Binding var followLocation: Bool

    @State private var cameraPosition: MapCameraPosition

    init(
        legRoutePoints: [[CLLocationCoordinate2D]],
        currentLegIndex: Int,
        destination: CLLocationCoordinate2D?,
        currentPosition: CLLocationCoordinate2D?,
        todaPoints: [TukiTodaPoint],
        relevantRoutePoints: [CLLocationCoordinate2D],
        recenterRequestKey: Int,
        legOverviewRequestKey: Int,
        followLocation: Binding<Bool>
    ) {
        self.legRoutePoints = legRoutePoints
        self.currentLegIndex = currentLegIndex
        self.destination = destination
        self.currentPosition = currentPosition
        self.todaPoints = todaPoints
        self.relevantRoutePoints = relevantRoutePoints
        self.recenterRequestKey = recenterRequestKey
        self.legOverviewRequestKey = legOverviewRequestKey
        self._followLocation = followLocation
        let initialCenter = currentPosition ?? destination ?? TukiMapCameraFraming.defaultCenter
        self._cameraPosition = State(initialValue: .camera(
            MapCamera(centerCoordinate: initialCenter, distance: 700, heading: 0, pitch: 0)
        ))
    }

    var body: some View {
        Map(position: $cameraPosition) {
            if !relevantRoutePoints.isEmpty {
                tukiRoutePolyline(relevantRoutePoints, emphasis: .faint)
            }
            ForEach(Array(legRoutePoints.enumerated()), id: \.offset) { index, points in
                if index >= currentLegIndex {
                    tukiRoutePolyline(points, emphasis: index == currentLegIndex ? .primary : .secondary)
                }
            }
            ForEach(todaPoints) { point in
                Annotation(point.name, coordinate: point.coordinate) {
                    Circle()
                        .fill(TukiPalette.orange)
                        .frame(width: 10, height: 10)
                        .overlay(Circle().stroke(.white, lineWidth: 1.5))
                }
                .annotationTitles(.hidden)
            }
            if let destination {
                Marker("Destination", coordinate: destination).tint(TukiPalette.orange)
            }
            if let currentPosition {
                Annotation("You", coordinate: currentPosition) {
                    ZStack {
                        Circle().fill(Color.blue.opacity(0.25)).frame(width: 26, height: 26)
                        Circle().fill(Color.blue).frame(width: 14, height: 14)
                        Circle().stroke(.white, lineWidth: 2).frame(width: 14, height: 14)
                    }
                }
                .annotationTitles(.hidden)
            }
        }
        .mapControls {}
        .ignoresSafeArea()
        .simultaneousGesture(
            DragGesture(minimumDistance: 2).onChanged { _ in followLocation = false }
        )
        .onChange(of: currentPosition.map { PointKey($0) }) { _, point in
            guard followLocation, let point else { return }
            withAnimation(.easeInOut(duration: 0.6)) {
                cameraPosition = .camera(MapCamera(centerCoordinate: point.coordinate, distance: 700, heading: 0, pitch: 0))
            }
        }
        .onChange(of: recenterRequestKey) { _, _ in
            followLocation = true
            if let currentPosition {
                withAnimation(.easeInOut(duration: 0.4)) {
                    cameraPosition = .camera(MapCamera(centerCoordinate: currentPosition, distance: 700, heading: 0, pitch: 0))
                }
            }
        }
        .onChange(of: legOverviewRequestKey) { _, _ in
            followLocation = false
            let points = legRoutePoints.indices.contains(currentLegIndex) ? legRoutePoints[currentLegIndex] : []
            withAnimation(.easeInOut(duration: 0.4)) {
                cameraPosition = .region(TukiMapCameraFraming.region(for: points))
            }
        }
    }
}

/// `CLLocationCoordinate2D` isn't `Equatable`, so `.onChange` needs a small wrapper to key off.
private struct PointKey: Equatable {
    let coordinate: CLLocationCoordinate2D
    init(_ coordinate: CLLocationCoordinate2D) { self.coordinate = coordinate }
    static func == (lhs: PointKey, rhs: PointKey) -> Bool {
        abs(lhs.coordinate.latitude - rhs.coordinate.latitude) < 0.000001
            && abs(lhs.coordinate.longitude - rhs.coordinate.longitude) < 0.000001
    }
}
