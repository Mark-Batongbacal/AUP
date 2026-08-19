package com.example.frontend.repository

import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RouteOption
import kotlinx.coroutines.delay

class MockRouteRepository : RouteRepository {
    override suspend fun getRoutes(origin: String, destination: String): ApiResult<List<RouteOption>> {
        delay(900) // pretend network latency
        return ApiResult.Success(listOf(
            RouteOption(
                id = "1",
                label = "Fastest",
                totalMinutes = 22,
                totalFare = 35.0,
                steps = listOf(
                    CommuteStep(mode = "Jeepney", from = origin, to = "San Fernando Terminal", minutes = 14, fare = 15.0),
                    CommuteStep(mode = "Tricycle", from = "San Fernando Terminal", to = destination, minutes = 8, fare = 20.0)
                )
            ),
            RouteOption(
                id = "2",
                label = "Cheapest",
                totalMinutes = 35,
                totalFare = 22.0,
                steps = listOf(
                    CommuteStep(mode = "Jeepney", from = origin, to = "Dolores Crossing", minutes = 20, fare = 12.0),
                    CommuteStep(mode = "Walk", from = "Dolores Crossing", to = "Guagua Terminal", minutes = 5, fare = null),
                    CommuteStep(mode = "Jeepney", from = "Guagua Terminal", to = destination, minutes = 10, fare = 10.0)
                )
            ),
            RouteOption(
                id = "3",
                label = "Fewest transfers",
                totalMinutes = 28,
                totalFare = 40.0,
                steps = listOf(
                    CommuteStep(mode = "Bus", from = origin, to = destination, minutes = 28, fare = 40.0)
                )
            )
        ))
    }
}
