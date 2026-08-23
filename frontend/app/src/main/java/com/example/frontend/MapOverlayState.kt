package com.example.frontend

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import org.maplibre.android.geometry.LatLng

/**
 * Shared process-scoped map overlay state.
 *
 * TODA points are transportation infrastructure, not route-specific guidance, so every map can
 * display the same active set without manually threading the list through every screen.
 * Jeepney geometry is loaded only for the route IDs in the journey the user selected.
 */
val LocalTukiDataProvider = staticCompositionLocalOf<TukiDataProvider?> { null }

object TukiMapOverlayState {
    var todaPoints by mutableStateOf<List<TodaPointOverlay>>(emptyList())
        private set

    var selectedJourneyJeepneyRouteIds by mutableStateOf<Set<Long>>(emptySet())
        private set

    var selectedJourneyJeepneyRoutes by mutableStateOf<List<TransitRouteOverlay>>(emptyList())
        private set

    private var todaLoadInProgress = false
    private var todaLoaded = false

    fun selectJourneyJeepneyRoutes(routeIds: List<Long?>) {
        selectedJourneyJeepneyRouteIds = routeIds.filterNotNull().toSet()
        selectedJourneyJeepneyRoutes = emptyList()
    }

    fun clearJourneyJeepneyRoutes() {
        selectedJourneyJeepneyRouteIds = emptySet()
        selectedJourneyJeepneyRoutes = emptyList()
    }

    suspend fun ensureSelectedJeepneyRoutes(dataProvider: TukiDataProvider?) {
        val requestedIds = selectedJourneyJeepneyRouteIds
        if (dataProvider == null || requestedIds.isEmpty()) {
            if (requestedIds.isEmpty()) selectedJourneyJeepneyRoutes = emptyList()
            return
        }

        val activeRoutes = when (val result = dataProvider.transportRouteRepository.getActiveRoutes()) {
            is ApiResult.Success -> result.data.filter { it.routeId in requestedIds }
            is ApiResult.Failure -> return
        }

        val overlays = buildList {
            activeRoutes.forEach { route ->
                when (val result = dataProvider.transportRouteRepository.getRoutePoints(route.routeId)) {
                    is ApiResult.Success -> {
                        val geometry = result.data.points
                            .sortedBy { it.pointOrder }
                            .map { point -> LatLng(point.latitude, point.longitude) }
                        if (geometry.size >= 2) {
                            add(
                                TransitRouteOverlay(
                                    routeId = route.routeId,
                                    routeCode = route.routeCode,
                                    routeName = route.routeName,
                                    points = geometry
                                )
                            )
                        }
                    }
                    is ApiResult.Failure -> Unit
                }
            }
        }

        if (selectedJourneyJeepneyRouteIds == requestedIds) {
            selectedJourneyJeepneyRoutes = overlays
        }
    }

    suspend fun ensureTodaPoints(dataProvider: TukiDataProvider?) {
        if (dataProvider == null || todaLoaded || todaLoadInProgress) return

        todaLoadInProgress = true
        when (val result = dataProvider.tricycleRepository.getActivePoints()) {
            is ApiResult.Success -> {
                todaPoints = result.data.map { point ->
                    TodaPointOverlay(
                        id = point.tricyclePointId,
                        name = point.pointName,
                        pointCode = point.pointCode,
                        latitude = point.centerLatitude,
                        longitude = point.centerLongitude,
                        radiusMeters = point.radiusMeters,
                        operatorName = point.operatorName,
                        baseFareText = point.baseFare?.let { fare ->
                            "₱${fare.stripTrailingZeros().toPlainString()}"
                        }
                    )
                }
                todaLoaded = true
            }

            is ApiResult.Failure -> Unit
        }
        todaLoadInProgress = false
    }
}
