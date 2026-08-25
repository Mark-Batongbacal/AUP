package com.example.frontend.navigation

import android.content.Context
import com.example.frontend.core.localization.AppLanguagePreference
import com.example.frontend.core.location.LocationDetectionFailureMessage
import com.example.frontend.core.location.NavigationSyncSignal
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.ai.ActiveTripAssistantRequest
import com.example.frontend.data.ai.AssistantResponseDto
import com.example.frontend.data.navigation.NavigationGeometryResponseDto
import com.example.frontend.data.navigation.NavigationLocationUpdate
import com.example.frontend.data.navigation.NavigationRerouteRequest
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.routing.JourneyPlanRequest
import com.example.frontend.data.routing.PlannedJourney
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
    private val users = provider.userRepository
    private val ai = provider.aiRepository
    private val navigationAssistantConversations = mutableMapOf<String, String>()

    suspend fun refreshPreferredLanguage(): String =
        when (val result = users.getCurrentUser()) {
            is ApiResult.Success -> result.data.preferredLanguage
            is ApiResult.Failure -> AppLanguagePreference.current()
        }

    suspend fun rerouteNow(
        sessionId: String,
        reason: String = "MANUAL",
        avoidTransportMode: String? = null
    ): ApiResult<NavigationSnapshotDto> =
        reroute(
            sessionId,
            NavigationRerouteRequest(
                reason = reason,
                avoidTransportMode = avoidTransportMode
            )
        )

    suspend fun recoverMissedLegTarget(sessionId: String): ApiResult<NavigationSnapshotDto> =
        reroute(sessionId, NavigationRerouteRequest(reason = "MISSED_LEG_TARGET"))

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

    suspend fun askNavigationAssistant(
        sessionId: String,
        message: String,
        destinationId: String? = null
    ): ApiResult<AssistantResponseDto> {
        val result = ai.askTrip(
            sessionId,
            ActiveTripAssistantRequest(
                message = message,
                destinationId = destinationId,
                conversationId = navigationAssistantConversations[sessionId]
            )
        )
        if (result is ApiResult.Success) {
            result.data.conversationId?.let { navigationAssistantConversations[sessionId] = it }
        }
        return result
    }

    suspend fun confirmAssistantReplan(
        sessionId: String,
        recommendationId: String
    ): ApiResult<NavigationSnapshotDto> {
        return when (val confirmation = ai.confirmTripReplan(sessionId, recommendationId)) {
            is ApiResult.Failure -> confirmation
            is ApiResult.Success -> {
                NavigationSyncSignal.requestImmediateSync(samples = 1)
                navigation.getActiveNavigation()
            }
        }
    }

    suspend fun refreshActiveNavigation(): ApiResult<NavigationSnapshotDto> =
        navigation.getActiveNavigation()

    /**
     * Uses the same journey planner and recommendation payload as RouteResultsScreen.
     * Missing live coordinates are resolved from the device, while missing destination
     * coordinates are resolved through Places before requesting the route cards.
     */
    suspend fun loadPreferencePreviews(
        originLatitude: Double?,
        originLongitude: Double?,
        destinationName: String,
        destinationLatitude: Double?,
        destinationLongitude: Double?
    ): ApiResult<List<TripPreferencePreview>> {
        val deviceLocation = if (originLatitude == null || originLongitude == null) {
            appContext.currentDeviceLocation()
        } else null
        val originLat = originLatitude ?: deviceLocation?.latitude
            ?: return ApiResult.Failure(null, LocationDetectionFailureMessage)
        val originLon = originLongitude ?: deviceLocation?.longitude
            ?: return ApiResult.Failure(null, LocationDetectionFailureMessage)

        var resolvedDestinationName = destinationName
        var destinationLat = destinationLatitude
        var destinationLon = destinationLongitude
        if (destinationLat == null || destinationLon == null) {
            when (val placeResult = places.searchPlaces(destinationName, originLat, originLon)) {
                is ApiResult.Success -> {
                    val place = placeResult.data.firstOrNull()
                        ?: return ApiResult.Failure(null, "Destination location is unavailable.")
                    resolvedDestinationName = place.name
                    destinationLat = place.latitude
                    destinationLon = place.longitude
                }
                is ApiResult.Failure -> return placeResult
            }
        }

        val finalDestinationLat = destinationLat
            ?: return ApiResult.Failure(null, "Destination location is unavailable.")
        val finalDestinationLon = destinationLon
            ?: return ApiResult.Failure(null, "Destination location is unavailable.")

        return when (
            val result = routing.planJourneys(
                JourneyPlanRequest(
                    originLatitude = originLat,
                    originLongitude = originLon,
                    destinationName = resolvedDestinationName,
                    destinationLatitude = finalDestinationLat,
                    destinationLongitude = finalDestinationLon
                )
            )
        ) {
            is ApiResult.Failure -> result
            is ApiResult.Success -> {
                val plans = result.data
                if (plans.isEmpty()) {
                    ApiResult.Failure(null, "No route preferences are available right now.")
                } else {
                    val efficient = findTagged(plans, "efficient")
                        ?: plans.minByOrNull { it.journey.source.generalizedCostPesos }
                    val cheapest = findTagged(plans, "cheapest")
                        ?: plans.minByOrNull { it.journey.source.totalFarePesos }
                    val fastest = findTagged(plans, "fastest")
                        ?: plans.minByOrNull { it.journey.source.totalTimeSeconds }

                    val previews = listOfNotNull(
                        efficient?.toPreferencePreview("efficient", "Best Overall"),
                        cheapest?.toPreferencePreview("cheapest", "Cheapest"),
                        fastest?.toPreferencePreview("fastest", "Fastest")
                    ).distinctBy { it.preference }

                    ApiResult.Success(previews)
                }
            }
        }
    }

    suspend fun currentLegGeometry(snapshot: NavigationSnapshotDto): ApiResult<NavigationGeometryResponseDto> {
        val leg = snapshot.currentLeg ?: return ApiResult.Failure(null, "Current route leg is unavailable.")
        // Keep the complete planned leg geometry stable. Live GPS is matched onto this geometry
        // locally; using the current location as the start would silently discard already-planned
        // points and make turn/landmark progress anchors drift.
        val startLat = leg.startLatitude ?: snapshot.currentLatitude
            ?: return ApiResult.Failure(null, "Current route location is unavailable.")
        val startLon = leg.startLongitude ?: snapshot.currentLongitude
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
        // Replans are meaningful server events. Force exactly this fresh fix through the normally
        // local repository before asking the backend to calculate the replacement plan.
        NavigationSyncSignal.requestImmediateSync(samples = 1)
        val locationResult = when (val update = navigation.updateLocation(sessionId, locationUpdate)) {
            is ApiResult.Failure -> return update
            is ApiResult.Success -> update
        }

        val changesConstraints = request.preference != null ||
            request.budget != null || request.clearBudget ||
            request.destinationName != null || request.destinationLatitude != null ||
            request.destinationLongitude != null
        if (locationResult.data.status.equals("REROUTE_SUCCEEDED", ignoreCase = true) && !changesConstraints) {
            // The location sync can itself trigger the backend's authoritative off-route reroute.
            // Do not immediately calculate a second replacement route for the same GPS fix.
            NavigationSyncSignal.requestImmediateSync(samples = 1)
            return locationResult
        }

        val rerouted = navigation.reroute(sessionId, request)
        if (rerouted is ApiResult.Success) {
            // TripTracking owns a short-lived coordinator repository while AppNavigation owns the
            // long-lived tracking repository. Force the next tracking fix to refresh that parent
            // cache from the backend so a successful reroute cannot fall back to stale geometry.
            NavigationSyncSignal.requestImmediateSync(samples = 1)
        }
        return rerouted
    }

    private fun findTagged(plans: List<PlannedJourney>, tag: String): PlannedJourney? =
        plans.firstOrNull { planned ->
            planned.journey.source.recommendationType
                .split(',')
                .any { it.trim().equals(tag, ignoreCase = true) }
        }

    private fun PlannedJourney.toPreferencePreview(preference: String, title: String): TripPreferencePreview {
        val source = journey.source
        val walkMeters = (
            source.originAccess.walkDistanceMeters +
                source.destinationAccess.walkDistanceMeters +
                source.transferWalkDistancesMeters.sum()
            ).roundToInt()
        return TripPreferencePreview(
            preference = preference,
            title = title,
            totalMinutes = (source.totalTimeSeconds / 60.0).roundToInt().coerceAtLeast(1),
            totalFarePesos = source.totalFarePesos,
            walkMeters = walkMeters.coerceAtLeast(0)
        )
    }
}
