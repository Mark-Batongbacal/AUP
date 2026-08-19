package com.example.frontend.previews

import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.auth.AuthIdentityDto
import com.example.frontend.data.auth.AuthRepository
import com.example.frontend.data.auth.AuthenticatedUser
import com.example.frontend.data.auth.RegisterRequest
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.places.PlacesRepository
import com.example.frontend.data.routing.JourneyPlan
import com.example.frontend.data.routing.JourneyPlanRequest
import com.example.frontend.data.routing.NearbyJeepneyRouteDto
import com.example.frontend.data.routing.PlannedJourney
import com.example.frontend.data.routing.RoutingRepository
import com.example.frontend.data.trips.PassengerTripDetailsDto
import com.example.frontend.data.trips.PassengerTripHistoryItemDto
import com.example.frontend.data.trips.StartTripRequest
import com.example.frontend.data.trips.TripAlertDto
import com.example.frontend.data.trips.TripRepository

object PreviewMocks {
    val authRepository = object : AuthRepository {
        override suspend fun login(userName: String, password: String): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun register(request: RegisterRequest): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun loginWithGoogle(idToken: String): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun loginWithFacebook(accessToken: String): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun loginWithFacebookOidc(idToken: String, nonce: String): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun getCurrentAuthIdentity(): ApiResult<AuthIdentityDto> = ApiResult.Failure(null, "Mock")
        override fun logoutLocalSession() {}
    }

    val placesRepository = object : PlacesRepository {
        override suspend fun searchPlaces(
            query: String,
            focusLatitude: Double?,
            focusLongitude: Double?
        ): ApiResult<List<DestinationSearchResultDto>> = ApiResult.Success(emptyList())
    }

    val routingRepository = object : RoutingRepository {
        override suspend fun planJourneys(request: JourneyPlanRequest): ApiResult<List<PlannedJourney>> = ApiResult.Success(emptyList())
        override suspend fun findNearbyRoutes(latitude: Double, longitude: Double): ApiResult<List<NearbyJeepneyRouteDto>> = ApiResult.Success(emptyList())
        override suspend fun planTrip(originLatitude: Double, originLongitude: Double, destinationLatitude: Double, destinationLongitude: Double): ApiResult<List<JourneyPlan>> = ApiResult.Success(emptyList())
    }

    val tripRepository = object : TripRepository {
        override suspend fun getHistory(): ApiResult<List<PassengerTripHistoryItemDto>> = ApiResult.Success(emptyList())
        override suspend fun startTrip(request: StartTripRequest): ApiResult<PassengerTripDetailsDto> = ApiResult.Failure(null, "Mock")
        override suspend fun getTrip(tripId: String): ApiResult<PassengerTripDetailsDto> = ApiResult.Failure(null, "Mock")
        override suspend fun getTripAlerts(tripId: String): ApiResult<List<TripAlertDto>> = ApiResult.Success(emptyList())
    }
}
