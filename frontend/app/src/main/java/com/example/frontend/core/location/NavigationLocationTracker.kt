package com.example.frontend.core.location

import android.annotation.SuppressLint
import android.content.Context
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.os.Looper
import com.google.android.gms.common.ConnectionResult
import com.google.android.gms.common.GoogleApiAvailability
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

/**
 * Fresh location fixes for an active navigation session.
 *
 * Prefer Google Play services' fused location provider so Tuki receives one high-accuracy stream
 * instead of independently consuming GPS and network-provider callbacks. Devices without usable
 * Google Play services fall back to Android's LocationManager implementation.
 */
@SuppressLint("MissingPermission")
fun Context.navigationLocationUpdates(
    minTimeMillis: Long = 1_000L,
    minDistanceMeters: Float = 1f
): Flow<Location> = callbackFlow {
    if (!hasDeviceLocationPermission()) {
        close(SecurityException("Location permission is not granted."))
        return@callbackFlow
    }

    val appContext = applicationContext
    val locationManager = appContext.getSystemService(Context.LOCATION_SERVICE) as LocationManager
    val fusedClient = LocationServices.getFusedLocationProviderClient(appContext)

    var fusedCallback: LocationCallback? = null
    var fallbackListener: LocationListener? = null
    var closed = false

    fun publish(location: Location) {
        rememberDeviceLocation(location)
        trySend(location)
    }

    fun startLocationManagerFallback() {
        if (closed || fallbackListener != null) return

        val providers = buildList {
            if (hasPreciseDeviceLocationPermission() &&
                locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)
            ) {
                add(LocationManager.GPS_PROVIDER)
            }
            if (locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) {
                add(LocationManager.NETWORK_PROVIDER)
            }
        }

        if (providers.isEmpty()) {
            close(IllegalStateException(LocationDetectionFailureMessage))
            return
        }

        val listener = LocationListener { location ->
            publish(location)
        }
        fallbackListener = listener

        try {
            providers.forEach { provider ->
                locationManager.requestLocationUpdates(
                    provider,
                    minTimeMillis,
                    minDistanceMeters,
                    listener,
                    Looper.getMainLooper()
                )
            }
        } catch (error: SecurityException) {
            runCatching { locationManager.removeUpdates(listener) }
            fallbackListener = null
            close(error)
        } catch (error: IllegalArgumentException) {
            runCatching { locationManager.removeUpdates(listener) }
            fallbackListener = null
            close(error)
        }
    }

    val playServicesAvailable = GoogleApiAvailability.getInstance()
        .isGooglePlayServicesAvailable(appContext) == ConnectionResult.SUCCESS

    if (playServicesAvailable) {
        val request = LocationRequest.Builder(
            if (hasPreciseDeviceLocationPermission()) Priority.PRIORITY_HIGH_ACCURACY
            else Priority.PRIORITY_BALANCED_POWER_ACCURACY,
            minTimeMillis
        )
            .setMinUpdateIntervalMillis((minTimeMillis / 2L).coerceAtLeast(250L))
            .setMinUpdateDistanceMeters(minDistanceMeters)
            .build()

        val callback = object : LocationCallback() {
            override fun onLocationResult(result: LocationResult) {
                result.locations.forEach(::publish)
            }
        }
        fusedCallback = callback

        fusedClient.requestLocationUpdates(
            request,
            callback,
            Looper.getMainLooper()
        ).addOnFailureListener {
            if (!closed) {
                fusedCallback = null
                startLocationManagerFallback()
            }
        }
    } else {
        startLocationManagerFallback()
    }

    awaitClose {
        closed = true
        fusedCallback?.let { callback ->
            fusedClient.removeLocationUpdates(callback)
        }
        fallbackListener?.let { listener ->
            locationManager.removeUpdates(listener)
        }
    }
}
