package com.example.frontend

import com.example.frontend.core.location.RouteCoordinate
import com.example.frontend.navigation.navigationCameraFrame
import org.maplibre.android.camera.CameraPosition
import org.maplibre.android.camera.CameraUpdateFactory
import org.maplibre.android.geometry.LatLng
import org.maplibre.android.geometry.LatLngBounds
import org.maplibre.android.maps.MapLibreMap
import org.maplibre.android.maps.MapView
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.roundToInt
import kotlin.math.sin

internal data class MapCameraInsets(
    val left: Int,
    val top: Int,
    val right: Int,
    val bottom: Int
)

internal fun fitMapCameraToRoute(
    map: MapLibreMap,
    mapView: MapView,
    routePoints: List<LatLng>,
    anchors: List<LatLng> = emptyList(),
    insets: MapCameraInsets,
    fallbackZoom: Double = 15.0
) {
    val coordinates = (routePoints + anchors).map { point ->
        RouteCoordinate(point.latitude, point.longitude)
    }
    val frame = navigationCameraFrame(coordinates) ?: return

    if (mapView.width <= 0 || mapView.height <= 0) {
        mapView.post {
            fitMapCameraToRoute(map, mapView, routePoints, anchors, insets, fallbackZoom)
        }
        return
    }

    val routeDetailsBearing = if (insets.isSymmetricPreviewFrame()) {
        routeTopToBottomCameraBearing(routePoints.firstOrNull(), routePoints.lastOrNull())
    } else {
        null
    }

    if (frame.distinctPointCount == 1) {
        val center = LatLng(
            (frame.south + frame.north) / 2.0,
            (frame.west + frame.east) / 2.0
        )
        map.animateCamera(
            CameraUpdateFactory.newCameraPosition(
                CameraPosition.Builder()
                    .target(center)
                    .zoom(fallbackZoom)
                    .bearing(routeDetailsBearing ?: map.cameraPosition.bearing)
                    .build()
            ),
            450
        )
        return
    }

    val bounds = LatLngBounds.Builder()
        .include(LatLng(frame.south, frame.west))
        .include(LatLng(frame.north, frame.east))
        .build()
    val horizontalScale = paddingScale(insets.left + insets.right, mapView.width)
    val verticalScale = paddingScale(insets.top + insets.bottom, mapView.height)
    val left = (insets.left * horizontalScale).roundToInt()
    val top = (insets.top * verticalScale).roundToInt()
    val right = (insets.right * horizontalScale).roundToInt()
    val bottom = (insets.bottom * verticalScale).roundToInt()

    if (routeDetailsBearing != null) {
        val fittedCamera = map.getCameraForLatLngBounds(
            bounds,
            intArrayOf(left, top, right, bottom),
            routeDetailsBearing,
            map.cameraPosition.tilt
        )
        if (fittedCamera != null) {
            map.animateCamera(
                CameraUpdateFactory.newCameraPosition(fittedCamera),
                500
            )
        } else {
            map.animateCamera(
                CameraUpdateFactory.newLatLngBounds(bounds, left, top, right, bottom),
                500
            )
        }
    } else {
        map.animateCamera(
            CameraUpdateFactory.newLatLngBounds(bounds, left, top, right, bottom),
            500
        )
    }
}

internal fun routeTopToBottomCameraBearing(start: LatLng?, destination: LatLng?): Double? {
    if (start == null || destination == null) return null
    if (start.latitude == destination.latitude && start.longitude == destination.longitude) return null

    val startLatitude = Math.toRadians(start.latitude)
    val destinationLatitude = Math.toRadians(destination.latitude)
    val longitudeDelta = Math.toRadians(destination.longitude - start.longitude)
    val y = sin(longitudeDelta) * cos(destinationLatitude)
    val x = cos(startLatitude) * sin(destinationLatitude) -
        sin(startLatitude) * cos(destinationLatitude) * cos(longitudeDelta)
    val routeBearing = (Math.toDegrees(atan2(y, x)) + 360.0) % 360.0

    // The camera points the opposite travel direction upward so the start/current-location marker
    // reads at the top of route-detail previews while the destination reads at the bottom.
    return (routeBearing + 180.0) % 360.0
}

private fun MapCameraInsets.isSymmetricPreviewFrame(): Boolean =
    left == right && top == bottom

private fun paddingScale(requestedPadding: Int, availableSize: Int): Double {
    if (requestedPadding <= 0) return 1.0
    val maximumPadding = (availableSize * 0.72).coerceAtLeast(1.0)
    return (maximumPadding / requestedPadding).coerceAtMost(1.0)
}
