package com.example.frontend.data.contributions

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationManager
import android.os.Looper
import androidx.core.content.ContextCompat
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeoutOrNull
import kotlin.coroutines.resume

private const val PreciseSubmissionAccuracyMeters = 35.0
private const val PreciseSubmissionLocationMaxAgeMillis = 10_000L
private const val PreciseSubmissionLocationTimeoutMillis = 20_000L
private const val PreciseSubmissionUpdateIntervalMillis = 1_000L
private const val PreciseSubmissionMinUpdateIntervalMillis = 500L

sealed interface PreciseTricycleSubmissionLocationResult {
    data class Success(val location: CapturedTricycleSubmissionLocation) :
        PreciseTricycleSubmissionLocationResult

    data object PrecisePermissionRequired : PreciseTricycleSubmissionLocationResult
    data object LocationServicesDisabled : PreciseTricycleSubmissionLocationResult
    data object AccuracyUnavailable : PreciseTricycleSubmissionLocationResult
}

fun Context.hasPreciseDeviceLocationPermission(): Boolean =
    ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) ==
        PackageManager.PERMISSION_GRANTED

@SuppressLint("MissingPermission")
suspend fun Context.acquirePreciseTricycleSubmissionLocation(): PreciseTricycleSubmissionLocationResult {
    if (!hasPreciseDeviceLocationPermission()) {
        return PreciseTricycleSubmissionLocationResult.PrecisePermissionRequired
    }

    val locationManager = getSystemService(Context.LOCATION_SERVICE) as LocationManager
    if (!locationManager.hasEnabledLocationProvider()) {
        return PreciseTricycleSubmissionLocationResult.LocationServicesDisabled
    }

    val fusedClient = LocationServices.getFusedLocationProviderClient(this)
    val now = System.currentTimeMillis()

    val cached = fusedClient.awaitLastLocation()
    if (cached.isPreciseTricycleSubmissionFix(now)) {
        return cached?.toCapturedTricycleSubmissionLocation(now)
            ?.let(PreciseTricycleSubmissionLocationResult::Success)
            ?: PreciseTricycleSubmissionLocationResult.AccuracyUnavailable
    }

    val fresh = withTimeoutOrNull(PreciseSubmissionLocationTimeoutMillis) {
        fusedClient.awaitPreciseLocation()
    } ?: return PreciseTricycleSubmissionLocationResult.AccuracyUnavailable

    return fresh.toCapturedTricycleSubmissionLocation()
        ?.let(PreciseTricycleSubmissionLocationResult::Success)
        ?: PreciseTricycleSubmissionLocationResult.AccuracyUnavailable
}

private fun LocationManager.hasEnabledLocationProvider(): Boolean =
    runCatching {
        isProviderEnabled(LocationManager.GPS_PROVIDER) ||
            isProviderEnabled(LocationManager.NETWORK_PROVIDER)
    }.getOrDefault(false)

internal fun isPreciseTricycleSubmissionFix(
    accuracyMeters: Double?,
    locationEpochMillis: Long,
    nowEpochMillis: Long
): Boolean {
    if (accuracyMeters == null || !accuracyMeters.isFinite()) return false
    if (accuracyMeters < 0.0 || accuracyMeters > PreciseSubmissionAccuracyMeters) return false
    if (locationEpochMillis <= 0L || nowEpochMillis <= 0L) return false

    val ageMillis = nowEpochMillis - locationEpochMillis
    return ageMillis in 0L..PreciseSubmissionLocationMaxAgeMillis
}

private fun Location?.isPreciseTricycleSubmissionFix(nowEpochMillis: Long): Boolean {
    if (this == null || !hasAccuracy()) return false
    return isPreciseTricycleSubmissionFix(
        accuracyMeters = accuracy.toDouble(),
        locationEpochMillis = time,
        nowEpochMillis = nowEpochMillis
    )
}

@SuppressLint("MissingPermission")
private suspend fun FusedLocationProviderClient.awaitLastLocation(): Location? =
    suspendCancellableCoroutine { continuation ->
        lastLocation
            .addOnSuccessListener { location ->
                if (continuation.isActive) continuation.resume(location)
            }
            .addOnFailureListener {
                if (continuation.isActive) continuation.resume(null)
            }
            .addOnCanceledListener {
                if (continuation.isActive) continuation.resume(null)
            }
    }

@SuppressLint("MissingPermission")
private suspend fun FusedLocationProviderClient.awaitPreciseLocation(): Location? =
    suspendCancellableCoroutine { continuation ->
        val request = LocationRequest.Builder(
            Priority.PRIORITY_HIGH_ACCURACY,
            PreciseSubmissionUpdateIntervalMillis
        )
            .setMinUpdateIntervalMillis(PreciseSubmissionMinUpdateIntervalMillis)
            .build()

        lateinit var callback: LocationCallback
        callback = object : LocationCallback() {
            override fun onLocationResult(result: LocationResult) {
                val now = System.currentTimeMillis()
                val precise = result.locations
                    .asSequence()
                    .filter { it.isPreciseTricycleSubmissionFix(now) }
                    .minByOrNull { it.accuracy }
                    ?: return

                removeLocationUpdates(this)
                if (continuation.isActive) continuation.resume(precise)
            }
        }

        requestLocationUpdates(request, callback, Looper.getMainLooper())
            .addOnFailureListener {
                runCatching { removeLocationUpdates(callback) }
                if (continuation.isActive) continuation.resume(null)
            }
            .addOnCanceledListener {
                runCatching { removeLocationUpdates(callback) }
                if (continuation.isActive) continuation.resume(null)
            }

        continuation.invokeOnCancellation {
            runCatching { removeLocationUpdates(callback) }
        }
    }
