package com.example.frontend.data.favorites

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class FavoriteRouteMapperTest {
    @Test
    fun favoriteWithNullRouteMetrics_mapsToSafeDisplayDefaults() {
        val favorite = FavoriteTripDto(
            favoriteTripId = "favorite-1",
            userId = null,
            recommendationId = "recommendation-1",
            origin = null,
            destination = "",
            recommendationType = null,
            totalMinutes = null,
            totalFare = null,
            walkingDistanceMeters = null,
            transferCount = null,
            timesUsed = null,
            note = null,
            createdAt = null
        ).toFavoriteRouteOrNull() ?: error("Expected favorite to map")

        assertEquals("favorite-1", favorite.id)
        assertEquals("recommendation-1", favorite.recommendationId)
        assertEquals("Unknown origin", favorite.origin)
        assertEquals("Unknown destination", favorite.destination)
        assertEquals("Route", favorite.recommendationType)
        assertEquals(0, favorite.minutes)
        assertEquals(0.0, favorite.totalFare, 0.0)
        assertEquals(0, favorite.walkingMeters)
        assertEquals(0, favorite.timesUsed)
        assertEquals("", favorite.note)
    }

    @Test
    fun favoriteWithoutRequiredIds_isSkipped() {
        val favorite = FavoriteTripDto(
            favoriteTripId = null,
            userId = "user-1",
            recommendationId = "recommendation-1",
            origin = "Porac",
            destination = "Dau",
            recommendationType = "Fastest",
            totalMinutes = 20.0,
            totalFare = 30.0,
            walkingDistanceMeters = 100.0,
            transferCount = 0,
            timesUsed = 1,
            note = null,
            createdAt = null
        )

        assertNull(favorite.toFavoriteRouteOrNull())
    }

    @Test
    fun favorites_deduplicateByRecommendationId() {
        val favorites = listOf(
            favoriteRoute("favorite-1", "recommendation-1"),
            favoriteRoute("favorite-2", "recommendation-1"),
            favoriteRoute("favorite-3", "recommendation-2")
        ).withoutDuplicateFavorites()

        assertEquals(listOf("favorite-1", "favorite-3"), favorites.map { it.id })
    }

    private fun favoriteRoute(favoriteId: String, recommendationId: String) =
        FavoriteTripDto(
            favoriteTripId = favoriteId,
            userId = "user-1",
            recommendationId = recommendationId,
            origin = "Porac",
            destination = "Dau",
            recommendationType = "Fastest",
            totalMinutes = 20.0,
            totalFare = 30.0,
            walkingDistanceMeters = 100.0,
            transferCount = 0,
            timesUsed = 1,
            note = null,
            createdAt = null
        ).toFavoriteRouteOrNull() ?: error("Expected favorite to map")
}
