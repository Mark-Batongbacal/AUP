package com.example.frontend

import androidx.compose.runtime.Composable
import com.google.android.gms.maps.model.LatLng
import com.google.maps.android.compose.GoogleMapComposable

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

@Composable
@GoogleMapComposable
fun TemporaryMapSampleMarker() {
    MapMarker(
        latitude = TemporaryMapSamples.marker.latitude,
        longitude = TemporaryMapSamples.marker.longitude,
        title = "Temporary map test point",
        snippet = "Mock coordinate only; remove when real transport data is available."
    )
}
