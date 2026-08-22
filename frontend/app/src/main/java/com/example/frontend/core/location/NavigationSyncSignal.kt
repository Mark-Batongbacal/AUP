package com.example.frontend.core.location

import java.util.concurrent.atomic.AtomicInteger

/**
 * Requests a short burst of backend location syncs when local navigation detects a condition that
 * needs server confirmation. Routine GPS updates remain fully local.
 */
object NavigationSyncSignal {
    // Five samples at the existing ~5 s coordinator cadence span about 20 seconds, which is enough
    // for the backend's sustained off-route confirmation window as well as multi-step leg-end state
    // transitions such as WalkingToPickup -> ApproachingBoardPoint -> WaitingToBoard.
    private const val DefaultConfirmationSamples = 5
    private val pendingSyncs = AtomicInteger(0)

    fun requestImmediateSync(samples: Int = DefaultConfirmationSamples) {
        val requested = samples.coerceAtLeast(1)
        while (true) {
            val current = pendingSyncs.get()
            if (current >= requested) return
            if (pendingSyncs.compareAndSet(current, requested)) return
        }
    }

    fun consumeImmediateSync(): Boolean {
        while (true) {
            val current = pendingSyncs.get()
            if (current <= 0) return false
            if (pendingSyncs.compareAndSet(current, current - 1)) return true
        }
    }

    fun reset() {
        pendingSyncs.set(0)
    }
}
