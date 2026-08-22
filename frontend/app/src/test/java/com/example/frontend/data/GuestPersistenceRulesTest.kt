package com.example.frontend.data

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.storage.AuthSession
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.core.location.LocationNotSupportedShortMessage
import com.example.frontend.data.favorites.AddFavoriteTripRequest
import com.example.frontend.data.favorites.FavoriteTripDto
import com.example.frontend.data.favorites.FavoritesApi
import com.example.frontend.data.favorites.FavoritesRepositoryImpl
import com.example.frontend.data.navigation.NavigationApi
import com.example.frontend.data.navigation.NavigationGeometryResponseDto
import com.example.frontend.data.navigation.NavigationLocationUpdate
import com.example.frontend.data.navigation.NavigationRepositoryImpl
import com.example.frontend.data.navigation.NavigationRerouteRequest
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.navigation.StartNavigationRequest
import com.example.frontend.data.routing.JeepneyAccessSegmentDto
import com.example.frontend.data.routing.JeepneyTripPlanDto
import com.example.frontend.data.routing.JourneyPlanRequest
import com.example.frontend.data.routing.MobileJourneyRecommendationDto
import com.example.frontend.data.routing.NearbyJeepneyRouteDto
import com.example.frontend.data.routing.RoutingApi
import com.example.frontend.data.routing.RoutingRepositoryImpl
import com.example.frontend.data.trips.PassengerTripDetailsDto
import com.example.frontend.data.trips.PassengerTripHistoryItemDto
import com.example.frontend.data.trips.StartTripRequest
import com.example.frontend.data.trips.TripAlertDto
import com.example.frontend.data.trips.TripRepositoryImpl
import com.example.frontend.data.trips.TripsApi
import com.example.frontend.data.tripsessions.TripSessionDto
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class GuestPersistenceRulesTest {
    @Test
    fun routePlanning_withoutSession_stillCallsAnonymousPlanningApi() = runBlocking {
        val api = FakeRoutingApi()
        val repository = RoutingRepositoryImpl(api, MemorySessionStore(null), ApiErrorParser())

        val result = repository.planJourneys(
            JourneyPlanRequest(15.1453, 120.5887, "Dau Terminal", 15.1790, 120.5900)
        )

        assertTrue(result is ApiResult.Success)
        assertTrue(api.planJourneysCalled)
    }

    @Test
    fun routePlanning_withUnsupportedOrigin_isBlockedBeforeNetwork() = runBlocking {
        val api = FakeRoutingApi()
        val repository = RoutingRepositoryImpl(api, MemorySessionStore(null), ApiErrorParser())

        val result = repository.planJourneys(
            JourneyPlanRequest(14.5995, 120.9842, "Dau Terminal", 15.1790, 120.5900)
        )

        assertTrue(result is ApiResult.Failure)
        assertEquals(LocationNotSupportedShortMessage, (result as ApiResult.Failure).message)
        assertFalse(api.planJourneysCalled)
    }

    @Test
    fun routePlanning_withUnsupportedDestination_isBlockedBeforeNetwork() = runBlocking {
        val api = FakeRoutingApi()
        val repository = RoutingRepositoryImpl(api, MemorySessionStore(null), ApiErrorParser())

        val result = repository.planJourneys(
            JourneyPlanRequest(15.1453, 120.5887, "Manila", 14.5995, 120.9842)
        )

        assertTrue(result is ApiResult.Failure)
        assertEquals(LocationNotSupportedShortMessage, (result as ApiResult.Failure).message)
        assertFalse(api.planJourneysCalled)
    }

    @Test
    fun recentJourneys_withoutSession_areBlockedBeforeNetwork() = runBlocking {
        val api = FakeTripsApi()
        val repository = TripRepositoryImpl(api, MemorySessionStore(null), ApiErrorParser())

        val result = repository.getRecentJourneys()

        assertTrue(result is ApiResult.Failure && result.isUnauthorized)
        assertFalse(api.recentCalled)
    }

    @Test
    fun addFavorite_withoutSession_isBlockedBeforeNetwork() = runBlocking {
        val api = FakeFavoritesApi()
        val repository = FavoritesRepositoryImpl(api, MemorySessionStore(null), ApiErrorParser())

        val result = repository.addFavorite("recommendation-1")

        assertTrue(result is ApiResult.Failure && result.isUnauthorized)
        assertFalse(api.addCalled)
    }

    @Test
    fun cancelNavigation_withoutSession_isBlockedBeforeNetwork() = runBlocking {
        val api = FakeNavigationApi()
        val repository = NavigationRepositoryImpl(api, MemorySessionStore(null), ApiErrorParser())

        val result = repository.cancel("session-1")

        assertTrue(result is ApiResult.Failure && result.isUnauthorized)
        assertFalse(api.cancelCalled)
    }

    private class MemorySessionStore(initial: AuthSession?) : AuthSessionStore {
        private var value = initial
        override fun read() = value
        override fun save(session: AuthSession) { value = session }
        override fun clear() { value = null }
    }

    private class FakeRoutingApi : RoutingApi {
        var planJourneysCalled = false

        override suspend fun planJourneys(
            request: JourneyPlanRequest
        ): Response<List<MobileJourneyRecommendationDto>> {
            planJourneysCalled = true
            return Response.success(
                listOf(
                    MobileJourneyRecommendationDto(
                        "transient-id",
                        JeepneyTripPlanDto(
                            recommendationType = "fastest",
                            legs = emptyList(),
                            originAccess = accessSegment(),
                            destinationAccess = accessSegment(),
                            transferWalkDistancesMeters = emptyList(),
                            transferWalkTimesSeconds = emptyList(),
                            totalTimeSeconds = 600.0,
                            totalFarePesos = 12.0,
                            generalizedCostPesos = 22.0,
                            transferCount = 0
                        )
                    )
                )
            )
        }

        override suspend fun nearby(
            latitude: Double,
            longitude: Double
        ): Response<List<NearbyJeepneyRouteDto>> = Response.success(emptyList())

        override suspend fun plan(
            originLatitude: Double,
            originLongitude: Double,
            destinationLatitude: Double,
            destinationLongitude: Double
        ): Response<List<JeepneyTripPlanDto>> = Response.success(emptyList())
    }

    private class FakeTripsApi : TripsApi {
        var recentCalled = false
        override suspend fun history(): Response<List<PassengerTripHistoryItemDto>> = Response.success(emptyList())
        override suspend fun recent(): Response<List<PassengerTripHistoryItemDto>> {
            recentCalled = true
            return Response.success(emptyList())
        }
        override suspend fun start(request: StartTripRequest): Response<PassengerTripDetailsDto> =
            error("not used")
        override suspend fun get(tripId: String): Response<PassengerTripDetailsDto> = error("not used")
        override suspend fun alerts(tripId: String): Response<List<TripAlertDto>> = Response.success(emptyList())
    }

    private class FakeFavoritesApi : FavoritesApi {
        var addCalled = false
        override suspend fun list(): Response<List<FavoriteTripDto>> = Response.success(emptyList())
        override suspend fun add(request: AddFavoriteTripRequest): Response<FavoriteTripDto> {
            addCalled = true
            return error("not used")
        }
        override suspend fun remove(favoriteTripId: String): Response<Unit> = Response.success(Unit)
    }

    private class FakeNavigationApi : NavigationApi {
        var cancelCalled = false
        override suspend fun start(request: StartNavigationRequest): Response<NavigationSnapshotDto> = error("not used")
        override suspend fun active(): Response<NavigationSnapshotDto> = error("not used")
        override suspend fun geometry(
            startLatitude: Double,
            startLongitude: Double,
            endLatitude: Double,
            endLongitude: Double,
            mode: String,
            routeId: Long?
        ): Response<NavigationGeometryResponseDto> = Response.success(NavigationGeometryResponseDto(emptyList()))
        override suspend fun location(
            sessionId: String,
            update: NavigationLocationUpdate
        ): Response<NavigationSnapshotDto> = error("not used")
        override suspend fun boarding(sessionId: String): Response<NavigationSnapshotDto> = error("not used")
        override suspend fun alighting(sessionId: String): Response<NavigationSnapshotDto> = error("not used")
        override suspend fun cancel(sessionId: String): Response<TripSessionDto> {
            cancelCalled = true
            return error("not used")
        }
        override suspend fun reroute(
            sessionId: String,
            request: NavigationRerouteRequest
        ): Response<NavigationSnapshotDto> = error("not used")
    }

    private companion object {
        fun accessSegment() = JeepneyAccessSegmentDto(
            mode = 0,
            walkDistanceMeters = 0.0,
            walkTimeSeconds = 0.0,
            trikePointId = null,
            trikePointName = null,
            trikePointLatitude = null,
            trikePointLongitude = null,
            trikeRideDistanceMeters = null,
            trikeRideTimeSeconds = null,
            totalTimeSeconds = 0.0,
            totalFarePesos = 0.0,
            generalizedCostPesos = 0.0
        )
    }
}
