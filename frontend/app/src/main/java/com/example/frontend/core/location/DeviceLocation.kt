package com.example.frontend.core.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationManager
import androidx.core.content.ContextCompat
import com.google.android.gms.location.CurrentLocationRequest
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume

private const val CurrentLocationRequestDurationMillis = 12_000L
private const val CachedLocationMaxAgeMillis = 25_000L

fun Context.hasDeviceLocationPermission(): Boolean =
    ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED ||
        ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED

fun Context.hasPreciseDeviceLocationPermission(): Boolean =
    ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED

@SuppressLint("MissingPermission")
suspend fun Context.currentDeviceLocation(): Location? {
    if (!hasDeviceLocationPermission()) return null

    val locationManager = getSystemService(Context.LOCATION_SERVICE) as LocationManager
    if (!locationManager.hasEnabledLocationProvider()) return null

    val fusedClient = LocationServices.getFusedLocationProviderClient(this)
    val now = System.currentTimeMillis()
    val cached = fusedClient.awaitLastLocation()

    if (cached.isRecentEnough(now, CachedLocationMaxAgeMillis)) {
        return cached
    }

    val request = CurrentLocationRequest.Builder()
        .setPriority(Priority.PRIORITY_HIGH_ACCURACY)
        .setMaxUpdateAgeMillis(CachedLocationMaxAgeMillis)
        .setDurationMillis(CurrentLocationRequestDurationMillis)
        .build()

    return fusedClient.awaitCurrentLocation(request)
        ?: cached?.takeIf { it.isRecentEnough(System.currentTimeMillis(), CachedLocationMaxAgeMillis) }
}

private fun LocationManager.hasEnabledLocationProvider(): Boolean =
    runCatching {
        isProviderEnabled(LocationManager.GPS_PROVIDER) ||
            isProviderEnabled(LocationManager.NETWORK_PROVIDER)
    }.getOrDefault(false)

private fun Location?.isRecentEnough(nowEpochMillis: Long, maxAgeMillis: Long): Boolean =
    this != null && isLocationTimestampRecent(time, nowEpochMillis, maxAgeMillis)

internal fun isLocationTimestampRecent(
    locationEpochMillis: Long,
    nowEpochMillis: Long,
    maxAgeMillis: Long
): Boolean {
    if (locationEpochMillis <= 0L || nowEpochMillis <= 0L || maxAgeMillis < 0L) return false
    val ageMillis = nowEpochMillis - locationEpochMillis
    return ageMillis in 0L..maxAgeMillis
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
private suspend fun FusedLocationProviderClient.awaitCurrentLocation(
    request: CurrentLocationRequest
): Location? = suspendCancellableCoroutine { continuation ->
    val cancellationTokenSource = CancellationTokenSource()

    getCurrentLocation(request, cancellationTokenSource.token)
        .addOnSuccessListener { location ->
            if (continuation.isActive) continuation.resume(location)
        }
        .addOnFailureListener {
            if (continuation.isActive) continuation.resume(null)
        }
        .addOnCanceledListener {
            if (continuation.isActive) continuation.resume(null)
        }

    continuation.invokeOnCancellation {
        cancellationTokenSource.cancel()
    }
}
