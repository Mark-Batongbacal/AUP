package com.example.frontend

import com.example.frontend.core.location.RouteCoordinate
import com.example.frontend.navigation.navigationCameraFrame
import org.maplibre.android.camera.CameraPosition
import org.maplibre.android.camera.CameraUpdateFactory
import org.maplibre.android.geometry.LatLng
import org.maplibre.android.geometry.LatLngBounds
import org.maplibre.android.maps.MapLibreMap
import org.maplibre.android.maps.MapView
import kotlin.math.roundToInt

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

    if (frame.distinctPointCount == 1) {
        val center = LatLng(
            (frame.south + frame.north) / 2.0,
            (frame.west + frame.east) / 2.0
        )
        map.animateCamera(
            CameraUpdateFactory.newCameraPosition(
                CameraPosition.Builder().target(center).zoom(fallbackZoom).build()
            ),
            500
        )
        return
    }

    val bounds = LatLngBounds.Builder()
        .include(LatLng(frame.south, frame.west))
        .include(LatLng(frame.north, frame.east))
        .build()
    val horizontalScale = paddingScale(insets.left + insets.right, mapView.width)
    val verticalScale = paddingScale(insets.top + insets.bottom, mapView.height)

    map.animateCamera(
        CameraUpdateFactory.newLatLngBounds(
            bounds,
            (insets.left * horizontalScale).roundToInt(),
            (insets.top * verticalScale).roundToInt(),
            (insets.right * horizontalScale).roundToInt(),
            (insets.bottom * verticalScale).roundToInt()
        ),
        600
    )
}

private fun paddingScale(requestedPadding: Int, availableSize: Int): Double {
    if (requestedPadding <= 0) return 1.0
    val maximumPadding = (availableSize * 0.72).coerceAtLeast(1.0)
    return (maximumPadding / requestedPadding).coerceAtMost(1.0)
}
