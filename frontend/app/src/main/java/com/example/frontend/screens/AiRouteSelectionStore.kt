package com.example.frontend.screens

import com.example.frontend.model.RouteOption

internal object AiRouteSelectionStore {
    private var pendingDestinationName: String? = null
    private var pendingRoute: RouteOption? = null

    fun save(destinationName: String, route: RouteOption) {
        pendingDestinationName = destinationName.trim()
        pendingRoute = route
    }

    fun consume(destinationName: String): RouteOption? {
        val expected = pendingDestinationName
        val route = pendingRoute
        if (expected == null || route == null || !expected.equals(destinationName.trim(), ignoreCase = true)) {
            return null
        }

        pendingDestinationName = null
        pendingRoute = null
        return route
    }
}
