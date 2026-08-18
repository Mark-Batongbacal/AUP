package com.example.frontend.data.drivers

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.common.DriverAvailabilitySessionDto
import com.example.frontend.data.common.DriverLocationDto
import com.example.frontend.data.common.DriverVehicleDto
import com.example.frontend.data.common.TransportStopSummaryDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import java.math.BigDecimal

data class DriverUserProfileDto(
    val userId: String,
    val firstName: String?,
    val lastName: String?,
    val phoneNumber: String?,
    val role: String,
    val profileImageUrl: String?,
    val isActive: Boolean
)

data class DriverDetailsDto(
    val driverId: String,
    val userId: String,
    val user: DriverUserProfileDto?,
    val licenseNumber: String?,
    val verificationStatus: String,
    val homeTerminalId: Long?,
    val homeTerminal: TransportStopSummaryDto?,
    val averageRating: BigDecimal?,
    val ratingCount: Int,
    val isAvailable: Boolean,
    val createdAt: String,
    val updatedAt: String?,
    val activeVehicles: List<DriverVehicleDto>,
    val currentLocation: DriverLocationDto?,
    val currentAvailabilitySession: DriverAvailabilitySessionDto?
)

data class DriverAvailabilityResponseDto(val driverId: String, val isAvailable: Boolean, val currentAvailabilitySession: DriverAvailabilitySessionDto?)

data class StartDriverAvailabilityRequest(
    val vehicleId: String? = null,
    val destinationStopId: Long? = null,
    val destinationName: String? = null,
    val destinationLatitude: Double? = null,
    val destinationLongitude: Double? = null,
    val availableSeats: Int = 1,
    val maximumDetourMeters: BigDecimal = BigDecimal("1000"),
    val startedAt: String? = null
)

data class StopDriverAvailabilityRequest(val endedAt: String? = null)
data class UpdateDriverLocationRequest(
    val latitude: Double?,
    val longitude: Double?,
    val headingDegrees: Double? = null,
    val speedKph: Double? = null,
    val accuracyMeters: Double? = null,
    val updatedAt: String? = null
)

interface DriversApi {
    @GET("api/drivers/{driverId}") suspend fun get(@Path("driverId") id: String): Response<DriverDetailsDto>
    @GET("api/drivers/{driverId}/vehicle") suspend fun vehicle(@Path("driverId") id: String): Response<DriverVehicleDto>
    @GET("api/drivers/{driverId}/availability") suspend fun availability(@Path("driverId") id: String): Response<DriverAvailabilityResponseDto>
    @POST("api/drivers/{driverId}/availability/start") suspend fun startAvailability(@Path("driverId") id: String, @Body request: StartDriverAvailabilityRequest): Response<DriverAvailabilitySessionDto>
    @POST("api/drivers/{driverId}/availability/stop") suspend fun stopAvailability(@Path("driverId") id: String, @Body request: StopDriverAvailabilityRequest): Response<Unit>
    @PUT("api/drivers/{driverId}/location") suspend fun updateLocation(@Path("driverId") id: String, @Body request: UpdateDriverLocationRequest): Response<DriverLocationDto>
}

interface DriverRepository {
    suspend fun getDriver(id: String): ApiResult<DriverDetailsDto>
    suspend fun getVehicle(id: String): ApiResult<DriverVehicleDto>
    suspend fun getAvailability(id: String): ApiResult<DriverAvailabilityResponseDto>
    suspend fun startAvailability(id: String, request: StartDriverAvailabilityRequest): ApiResult<DriverAvailabilitySessionDto>
    suspend fun stopAvailability(id: String, request: StopDriverAvailabilityRequest = StopDriverAvailabilityRequest()): ApiResult<Unit>
    suspend fun updateLocation(id: String, request: UpdateDriverLocationRequest): ApiResult<DriverLocationDto>
}

class DriverRepositoryImpl(private val api: DriversApi, private val sessions: AuthSessionStore, private val errors: ApiErrorParser) : DriverRepository {
    override suspend fun getDriver(id: String) = call { api.get(id) }
    override suspend fun getVehicle(id: String) = call { api.vehicle(id) }
    override suspend fun getAvailability(id: String) = call { api.availability(id) }
    override suspend fun startAvailability(id: String, request: StartDriverAvailabilityRequest) = call { api.startAvailability(id, request) }
    override suspend fun stopAvailability(id: String, request: StopDriverAvailabilityRequest) = authenticatedApiCall(sessions, errors, Unit) { api.stopAvailability(id, request) }
    override suspend fun updateLocation(id: String, request: UpdateDriverLocationRequest) = call { api.updateLocation(id, request) }
    private suspend fun <T : Any> call(block: suspend () -> Response<T>) = authenticatedApiCall(sessions, errors, request = block)
}

