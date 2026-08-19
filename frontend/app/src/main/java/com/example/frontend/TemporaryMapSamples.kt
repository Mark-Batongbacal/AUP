package com.example.frontend

import org.maplibre.android.geometry.LatLng

/**
 * Temporary mock coordinates for verifying only that markers and polylines render.
 *
 * These points are not official jeepney, tricycle, PUV station, or route data.
 * Remove this object and pass backend-provided coordinates into MapScreen once
 * real transportation route data is available.
 */
object TemporaryMapSamples {
    val marker = LatLng(15.1453, 120.5887)

    val routePoints = listOf(
        LatLng(15.1453, 120.5887),
        LatLng(15.1464, 120.5902),
        LatLng(15.1478, 120.5916),
        LatLng(15.1492, 120.5930)
    )
}
