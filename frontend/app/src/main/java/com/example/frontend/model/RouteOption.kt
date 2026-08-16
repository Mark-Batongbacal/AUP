package com.example.frontend.model

class RouteOption (
    val id: String,
    val label: String,
    val totalMinutes: Int,
    val totalFare: Double,
    val steps: List<CommuteStep>
)