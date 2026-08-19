package com.example.frontend.data.tricycle

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import java.math.BigDecimal

data class CreateTricyclePointRequestDto(
    val pointCode: String,
    val pointName: String,
    val coordinates: List<Double>?,
    val radiusMeters: Int,
    val stopId: Long? = null,
    val description: String? = null,
    val address: String? = null,
    val operatorName: String? = null,
    val baseFare: BigDecimal? = null,
    val farePerKilometer: BigDecimal? = null,
    val averageWaitingTimeSeconds: Int? = null,
    val serviceStartTime: String? = null,
    val serviceEndTime: String? = null,
    val isActive: Boolean = true
)

data class TricyclePointResponseDto(
    val tricyclePointId: Long,
    val stopId: Long?,
    val pointCode: String,
    val pointName: String,
    val description: String?,
    val address: String?,
    val operatorName: String?,
    val centerLatitude: Double,
    val centerLongitude: Double,
    val radiusMeters: Int,
    val baseFare: BigDecimal?,
    val farePerKilometer: BigDecimal?,
    val averageWaitingTimeSeconds: Int?,
    val serviceStartTime: String?,
    val serviceEndTime: String?,
    val isActive: Boolean
)

interface TricyclePointsApi {
    @GET("api/tricycle-points") suspend fun active(): Response<List<TricyclePointResponseDto>>
    @GET("api/tricycle-points/{tricyclePointId}") suspend fun get(@Path("tricyclePointId") id: Long): Response<TricyclePointResponseDto>
    @POST("api/tricycle-points") suspend fun create(@Body request: CreateTricyclePointRequestDto): Response<TricyclePointResponseDto>
}

interface TricycleRepository {
    suspend fun getActivePoints(): ApiResult<List<TricyclePointResponseDto>>
    suspend fun getPoint(id: Long): ApiResult<TricyclePointResponseDto>
}

class TricycleRepositoryImpl(
    private val api: TricyclePointsApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : TricycleRepository {
    override suspend fun getActivePoints() = authenticatedApiCall(sessions, errors) { api.active() }
    override suspend fun getPoint(id: Long) = authenticatedApiCall(sessions, errors) { api.get(id) }
}
