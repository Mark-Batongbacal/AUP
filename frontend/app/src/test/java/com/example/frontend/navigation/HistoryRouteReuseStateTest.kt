package com.example.frontend.navigation

import com.example.frontend.model.RouteOption
import org.junit.After
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class HistoryRouteReuseStateTest {
    @After
    fun tearDown() {
        HistoryRouteReuseState.clear()
    }

    @Test
    fun prepare_doesNotArmAutoStartUntilRouteResultsConsumesSelection() {
        HistoryRouteReuseState.prepare(sampleReuse())

        assertFalse(HistoryRouteReuseState.consumeAutoStart())
        assertNotNull(HistoryRouteReuseState.takePendingSelection("Porac", "Dau Terminal"))
        assertTrue(HistoryRouteReuseState.consumeAutoStart())
        assertFalse(HistoryRouteReuseState.consumeAutoStart())
    }

    @Test
    fun mismatchedRoute_clearsPendingReuseAndAutoStart() {
        HistoryRouteReuseState.prepare(sampleReuse())

        assertNull(HistoryRouteReuseState.takePendingSelection("Angeles", "Dau Terminal"))
        assertFalse(HistoryRouteReuseState.consumeAutoStart())
        assertNull(HistoryRouteReuseState.takePendingSelection("Porac", "Dau Terminal"))
    }

    private fun sampleReuse() = PendingHistoryRouteReuse(
        option = RouteOption(
            id = "recommendation-1",
            label = "Previous route",
            totalMinutes = 30,
            totalFare = 25.0
        ),
        originName = "Porac",
        destinationName = "Dau Terminal",
        originLatitude = 15.0710,
        originLongitude = 120.5420
    )
}
