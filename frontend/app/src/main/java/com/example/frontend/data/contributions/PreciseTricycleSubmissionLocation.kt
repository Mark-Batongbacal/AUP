package com.example.frontend.data.contributions

import android.content.Context
import android.location.LocationManager
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.location.hasPreciseDeviceLocationPermission as hasCorePreciseDeviceLocationPermission

sealed interface PreciseTricycleSubmissionLocationResult {
    data class Success(val location: CapturedTricycleSubmissionLocation) :
        PreciseTricycleSubmissionLocationResult

    data object PrecisePermissionRequired : PreciseTricycleSubmissionLocationResult
    data object LocationServicesDisabled : PreciseTricycleSubmissionLocationResult
    data object AccuracyUnavailable : PreciseTricycleSubmissionLocationResult
}

fun Context.hasPreciseDeviceLocationPermission(): Boolean =
    hasCorePreciseDeviceLocationPermission()

suspend fun Context.acquirePreciseTricycleSubmissionLocation(): PreciseTricycleSubmissionLocationResult {
    if (!hasCorePreciseDeviceLocationPermission()) {
        return PreciseTricycleSubmissionLocationResult.PrecisePermissionRequired
    }

    val locationManager = getSystemService(Context.LOCATION_SERVICE) as LocationManager
    if (!locationManager.hasEnabledLocationProvider()) {
        return PreciseTricycleSubmissionLocationResult.LocationServicesDisabled
    }

    // Deliberately use the exact same one-shot location source as HomeScreen.
    // Contributions add verification around the result rather than maintaining
    // a second GPS engine with different provider, timeout, or cache behavior.
    val detected = currentDeviceLocation()
        ?.toCapturedTricycleSubmissionLocation()
        ?: return PreciseTricycleSubmissionLocationResult.AccuracyUnavailable

    return PreciseTricycleSubmissionLocationResult.Success(detected)
}

private fun LocationManager.hasEnabledLocationProvider(): Boolean =
    runCatching {
        isProviderEnabled(LocationManager.GPS_PROVIDER) ||
            isProviderEnabled(LocationManager.NETWORK_PROVIDER)
    }.getOrDefault(false)
