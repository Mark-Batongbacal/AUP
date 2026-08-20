package com.example.frontend.data.trips

import com.example.frontend.model.CommuteStep
import com.example.frontend.model.HistoryLeg
import com.example.frontend.model.RecentCommute
import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.ZoneOffset

fun PassengerTripHistoryItemDto.toRecentCommute(
    originName: String = this.originName,
    destinationName: String = this.destinationName
): RecentCommute {
    val orderedLegs = recommendation?.legs?.sortedBy { it.legOrder }.orEmpty()

    return RecentCommute(
        id = passengerTripId,
        recommendationId = recommendation?.recommendationId,
        origin = originName,
        destination = destinationName,
        originLatitude = originLatitude,
        originLongitude = originLongitude,
        destinationLatitude = destinationLatitude,
        destinationLongitude = destinationLongitude,
        legs = orderedLegs.size,
        minutes = recommendation?.totalMinutes?.toInt() ?: 0,
        status = status.toDisplayTripStatus(),
        endedAt = completedAt,
        wasRerouted = rerouted,
        rerouteCount = rerouteCount,
        dateGroup = recentDateGroup(completedAt ?: startedAt ?: createdAt),
        steps = orderedLegs.map { leg ->
            CommuteStep(
                mode = leg.transportMode?.name
                    ?: leg.route?.routeName
                    ?: "Transit",
                from = leg.fromName
                    ?: leg.fromStop?.name
                    ?: originName,
                to = leg.toName
                    ?: leg.toStop?.name
                    ?: destinationName,
                minutes = leg.estimatedMinutes.toInt(),
                fare = leg.estimatedFare.toDouble()
            )
        },
        historyLegs = orderedLegs.map { leg ->
            HistoryLeg(
                mode = leg.transportMode?.code ?: "TRANSIT",
                routeId = leg.routeId,
                routeName = leg.route?.routeName,
                from = leg.fromName ?: leg.fromStop?.name ?: originName,
                to = leg.toName ?: leg.toStop?.name ?: destinationName,
                startLatitude = leg.startLatitude,
                startLongitude = leg.startLongitude,
                endLatitude = leg.endLatitude,
                endLongitude = leg.endLongitude
            )
        }
    )
}

private fun recentDateGroup(timestamp: String): String {
    val zone = ZoneId.systemDefault()
    val date = runCatching {
        Instant.parse(timestamp).atZone(zone).toLocalDate()
    }.recoverCatching {
        OffsetDateTime.parse(timestamp).atZoneSameInstant(zone).toLocalDate()
    }.recoverCatching {
        LocalDateTime.parse(timestamp)
            .atZone(ZoneOffset.UTC)
            .withZoneSameInstant(zone)
            .toLocalDate()
    }.getOrNull() ?: return "Earlier"

    val today = LocalDate.now(zone)
    return when (date) {
        today -> "Today"
        today.minusDays(1) -> "Yesterday"
        else -> "Earlier"
    }
}

private fun String.toDisplayTripStatus(): String = when (trim().uppercase()) {
    "ARRIVED", "COMPLETED" -> "Completed"
    "CANCELLED" -> "Cancelled"
    else -> replace('_', ' ')
        .lowercase()
        .replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
}
