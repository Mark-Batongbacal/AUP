package com.example.frontend.navigation

import com.example.frontend.core.location.RouteCoordinate
import com.example.frontend.data.navigation.NavigationInstructionDetailDto
import com.example.frontend.data.navigation.NavigationLandmarkDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

class LocalNavigationEngineTest {
    @Test
    fun update_projectsGpsAndShrinksRemainingRouteLocally() {
        val engine = LocalNavigationEngine()
        val route = route()

        val progress = engine.update(
            raw = RouteCoordinate(15.0, 120.0045),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "WALK",
            route = route,
            instructions = emptyList(),
            landmarks = emptyList()
        )!!

        assertTrue(progress.progressMeters > 400)
        assertTrue(progress.remainingMeters > 400)
        assertTrue(progress.remainingMeters < 700)
        assertEquals(progress.matchedLocation, progress.remainingRoute.first())
    }

    @Test
    fun walkingTurn_isSelectedFromCachedManeuverPackage() {
        val engine = LocalNavigationEngine()
        val route = route()
        val instructions = listOf(
            NavigationInstructionDetailDto(
                sequence = 1,
                type = "TurnRight",
                legIndex = 0,
                text = "Turn right onto Mabini Street.",
                streetName = "Mabini Street",
                latitude = 15.0,
                longitude = 120.005,
                distanceFromLegStartMeters = 537.0,
                triggerDistanceMeters = 30.0
            )
        )

        val progress = engine.update(
            raw = RouteCoordinate(15.0, 120.0046),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "WALK",
            route = route,
            instructions = instructions,
            landmarks = emptyList()
        )!!

        assertEquals("TurnRight", progress.currentGuidance?.type)
        assertTrue(progress.currentGuidance!!.distanceMeters < 100)
    }

    @Test
    fun legEnd_requiresConsecutiveGoodFixes() {
        val engine = LocalNavigationEngine()
        val route = listOf(
            RouteCoordinate(15.0, 120.0),
            RouteCoordinate(15.0, 120.001)
        )

        val first = engine.update(
            raw = RouteCoordinate(15.0, 120.00099),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "WALK",
            route = route,
            instructions = emptyList(),
            landmarks = emptyList()
        )!!
        val second = engine.update(
            raw = RouteCoordinate(15.0, 120.001),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "WALK",
            route = route,
            instructions = emptyList(),
            landmarks = emptyList()
        )!!

        assertEquals(LocalLegProximity.APPROACHING, first.legProximity)
        assertEquals(LocalLegProximity.REACHED, second.legProximity)
        assertTrue(second.shouldForceServerSync)
    }

    @Test
    fun progressLandmark_triggersOnlyOnce() {
        val engine = LocalNavigationEngine()
        val route = route()
        val landmark = NavigationLandmarkDto(
            name = "Jollibee",
            category = "fast_food",
            role = "PROGRESS_REFERENCE",
            relation = "ALONG_ROUTE",
            latitude = 15.0,
            longitude = 120.004,
            distanceFromTargetMeters = 0.0,
            triggerBeforeMeters = 20.0,
            triggerAfterMeters = 20.0
        )

        engine.update(
            raw = RouteCoordinate(15.0, 120.0035),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "JEEPNEY",
            route = route,
            instructions = emptyList(),
            landmarks = listOf(landmark)
        )
        val crossed = engine.update(
            raw = RouteCoordinate(15.0, 120.0041),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "JEEPNEY",
            route = route,
            instructions = emptyList(),
            landmarks = listOf(landmark)
        )!!
        val later = engine.update(
            raw = RouteCoordinate(15.0, 120.0045),
            accuracyMeters = 5.0,
            legIndex = 0,
            transportMode = "JEEPNEY",
            route = route,
            instructions = emptyList(),
            landmarks = listOf(landmark)
        )!!

        assertNotNull(crossed.landmarkEvent)
        assertEquals("Jollibee", crossed.landmarkEvent?.name)
        assertEquals(null, later.landmarkEvent)
    }

    private fun route() = listOf(
        RouteCoordinate(15.0, 120.0),
        RouteCoordinate(15.0, 120.005),
        RouteCoordinate(15.0, 120.01)
    )
}
