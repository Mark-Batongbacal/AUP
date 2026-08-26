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

    private class RecordingNavigationRepository : NavigationRepository {
        var locationCalls = 0
        var rerouteCalls = 0
        var rerouteSessionId: String? = null
        var rerouteRequest: NavigationRerouteRequest? = null

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
            routeId: Long?
        ): ApiResult<NavigationGeometryResponseDto> = error("Unexpected getGeometry call")

        override suspend fun confirmBoarding(sessionId: String): ApiResult<NavigationSnapshotDto> =
            error("Unexpected confirmBoarding call")

        override suspend fun confirmAlighting(sessionId: String): ApiResult<NavigationSnapshotDto> =
            error("Unexpected confirmAlighting call")

        override suspend fun cancel(sessionId: String): ApiResult<TripSessionDto> =
            error("Unexpected cancel call")

        override fun saveLocalActiveNavigation(snapshot: NavigationSnapshotDto) =
            error("Unexpected saveLocalActiveNavigation call")

        override fun clearLocalActiveNavigation(sessionId: String) =
            error("Unexpected clearLocalActiveNavigation call")

        override fun clearLocalNavigation() = error("Unexpected clearLocalNavigation call")
    }
}
