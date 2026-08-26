package com.example.frontend.data

import com.example.frontend.core.location.NavigationSyncSignal
import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.storage.AuthSession
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.navigation.NavigationApi
import com.example.frontend.data.navigation.NavigationGeometryPointDto
import com.example.frontend.data.navigation.NavigationGeometryResponseDto
import com.example.frontend.data.navigation.NavigationLegDto
import com.example.frontend.data.navigation.NavigationLocalStore
import com.example.frontend.data.navigation.NavigationLocationUpdate
import com.example.frontend.data.navigation.NavigationRepositoryImpl
import com.example.frontend.data.navigation.NavigationRerouteRequest
import com.example.frontend.data.navigation.ResolveAlightStatusRequest
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.navigation.StartNavigationRequest
import com.example.frontend.data.tripsessions.TripSessionDto
import kotlinx.coroutines.runBlocking
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response
import java.math.BigDecimal

class NavigationPersistenceRecoveryTest {
    @Test
    fun repositoryRecreation_restoresPersistedPackageAndKeepsRoutineFixLocal() = runBlocking {
        NavigationSyncSignal.reset()
        val store = MemoryNavigationLocalStore(active = snapshot())
        val api = FakeNavigationApi()
        val repository = NavigationRepositoryImpl(api, SessionStore(), ApiErrorParser(), store)

        val restored = repository.restoreActiveNavigation()
        assertNotNull(restored)
        assertEquals("session-1", restored?.sessionId)

        val result = repository.updateLocation(
            "session-1",
            NavigationLocationUpdate(15.001, 120.001, 5.0, "2026-08-22T13:00:00Z")
        )

        assertTrue(result is ApiResult.Success)
        assertEquals(0, api.locationCalls)
        assertEquals(15.001, store.active?.currentLatitude!!, 0.0)
        assertEquals(120.001, store.active?.currentLongitude!!, 0.0)
    }

    @Test
    fun transientActiveFailure_fallsBackToPersistedPackage() = runBlocking {
        val store = MemoryNavigationLocalStore(active = snapshot())
        val api = FakeNavigationApi().apply { activeResponse = errorResponse(503) }
        val repository = NavigationRepositoryImpl(api, SessionStore(), ApiErrorParser(), store)

        val result = repository.getActiveNavigation()

        assertTrue(result is ApiResult.Success)
        assertEquals("session-1", (result as ApiResult.Success).data.sessionId)
        assertEquals(1, api.activeCalls)
    }

    @Test
    fun authoritativeNotFound_doesNotResurrectPersistedPackage() = runBlocking {
        val store = MemoryNavigationLocalStore(active = snapshot())
        val api = FakeNavigationApi().apply { activeResponse = errorResponse(404) }
        val repository = NavigationRepositoryImpl(api, SessionStore(), ApiErrorParser(), store)

        val result = repository.getActiveNavigation()

        assertTrue(result is ApiResult.Failure)
        assertEquals(404, (result as ApiResult.Failure).statusCode)
        assertTrue(repository.restoreActiveNavigation() == null)
        assertTrue(store.active == null)
    }

    @Test
    fun transientGeometryFailure_usesPersistedLegGeometry() = runBlocking {
        val cached = NavigationGeometryResponseDto(
            listOf(
                NavigationGeometryPointDto(15.0, 120.0),
                NavigationGeometryPointDto(15.001, 120.001)
            )
        )
        val store = MemoryNavigationLocalStore(geometry = cached)
        val api = FakeNavigationApi().apply { geometryResponse = errorResponse(503) }
        val repository = NavigationRepositoryImpl(api, SessionStore(), ApiErrorParser(), store)

        val result = repository.getGeometry(15.0, 120.0, 15.001, 120.001, "WALK")

        assertTrue(result is ApiResult.Success)
        assertEquals(cached, (result as ApiResult.Success).data)
    }

    @Test
    fun failedMeaningfulSync_isRetriedOnNextLocationTick() = runBlocking {
        NavigationSyncSignal.reset()
        val store = MemoryNavigationLocalStore(active = snapshot())
        val api = FakeNavigationApi().apply { locationResponse = errorResponse(503) }
        val repository = NavigationRepositoryImpl(api, SessionStore(), ApiErrorParser(), store)
        val update = NavigationLocationUpdate(15.001, 120.001, 5.0, "2026-08-22T13:00:00Z")

        NavigationSyncSignal.requestImmediateSync(samples = 1)
        assertTrue(repository.updateLocation("session-1", update) is ApiResult.Failure)
        api.locationResponse = Response.success(snapshot())
        assertTrue(repository.updateLocation("session-1", update) is ApiResult.Success)

        assertEquals(2, api.locationCalls)
        NavigationSyncSignal.reset()
    }

