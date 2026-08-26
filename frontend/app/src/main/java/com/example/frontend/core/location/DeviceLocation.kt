package com.example.frontend.core.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.os.Looper
import androidx.core.content.ContextCompat
import com.google.android.gms.common.ConnectionResult
import com.google.android.gms.common.GoogleApiAvailability
import com.google.android.gms.location.CurrentLocationRequest
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeoutOrNull
import kotlin.coroutines.resume

private const val SharedLocationImmediateMaxAgeMillis = 5_000L
private const val ImmediateCachedLocationMaxAgeMillis = 10_000L
private const val FallbackCachedLocationMaxAgeMillis = 30_000L
private const val CurrentLocationRequestDurationMillis = 8_000L
private const val LocationManagerFallbackTimeoutMillis = 5_000L

fun Context.hasDeviceLocationPermission(): Boolean =
    ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED ||
        ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED

fun Context.hasPreciseDeviceLocationPermission(): Boolean =
    ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED

/**
 * Last fresh fix already obtained elsewhere in the app, most importantly by the active-navigation
 * fused location stream. Keeping this in-process avoids starting a second cold GPS lookup when the
 * app already knows where the device is.
 */
private object RecentDeviceLocationCache {
    @Volatile
    private var latest: Location? = null

    fun remember(location: Location) {
        val copy = Location(location)
        val existing = latest
        if (existing == null || copy.time >= existing.time) {
            latest = copy
        }
    }

    fun recent(nowEpochMillis: Long, maxAgeMillis: Long): Location? =
        latest
            ?.takeIf { it.isRecentEnough(nowEpochMillis, maxAgeMillis) }
            ?.let(::Location)
}

/** Records a location produced by a long-lived GPS stream for reuse by one-shot location actions. */
internal fun rememberDeviceLocation(location: Location) {
    RecentDeviceLocationCache.remember(location)
}

/**
 * Gets a reliable one-shot current location for route planning, "Use current", AI origin context,
 * and other non-navigation actions.
 *
 * Order of preference:
 * 1. a very fresh fix already produced by active navigation,
 * 2. a recent Google Fused Location cache,
 * 3. a fresh Fused Location request,
 * 4. Android LocationManager cache/fresh fallback,
 * 5. the newest still-reasonable cached fix.
 *
 * The backend is deliberately not involved in acquiring GPS. It only receives the resulting
 * coordinates later for operations such as reverse geocoding or route planning.
 */
@SuppressLint("MissingPermission")
suspend fun Context.currentDeviceLocation(): Location? {
    if (!hasDeviceLocationPermission()) return null

    val appContext = applicationContext
    val now = System.currentTimeMillis()
    RecentDeviceLocationCache.recent(now, SharedLocationImmediateMaxAgeMillis)?.let { return it }

    val locationManager = appContext.getSystemService(Context.LOCATION_SERVICE) as LocationManager
    val precisePermission = appContext.hasPreciseDeviceLocationPermission()
    val playServicesAvailable = GoogleApiAvailability.getInstance()
        .isGooglePlayServicesAvailable(appContext) == ConnectionResult.SUCCESS

    var fusedCached: Location? = null
    if (playServicesAvailable) {
        val fusedClient = LocationServices.getFusedLocationProviderClient(appContext)
        fusedCached = fusedClient.awaitLastLocation()

        if (fusedCached.isRecentEnough(now, ImmediateCachedLocationMaxAgeMillis)) {
            rememberDeviceLocation(fusedCached!!)
            return fusedCached
        }

        val request = CurrentLocationRequest.Builder()
            .setPriority(
                if (precisePermission) Priority.PRIORITY_HIGH_ACCURACY
                else Priority.PRIORITY_BALANCED_POWER_ACCURACY
            )
            .setMaxUpdateAgeMillis(ImmediateCachedLocationMaxAgeMillis)
            .setDurationMillis(CurrentLocationRequestDurationMillis)
            .build()

        val freshFused = fusedClient.awaitCurrentLocation(request)
        if (freshFused.isRecentEnough(System.currentTimeMillis(), FallbackCachedLocationMaxAgeMillis)) {
            rememberDeviceLocation(freshFused!!)
            return freshFused
        }
    }

    // Fused Location can be unavailable or temporarily fail even when Android still has a usable
    // GPS/network provider. The navigation stream already has this fallback; one-shot lookup should
    // be equally resilient.
    val locationManagerCached = locationManager.latestLastKnownLocation()
    val cachedBeforeFallback = newestRecentLocation(
        candidates = listOfNotNull(
            fusedCached,
            locationManagerCached,
            RecentDeviceLocationCache.recent(System.currentTimeMillis(), FallbackCachedLocationMaxAgeMillis)
        ),
        nowEpochMillis = System.currentTimeMillis(),
        maxAgeMillis = ImmediateCachedLocationMaxAgeMillis
    )
    if (cachedBeforeFallback != null) {
        rememberDeviceLocation(cachedBeforeFallback)
        return cachedBeforeFallback
    }

    val enabledProviders = locationManager.enabledLocationProviders(allowGps = precisePermission)
    val freshFallback = if (enabledProviders.isEmpty()) {
        null
    } else {
        withTimeoutOrNull(LocationManagerFallbackTimeoutMillis) {
            locationManager.awaitFreshLocation(enabledProviders)
        }
    }
    if (freshFallback.isRecentEnough(System.currentTimeMillis(), FallbackCachedLocationMaxAgeMillis)) {
        rememberDeviceLocation(freshFallback!!)
        return freshFallback
    }

    // A short-lived cached fix is preferable to reporting location as unavailable after both fresh
    // providers were attempted. Thirty seconds is intentionally only a last-resort window.
    return newestRecentLocation(
        candidates = listOfNotNull(
            freshFallback,
            fusedCached,
            locationManagerCached,
            RecentDeviceLocationCache.recent(System.currentTimeMillis(), FallbackCachedLocationMaxAgeMillis)
        ),
        nowEpochMillis = System.currentTimeMillis(),
        maxAgeMillis = FallbackCachedLocationMaxAgeMillis
    )?.also(::rememberDeviceLocation)
}

