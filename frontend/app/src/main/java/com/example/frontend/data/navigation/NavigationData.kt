package com.example.frontend.data.navigation

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.apiCall
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.tripsessions.TripSessionDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query
import java.math.BigDecimal

data class StartNavigationRequest(val recommendationId: String)
data class NavigationRerouteRequest(
    val reason: String = "MANUAL",
    val preference: String? = null,
    val budget: BigDecimal? = null,
    val clearBudget: Boolean = false,
    val destinationName: String? = null,
    val destinationLatitude: Double? = null,
    val destinationLongitude: Double? = null
)
data class NavigationGeometryPointDto(val latitude: Double, val longitude: Double)
data class NavigationGeometryResponseDto(val points: List<NavigationGeometryPointDto>)

data class NavigationLocationUpdate(
    val latitude: Double,
    val longitude: Double,
    val accuracyMeters: Double,
    val timestamp: String,
    val speedMetersPerSecond: Double? = null,
    val bearingDegrees: Double? = null
)

data class NavigationLegDto(
    val legIndex: Int,
    val transportMode: String,
    val routeId: Long? = null,
    val routeName: String?,
    val fromName: String?,
    val toName: String?,
    val startLatitude: Double?,
    val startLongitude: Double?,
    val endLatitude: Double?,
    val endLongitude: Double?,
    val distanceMeters: Double?,
    val fare: BigDecimal
)

data class NavigationInstructionSnapshotDto(
    val type: String,
    val routeName: String?,
    val transportMode: String?,
    val distanceMeters: Double?,
    val requiresConfirmation: Boolean,
    val text: String? = null
)

data class NavigationLandmarkDto(
    val name: String,
    val category: String,
    val role: String,
    val relation: String,
    val latitude: Double,
    val longitude: Double,
    val distanceFromTargetMeters: Double
)

data class NavigationStopInfoDto(val routeName: String?, val latitude: Double?, val longitude: Double?, val landmark: NavigationLandmarkDto?)
data class NavigationTriggeredEventDto(val type: String, val landmarkName: String?)
data class NavigationTripSummaryDto(
    val destinationName: String,
    val durationMinutes: Int?,
    val approxFareSpent: BigDecimal,
    val transitLegs: Int,
    val transfers: Int
)

data class NavigationSnapshotDto(
    val sessionId: String,
    val state: String,
    val currentLegIndex: Int,
    val currentLeg: NavigationLegDto?,
    val nextInstruction: NavigationInstructionSnapshotDto?,
    val spokenInstruction: String?,
    val remainingDistanceMeters: Double?,
    val progressMeters: Double,
    val boardInfo: NavigationStopInfoDto?,
    val alightInfo: NavigationStopInfoDto?,
    val landmark: NavigationLandmarkDto?,
    val requiresBoardingConfirmation: Boolean,
    val requiresAlightingConfirmation: Boolean,
    val rerouteRequired: Boolean,
    val status: String,
    val triggeredEvents: List<NavigationTriggeredEventDto>,
    val currentLatitude: Double? = null,
    val currentLongitude: Double? = null,
    val approxFareSpent: BigDecimal = BigDecimal.ZERO,
    val estimatedRemainingFare: BigDecimal = BigDecimal.ZERO,
    val followingInstruction: NavigationInstructionSnapshotDto? = null,
    val tripSummary: NavigationTripSummaryDto? = null
) {
    fun displayInstruction(): String? = spokenInstruction?.takeIf { it.isNotBlank() }
}

interface NavigationApi {
    @POST("api/navigation/start") suspend fun start(@Body request: StartNavigationRequest): Response<NavigationSnapshotDto>
    @GET("api/navigation/active") suspend fun active(): Response<NavigationSnapshotDto>
    @GET("api/navigation/geometry") suspend fun geometry(
        @Query("startLat") startLatitude: Double,
        @Query("startLon") startLongitude: Double,
        @Query("endLat") endLatitude: Double,
        @Query("endLon") endLongitude: Double,
        @Query("mode") mode: String,
        @Query("routeId") routeId: Long? = null
    ): Response<NavigationGeometryResponseDto>
    @POST("api/navigation/{sessionId}/location") suspend fun location(@Path("sessionId") sessionId: String, @Body update: NavigationLocationUpdate): Response<NavigationSnapshotDto>
    @POST("api/navigation/{sessionId}/boarding") suspend fun boarding(@Path("sessionId") sessionId: String): Response<NavigationSnapshotDto>
    @POST("api/navigation/{sessionId}/alighting") suspend fun alighting(@Path("sessionId") sessionId: String): Response<NavigationSnapshotDto>
    @POST("api/tripsessions/{sessionId}/cancel") suspend fun cancel(@Path("sessionId") sessionId: String): Response<TripSessionDto>
    @POST("api/navigation/{sessionId}/reroute") suspend fun reroute(@Path("sessionId") sessionId: String, @Body request: NavigationRerouteRequest): Response<NavigationSnapshotDto>
}

interface NavigationRepository {
    suspend fun startNavigation(recommendationId: String): ApiResult<NavigationSnapshotDto>
    suspend fun getActiveNavigation(): ApiResult<NavigationSnapshotDto>
    suspend fun getGeometry(startLatitude: Double, startLongitude: Double, endLatitude: Double, endLongitude: Double, mode: String, routeId: Long? = null): ApiResult<NavigationGeometryResponseDto>
    suspend fun updateLocation(sessionId: String, update: NavigationLocationUpdate): ApiResult<NavigationSnapshotDto>
    suspend fun confirmBoarding(sessionId: String): ApiResult<NavigationSnapshotDto>
    suspend fun confirmAlighting(sessionId: String): ApiResult<NavigationSnapshotDto>
    suspend fun cancel(sessionId: String): ApiResult<TripSessionDto>
    suspend fun reroute(sessionId: String, request: NavigationRerouteRequest = NavigationRerouteRequest()): ApiResult<NavigationSnapshotDto>
}

class NavigationRepositoryImpl(
    private val api: NavigationApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : NavigationRepository {
    override suspend fun startNavigation(recommendationId: String) = call { api.start(StartNavigationRequest(recommendationId)) }
    override suspend fun getActiveNavigation() = call { api.active() }
    override suspend fun getGeometry(startLatitude: Double, startLongitude: Double, endLatitude: Double, endLongitude: Double, mode: String, routeId: Long?) =
        apiCall(errors) { api.geometry(startLatitude, startLongitude, endLatitude, endLongitude, mode, routeId) }
    override suspend fun updateLocation(sessionId: String, update: NavigationLocationUpdate) = call { api.location(sessionId, update) }
    override suspend fun confirmBoarding(sessionId: String) = call { api.boarding(sessionId) }
    override suspend fun confirmAlighting(sessionId: String) = call { api.alighting(sessionId) }
    override suspend fun cancel(sessionId: String) = call { api.cancel(sessionId) }
    override suspend fun reroute(sessionId: String, request: NavigationRerouteRequest) = call { api.reroute(sessionId, request) }
    private suspend fun <T : Any> call(block: suspend () -> Response<T>) = authenticatedApiCall(sessions, errors, request = block)
}
