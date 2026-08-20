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
    val destinationLatitude = destinationLatitude ?: return null
    val destinationLongitude = destinationLongitude ?: return null

    return RepeatTripRouteSeed(
        originName = origin,
        originLatitude = originLatitude,
        originLongitude = originLongitude,
        destination = DestinationSearchResultDto(
            id = "recent-$id",
            name = destination,
            latitude = destinationLatitude,
            longitude = destinationLongitude,
            category = "recent",
            source = "history",
            address = null
        )
    )
}
