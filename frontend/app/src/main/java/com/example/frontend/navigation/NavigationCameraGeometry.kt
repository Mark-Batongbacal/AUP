package com.example.frontend.navigation

import com.example.frontend.core.location.RouteCoordinate

private const val MinimumCameraSpanDegrees = 0.00005

/**
 * Platform-independent route bounds so malformed or one-dimensional geometry cannot crash a map.
 */
internal data class NavigationCameraFrame(
    val south: Double,
    val west: Double,
    val north: Double,
    val east: Double,
    val distinctPointCount: Int
)

internal fun navigationCameraFrame(coordinates: List<RouteCoordinate>): NavigationCameraFrame? {
    val points = coordinates
        .filter { point ->
            point.latitude.isFinite() &&
                point.longitude.isFinite() &&
                point.latitude in -90.0..90.0 &&
                point.longitude in -180.0..180.0
        }
        .distinct()

    if (points.isEmpty()) return null

    val minimumLatitude = points.minOf { it.latitude }
    val maximumLatitude = points.maxOf { it.latitude }
    val minimumLongitude = points.minOf { it.longitude }
    val maximumLongitude = points.maxOf { it.longitude }
    val latitudeExpansion =
        ((MinimumCameraSpanDegrees - (maximumLatitude - minimumLatitude)) / 2.0)
            .coerceAtLeast(0.0)
    val longitudeExpansion =
        ((MinimumCameraSpanDegrees - (maximumLongitude - minimumLongitude)) / 2.0)
            .coerceAtLeast(0.0)

    return NavigationCameraFrame(
        south = (minimumLatitude - latitudeExpansion).coerceAtLeast(-90.0),
        west = (minimumLongitude - longitudeExpansion).coerceAtLeast(-180.0),
        north = (maximumLatitude + latitudeExpansion).coerceAtMost(90.0),
        east = (maximumLongitude + longitudeExpansion).coerceAtMost(180.0),
        distinctPointCount = points.size
    )
}

internal fun joinedNavigationLegs(legs: List<List<RouteCoordinate>>): List<RouteCoordinate> =
    buildList {
        legs.forEach { leg ->
            leg.forEach { coordinate ->
                if (lastOrNull() != coordinate) add(coordinate)
            }
        }
    }
