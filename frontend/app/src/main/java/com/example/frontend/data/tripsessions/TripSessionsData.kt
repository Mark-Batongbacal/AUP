package com.example.frontend.data.tripsessions

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import com.google.gson.JsonElement
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import java.math.BigDecimal

data class CreateTripSessionRequest(val recommendationId: String)
data class RerouteRequest(val reason: String = "OFF_ROUTE")

data class TripSessionDto(
    val tripSessionId: String,
    val userId: String,
    val recommendationId: String,
    val originLatitude: Double,
    val originLongitude: Double,
    val destinationLatitude: Double,
    val destinationLongitude: Double,
    val destinationName: String?,
    val currentLegIndex: Int,
    val currentNavigationState: Int,
    val currentProgressMeters: Double,
    val currentRouteProgressMeters: Double?,
    val startedAt: String?,
    val lastLocationAt: String?,
    val lastLatitude: Double?,
    val lastLongitude: Double?,
    val lastAccuracyMeters: Double?,
    val consecutiveStateConfirmationSamples: Int,
    val consecutiveOffRouteSamples: Int,
    val offRouteSuspectedAt: String?,
    val lastRerouteReason: String?,
    val lastNavigationStatus: String?,
    val completedAt: String?,
    val cancelledAt: String?,
    val originalBudget: BigDecimal?,
    val originalPreference: String?,
    val lastRerouteAt: String?,
    val rerouteCount: Int,
    val createdAt: String,
    val updatedAt: String,
    val user: JsonElement?,
    val recommendation: JsonElement?,
    val navigationInstructions: List<NavigationInstructionDto>,
    val cachedLandmarks: List<TripLandmarkCandidateDto>
)

data class NavigationInstructionDto(
    val navigationInstructionId: String,
    val tripSessionId: String,
    val sequence: Int,
    val type: Int,
    val audience: Int,
    val legIndex: Int,
    val text: String,
    val streetName: String?,
    val sourceManeuverType: Int?,
    val beginShapeIndex: Int?,
    val endShapeIndex: Int?,
    val latitude: Double?,
    val longitude: Double?,
    val distanceFromLegStartMeters: Double?,
    val distanceFromRouteStartMeters: Double?,
    val triggerDistanceMeters: Double,
    val requiresConfirmation: Boolean,
    val tripSession: JsonElement?
)

data class TripLandmarkCandidateDto(
    val tripLandmarkCandidateId: String,
    val tripSessionId: String,
    val legIndex: Int,
    val externalPlaceId: String,
    val name: String,
    val category: String,
    val latitude: Double,
    val longitude: Double,
    val distanceFromRouteStartMeters: Double,
    val triggerBeforeMeters: Double,
    val triggerAfterMeters: Double,
    val cachedAt: String,
    val triggeredAt: String?,
    val tripSession: JsonElement?
)

data class LocationUpdate(
    val latitude: Double,
    val longitude: Double,
    val accuracyMeters: Double,
    val timestamp: String,
    val speedMetersPerSecond: Double? = null,
    val bearingDegrees: Double? = null
)

data class LocationUpdateResultDto(
    val accepted: Boolean,
    val status: String,
    val distanceFromLegStartMeters: Double?,
    val distanceFromRouteStartMeters: Double?,
    val distanceFromGeometryMeters: Double?,
    val triggeredInstructions: List<NavigationInstructionDto>?
)

data class RerouteResultDto(val succeeded: Boolean, val status: String, val recommendationId: String?)

sealed interface NavigationState {
    data class Known(val wireValue: Int, val name: String) : NavigationState
    data class Unknown(val wireValue: Int) : NavigationState

    companion object {
        private val names = listOf("Planned", "Starting", "WalkingToPickup", "ApproachingBoardPoint", "WaitingToBoard", "OnJeepney", "OnTricycle", "ApproachingAlightPoint", "Transferring", "WalkingToDestination", "OffRoute", "Rerouting", "Arrived", "Cancelled")
        fun fromWireValue(value: Int): NavigationState = names.getOrNull(value)?.let { Known(value, it) } ?: Unknown(value)
    }
}

interface TripSessionsApi {
    @POST("api/tripsessions") suspend fun create(@Body request: CreateTripSessionRequest): Response<TripSessionDto>
    @GET("api/tripsessions/{id}") suspend fun get(@Path("id") id: String): Response<TripSessionDto>
    @GET("api/tripsessions/active") suspend fun active(): Response<TripSessionDto>
    @POST("api/tripsessions/{id}/start") suspend fun start(@Path("id") id: String): Response<TripSessionDto>
    @POST("api/tripsessions/{id}/cancel") suspend fun cancel(@Path("id") id: String): Response<TripSessionDto>
    @POST("api/tripsessions/{id}/boarding-confirmed") suspend fun boarding(@Path("id") id: String): Response<TripSessionDto>
    @POST("api/tripsessions/{id}/alighting-confirmed") suspend fun alighting(@Path("id") id: String): Response<TripSessionDto>
    @GET("api/tripsessions/{id}/instructions") suspend fun instructions(@Path("id") id: String): Response<List<NavigationInstructionDto>>
    @POST("api/tripsessions/{id}/location") suspend fun location(@Path("id") id: String, @Body update: LocationUpdate): Response<LocationUpdateResultDto>
    @POST("api/tripsessions/{id}/reroute") suspend fun reroute(@Path("id") id: String, @Body request: RerouteRequest): Response<RerouteResultDto>
}

interface TripSessionRepository {
    suspend fun create(request: CreateTripSessionRequest): ApiResult<TripSessionDto>
    suspend fun get(id: String): ApiResult<TripSessionDto>
    suspend fun getActive(): ApiResult<TripSessionDto>
    suspend fun start(id: String): ApiResult<TripSessionDto>
    suspend fun cancel(id: String): ApiResult<TripSessionDto>
    suspend fun confirmBoarding(id: String): ApiResult<TripSessionDto>
    suspend fun confirmAlighting(id: String): ApiResult<TripSessionDto>
    suspend fun getInstructions(id: String): ApiResult<List<NavigationInstructionDto>>
    suspend fun updateLocation(id: String, update: LocationUpdate): ApiResult<LocationUpdateResultDto>
    suspend fun reroute(id: String, reason: String = "OFF_ROUTE"): ApiResult<RerouteResultDto>
}

class TripSessionRepositoryImpl(private val api: TripSessionsApi, private val sessions: AuthSessionStore, private val errors: ApiErrorParser) : TripSessionRepository {
    override suspend fun create(request: CreateTripSessionRequest) = call { api.create(request) }
    override suspend fun get(id: String) = call { api.get(id) }
    override suspend fun getActive() = call { api.active() }
    override suspend fun start(id: String) = call { api.start(id) }
    override suspend fun cancel(id: String) = call { api.cancel(id) }
    override suspend fun confirmBoarding(id: String) = call { api.boarding(id) }
    override suspend fun confirmAlighting(id: String) = call { api.alighting(id) }
    override suspend fun getInstructions(id: String) = call { api.instructions(id) }
    override suspend fun updateLocation(id: String, update: LocationUpdate) = call { api.location(id, update) }
    override suspend fun reroute(id: String, reason: String) = call { api.reroute(id, RerouteRequest(reason)) }
    private suspend fun <T : Any> call(block: suspend () -> Response<T>) = authenticatedApiCall(sessions, errors, request = block)
}
