package com.example.frontend.navigation

import com.example.frontend.core.location.NavigationSyncSignal
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.navigation.NavigationGeometryResponseDto
import com.example.frontend.data.navigation.NavigationLocationUpdate
import com.example.frontend.data.navigation.NavigationRepository
import com.example.frontend.data.navigation.NavigationRerouteRequest
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.tripsessions.TripSessionDto
import com.google.gson.Gson
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.math.BigDecimal

class NavigationRerouteDispatcherTest {
    @Test
    fun reroute_fetchesCurrentGpsAndSendsItDirectlyWithoutLocationUpdate() = runBlocking {
        NavigationSyncSignal.reset()
        val navigation = RecordingNavigationRepository()
        var gpsRequests = 0
        val currentFix = RerouteGpsFix(
            latitude = 15.25,
            longitude = 120.75,
            accuracyMeters = 7.5,
            timestamp = "2026-08-26T10:15:30Z",
            speedMetersPerSecond = 3.25,
            bearingDegrees = 92.0
        )
        val dispatcher = NavigationRerouteDispatcher(navigation) {
            gpsRequests++
            currentFix
        }

        dispatcher.reroute(
            "session-1",
            NavigationRerouteRequest(reason = "OFF_ROUTE", avoidTransportMode = "TRICYCLE")
        )

        assertEquals(1, gpsRequests)
        assertEquals(1, navigation.rerouteCalls)
        assertEquals(0, navigation.locationCalls)
        assertEquals("session-1", navigation.rerouteSessionId)
        assertEquals(
            NavigationRerouteRequest(
                reason = "OFF_ROUTE",
                avoidTransportMode = "TRICYCLE",
                latitude = currentFix.latitude,
                longitude = currentFix.longitude,
                accuracyMeters = currentFix.accuracyMeters,
                timestamp = currentFix.timestamp,
                speedMetersPerSecond = currentFix.speedMetersPerSecond,
                bearingDegrees = currentFix.bearingDegrees
            ),
            navigation.rerouteRequest
        )
    }

    @Test
    fun rerouteRequest_serializesCurrentGpsUsingApiFieldNames() {
        val json = Gson().toJson(
            NavigationRerouteRequest(
                latitude = 15.25,
                longitude = 120.75,
                accuracyMeters = 7.5,
                timestamp = "2026-08-26T10:15:30Z",
                speedMetersPerSecond = 3.25,
                bearingDegrees = 92.0
            )
        )

        assertTrue(json.contains("\"latitude\":15.25"))
        assertTrue(json.contains("\"longitude\":120.75"))
        assertTrue(json.contains("\"accuracyMeters\":7.5"))
        assertTrue(json.contains("\"timestamp\":\"2026-08-26T10:15:30Z\""))
        assertTrue(json.contains("\"speedMetersPerSecond\":3.25"))
        assertTrue(json.contains("\"bearingDegrees\":92.0"))
    }

    @Test
    fun alreadyOff_callsOnlyExplicitAlightResolution() = runBlocking {
        val navigation = RecordingNavigationRepository()
        navigation.resolveResult = ApiResult.Success(snapshot("ALIGHTING_RECOVERED"))
        var recoveries = 0
        val dispatcher = AlightStatusRecoveryDispatcher(navigation) {
            recoveries++
            ApiResult.Success(snapshot("REROUTE_SUCCEEDED"))
        }

        dispatcher.resolve("session-1", alreadyOff = true)

        assertEquals(listOf(true), navigation.alightResolutions)
        assertEquals(0, recoveries)
    }

    @Test
    fun stillRiding_resolvesStatusThenTriggersMissedAlightRecovery() = runBlocking {
        val navigation = RecordingNavigationRepository()
        navigation.resolveResult = ApiResult.Success(snapshot("MISSED_ALIGHT"))
        var recoveredSession: String? = null
        val dispatcher = AlightStatusRecoveryDispatcher(navigation) { sessionId ->
            recoveredSession = sessionId
            ApiResult.Success(snapshot("REROUTE_SUCCEEDED"))
        }

        dispatcher.resolve("session-1", alreadyOff = false)

        assertEquals(listOf(false), navigation.alightResolutions)
        assertEquals("session-1", recoveredSession)
    }

    private class RecordingNavigationRepository : NavigationRepository {
        var locationCalls = 0
        var rerouteCalls = 0
        var rerouteSessionId: String? = null
        var rerouteRequest: NavigationRerouteRequest? = null
        var resolveResult: ApiResult<NavigationSnapshotDto> = ApiResult.Failure(null, "Recorded")
        val alightResolutions = mutableListOf<Boolean>()

        override suspend fun updateLocation(
            sessionId: String,
            update: NavigationLocationUpdate
        ): ApiResult<NavigationSnapshotDto> {
            locationCalls++
            return ApiResult.Failure(null, "Unexpected location update")
        }

        override suspend fun reroute(
            sessionId: String,
            request: NavigationRerouteRequest
        ): ApiResult<NavigationSnapshotDto> {
            rerouteCalls++
            rerouteSessionId = sessionId
            rerouteRequest = request
            return ApiResult.Failure(null, "Recorded")
        }

        override suspend fun startNavigation(recommendationId: String): ApiResult<NavigationSnapshotDto> =
            error("Unexpected startNavigation call")

        override suspend fun getActiveNavigation(): ApiResult<NavigationSnapshotDto> =
            error("Unexpected getActiveNavigation call")

        override fun restoreActiveNavigation(): NavigationSnapshotDto? =
            error("Unexpected restoreActiveNavigation call")

        override suspend fun getGeometry(
            startLatitude: Double,
            startLongitude: Double,
            endLatitude: Double,
            endLongitude: Double,
            mode: String,
            routeId: Long?,
            startRouteProgressMeters: Double?,
            endRouteProgressMeters: Double?
        ): ApiResult<NavigationGeometryResponseDto> = error("Unexpected getGeometry call")

        override suspend fun confirmBoarding(sessionId: String): ApiResult<NavigationSnapshotDto> =
            error("Unexpected confirmBoarding call")

        override suspend fun confirmAlighting(sessionId: String): ApiResult<NavigationSnapshotDto> =
            error("Unexpected confirmAlighting call")

        override suspend fun resolveAlightStatus(
            sessionId: String,
            alreadyOff: Boolean
        ): ApiResult<NavigationSnapshotDto> {
            alightResolutions += alreadyOff
            return resolveResult
        }

        override suspend fun cancel(sessionId: String): ApiResult<TripSessionDto> =
            error("Unexpected cancel call")

        override fun saveLocalActiveNavigation(snapshot: NavigationSnapshotDto) =
            error("Unexpected saveLocalActiveNavigation call")

        override fun clearLocalActiveNavigation(sessionId: String) =
            error("Unexpected clearLocalActiveNavigation call")

        override fun clearLocalNavigation() = error("Unexpected clearLocalNavigation call")
    }

    private companion object {
        fun snapshot(status: String) = NavigationSnapshotDto(
            sessionId = "session-1",
            state = "ApproachingAlightPoint",
            currentLegIndex = 0,
            currentLeg = null,
            nextInstruction = null,
            spokenInstruction = null,
            remainingDistanceMeters = null,
            progressMeters = 0.0,
            boardInfo = null,
            alightInfo = null,
            landmark = null,
            requiresBoardingConfirmation = false,
            requiresAlightingConfirmation = false,
            rerouteRequired = false,
            status = status,
            triggeredEvents = emptyList(),
            approxFareSpent = BigDecimal.ZERO,
            estimatedRemainingFare = BigDecimal.ZERO
        )
    }
}
