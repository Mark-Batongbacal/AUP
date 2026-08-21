package com.example.frontend.core.location

const val LocationNotSupportedTitle = "Location Not Yet Supported"
const val LocationNotSupportedMessage =
    "TUKI is currently available only within Porac, Angeles City, Dau, and Mabalacat. Support for additional locations will be available in the future."
const val LocationNotSupportedShortMessage =
    "TUKI is currently available only within Porac, Angeles City, Dau, and Mabalacat."
const val LocationDetectionFailureMessage =
    "Unable to detect your current location. Please check your device's location settings and try again."

object TukiServiceArea {
    private const val SouthLatitude = 15.00
    private const val NorthLatitude = 15.30
    private const val WestLongitude = 120.43
    private const val EastLongitude = 120.68

    fun contains(latitude: Double, longitude: Double): Boolean =
        latitude in SouthLatitude..NorthLatitude &&
            longitude in WestLongitude..EastLongitude
}

fun isLocationSupported(latitude: Double, longitude: Double): Boolean =
    TukiServiceArea.contains(latitude, longitude)

fun isRouteSupported(
    originLatitude: Double,
    originLongitude: Double,
    destinationLatitude: Double,
    destinationLongitude: Double
): Boolean =
    isLocationSupported(originLatitude, originLongitude) &&
        isLocationSupported(destinationLatitude, destinationLongitude)
