package com.example.frontend.data.contributions

import android.location.Location
import java.time.Instant
import kotlin.math.asin
import kotlin.math.cos
import kotlin.math.sin
import kotlin.math.sqrt

private const val MaxSubmissionLocationAgeMillis = 30_000L
private const val MaxSubmissionLocationFutureSkewMillis = 10_000L
private const val MaxSupportedAccuracyMeters = 100_000.0
private const val MaxSubmissionLocationJumpMeters = 100.0
private const val EarthRadiusMeters = 6_371_000.0

data class CapturedTricycleSubmissionLocation(
    val latitude: Double,
    val longitude: Double,
    val accuracyMeters: Double?,
    val capturedAt: Instant
)

fun Location.toCapturedTricycleSubmissionLocation(
    nowEpochMillis: Long = System.currentTimeMillis()
): CapturedTricycleSubmissionLocation? =
    validateTricycleSubmissionLocation(
        latitude = latitude,
        longitude = longitude,
        accuracyMeters = if (hasAccuracy()) accuracy.toDouble() else null,
        capturedAtEpochMillis = time.takeIf { it > 0L } ?: nowEpochMillis,
        nowEpochMillis = nowEpochMillis
    )

internal fun validateTricycleSubmissionLocation(
    latitude: Double,
    longitude: Double,
    accuracyMeters: Double?,
    capturedAtEpochMillis: Long,
    nowEpochMillis: Long
): CapturedTricycleSubmissionLocation? {
    if (!latitude.isFinite() || latitude !in -90.0..90.0) return null
    if (!longitude.isFinite() || longitude !in -180.0..180.0) return null
    if (capturedAtEpochMillis <= 0L || nowEpochMillis <= 0L) return null

    val ageMillis = nowEpochMillis - capturedAtEpochMillis
    if (ageMillis > MaxSubmissionLocationAgeMillis || ageMillis < -MaxSubmissionLocationFutureSkewMillis) {
        return null
    }

    val normalizedAccuracy = when {
        accuracyMeters == null -> null
        !accuracyMeters.isFinite() -> return null
        accuracyMeters < 0.0 || accuracyMeters > MaxSupportedAccuracyMeters -> return null
        else -> accuracyMeters
    }

    return CapturedTricycleSubmissionLocation(
        latitude = latitude,
        longitude = longitude,
        accuracyMeters = normalizedAccuracy,
        capturedAt = Instant.ofEpochMilli(capturedAtEpochMillis)
    )
}

internal fun areTricycleSubmissionLocationsConsistent(
    initial: CapturedTricycleSubmissionLocation,
    final: CapturedTricycleSubmissionLocation,
    maxJumpMeters: Double = MaxSubmissionLocationJumpMeters
): Boolean {
    if (!maxJumpMeters.isFinite() || maxJumpMeters < 0.0) return false
    return distanceMeters(
        initial.latitude,
        initial.longitude,
        final.latitude,
        final.longitude
    ) <= maxJumpMeters
}

internal fun distanceMeters(
    latitude1: Double,
    longitude1: Double,
    latitude2: Double,
    longitude2: Double
): Double {
    val lat1 = Math.toRadians(latitude1)
    val lat2 = Math.toRadians(latitude2)
    val deltaLat = Math.toRadians(latitude2 - latitude1)
    val deltaLon = Math.toRadians(longitude2 - longitude1)

    val a = sin(deltaLat / 2.0) * sin(deltaLat / 2.0) +
        cos(lat1) * cos(lat2) * sin(deltaLon / 2.0) * sin(deltaLon / 2.0)
    val c = 2.0 * asin(sqrt(a.coerceIn(0.0, 1.0)))
    return EarthRadiusMeters * c
}
