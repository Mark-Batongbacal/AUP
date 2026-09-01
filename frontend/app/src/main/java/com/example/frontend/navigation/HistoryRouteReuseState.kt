package com.example.frontend.navigation

import com.example.frontend.model.RouteOption

/**
 * Short-lived handoff used when a passenger chooses "Use this route again" from Journey History.
 * The existing route-results/navigation pipeline remains the owner of route selection and trip
 * start behavior; this state only lets that pipeline reuse the historical recommendation without
 * forcing the passenger to select the same route a second time.
 */
data class PendingHistoryRouteReuse(
    val option: RouteOption,
    val originName: String,
    val destinationName: String,
    val originLatitude: Double?,
    val originLongitude: Double?
)

object HistoryRouteReuseState {
    private var pendingSelection: PendingHistoryRouteReuse? = null
    private var autoStartNextRouteDetails: Boolean = false

    @Synchronized
    fun prepare(reuse: PendingHistoryRouteReuse) {
        pendingSelection = reuse
        // Do not arm auto-start until RouteResults actually consumes this handoff. This prevents
        // a failed/no-op history navigation from accidentally starting an unrelated later route.
        autoStartNextRouteDetails = false
    }

    @Synchronized
    fun clear() {
        pendingSelection = null
        autoStartNextRouteDetails = false
    }

    @Synchronized
    fun takePendingSelection(origin: String, destination: String): PendingHistoryRouteReuse? {
        val pending = pendingSelection ?: return null
        val sameOrigin = pending.originName.equals(origin, ignoreCase = true)
        val sameDestination = pending.destinationName.equals(destination, ignoreCase = true)
        if (!sameOrigin || !sameDestination) {
            clear()
            return null
        }

        pendingSelection = null
        autoStartNextRouteDetails = true
        return pending
    }

    @Synchronized
    fun consumeAutoStart(): Boolean {
        if (!autoStartNextRouteDetails) return false
        autoStartNextRouteDetails = false
        return true
    }
}
