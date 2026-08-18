package com.example.frontend.data.routing

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Query

data class NearbyJeepneyRouteDto(
    val routeId: String,
    val routeName: String,
    val routeDistanceMeters: Double,
    val nearestPointLatitude: Double,
    val nearestPointLongitude: Double,
    val walkingDistanceMeters: Double,
    val walkingTimeSeconds: Double
)

data class JeepneyAccessSegmentDto(
    val mode: Int,
    val walkDistanceMeters: Double,
    val walkTimeSeconds: Double,
    val trikePointId: String?,
    val trikePointName: String?,
    val trikePointLatitude: Double?,
    val trikePointLongitude: Double?,
    val trikeRideDistanceMeters: Double?,
    val trikeRideTimeSeconds: Double?,
    val totalTimeSeconds: Double,
    val totalFarePesos: Double,
    val generalizedCostPesos: Double
)

data class JeepneyTripLegDto(
    val mode: Int,
    val routeId: String?,
    val routeName: String?,
    val boardLatitude: Double,
    val boardLongitude: Double,
    val alightLatitude: Double,
    val alightLongitude: Double,
    val originLatitude: Double,
    val originLongitude: Double,
    val destinationLatitude: Double,
    val destinationLongitude: Double,
    val distanceMeters: Double,
    val durationSeconds: Double,
    val farePesos: Double,
    val generalizedCostPesos: Double,
    val walkDistanceMeters: Double?,
    val walkTimeSeconds: Double?,
    val trikeDistanceMeters: Double?,
    val trikeTimeSeconds: Double?,
    val jeepneyDistanceMeters: Double?,
    val jeepneyTimeSeconds: Double?,
    val trikePointId: String?,
    val trikePointName: String?
)

data class JeepneyTripPlanDto(
    val recommendationType: String,
    val legs: List<JeepneyTripLegDto>,
    val originAccess: JeepneyAccessSegmentDto,
    val destinationAccess: JeepneyAccessSegmentDto,
    val transferWalkDistancesMeters: List<Double>,
    val transferWalkTimesSeconds: List<Double>,
    val totalTimeSeconds: Double,
    val totalFarePesos: Double,
    val generalizedCostPesos: Double,
    val transferCount: Int
)

sealed interface TransitMode {
    data object Walk : TransitMode
    data object Trike : TransitMode
    data object Jeepney : TransitMode
    data class Unknown(val rawValue: Int) : TransitMode

    companion object {
        fun fromWireValue(value: Int): TransitMode = when (value) {
            0 -> Walk
            1 -> Trike
            2 -> Jeepney
            else -> Unknown(value)
        }
    }
}

data class RouteCoordinate(val latitude: Double, val longitude: Double)

data class JourneyLeg(
    val mode: TransitMode,
    val routeId: String?,
    val routeName: String?,
    val origin: RouteCoordinate,
    val destination: RouteCoordinate,
    val board: RouteCoordinate,
    val alight: RouteCoordinate,
    val distanceMeters: Double,
    val durationSeconds: Double,
    val farePesos: Double,
    val source: JeepneyTripLegDto
)

data class JourneyPlan(val legs: List<JourneyLeg>, val source: JeepneyTripPlanDto)

fun JeepneyTripPlanDto.toDomain() = JourneyPlan(
    legs = legs.map { leg ->
        JourneyLeg(
            mode = TransitMode.fromWireValue(leg.mode),
            routeId = leg.routeId,
            routeName = leg.routeName,
            origin = RouteCoordinate(leg.originLatitude, leg.originLongitude),
            destination = RouteCoordinate(leg.destinationLatitude, leg.destinationLongitude),
            board = RouteCoordinate(leg.boardLatitude, leg.boardLongitude),
            alight = RouteCoordinate(leg.alightLatitude, leg.alightLongitude),
            distanceMeters = leg.distanceMeters,
            durationSeconds = leg.durationSeconds,
            farePesos = leg.farePesos,
            source = leg
        )
    },
    source = this
)

interface RoutingApi {
    @GET("api/test/jeepney/nearby")
    suspend fun nearby(@Query("lat") latitude: Double, @Query("lon") longitude: Double): Response<List<NearbyJeepneyRouteDto>>

    @GET("api/test/jeepney/plan")
    suspend fun plan(
        @Query("originLat") originLatitude: Double,
        @Query("originLon") originLongitude: Double,
        @Query("destinationLat") destinationLatitude: Double,
        @Query("destinationLon") destinationLongitude: Double
    ): Response<List<JeepneyTripPlanDto>>
}

interface RoutingRepository {
    suspend fun findNearbyRoutes(latitude: Double, longitude: Double): ApiResult<List<NearbyJeepneyRouteDto>>
    suspend fun planTrip(originLatitude: Double, originLongitude: Double, destinationLatitude: Double, destinationLongitude: Double): ApiResult<List<JourneyPlan>>
}

class RoutingRepositoryImpl(
    private val api: RoutingApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : RoutingRepository {
    override suspend fun findNearbyRoutes(latitude: Double, longitude: Double) =
        authenticatedApiCall(sessions, errors) { api.nearby(latitude, longitude) }

    override suspend fun planTrip(originLatitude: Double, originLongitude: Double, destinationLatitude: Double, destinationLongitude: Double): ApiResult<List<JourneyPlan>> =
        when (val result = authenticatedApiCall(sessions, errors) {
            api.plan(originLatitude, originLongitude, destinationLatitude, destinationLongitude)
        }) {
            is ApiResult.Success -> ApiResult.Success(result.data.map(JeepneyTripPlanDto::toDomain))
            is ApiResult.Failure -> result
        }
}

