package com.example.frontend

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.maplibre.android.geometry.LatLng

class MapCameraFramingTest {
    @Test
    fun routeTopToBottomCameraBearing_putsNorthboundDestinationBelowStart() {
        val bearing = routeTopToBottomCameraBearing(
            start = LatLng(15.10, 120.58),
            destination = LatLng(15.20, 120.58)
        )

        assertEquals(180.0, bearing!!, 0.5)
    }

    @Test
    fun routeTopToBottomCameraBearing_putsEastboundDestinationBelowStart() {
        val bearing = routeTopToBottomCameraBearing(
            start = LatLng(15.10, 120.58),
            destination = LatLng(15.10, 120.68)
        )

        assertEquals(270.0, bearing!!, 1.0)
    }

    @Test
    fun routeTopToBottomCameraBearing_returnsNullForSamePoint() {
        assertNull(
            routeTopToBottomCameraBearing(
                start = LatLng(15.10, 120.58),
                destination = LatLng(15.10, 120.58)
            )
        )
    }
}
