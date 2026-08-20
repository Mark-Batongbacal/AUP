package com.example.frontend.data.trips

import com.example.frontend.data.common.TransportModeSummaryDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.math.BigDecimal

class RecentCommuteMapperTest {
    @Test
    fun historyDto_mapsTripDetailDataForCompletedJourney() {
        val dto = historyItem(status = "COMPLETED")

        val commute = dto.toRecentCommute()

        assertEquals("trip-1", commute.id)
        assertEquals("recommendation-1", commute.recommendationId)
        assertEquals("Porac", commute.origin)
        assertEquals("Dau Terminal", commute.destination)
        assertEquals("Completed", commute.status)
        assertEquals(2, commute.legs)
        assertEquals(34, commute.minutes)
        assertEquals(2, commute.steps.size)
        assertEquals("Jeepney", commute.steps[0].mode)
        assertEquals("Porac Plaza", commute.steps[0].from)
        assertEquals("Angeles", commute.steps[0].to)
        assertEquals(15.0, commute.steps[0].fare!!, 0.0)
        assertEquals(2, commute.historyLegs.size)
        assertEquals(15.0710, commute.historyLegs[0].startLatitude!!, 0.0)
        assertEquals(120.5900, commute.historyLegs[1].endLongitude!!, 0.0)
        assertTrue(commute.wasRerouted)
        assertEquals(1, commute.rerouteCount)
    }

    @Test
    fun historyDto_mapsCancelledJourneyStatusAndAvailableLegs() {
        val dto = historyItem(status = "CANCELLED")

        val commute = dto.toRecentCommute()

        assertEquals("Cancelled", commute.status)
        assertEquals(2, commute.steps.size)
        assertFalse(commute.historyLegs.isEmpty())
    }

    private fun historyItem(status: String) = PassengerTripHistoryItemDto(
        passengerTripId = "trip-1",
        status = status,
        originName = "Porac",
        destinationName = "Dau Terminal",
        originLatitude = 15.0710,
        originLongitude = 120.5420,
        destinationLatitude = 15.1790,
        destinationLongitude = 120.5900,
        startedAt = "2026-08-20T01:00:00Z",
        completedAt = "2026-08-20T01:34:00Z",
        createdAt = "2026-08-20T00:55:00Z",
        recommendation = RecommendationDetailsDto(
            recommendationId = "recommendation-1",
            tripSearchId = "search-1",
            recommendationType = "efficient",
            rankNumber = 1,
            totalFare = BigDecimal("30.00"),
            totalMinutes = BigDecimal("34.0"),
            totalDistanceMeters = BigDecimal("12000"),
            walkingDistanceMeters = BigDecimal("200"),
            transferCount = 1,
            recommendationScore = BigDecimal("0.9"),
            explanation = null,
            generatedAt = "2026-08-20T00:55:00Z",
            legs = listOf(
                leg(
                    order = 2,
                    mode = "Tricycle",
                    modeCode = "TRICYCLE",
                    from = "Angeles",
                    to = "Dau Terminal",
                    fare = "15.00",
                    startLat = 15.1453,
                    startLon = 120.5887,
                    endLat = 15.1790,
                    endLon = 120.5900
                ),
                leg(
                    order = 1,
                    mode = "Jeepney",
                    modeCode = "JEEPNEY",
                    from = "Porac Plaza",
                    to = "Angeles",
                    fare = "15.00",
                    startLat = 15.0710,
                    startLon = 120.5420,
                    endLat = 15.1453,
                    endLon = 120.5887
                )
            )
        ),
        rerouted = true,
        rerouteCount = 1,
        lastRerouteReason = "OFF_ROUTE",
        lastRerouteAt = "2026-08-20T01:10:00Z"
    )

    private fun leg(
        order: Int,
        mode: String,
        modeCode: String,
        from: String,
        to: String,
        fare: String,
        startLat: Double,
        startLon: Double,
        endLat: Double,
        endLon: Double
    ) = RecommendationLegDto(
        legId = "leg-$order",
        recommendationId = "recommendation-1",
        legOrder = order,
        transportModeId = order,
        transportMode = TransportModeSummaryDto(
            transportModeId = order,
            code = modeCode,
            name = mode,
            isMotorized = modeCode != "WALK",
            allowsLiveDriver = modeCode == "TRICYCLE",
            iconName = null
        ),
        routeId = order.toLong(),
        route = null,
        fromStopId = null,
        fromStop = null,
        toStopId = null,
        toStop = null,
        fromName = from,
        toName = to,
        startLatitude = startLat,
        startLongitude = startLon,
        endLatitude = endLat,
        endLongitude = endLon,
        distanceMeters = BigDecimal("1000"),
        estimatedMinutes = BigDecimal("17"),
        estimatedFare = BigDecimal(fare),
        instructions = null,
        createdAt = "2026-08-20T00:55:00Z"
    )
}
