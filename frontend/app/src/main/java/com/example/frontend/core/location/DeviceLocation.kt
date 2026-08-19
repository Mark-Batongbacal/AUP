package com.example.frontend.core.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationManager
import androidx.core.content.ContextCompat
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume

fun Context.hasDeviceLocationPermission(): Boolean =
    ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED ||
        ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED

@SuppressLint("MissingPermission")
suspend fun Context.currentDeviceLocation(): Location? {
    if (!hasDeviceLocationPermission()) return null

    val manager = getSystemService(Context.LOCATION_SERVICE) as LocationManager
    val providers = manager.getProviders(true)

    val cached = providers
        .mapNotNull { provider -> runCatching { manager.getLastKnownLocation(provider) }.getOrNull() }
        .maxByOrNull { it.time }

    if (cached != null) return cached

    val provider = when {
        manager.isProviderEnabled(LocationManager.GPS_PROVIDER) -> LocationManager.GPS_PROVIDER
        manager.isProviderEnabled(LocationManager.NETWORK_PROVIDER) -> LocationManager.NETWORK_PROVIDER
        else -> providers.firstOrNull()
    } ?: return null

    return suspendCancellableCoroutine { continuation ->
        val listener = android.location.LocationListener { location ->
            if (continuation.isActive) continuation.resume(location)
        }

        runCatching {
            manager.requestSingleUpdate(provider, listener, null)
        }.onFailure {
            if (continuation.isActive) continuation.resume(null)
        }

        continuation.invokeOnCancellation {
            runCatching { manager.removeUpdates(listener) }
        }
    }
}
