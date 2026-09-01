package com.example.frontend

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.maplibre.android.geometry.LatLng

class MapCameraFramingTest {
    @Test
    fun routePreviewBearing_placesNorthboundStartAboveDestination() {
        val bearing = routeTopToBottomCameraBearing(
            start = LatLng(15.10, 120.58),
            destination = LatLng(15.20, 120.58)
        )

        assertEquals(180.0, bearing!!, 0.01)
    }

    @Test
    fun routePreviewBearing_placesEastboundStartAboveDestination() {
        val bearing = routeTopToBottomCameraBearing(
            start = LatLng(15.10, 120.50),
            destination = LatLng(15.10, 120.60)
        )

        assertEquals(270.0, bearing!!, 0.2)
    }

    @Test
    fun routePreviewBearing_returnsNullForIdenticalAnchors() {
        val point = LatLng(15.10, 120.58)

        assertNull(routeTopToBottomCameraBearing(point, point))
    }
}
