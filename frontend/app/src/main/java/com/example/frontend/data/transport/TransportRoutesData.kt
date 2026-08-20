package com.example.frontend.data.transport

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.apiCall
import com.example.frontend.core.storage.AuthSessionStore
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import java.math.BigDecimal

data class TransportRouteListItemDto(val routeId: Long, val routeCode: String, val routeName: String, val isActive: Boolean)
data class TransportRoutePolylineDto(val routeId: Long, val routeCode: String, val routeName: String, val precision: Int, val polyline: String)
data class RoutePointResponseDto(val routePointId: Long, val pointOrder: Int, val latitude: Double, val longitude: Double)
data class RoutePointsResponseDto(val routeId: Long, val points: List<RoutePointResponseDto>)
data class CreatedTransportRouteDto(
    val routeId: Long,
    val routeCode: String,
    val routeName: String,
    val originName: String,
    val destinationName: String,
    val encodedPolyline: String?,
    val points: List<RoutePointResponseDto>
)

data class CreateJeepneyRouteRequest(
    val routeCode: String?,
    val routeName: String?,
    val originName: String?,
    val destinationName: String?,
    val points: List<List<Double>>?,
    val description: String? = null,
    val baseFare: BigDecimal? = null
)

interface TransportRoutesApi {
    @GET("api/transport-routes") suspend fun activeRoutes(): Response<List<TransportRouteListItemDto>>
    @GET("api/transport-routes/latest/polyline") suspend fun latestPolyline(): Response<TransportRoutePolylineDto>
    @POST("api/transport-routes") suspend fun create(@Body request: CreateJeepneyRouteRequest): Response<CreatedTransportRouteDto>
    @GET("api/transport-routes/{routeId}/points") suspend fun points(@Path("routeId") routeId: Long): Response<RoutePointsResponseDto>
    @PUT("api/transport-routes/{routeId}/points") suspend fun replacePoints(@Path("routeId") routeId: Long, @Body points: List<List<Double>>): Response<RoutePointsResponseDto>
}

interface TransportRouteRepository {
    suspend fun getActiveRoutes(): ApiResult<List<TransportRouteListItemDto>>
    suspend fun getLatestPolyline(): ApiResult<TransportRoutePolylineDto>
    suspend fun getRoutePoints(routeId: Long): ApiResult<RoutePointsResponseDto>
}

class TransportRouteRepositoryImpl(private val api: TransportRoutesApi, private val sessions: AuthSessionStore, private val errors: ApiErrorParser) : TransportRouteRepository {
    override suspend fun getActiveRoutes() = apiCall(errors) { api.activeRoutes() }
    override suspend fun getLatestPolyline() = apiCall(errors) { api.latestPolyline() }
    override suspend fun getRoutePoints(routeId: Long) = apiCall(errors) { api.points(routeId) }
}
