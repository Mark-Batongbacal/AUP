package com.example.frontend.model.network

/**
 * Matches backend.Controllers.RouteDto
 */
data class RouteDto(
    val routeName: String,
    val points: List<RoutePointDto>
)

/**
 * Matches backend.Controllers.RoutePointDto
 */
data class RoutePointDto(
    val latitude: Double,
    val longitude: Double
)
