package com.example.frontend.core.location

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class RouteMatcherTest {
    @Test
    fun match_projectsLocationOntoRouteSegment() {
        val route = listOf(
            RouteCoordinate(15.0, 120.0),
            RouteCoordinate(15.0, 120.002)
        )

        val match = RouteMatcher.match(RouteCoordinate(15.0001, 120.001), route)

        requireNotNull(match)
        assertEquals(15.0, match.coordinate.latitude, 0.00001)
        assertEquals(120.001, match.coordinate.longitude, 0.00001)
        assertTrue(match.distanceToRouteMeters in 9.0..13.0)
        assertTrue(match.progressMeters > 90.0)
        assertTrue(match.remainingDistanceMeters > 90.0)
    }

    @Test
    fun remainingRoute_startsAtProjectedPositionInsteadOfOldValhallaStart() {
        val route = listOf(
            RouteCoordinate(15.0, 120.0),
            RouteCoordinate(15.0, 120.001),
            RouteCoordinate(15.0, 120.002)
        )
        val match = RouteMatcher.match(RouteCoordinate(15.00005, 120.0014), route)

        val remaining = RouteMatcher.remainingRoute(route, match)

        requireNotNull(match)
        assertEquals(match.coordinate, remaining.first())
        assertEquals(route.last(), remaining.last())
        assertTrue(remaining.size <= route.size)
    }

    @Test
    fun match_doesNotJumpToCloserFutureSegmentPastMaximumProgress() {
        val route = listOf(
            RouteCoordinate(15.0, 120.0),
            RouteCoordinate(15.0, 120.005),
            RouteCoordinate(15.00005, 120.005),
            RouteCoordinate(15.00005, 120.0)
        )

        // This point is almost directly on the later return segment, but the caller has only
        // allowed progress through the beginning of the first segment.
        val match = RouteMatcher.match(
            raw = RouteCoordinate(15.00005, 120.0005),
            route = route,
            minimumProgressMeters = 0.0,
            maximumProgressMeters = 120.0
        )

        requireNotNull(match)
        assertEquals(0, match.segmentIndex)
        assertTrue(match.progressMeters < 120.0)
    }
}
