package com.example.frontend.data

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.storage.AuthSession
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.navigation.NavigationApi
import com.example.frontend.data.navigation.NavigationLocationUpdate
import com.example.frontend.data.navigation.NavigationRepositoryImpl
import com.example.frontend.data.navigation.NavigationRerouteRequest
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.navigation.StartNavigationRequest
import com.google.gson.Gson
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class NavigationRepositoryTest {
    @Test
    fun snapshotJson_parsesStructuredLandmarkRoleRelationAndSpeech() {
        val snapshot = Gson().fromJson(
            snapshotJson("\"Pagkalagpas ng Jollibee, para ka na.\""),
            NavigationSnapshotDto::class.java
        )

        assertEquals("ApproachingAlightPoint", snapshot.state)
        assertEquals("PrepareToAlight", snapshot.nextInstruction?.type)
        assertEquals("ALIGHT_REFERENCE", snapshot.landmark?.role)
        assertEquals("BEFORE_ALIGHT", snapshot.landmark?.relation)
        assertEquals("Pagkalagpas ng Jollibee, para ka na.", snapshot.displayInstruction())
    }

    @Test
    fun missingSpokenInstruction_keepsStructuredStateUsable() {
        val snapshot = Gson().fromJson(snapshotJson("null"), NavigationSnapshotDto::class.java)

        assertNull(snapshot.spokenInstruction)
        assertNull(snapshot.displayInstruction())
        assertEquals("PrepareToAlight", snapshot.nextInstruction?.type)
        assertTrue(snapshot.requiresAlightingConfirmation)
        assertEquals(120.0, snapshot.remainingDistanceMeters!!, 0.0)
    }

    @Test
    fun aiText_isDisplayedButNeverParsedForState() {
        val snapshot = Gson().fromJson(snapshotJson("\"You have arrived.\""), NavigationSnapshotDto::class.java)

        assertEquals("You have arrived.", snapshot.displayInstruction())
        assertEquals("ApproachingAlightPoint", snapshot.state)
        assertFalse(snapshot.state == "Arrived")
    }

    @Test
    fun repository_locationActiveAndConfirmationsReturnSnapshots() = runBlocking {
        val api = FakeNavigationApi(snapshot())
        val repository = NavigationRepositoryImpl(api, SessionStore(), ApiErrorParser())
        val update = NavigationLocationUpdate(15.0, 120.0, 5.0, "2026-08-19T00:00:00Z")

        assertTrue(repository.getActiveNavigation() is ApiResult.Success)
        assertTrue(repository.updateLocation("session-1", update) is ApiResult.Success)
        assertTrue(repository.confirmBoarding("session-1") is ApiResult.Success)
        assertTrue(repository.confirmAlighting("session-1") is ApiResult.Success)
        assertEquals("session-1", api.locationSession)
        assertEquals(update, api.locationUpdate)
        assertEquals(1, api.activeCalls)
        assertEquals(1, api.boardingCalls)
        assertEquals(1, api.alightingCalls)
    }

    private class FakeNavigationApi(private val response: NavigationSnapshotDto) : NavigationApi {
        var locationSession: String? = null
        var locationUpdate: NavigationLocationUpdate? = null
        var activeCalls = 0
        var boardingCalls = 0
        var alightingCalls = 0
        override suspend fun start(request: StartNavigationRequest) = Response.success(response)
        override suspend fun active(): Response<NavigationSnapshotDto> {
            activeCalls++
            return Response.success(response)
        }
        override suspend fun location(sessionId: String, update: NavigationLocationUpdate): Response<NavigationSnapshotDto> {
            locationSession = sessionId
            locationUpdate = update
            return Response.success(response)
        }
        override suspend fun boarding(sessionId: String): Response<NavigationSnapshotDto> {
            boardingCalls++
            return Response.success(response)
        }
        override suspend fun alighting(sessionId: String): Response<NavigationSnapshotDto> {
            alightingCalls++
            return Response.success(response)
        }
        override suspend fun cancel(sessionId: String) = Response.success(response)
        override suspend fun reroute(sessionId: String, request: NavigationRerouteRequest) = Response.success(response)
    }

    private class SessionStore : AuthSessionStore {
        private val session = AuthSession("key", "2099-01-01T00:00:00Z", "ApiKey", "X-Api-Key")
        override fun read() = session
        override fun save(session: AuthSession) = Unit
        override fun clear() = Unit
    }

    private companion object {
        fun snapshotJson(spoken: String) = """{
          "sessionId":"session-1","state":"ApproachingAlightPoint","currentLegIndex":0,
          "currentLeg":{"legIndex":0,"transportMode":"JEEPNEY","routeName":"Marisol","fromName":"Gate","toName":"Market","startLatitude":15.0,"startLongitude":120.0,"endLatitude":15.1,"endLongitude":120.1,"distanceMeters":1000.0,"fare":13.0},
          "nextInstruction":{"type":"PrepareToAlight","routeName":"Marisol","transportMode":"JEEPNEY","distanceMeters":120.0,"requiresConfirmation":false},
          "spokenInstruction":$spoken,"remainingDistanceMeters":120.0,"progressMeters":880.0,
          "boardInfo":null,"alightInfo":null,
          "landmark":{"name":"Jollibee","category":"fast_food","role":"ALIGHT_REFERENCE","relation":"BEFORE_ALIGHT","latitude":15.09,"longitude":120.09,"distanceFromTargetMeters":120.0},
          "requiresBoardingConfirmation":false,"requiresAlightingConfirmation":true,"rerouteRequired":false,
          "status":"ApproachingAlightPoint","triggeredEvents":[]
        }"""

        fun snapshot(): NavigationSnapshotDto = Gson().fromJson(
            snapshotJson("\"Tuki says keep going.\""), NavigationSnapshotDto::class.java)
    }
}
