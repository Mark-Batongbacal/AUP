package com.example.frontend.data.contributions

import android.location.Location
import java.time.Instant

private const val MaxSubmissionLocationAgeMillis = 30_000L
private const val MaxSubmissionLocationFutureSkewMillis = 10_000L
private const val MaxSupportedAccuracyMeters = 100_000.0

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
