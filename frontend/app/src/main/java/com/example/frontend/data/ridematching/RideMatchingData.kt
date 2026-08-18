package com.example.frontend.data.ridematching

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.common.DriverAvailabilitySessionDto
import com.example.frontend.data.common.DriverVehicleDto
import com.example.frontend.data.common.TransportModeSummaryDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import java.math.BigDecimal

data class CreateRideRequestRequest(
    val pickupName: String?,
    val pickupLatitude: Double?,
    val pickupLongitude: Double?,
    val dropoffName: String?,
    val dropoffLatitude: Double?,
    val dropoffLongitude: Double?,
    val passengerCount: Int = 1,
    val transportModeId: Int? = null,
    val maxBudget: BigDecimal? = null,
    val requestedAt: String? = null,
    val expiresAt: String? = null
)

data class CreateRideMatchRequest(
    val driverId: String?,
    val vehicleId: String? = null,
    val pickupDistanceMeters: BigDecimal? = null,
    val detourDistanceMeters: BigDecimal? = null,
    val estimatedPickupMinutes: BigDecimal? = null,
    val estimatedTripMinutes: BigDecimal? = null,
    val estimatedFare: BigDecimal? = null,
    val matchScore: BigDecimal? = null,
    val offeredAt: String? = null
)

data class AcceptRideMatchRequest(val acceptedAt: String? = null)

data class RideRequestDetailsDto(
    val requestId: String,
    val passengerUserId: String,
    val transportModeId: Int?,
    val transportMode: TransportModeSummaryDto?,
    val pickupName: String?,
    val pickupLatitude: Double,
    val pickupLongitude: Double,
    val dropoffName: String?,
    val dropoffLatitude: Double,
    val dropoffLongitude: Double,
    val passengerCount: Int,
    val maxBudget: BigDecimal?,
    val status: String,
    val requestedAt: String,
    val expiresAt: String?,
    val updatedAt: String
)

data class DriverSummaryDto(
    val driverId: String,
    val userId: String,
    val licenseNumber: String?,
    val verificationStatus: String,
    val homeTerminalId: Long?,
    val averageRating: BigDecimal?,
    val ratingCount: Int,
    val isAvailable: Boolean,
    val createdAt: String,
    val updatedAt: String?
)

data class RideMatchDetailsDto(
    val matchId: String,
    val requestId: String,
    val driverId: String,
    val sessionId: Long?,
    val vehicleId: String?,
    val pickupDistanceMeters: BigDecimal?,
    val detourDistanceMeters: BigDecimal?,
    val estimatedPickupMinutes: BigDecimal?,
    val estimatedTripMinutes: BigDecimal?,
    val estimatedFare: BigDecimal?,
    val matchScore: BigDecimal?,
    val status: String,
    val offeredAt: String,
    val acceptedAt: String?,
    val completedAt: String?,
    val request: RideRequestDetailsDto?,
    val driver: DriverSummaryDto?,
    val availabilitySession: DriverAvailabilitySessionDto?,
    val vehicle: DriverVehicleDto?
)

interface RideMatchingApi {
    @POST("api/ride-matching/requests") suspend fun createRequest(@Body request: CreateRideRequestRequest): Response<RideRequestDetailsDto>
    @GET("api/ride-matching/requests/{requestId}") suspend fun getRequest(@Path("requestId") id: String): Response<RideRequestDetailsDto>
    @POST("api/ride-matching/requests/{requestId}/match") suspend fun createMatch(@Path("requestId") id: String, @Body request: CreateRideMatchRequest): Response<RideMatchDetailsDto>
    @GET("api/ride-matching/matches/{matchId}") suspend fun getMatch(@Path("matchId") id: String): Response<RideMatchDetailsDto>
    @POST("api/ride-matching/matches/{matchId}/accept") suspend fun accept(@Path("matchId") id: String, @Body request: AcceptRideMatchRequest): Response<Unit>
    @POST("api/ride-matching/matches/{matchId}/reject") suspend fun reject(@Path("matchId") id: String): Response<Unit>
    @POST("api/ride-matching/matches/{matchId}/cancel") suspend fun cancel(@Path("matchId") id: String): Response<Unit>
}

interface RideMatchingRepository {
    suspend fun createRideRequest(request: CreateRideRequestRequest): ApiResult<RideRequestDetailsDto>
    suspend fun getRideRequest(id: String): ApiResult<RideRequestDetailsDto>
    suspend fun getMatch(id: String): ApiResult<RideMatchDetailsDto>
    suspend fun acceptMatch(id: String, request: AcceptRideMatchRequest = AcceptRideMatchRequest()): ApiResult<Unit>
    suspend fun rejectMatch(id: String): ApiResult<Unit>
    suspend fun cancelMatch(id: String): ApiResult<Unit>
}

class RideMatchingRepositoryImpl(private val api: RideMatchingApi, private val sessions: AuthSessionStore, private val errors: ApiErrorParser) : RideMatchingRepository {
    override suspend fun createRideRequest(request: CreateRideRequestRequest) = call { api.createRequest(request) }
    override suspend fun getRideRequest(id: String) = call { api.getRequest(id) }
    override suspend fun getMatch(id: String) = call { api.getMatch(id) }
    override suspend fun acceptMatch(id: String, request: AcceptRideMatchRequest) = emptyCall { api.accept(id, request) }
    override suspend fun rejectMatch(id: String) = emptyCall { api.reject(id) }
    override suspend fun cancelMatch(id: String) = emptyCall { api.cancel(id) }
    private suspend fun <T : Any> call(block: suspend () -> Response<T>) = authenticatedApiCall(sessions, errors, request = block)
    private suspend fun emptyCall(block: suspend () -> Response<Unit>) = authenticatedApiCall(sessions, errors, Unit, block)
}
