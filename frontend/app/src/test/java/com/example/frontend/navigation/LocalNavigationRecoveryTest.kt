package com.example.frontend.navigation

import com.example.frontend.core.location.RouteCoordinate
import com.example.frontend.data.navigation.NavigationInstructionDetailDto
import com.example.frontend.data.navigation.NavigationLandmarkDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class LocalNavigationRecoveryTest {
    @Test
    fun firstFixAfterRecreation_doesNotReplayAlreadyPassedLandmark() {
        val engine = LocalNavigationEngine()
        val route = route()
        val landmarks = listOf(
            NavigationLandmarkDto(
                name = "Past Jollibee",
                category = "fast_food",
                role = "PROGRESS_REFERENCE",
                relation = "ALONG_ROUTE",
                latitude = 15.0,
                longitude = 120.003,
                distanceFromTargetMeters = 0.0,
                triggerBeforeMeters = 20.0,
                triggerAfterMeters = 20.0
            ),
            NavigationLandmarkDto(
                name = "Future Mercury",
                category = "pharmacy",
                role = "PROGRESS_REFERENCE",
                relation = "ALONG_ROUTE",
                latitude = 15.0,
                longitude = 120.007,
                distanceFromTargetMeters = 0.0,
                triggerBeforeMeters = 20.0,
                triggerAfterMeters = 20.0
            )
        )

        val restored = engine.update(
            raw = RouteCoordinate(15.0, 120.006),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "WALK",
            route = route,
            instructions = emptyList(),
            landmarks = landmarks
        )!!
        val crossedFuture = engine.update(
            raw = RouteCoordinate(15.0, 120.0071),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "WALK",
            route = route,
            instructions = emptyList(),
            landmarks = landmarks
        )!!

        assertNull(restored.landmarkEvent)
        assertEquals("Future Mercury", crossedFuture.landmarkEvent?.name)
    }

    @Test
    fun firstFixAfterRecreation_skipsPassedManeuverAndSelectsNextOne() {
        val engine = LocalNavigationEngine()
        val route = route()
        val instructions = listOf(
            NavigationInstructionDetailDto(
                sequence = 1,
                type = "TurnLeft",
                legIndex = 0,
                text = "Turn left.",
                latitude = 15.0,
                longitude = 120.003,
                distanceFromLegStartMeters = null,
                triggerDistanceMeters = 30.0
            ),
            NavigationInstructionDetailDto(
                sequence = 2,
                type = "TurnRight",
                legIndex = 0,
                text = "Turn right.",
                latitude = 15.0,
                longitude = 120.008,
                distanceFromLegStartMeters = null,
                triggerDistanceMeters = 30.0
            )
        )

        val restored = engine.update(
            raw = RouteCoordinate(15.0, 120.006),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "WALK",
            route = route,
            instructions = instructions,
            landmarks = emptyList()
        )!!

        assertEquals(2, restored.currentGuidance?.sequence)
        assertEquals("TurnRight", restored.currentGuidance?.type)
    }

    private fun route() = listOf(
        RouteCoordinate(15.0, 120.0),
        RouteCoordinate(15.0, 120.005),
        RouteCoordinate(15.0, 120.01)
    )
}
