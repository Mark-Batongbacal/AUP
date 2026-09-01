package com.example.frontend.navigation

import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.model.RecentCommute

data class RepeatTripRouteSeed(
    val originName: String,
    val originLatitude: Double?,
    val originLongitude: Double?,
    val destination: DestinationSearchResultDto
)

fun RecentCommute.toRepeatTripRouteSeed(): RepeatTripRouteSeed? {
    val firstHistoryLeg = historyLegs.firstOrNull()
    val lastHistoryLeg = historyLegs.lastOrNull()
    val resolvedOriginLatitude = originLatitude ?: firstHistoryLeg?.startLatitude
    val resolvedOriginLongitude = originLongitude ?: firstHistoryLeg?.startLongitude
    val resolvedDestinationLatitude = destinationLatitude ?: lastHistoryLeg?.endLatitude ?: return null
    val resolvedDestinationLongitude = destinationLongitude ?: lastHistoryLeg?.endLongitude ?: return null

    return RepeatTripRouteSeed(
        originName = origin,
        originLatitude = resolvedOriginLatitude,
        originLongitude = resolvedOriginLongitude,
        destination = DestinationSearchResultDto(
            id = "recent-$id",
            name = destination,
            latitude = resolvedDestinationLatitude,
            longitude = resolvedDestinationLongitude,
            category = "recent",
            source = "history",
            address = null
        )
    )
}
