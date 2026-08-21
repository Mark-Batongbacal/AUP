package com.example.frontend.navigation

import android.content.Context
import com.example.frontend.core.location.LocationDetectionFailureMessage
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.navigation.NavigationGeometryResponseDto
import com.example.frontend.data.navigation.NavigationLocationUpdate
import com.example.frontend.data.navigation.NavigationRerouteRequest
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.routing.JourneyPlanRequest
import java.math.BigDecimal
import java.time.Instant
import kotlin.math.roundToInt

data class TripPreferencePreview(
    val preference: String,
    val title: String,
    val totalMinutes: Int,
    val totalFarePesos: Double,
    val walkMeters: Int
)

class TripOptionsCoordinator(context: Context) {
    private val appContext = context.applicationContext
    private val provider = TukiDataProvider(appContext)
    private val navigation = provider.navigationRepository
    private val places = provider.placesRepository
    private val routing = provider.routingRepository

    suspend fun rerouteNow(sessionId: String): ApiResult<NavigationSnapshotDto> =
        reroute(sessionId, NavigationRerouteRequest(reason = "MANUAL"))

    suspend fun changePreference(sessionId: String, preference: String): ApiResult<NavigationSnapshotDto> =
        reroute(sessionId, NavigationRerouteRequest(reason = "PREFERENCE_CHANGED", preference = preference))

    suspend fun changeBudget(sessionId: String, budget: BigDecimal?, clearBudget: Boolean): ApiResult<NavigationSnapshotDto> =
        reroute(sessionId, NavigationRerouteRequest(reason = "BUDGET_CHANGED", budget = budget, clearBudget = clearBudget))

    suspend fun searchDestinations(query: String, latitude: Double?, longitude: Double?): ApiResult<List<DestinationSearchResultDto>> =
        places.searchPlaces(query, latitude, longitude)

    suspend fun changeDestination(sessionId: String, destination: DestinationSearchResultDto): ApiResult<NavigationSnapshotDto> =
        reroute(
            sessionId,
            NavigationRerouteRequest(
                reason = "DESTINATION_CHANGED",
                destinationName = destination.name,
                destinationLatitude = destination.latitude,
                destinationLongitude = destination.longitude
            )
        )

    /**
     * Loads live route summaries for the three preference cards using the same journey
     * planner as the route-results screen. Values are real planner results, not UI placeholders.
     */
    suspend fun loadPreferencePreviews(
        originLatitude: Double?,
        originLongitude: Double?,
        destinationName: String,
        destinationLatitude: Double?,
        destinationLongitude: Double?
    ): ApiResult<List<TripPreferencePreview>> {
        val originLat = originLatitude ?: return ApiResult.Failure(null, "Current location is unavailable.")
        val originLon = originLongitude ?: return ApiResult.Failure(null, "Current location is unavailable.")
        val destinationLat = destinationLatitude ?: return ApiResult.Failure(null, "Destination location is unavailable.")
        val destinationLon = destinationLongitude ?: return ApiResult.Failure(null, "Destination location is unavailable.")

        val definitions = listOf(
            "efficient" to "Best Overall",
            "cheapest" to "Cheapest",
            "fastest" to "Fastest"
        )
        val previews = mutableListOf<TripPreferencePreview>()
        var lastFailure: ApiResult.Failure? = null

        for ((preference, title) in definitions) {
            when (
                val result = routing.planJourneys(
                    JourneyPlanRequest(
                        originLatitude = originLat,
                        originLongitude = originLon,
                        destinationName = destinationName,
                        destinationLatitude = destinationLat,
                        destinationLongitude = destinationLon,
                        preference = preference
                    )
                )
            ) {
                is ApiResult.Success -> {
                    val selected = result.data.firstOrNull { planned ->
                        planned.journey.source.recommendationType
                            .split(',')
                            .any { it.trim().equals(preference, ignoreCase = true) }
                    } ?: result.data.firstOrNull()

                    if (selected != null) {
                        val source = selected.journey.source
                        val walkMeters = (
                            source.originAccess.walkDistanceMeters +
                                source.destinationAccess.walkDistanceMeters +
                                source.transferWalkDistancesMeters.sum()
                            ).roundToInt()
                        previews += TripPreferencePreview(
                            preference = preference,
                            title = title,
                            totalMinutes = (source.totalTimeSeconds / 60.0).roundToInt().coerceAtLeast(1),
                            totalFarePesos = source.totalFarePesos,
                            walkMeters = walkMeters.coerceAtLeast(0)
                        )
                    }
                }
                is ApiResult.Failure -> lastFailure = result
            }
        }

        return if (previews.isNotEmpty()) {
            ApiResult.Success(previews)
        } else {
            lastFailure ?: ApiResult.Failure(null, "Route preference summaries are unavailable right now.")
        }
    }

    suspend fun currentLegGeometry(snapshot: NavigationSnapshotDto): ApiResult<NavigationGeometryResponseDto> {
        val leg = snapshot.currentLeg ?: return ApiResult.Failure(null, "Current route leg is unavailable.")
        val startLat = snapshot.currentLatitude ?: leg.startLatitude
            ?: return ApiResult.Failure(null, "Current route location is unavailable.")
        val startLon = snapshot.currentLongitude ?: leg.startLongitude
            ?: return ApiResult.Failure(null, "Current route location is unavailable.")
        val endLat = leg.endLatitude ?: return ApiResult.Failure(null, "Current route destination is unavailable.")
        val endLon = leg.endLongitude ?: return ApiResult.Failure(null, "Current route destination is unavailable.")
        return navigation.getGeometry(
            startLatitude = startLat,
            startLongitude = startLon,
            endLatitude = endLat,
            endLongitude = endLon,
            mode = leg.transportMode,
            routeId = leg.routeId
        )
    }

    private suspend fun reroute(sessionId: String, request: NavigationRerouteRequest): ApiResult<NavigationSnapshotDto> {
        val location = appContext.currentDeviceLocation()
            ?: return ApiResult.Failure(null, LocationDetectionFailureMessage)
        val timestampMillis = if (location.time > 0L) location.time else System.currentTimeMillis()
        val locationUpdate = NavigationLocationUpdate(
            latitude = location.latitude,
            longitude = location.longitude,
            accuracyMeters = location.accuracy.toDouble(),
            timestamp = Instant.ofEpochMilli(timestampMillis).toString(),
            speedMetersPerSecond = if (location.hasSpeed()) location.speed.toDouble() else null,
            bearingDegrees = if (location.hasBearing()) location.bearing.toDouble() else null
        )
        when (val update = navigation.updateLocation(sessionId, locationUpdate)) {
            is ApiResult.Failure -> return update
            is ApiResult.Success -> Unit
        }
        return navigation.reroute(sessionId, request)
    }
}
