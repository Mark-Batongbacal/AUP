package com.example.frontend.data.trips

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.common.TransportModeSummaryDto
import com.example.frontend.data.common.TransportStopSummaryDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import java.math.BigDecimal

data class StartTripRequest(val recommendationId: String?, val startedAt: String? = null)

data class TransportRouteSummaryDto(
    val routeId: Long,
    val routeCode: String,
    val routeName: String,
    val transportModeId: Int,
    val baseFare: BigDecimal?,
    val estimatedTotalMinutes: Int?,
    val isActive: Boolean
)

data class RecommendationLegDto(
    val legId: String,
    val recommendationId: String,
    val legOrder: Int,
    val transportModeId: Int,
    val transportMode: TransportModeSummaryDto?,
    val routeId: Long?,
    val route: TransportRouteSummaryDto?,
    val fromStopId: Long?,
    val fromStop: TransportStopSummaryDto?,
    val toStopId: Long?,
    val toStop: TransportStopSummaryDto?,
    val fromName: String?,
    val toName: String?,
    val startLatitude: Double?,
    val startLongitude: Double?,
    val endLatitude: Double?,
    val endLongitude: Double?,
    val distanceMeters: BigDecimal?,
    val estimatedMinutes: BigDecimal,
    val estimatedFare: BigDecimal,
    val instructions: String?,
    val createdAt: String
)

data class RecommendationDetailsDto(
    val recommendationId: String,
    val tripSearchId: String,
    val recommendationType: String,
    val rankNumber: Int,
    val totalFare: BigDecimal,
    val totalMinutes: BigDecimal,
    val totalDistanceMeters: BigDecimal?,
    val walkingDistanceMeters: BigDecimal,
    val transferCount: Int,
    val recommendationScore: BigDecimal?,
    val explanation: String?,
    val generatedAt: String,
    val legs: List<RecommendationLegDto>
)

data class TripAlertDto(
    val alertId: String,
    val passengerTripId: String,
    val legId: String?,
    val targetStopId: Long?,
    val targetStop: TransportStopSummaryDto?,
    val alertType: String,
    val title: String?,
    val message: String,
    val triggerDistanceMeters: BigDecimal?,
    val isTriggered: Boolean,
    val triggeredAt: String?,
    val createdAt: String
)

data class PassengerTripDetailsDto(
    val passengerTripId: String,
    val userId: String,
    val recommendationId: String,
    val currentLegOrder: Int,
    val status: String,
    val startedAt: String?,
    val completedAt: String?,
    val createdAt: String,
    val updatedAt: String,
    val recommendation: RecommendationDetailsDto?,
    val alerts: List<TripAlertDto>
)

data class PassengerTripHistoryItemDto(
    val passengerTripId: String,
    val status: String,
    val originName: String,
    val destinationName: String,
    val originLatitude: Double,
    val originLongitude: Double,
    val destinationLatitude: Double,
    val destinationLongitude: Double,
    val startedAt: String?,
    val completedAt: String?,
    val createdAt: String,
    val recommendation: RecommendationDetailsDto?,
    val rerouted: Boolean = false,
    val rerouteCount: Int = 0,
    val lastRerouteReason: String? = null,
    val lastRerouteAt: String? = null
)

interface TripsApi {
    @GET("api/trips") suspend fun history(): Response<List<PassengerTripHistoryItemDto>>
    @GET("api/trips/recent") suspend fun recent(): Response<List<PassengerTripHistoryItemDto>>
    @POST("api/trips") suspend fun start(@Body request: StartTripRequest): Response<PassengerTripDetailsDto>
    @GET("api/trips/{tripId}") suspend fun get(@Path("tripId") tripId: String): Response<PassengerTripDetailsDto>
    @GET("api/trips/{tripId}/alerts") suspend fun alerts(@Path("tripId") tripId: String): Response<List<TripAlertDto>>
}

interface TripRepository {
    suspend fun getHistory(): ApiResult<List<PassengerTripHistoryItemDto>>
    suspend fun getRecentJourneys(): ApiResult<List<PassengerTripHistoryItemDto>>
    suspend fun startTrip(request: StartTripRequest): ApiResult<PassengerTripDetailsDto>
    suspend fun getTrip(tripId: String): ApiResult<PassengerTripDetailsDto>
    suspend fun getTripAlerts(tripId: String): ApiResult<List<TripAlertDto>>
}

class TripRepositoryImpl(
    private val api: TripsApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : TripRepository {
    override suspend fun getHistory(): ApiResult<List<PassengerTripHistoryItemDto>> {
        val history = authenticatedApiCall(sessions, errors) { api.history() }
        if (history is ApiResult.Success && history.data.isNotEmpty()) {
            return history
        }
        if (history is ApiResult.Failure && history.isUnauthorized) {
            return history
        }
        return authenticatedApiCall(sessions, errors) { api.recent() }
    }

    override suspend fun getRecentJourneys() = authenticatedApiCall(sessions, errors) { api.recent() }
    override suspend fun startTrip(request: StartTripRequest) = authenticatedApiCall(sessions, errors) { api.start(request) }
    override suspend fun getTrip(tripId: String) = authenticatedApiCall(sessions, errors) { api.get(tripId) }
    override suspend fun getTripAlerts(tripId: String) = authenticatedApiCall(sessions, errors) { api.alerts(tripId) }
}
