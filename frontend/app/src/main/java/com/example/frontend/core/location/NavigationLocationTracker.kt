package com.example.frontend.core.location

import android.annotation.SuppressLint
import android.content.Context
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.os.Looper
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

/** Fresh platform location fixes for an active navigation session. */
@SuppressLint("MissingPermission")
fun Context.navigationLocationUpdates(
    minTimeMillis: Long = 1_000L,
    minDistanceMeters: Float = 1f
): Flow<Location> = callbackFlow {
    if (!hasDeviceLocationPermission()) {
        close(SecurityException("Location permission is not granted."))
        return@callbackFlow
    }

    val manager = getSystemService(Context.LOCATION_SERVICE) as LocationManager
    val providers = buildList {
        if (manager.isProviderEnabled(LocationManager.GPS_PROVIDER)) add(LocationManager.GPS_PROVIDER)
        if (manager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) add(LocationManager.NETWORK_PROVIDER)
    }

    if (providers.isEmpty()) {
        close(IllegalStateException(LocationDetectionFailureMessage))
        return@callbackFlow
    }

    val listener = LocationListener { location -> trySend(location) }
    try {
        providers.forEach { provider ->
            manager.requestLocationUpdates(
                provider,
                minTimeMillis,
                minDistanceMeters,
                listener,
                Looper.getMainLooper()
            )
        }
    } catch (error: SecurityException) {
        close(error)
        return@callbackFlow
    } catch (error: IllegalArgumentException) {
        close(error)
        return@callbackFlow
    }

    awaitClose { manager.removeUpdates(listener) }
}
