package com.example.frontend.model

data class RecentCommute(
    val id: String,
    val recommendationId: String? = null,
    val recommendationType: String = "",
    val origin: String,
    val destination: String,
    val originLatitude: Double? = null,
    val originLongitude: Double? = null,
    val destinationLatitude: Double? = null,
    val destinationLongitude: Double? = null,
    val legs: Int,
    val minutes: Int,
    val totalFare: Double = 0.0,
    val walkingMeters: Int = 0,
    val status: String = "",
    val endedAt: String? = null,
    val wasRerouted: Boolean = false,
    val rerouteCount: Int = 0,
    val dateGroup: String = "",
    val steps: List<CommuteStep> = emptyList(),
    val historyLegs: List<HistoryLeg> = emptyList()
)

data class HistoryLeg(
    val mode: String,
    val routeId: Long? = null,
    val routeName: String? = null,
    val from: String,
    val to: String,
    val startLatitude: Double?,
    val startLongitude: Double?,
    val endLatitude: Double?,
    val endLongitude: Double?
)

data class CommuteStep(
    val mode: String,
    val from: String,
    val to: String,
    val minutes: Int,
    val fare: Double? = null,
    val distanceMeters: Double? = null,
    val instructions: String? = null
)
