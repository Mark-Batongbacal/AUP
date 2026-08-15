package com.example.frontend.model

data class RecentCommute(
    val id: String,
    val origin: String,
    val destination: String,
    val legs: Int,      // number of rides/transfers
    val minutes: Int,   // estimated total travel time
    val steps: List<CommuteStep> = emptyList()
)

data class CommuteStep(
    val mode: String,   // "Jeepney", "Tricycle", "Walk", "Bus"
    val from: String,
    val to: String,
    val minutes: Int,
    val fare: Double? = null
)