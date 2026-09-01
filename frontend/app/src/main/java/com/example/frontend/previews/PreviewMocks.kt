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
        override suspend fun loginAsGuest(): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun register(request: RegisterRequest): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun requestRegistrationOtp(email: String): ApiResult<Unit> = ApiResult.Failure(null, "Mock")
        override suspend fun verifyRegistrationOtp(email: String, code: String): ApiResult<Unit> = ApiResult.Failure(null, "Mock")
        override suspend fun loginWithGoogle(idToken: String): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun loginWithFacebook(accessToken: String): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun loginWithFacebookOidc(idToken: String, nonce: String): ApiResult<AuthenticatedUser> = ApiResult.Failure(null, "Mock")
        override suspend fun getCurrentAuthIdentity(): ApiResult<AuthIdentityDto> = ApiResult.Failure(null, "Mock")
        override suspend fun requestPasswordReset(email: String): ApiResult<Unit> = ApiResult.Failure(null, "Mock")
        override suspend fun verifyPasswordResetOtp(email: String, code: String): ApiResult<Unit> = ApiResult.Failure(null, "Mock")
        override suspend fun resetPassword(email: String, code: String, newPassword: String): ApiResult<Unit> = ApiResult.Failure(null, "Mock")
        override suspend fun requestChangePasswordOtp(currentPassword: String): ApiResult<Unit> = ApiResult.Failure(null, "Mock")
        override suspend fun verifyChangePasswordOtp(currentPassword: String, code: String): ApiResult<Unit> = ApiResult.Failure(null, "Mock")
        override suspend fun changePassword(currentPassword: String, code: String, newPassword: String): ApiResult<Unit> = ApiResult.Failure(null, "Mock")
        override fun logoutLocalSession() {}
    }

    val placesRepository = object : PlacesRepository {
        override suspend fun searchPlaces(
            query: String,
            focusLatitude: Double?,
            focusLongitude: Double?
        ): ApiResult<List<DestinationSearchResultDto>> = ApiResult.Success(
            listOf(
                DestinationSearchResultDto("1", "SM City Clark", 15.1764, 120.5786, "Mall", "Pelias", "Clark Freeport Zone"),
                DestinationSearchResultDto("2", "Dau Terminal", 15.1794, 120.5886, "Terminal", "Pelias", "Mabalacat City"),
                DestinationSearchResultDto("3", "AUF Main Gate", 15.1450, 120.5944, "Education", "Pelias", "Angeles City")
            )
        )

        override suspend fun searchMorePlaces(
            query: String,
            focusLatitude: Double?,
            focusLongitude: Double?
        ): ApiResult<List<DestinationSearchResultDto>> = searchPlaces(query, focusLatitude, focusLongitude)

        override suspend fun reverseGeocode(
            latitude: Double,
            longitude: Double
        ): ApiResult<DestinationSearchResultDto> = ApiResult.Success(
            DestinationSearchResultDto("loc", "Pampanga St.", latitude, longitude, "Address", "Pelias", "Mabalacat")
        )
    }

    val routingRepository = object : RoutingRepository {
        override suspend fun planJourneys(request: JourneyPlanRequest): ApiResult<List<PlannedJourney>> = ApiResult.Success(emptyList())
        override suspend fun findNearbyRoutes(latitude: Double, longitude: Double): ApiResult<List<NearbyJeepneyRouteDto>> = ApiResult.Success(emptyList())
        override suspend fun planTrip(originLatitude: Double, originLongitude: Double, destinationLatitude: Double, destinationLongitude: Double): ApiResult<List<JourneyPlan>> = ApiResult.Success(emptyList())
    }

    val tripRepository = object : TripRepository {
        override suspend fun getHistory(): ApiResult<List<PassengerTripHistoryItemDto>> = ApiResult.Success(
            listOf(
                PassengerTripHistoryItemDto(
                    passengerTripId = "1",
                    status = "Completed",
                    originName = "Home",
                    destinationName = "Office",
                    originLatitude = 15.1,
                    originLongitude = 120.1,
                    destinationLatitude = 15.2,
                    destinationLongitude = 120.2,
                    startedAt = "2026-08-20T10:00:00Z",
                    completedAt = "2026-08-20T10:30:00Z",
                    createdAt = "2026-08-20T09:50:00Z",
                    recommendation = null
                ),
                PassengerTripHistoryItemDto(
                    passengerTripId = "2",
                    status = "Completed",
                    originName = "Office",
                    destinationName = "SM Clark",
                    originLatitude = 15.2,
                    originLongitude = 120.2,
                    destinationLatitude = 15.3,
                    destinationLongitude = 120.3,
                    startedAt = "2026-08-21T18:00:00Z",
                    completedAt = "2026-08-21T18:15:00Z",
                    createdAt = "2026-08-21T17:55:00Z",
                    recommendation = null
                )
            )
        )
        override suspend fun getRecentJourneys(): ApiResult<List<PassengerTripHistoryItemDto>> = getHistory()
        override suspend fun startTrip(request: StartTripRequest): ApiResult<PassengerTripDetailsDto> = ApiResult.Failure(null, "Mock")
        override suspend fun getTrip(tripId: String): ApiResult<PassengerTripDetailsDto> = ApiResult.Failure(null, "Mock")
        override suspend fun getTripAlerts(tripId: String): ApiResult<List<TripAlertDto>> = ApiResult.Success(emptyList())
    }
}
