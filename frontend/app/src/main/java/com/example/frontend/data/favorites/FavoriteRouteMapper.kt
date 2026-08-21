package com.example.frontend.data.favorites

import com.example.frontend.model.FavoriteRoute
import kotlin.math.roundToInt

fun FavoriteTripDto.toFavoriteRouteOrNull(): FavoriteRoute? {
    val favoriteId = favoriteTripId?.takeIf { it.isNotBlank() } ?: return null
    val routeRecommendationId = recommendationId?.takeIf { it.isNotBlank() } ?: return null

    return FavoriteRoute(
        id = favoriteId,
        recommendationId = routeRecommendationId,
        origin = origin?.takeIf { it.isNotBlank() } ?: "Unknown origin",
        destination = destination?.takeIf { it.isNotBlank() } ?: "Unknown destination",
        recommendationType = recommendationType?.takeIf { it.isNotBlank() } ?: "Route",
        minutes = totalMinutes?.roundToInt()?.coerceAtLeast(0) ?: 0,
        totalFare = totalFare ?: 0.0,
        walkingMeters = walkingDistanceMeters?.roundToInt()?.coerceAtLeast(0) ?: 0,
        timesUsed = timesUsed ?: 0,
        note = note.orEmpty()
    )
}

fun List<FavoriteRoute>.withoutDuplicateFavorites(): List<FavoriteRoute> =
    distinctBy { route -> route.recommendationId.takeIf { it.isNotBlank() } ?: route.id }
