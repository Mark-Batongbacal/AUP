package com.example.frontend.core.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import androidx.core.content.ContextCompat
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeoutOrNull
import kotlin.coroutines.resume

private const val FreshLocationTimeoutMillis = 5_000L
private const val CachedLocationMaxAgeMillis = 15_000L

fun Context.hasDeviceLocationPermission(): Boolean =
    ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED ||
        ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED

@SuppressLint("MissingPermission")
suspend fun Context.currentDeviceLocation(): Location? {
    if (!hasDeviceLocationPermission()) return null

    val manager = getSystemService(Context.LOCATION_SERVICE) as LocationManager
    val providers = manager.getProviders(true)
    val provider = when {
        manager.isProviderEnabled(LocationManager.GPS_PROVIDER) -> LocationManager.GPS_PROVIDER
        manager.isProviderEnabled(LocationManager.NETWORK_PROVIDER) -> LocationManager.NETWORK_PROVIDER
        else -> providers.firstOrNull()
    } ?: return null

    val fresh = withTimeoutOrNull(FreshLocationTimeoutMillis) {
        manager.awaitFreshLocation(provider)
    }
    if (fresh != null) return fresh

    val now = System.currentTimeMillis()
    return providers
        .mapNotNull { candidate -> runCatching { manager.getLastKnownLocation(candidate) }.getOrNull() }
        .filter { it.time > 0L && now - it.time <= CachedLocationMaxAgeMillis }
        .maxByOrNull { it.time }
}

@SuppressLint("MissingPermission")
private suspend fun LocationManager.awaitFreshLocation(provider: String): Location? =
    suspendCancellableCoroutine { continuation ->
        lateinit var listener: LocationListener
        listener = LocationListener { location ->
            removeUpdates(listener)
            if (continuation.isActive) continuation.resume(location)
        }

        runCatching {
            requestSingleUpdate(provider, listener, null)
        }.onFailure {
            removeUpdates(listener)
            if (continuation.isActive) continuation.resume(null)
        }

        continuation.invokeOnCancellation {
            runCatching { removeUpdates(listener) }
        }
    }
