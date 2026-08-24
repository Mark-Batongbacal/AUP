package com.example.frontend.navigation

import com.example.frontend.core.location.RouteCoordinate
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class NavigationCameraGeometryTest {
    @Test
    fun frameContainsEveryValidRouteCoordinate() {
        val frame = navigationCameraFrame(
            listOf(
                RouteCoordinate(15.1450, 120.5860),
                RouteCoordinate(15.1530, 120.5940),
                RouteCoordinate(15.1490, 120.5890)
            )
        )!!

        assertEquals(15.1450, frame.south, 0.0000001)
        assertEquals(120.5860, frame.west, 0.0000001)
        assertEquals(15.1530, frame.north, 0.0000001)
        assertEquals(120.5940, frame.east, 0.0000001)
        assertEquals(3, frame.distinctPointCount)
    }

    @Test
    fun invalidCoordinatesAreIgnored() {
        val frame = navigationCameraFrame(
            listOf(
                RouteCoordinate(Double.NaN, 120.0),
                RouteCoordinate(91.0, 120.0),
                RouteCoordinate(15.0, Double.POSITIVE_INFINITY),
                RouteCoordinate(15.1, 120.5)
            )
        )

        assertNotNull(frame)
        assertEquals(1, frame!!.distinctPointCount)
    }

    @Test
    fun emptyOrEntirelyInvalidRoutesHaveNoFrame() {
        assertNull(navigationCameraFrame(emptyList()))
        assertNull(navigationCameraFrame(listOf(RouteCoordinate(15.0, 181.0))))
    }

    @Test
    fun straightOrSinglePointRoutesHaveNonZeroBounds() {
        val horizontal = navigationCameraFrame(
            listOf(RouteCoordinate(15.0, 120.0), RouteCoordinate(15.0, 120.1))
        )!!
        val single = navigationCameraFrame(listOf(RouteCoordinate(15.0, 120.0)))!!

        assertTrue(horizontal.north > horizontal.south)
        assertTrue(single.north > single.south)
        assertTrue(single.east > single.west)
    }

    @Test
    fun joiningLegsKeepsOrderWithoutDuplicatingTransferPoints() {
        val transfer = RouteCoordinate(15.01, 120.01)
        val points = joinedNavigationLegs(
            listOf(
                listOf(RouteCoordinate(15.0, 120.0), transfer),
                listOf(transfer, RouteCoordinate(15.02, 120.02))
            )
        )

        assertEquals(3, points.size)
        assertEquals(transfer, points[1])
        assertEquals(RouteCoordinate(15.02, 120.02), points.last())
    }
}
