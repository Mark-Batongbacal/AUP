package com.example.frontend.core.location

import kotlin.math.asin
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.pow
import kotlin.math.sin
import kotlin.math.sqrt

data class RouteCoordinate(val latitude: Double, val longitude: Double)

data class RouteMatch(
    val coordinate: RouteCoordinate,
    val segmentIndex: Int,
    val segmentFraction: Double,
    val distanceToRouteMeters: Double,
    val progressMeters: Double,
    val remainingDistanceMeters: Double
)

object RouteMatcher {
    private const val EarthRadiusMeters = 6_371_000.0

    fun match(
        raw: RouteCoordinate,
        route: List<RouteCoordinate>,
        minimumProgressMeters: Double = 0.0,
        maximumProgressMeters: Double = Double.POSITIVE_INFINITY
    ): RouteMatch? {
        if (route.size < 2 || maximumProgressMeters + 0.01 < minimumProgressMeters) return null

        val segmentLengths = DoubleArray(route.lastIndex)
        var totalDistance = 0.0
        for (index in 0 until route.lastIndex) {
            val length = distanceMeters(route[index], route[index + 1])
            segmentLengths[index] = length
            totalDistance += length
        }

        var best: RouteMatch? = null
        var progressBeforeSegment = 0.0

        for (index in 0 until route.lastIndex) {
            val start = route[index]
            val end = route[index + 1]
            val segmentLength = segmentLengths[index]
            val segmentEndProgress = progressBeforeSegment + segmentLength

            if (segmentEndProgress + 0.01 < minimumProgressMeters) {
                progressBeforeSegment = segmentEndProgress
                continue
            }
            if (progressBeforeSegment > maximumProgressMeters + 0.01) break

            val projection = project(raw, start, end)
            val progress = progressBeforeSegment + segmentLength * projection.second
            if (progress + 0.01 >= minimumProgressMeters && progress <= maximumProgressMeters + 0.01) {
                val distance = distanceMeters(raw, projection.first)
                val candidate = RouteMatch(
                    coordinate = projection.first,
                    segmentIndex = index,
                    segmentFraction = projection.second,
                    distanceToRouteMeters = distance,
                    progressMeters = progress,
                    remainingDistanceMeters = max(0.0, totalDistance - progress)
                )
                if (best == null || candidate.distanceToRouteMeters < best.distanceToRouteMeters) {
                    best = candidate
                }
            }

            progressBeforeSegment = segmentEndProgress
        }

        return best
    }

    fun remainingRoute(route: List<RouteCoordinate>, match: RouteMatch?): List<RouteCoordinate> {
        if (match == null || route.size < 2) return route
        return buildList {
            add(match.coordinate)
            route.drop(match.segmentIndex + 1).forEach { point ->
                if (lastOrNull() != point) add(point)
            }
        }
    }

    fun distanceMeters(a: RouteCoordinate, b: RouteCoordinate): Double {
        val lat1 = Math.toRadians(a.latitude)
        val lat2 = Math.toRadians(b.latitude)
        val dLat = lat2 - lat1
        val dLon = Math.toRadians(b.longitude - a.longitude)
        val h = sin(dLat / 2).pow(2) + cos(lat1) * cos(lat2) * sin(dLon / 2).pow(2)
        return 2 * EarthRadiusMeters * asin(sqrt(h.coerceIn(0.0, 1.0)))
    }

    private fun project(
        point: RouteCoordinate,
        start: RouteCoordinate,
        end: RouteCoordinate
    ): Pair<RouteCoordinate, Double> {
        val referenceLat = Math.toRadians((start.latitude + end.latitude + point.latitude) / 3.0)
        fun x(longitude: Double) = Math.toRadians(longitude) * EarthRadiusMeters * cos(referenceLat)
        fun y(latitude: Double) = Math.toRadians(latitude) * EarthRadiusMeters

        val ax = x(start.longitude)
        val ay = y(start.latitude)
        val bx = x(end.longitude)
        val by = y(end.latitude)
        val px = x(point.longitude)
        val py = y(point.latitude)
        val dx = bx - ax
        val dy = by - ay
        val denominator = dx * dx + dy * dy
        val fraction = if (denominator == 0.0) {
            0.0
        } else {
            (((px - ax) * dx + (py - ay) * dy) / denominator).coerceIn(0.0, 1.0)
        }

        return RouteCoordinate(
            latitude = start.latitude + (end.latitude - start.latitude) * fraction,
            longitude = start.longitude + (end.longitude - start.longitude) * fraction
        ) to fraction
    }
}