    @Test
    fun guestActiveTrip_canBeSavedResumedAndEndedLocally() {
        val guestSnapshot = snapshot().copy(sessionId = "guest-session-1", state = "GuestActive")
        val store = MemoryNavigationLocalStore()
        val repository = NavigationRepositoryImpl(
            FakeNavigationApi(),
            GuestSessionStore(),
            ApiErrorParser(),
            store
        )

        repository.saveLocalActiveNavigation(guestSnapshot)

        assertEquals("guest-session-1", repository.restoreActiveNavigation()?.sessionId)
        assertEquals("guest-session-1", store.active?.sessionId)

        repository.clearLocalActiveNavigation("guest-session-1")

        assertTrue(repository.restoreActiveNavigation() == null)
        assertTrue(store.active == null)
    }

    private class MemoryNavigationLocalStore(
        var active: NavigationSnapshotDto? = null,
        var geometry: NavigationGeometryResponseDto? = null
    ) : NavigationLocalStore {
        override fun readActiveSnapshot(): NavigationSnapshotDto? = active
        override fun saveActiveSnapshot(snapshot: NavigationSnapshotDto) { active = snapshot }
        override fun clearActiveSnapshot(sessionId: String?) {
            if (sessionId == null || active?.sessionId == sessionId) active = null
        }
        override fun readGeometry(cacheKey: String): NavigationGeometryResponseDto? = geometry
        override fun saveGeometry(cacheKey: String, response: NavigationGeometryResponseDto) { geometry = response }
        override fun clearAll() { active = null; geometry = null }
    }

    private class SessionStore : AuthSessionStore {
        private var session: AuthSession? = AuthSession(
            "key",
            "2099-01-01T00:00:00Z",
            "ApiKey",
            "X-Api-Key"
        )
        override fun read(): AuthSession? = session
        override fun save(session: AuthSession) { this.session = session }
        override fun clear() { session = null }
    }

    private class GuestSessionStore : AuthSessionStore {
        override fun read(): AuthSession? = null
        override fun save(session: AuthSession) = Unit
        override fun clear() = Unit
    }

    private class FakeNavigationApi : NavigationApi {
        var activeCalls = 0
        var locationCalls = 0
        var activeResponse: Response<NavigationSnapshotDto> = Response.success(snapshot())
        var locationResponse: Response<NavigationSnapshotDto> = Response.success(snapshot())
        var geometryResponse: Response<NavigationGeometryResponseDto> = Response.success(
            NavigationGeometryResponseDto(emptyList())
        )

        override suspend fun start(request: StartNavigationRequest): Response<NavigationSnapshotDto> =
            Response.success(snapshot())

        override suspend fun active(): Response<NavigationSnapshotDto> {
            activeCalls++
            return activeResponse
        }

        override suspend fun geometry(
            startLatitude: Double,
            startLongitude: Double,
            endLatitude: Double,
            endLongitude: Double,
            mode: String,
            routeId: Long?
        ): Response<NavigationGeometryResponseDto> = geometryResponse

        override suspend fun location(
            sessionId: String,
            update: NavigationLocationUpdate
        ): Response<NavigationSnapshotDto> {
            locationCalls++
            return locationResponse
        }

        override suspend fun boarding(sessionId: String): Response<NavigationSnapshotDto> =
            Response.success(snapshot())

        override suspend fun alighting(sessionId: String): Response<NavigationSnapshotDto> =
            Response.success(snapshot())

        override suspend fun resolveAlightStatus(
            sessionId: String,
            request: ResolveAlightStatusRequest
        ): Response<NavigationSnapshotDto> = Response.success(snapshot())

        override suspend fun cancel(sessionId: String): Response<TripSessionDto> = Response.success(null)

        override suspend fun reroute(
            sessionId: String,
            request: NavigationRerouteRequest
        ): Response<NavigationSnapshotDto> = Response.success(snapshot())
    }

    private companion object {
        fun snapshot() = NavigationSnapshotDto(
            sessionId = "session-1",
            state = "WalkingToDestination",
            currentLegIndex = 0,
            currentLeg = NavigationLegDto(
                legIndex = 0,
                transportMode = "WALK",
                routeId = null,
                routeName = null,
                fromName = "Origin",
                toName = "Destination",
                startLatitude = 15.0,
                startLongitude = 120.0,
                endLatitude = 15.01,
                endLongitude = 120.01,
                distanceMeters = 1500.0,
                fare = BigDecimal.ZERO
            ),
            nextInstruction = null,
            spokenInstruction = "Diretso lang tayo.",
            remainingDistanceMeters = 1200.0,
            progressMeters = 300.0,
            boardInfo = null,
            alightInfo = null,
            landmark = null,
            requiresBoardingConfirmation = false,
            requiresAlightingConfirmation = false,
            rerouteRequired = false,
            status = "ON_ROUTE",
            triggeredEvents = emptyList(),
            spokenInstructionTemplate = "Diretso lang tayo nang {distance}."
        )

        fun <T> errorResponse(code: Int): Response<T> = Response.error(
            code,
            "{\"error\":\"offline\"}".toResponseBody("application/json".toMediaType())
        )
    }
}
