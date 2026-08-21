package com.example.frontend.model

data class FavoriteRoute(
    val id: String,
    val recommendationId: String = "",
    val origin: String,
    val destination: String,
    val recommendationType: String = "",
    val minutes: Int = 0,
    val totalFare: Double = 0.0,
    val walkingMeters: Int = 0,
    val timesUsed: Int = 0,
    val note: String = ""
)
