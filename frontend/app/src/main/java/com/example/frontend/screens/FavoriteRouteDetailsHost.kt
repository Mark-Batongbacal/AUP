package com.example.frontend.screens

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import com.example.frontend.LocalTukiDataProvider
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.trips.toRecentCommute
import com.example.frontend.model.FavoriteRoute
import com.example.frontend.model.RecentCommute
import org.maplibre.android.geometry.LatLng

/**
 * Resolves a favorite back to the same saved trip data used by Recent/History and renders the
 * existing CommuteDetailScreen. Favorites intentionally do not invent missing leg information.
 */
@Composable
fun FavoriteRouteDetailsHost(
    favorite: FavoriteRoute,
    onBack: () -> Unit,
    onRepeatTrip: (RecentCommute) -> Unit = {}
) {
    val dataProvider = LocalTukiDataProvider.current
    var commute by remember(favorite.id, favorite.recommendationId) { mutableStateOf<RecentCommute?>(null) }
    var legGeometries by remember(favorite.id, favorite.recommendationId) {
        mutableStateOf<List<List<LatLng>>>(emptyList())
    }
    var loading by remember(favorite.id, favorite.recommendationId) { mutableStateOf(true) }
    var geometryLoading by remember(favorite.id, favorite.recommendationId) { mutableStateOf(false) }
    var error by remember(favorite.id, favorite.recommendationId) { mutableStateOf<String?>(null) }

    LaunchedEffect(favorite.id, favorite.recommendationId, dataProvider) {
        if (dataProvider == null) {
            error = "Favorite route details are unavailable right now."
            loading = false
            return@LaunchedEffect
        }

        loading = true
        error = null
        commute = null
        legGeometries = emptyList()

        when (val history = dataProvider.tripRepository.getHistory()) {
            is ApiResult.Success -> {
                val recommendationId = favorite.recommendationId.takeIf { it.isNotBlank() }
                val match = history.data
                    .filter { item ->
                        recommendationId != null &&
                            item.recommendation?.recommendationId == recommendationId
                    }
                    .maxByOrNull { item -> item.completedAt ?: item.startedAt ?: item.createdAt }

                if (match == null) {
                    error = "This favorite does not have a saved trip history entry yet."
                } else {
                    var originName = match.originName
                    var destinationName = match.destinationName

                    if (originName.isGenericFavoriteLocationLabel()) {
                        when (val place = dataProvider.placesRepository.reverseGeocode(
                            match.originLatitude,
                            match.originLongitude
                        )) {
                            is ApiResult.Success -> originName = place.data.name
                            is ApiResult.Failure -> Unit
                        }
                    }
                    if (destinationName.isGenericFavoriteLocationLabel()) {
                        when (val place = dataProvider.placesRepository.reverseGeocode(
                            match.destinationLatitude,
                            match.destinationLongitude
                        )) {
                            is ApiResult.Success -> destinationName = place.data.name
                            is ApiResult.Failure -> Unit
                        }
                    }

                    val resolvedCommute = match.toRecentCommute(
                        originName = originName,
                        destinationName = destinationName
                    )
                    commute = resolvedCommute

                    geometryLoading = true
                    val geometries = mutableListOf<List<LatLng>>()
                    for (leg in resolvedCommute.historyLegs) {
                        val startLat = leg.startLatitude
                        val startLon = leg.startLongitude
                        val endLat = leg.endLatitude
                        val endLon = leg.endLongitude
                        if (startLat == null || startLon == null || endLat == null || endLon == null) {
                            geometries.add(emptyList())
                            continue
                        }

                        when (val result = dataProvider.navigationRepository.getGeometry(
                            startLatitude = startLat,
                            startLongitude = startLon,
                            endLatitude = endLat,
                            endLongitude = endLon,
                            mode = leg.mode,
                            routeId = leg.routeId
                        )) {
                            is ApiResult.Success -> geometries.add(
                                result.data.points.map { point ->
                                    LatLng(point.latitude, point.longitude)
                                }
                            )
                            is ApiResult.Failure -> geometries.add(emptyList())
                        }
                    }
                    legGeometries = geometries
                    geometryLoading = false
                }
            }

            is ApiResult.Failure -> error = history.message
        }

        loading = false
    }

    when {
        loading -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            CircularProgressIndicator()
        }

        commute != null -> CommuteDetailScreen(
            commute = commute!!,
            legGeometries = legGeometries,
            isGeometryLoading = geometryLoading,
            onBack = onBack,
            onRepeatTrip = { onRepeatTrip(commute!!) }
        )

        else -> FavoriteDetailError(
            message = error ?: "Favorite route details could not be loaded.",
            onBack = onBack
        )
    }
}

@Composable
private fun FavoriteDetailError(message: String, onBack: () -> Unit) {
    androidx.compose.foundation.layout.Column(
        modifier = Modifier.fillMaxSize(),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = androidx.compose.foundation.layout.Arrangement.Center
    ) {
        Text(message, color = MaterialTheme.colorScheme.error)
        androidx.compose.material3.TextButton(onClick = onBack) {
            Text("Back to Favorites")
        }
    }
}

private fun String.isGenericFavoriteLocationLabel(): Boolean {
    val value = trim().lowercase()
    return value.isBlank() ||
        value == "current location" ||
        value == "pinned destination" ||
        value == "unknown origin" ||
        value == "unknown destination"
}
