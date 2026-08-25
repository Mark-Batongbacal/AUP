package com.example.frontend.screens

data class PlanningOriginSnapshot(
    val name: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null
) {
    val hasCoordinates: Boolean
        get() = latitude != null && longitude != null
}

/**
 * Short-lived handoff from HomeScreen to the planning AI overlay.
 *
 * HomeScreen already owns the passenger's effective planning origin: either the
 * detected device location or a location the passenger explicitly selected from
 * the fallback picker. Ask TUKI should consume that exact origin instead of
 * starting a second, independent GPS lookup.
 *
 * This is UI handoff state only. Durable assistant memory remains backend-owned.
 */
object PlanningOriginContext {
    @Volatile
    private var value = PlanningOriginSnapshot()

    fun update(name: String?, latitude: Double?, longitude: Double?) {
        value = PlanningOriginSnapshot(
            name = name?.trim()?.takeIf { it.isNotEmpty() },
            latitude = latitude,
            longitude = longitude
        )
    }

    fun snapshot(): PlanningOriginSnapshot = value
}
