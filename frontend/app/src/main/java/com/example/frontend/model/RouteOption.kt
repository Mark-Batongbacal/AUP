package com.example.frontend.model

data class RoutePoint(
    val latitude: Double,
    val longitude: Double
)

class RouteOption (
    val id: String,
    val label: String,
    val totalMinutes: Int,
    val totalFare: Double,
    val steps: List<CommuteStep> = emptyList(),
    val description: String = "",    // e.g. "Jeepney + short tricycle transfer"
    val walkMeters: Int = 0,
    val transfers: Int = 0,
    val generalCost: Double = totalFare, // fare + time-value estimate shown on the carousel
    val isRecommended: Boolean = false,
    val routePoints: List<RoutePoint> = emptyList(),
    val legRoutePoints: List<List<RoutePoint>> = emptyList(),
    val legEndPoints: List<RoutePoint> = emptyList(),
    val legRouteIds: List<Long?> = emptyList()
)