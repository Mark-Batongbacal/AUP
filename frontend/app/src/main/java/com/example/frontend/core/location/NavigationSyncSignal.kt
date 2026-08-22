package com.example.frontend.core.location

import java.util.concurrent.atomic.AtomicBoolean

/**
 * Bridges local route-corridor detection with the repository heartbeat without coupling the map
 * to Retrofit. Only one passenger navigation session is active at a time, so a single pending
 * immediate-sync flag is sufficient.
 */
object NavigationSyncSignal {
    private val immediateSyncRequested = AtomicBoolean(false)

    fun requestImmediateSync() {
        immediateSyncRequested.set(true)
    }

    fun consumeImmediateSync(): Boolean = immediateSyncRequested.getAndSet(false)

    fun reset() {
        immediateSyncRequested.set(false)
    }
}
