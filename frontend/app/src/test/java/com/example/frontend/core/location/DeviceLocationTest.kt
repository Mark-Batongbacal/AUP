package com.example.frontend.core.location

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DeviceLocationTest {
    @Test
    fun recentLocationWithinWindow_isAccepted() {
        val now = 100_000L

        assertTrue(
            isLocationTimestampRecent(
                locationEpochMillis = 80_000L,
                nowEpochMillis = now,
                maxAgeMillis = 25_000L
            )
        )
    }

    @Test
    fun staleLocationOutsideWindow_isRejected() {
        val now = 100_000L

        assertFalse(
            isLocationTimestampRecent(
                locationEpochMillis = 70_000L,
                nowEpochMillis = now,
                maxAgeMillis = 25_000L
            )
        )
    }

    @Test
    fun futureLocationTimestamp_isRejected() {
        val now = 100_000L

        assertFalse(
            isLocationTimestampRecent(
                locationEpochMillis = 100_001L,
                nowEpochMillis = now,
                maxAgeMillis = 25_000L
            )
        )
    }

    @Test
    fun invalidTimestampInputs_areRejected() {
        assertFalse(isLocationTimestampRecent(0L, 100_000L, 25_000L))
        assertFalse(isLocationTimestampRecent(80_000L, 0L, 25_000L))
        assertFalse(isLocationTimestampRecent(80_000L, 100_000L, -1L))
    }
}