private fun LocationManager.enabledLocationProviders(allowGps: Boolean): List<String> = buildList {
    if (allowGps && runCatching { isProviderEnabled(LocationManager.GPS_PROVIDER) }.getOrDefault(false)) {
        add(LocationManager.GPS_PROVIDER)
    }
    if (runCatching { isProviderEnabled(LocationManager.NETWORK_PROVIDER) }.getOrDefault(false)) {
        add(LocationManager.NETWORK_PROVIDER)
    }
}

@SuppressLint("MissingPermission")
private fun LocationManager.latestLastKnownLocation(): Location? =
    runCatching { getProviders(true) }
        .getOrDefault(emptyList())
        .mapNotNull { provider -> runCatching { getLastKnownLocation(provider) }.getOrNull() }
        .filter { it.time > 0L }
        .maxByOrNull { it.time }

private fun Location?.isRecentEnough(nowEpochMillis: Long, maxAgeMillis: Long): Boolean =
    this != null && isLocationTimestampRecent(time, nowEpochMillis, maxAgeMillis)

private fun newestRecentLocation(
    candidates: List<Location>,
    nowEpochMillis: Long,
    maxAgeMillis: Long
): Location? = candidates
    .filter { it.isRecentEnough(nowEpochMillis, maxAgeMillis) }
    .maxByOrNull { it.time }

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

@SuppressLint("MissingPermission")
private suspend fun LocationManager.awaitFreshLocation(providers: List<String>): Location? =
    suspendCancellableCoroutine { continuation ->
        if (providers.isEmpty()) {
            continuation.resume(null)
            return@suspendCancellableCoroutine
        }

        lateinit var listener: LocationListener
        listener = LocationListener { location ->
            if (!location.isRecentEnough(System.currentTimeMillis(), FallbackCachedLocationMaxAgeMillis)) {
                return@LocationListener
            }
            runCatching { removeUpdates(listener) }
            if (continuation.isActive) continuation.resume(location)
        }

        var registeredProviders = 0
        providers.forEach { provider ->
            val registered = runCatching {
                requestLocationUpdates(
                    provider,
                    0L,
                    0f,
                    listener,
                    Looper.getMainLooper()
                )
            }.isSuccess
            if (registered) registeredProviders++
        }

        if (registeredProviders == 0) {
            runCatching { removeUpdates(listener) }
            if (continuation.isActive) continuation.resume(null)
            return@suspendCancellableCoroutine
        }

        continuation.invokeOnCancellation {
            runCatching { removeUpdates(listener) }
        }
    }
