package com.example.frontend.core.location

import kotlin.math.max

data class RouteCorridorDecision(
    val outsideRoute: Boolean,
    val consecutiveOutsideFixes: Int,
    val toleranceMeters: Double,
    val shouldForceSync: Boolean
)

class RouteCorridorDetector(
    private val requiredOutsideFixes: Int = 3,
    private val baseToleranceMeters: Double = 45.0,
    private val accuracyMultiplier: Double = 1.25,
    private val maximumToleranceMeters: Double = 85.0
) {
    private var outsideFixes = 0

    fun update(distanceToRouteMeters: Double, accuracyMeters: Double?): RouteCorridorDecision {
        val tolerance = max(
            baseToleranceMeters,
            (accuracyMeters ?: 0.0).coerceAtLeast(0.0) * accuracyMultiplier
        ).coerceAtMost(maximumToleranceMeters)

        val outside = distanceToRouteMeters > tolerance
        outsideFixes = if (outside) outsideFixes + 1 else 0

        return RouteCorridorDecision(
            outsideRoute = outside,
            consecutiveOutsideFixes = outsideFixes,
            toleranceMeters = tolerance,
            shouldForceSync = outsideFixes >= requiredOutsideFixes
        )
    }

    fun reset() {
        outsideFixes = 0
    }
}
