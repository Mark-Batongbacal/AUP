package com.example.frontend.navigation

import com.example.frontend.model.RecentCommute
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class RecentJourneyNavigationTest {
    @Test
    fun repeatTripSeed_usesPreviousOriginAndDestinationForFreshPlanning() {
        val commute = RecentCommute(
            id = "trip-1",
            recommendationId = "recommendation-1",
            origin = "Porac",
            destination = "Dau Terminal",
            originLatitude = 15.0710,
            originLongitude = 120.5420,
            destinationLatitude = 15.1790,
            destinationLongitude = 120.5900,
            legs = 2,
            minutes = 34
        )

        val seed = commute.toRepeatTripRouteSeed() ?: error("Expected repeat trip seed")
        assertEquals("Porac", seed.originName)
        assertEquals(15.0710, seed.originLatitude!!, 0.0)
        assertEquals(120.5420, seed.originLongitude!!, 0.0)
        assertEquals("recent-trip-1", seed.destination.id)
        assertEquals("Dau Terminal", seed.destination.name)
        assertEquals(15.1790, seed.destination.latitude, 0.0)
        assertEquals(120.5900, seed.destination.longitude, 0.0)
        assertEquals("history", seed.destination.source)
    }

    @Test
    fun repeatTripSeed_isUnavailableWithoutDestinationCoordinates() {
        val commute = RecentCommute(
            id = "trip-1",
            origin = "Porac",
            destination = "Dau Terminal",
            originLatitude = 15.0710,
            originLongitude = 120.5420,
            legs = 2,
            minutes = 34
        )

        assertNull(commute.toRepeatTripRouteSeed())
    }
}
