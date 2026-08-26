package com.example.frontend.data.contributions

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class TricycleSubmissionLocationTest {
    @Test
    fun validRecentLocation_isAcceptedWithAccuracy() {
        val now = 1_800_000_000_000L
        val result = validateTricycleSubmissionLocation(
            latitude = 15.2145,
            longitude = 120.5891,
            accuracyMeters = 8.5,
            capturedAtEpochMillis = now - 5_000L,
            nowEpochMillis = now
        )

        assertNotNull(result)
        assertEquals(15.2145, result!!.latitude, 0.0)
        assertEquals(120.5891, result.longitude, 0.0)
        assertEquals(8.5, result.accuracyMeters!!, 0.0)
    }

    @Test
    fun lowAccuracyLocation_isPreservedForAdminVerification() {
        val now = 1_800_000_000_000L
        val result = validateTricycleSubmissionLocation(
            latitude = 15.2145,
            longitude = 120.5891,
            accuracyMeters = 73.0,
            capturedAtEpochMillis = now - 2_000L,
            nowEpochMillis = now
        )

        assertNotNull(result)
        assertEquals(73.0, result!!.accuracyMeters!!, 0.0)
    }

    @Test
    fun staleLocation_isRejected() {
        val now = 1_800_000_000_000L
        val result = validateTricycleSubmissionLocation(
            latitude = 15.2145,
            longitude = 120.5891,
            accuracyMeters = 10.0,
            capturedAtEpochMillis = now - 31_000L,
            nowEpochMillis = now
        )

        assertNull(result)
    }

    @Test
    fun impossibleCoordinates_areRejected() {
        val now = 1_800_000_000_000L
        assertNull(
            validateTricycleSubmissionLocation(
                latitude = 91.0,
                longitude = 120.0,
                accuracyMeters = 5.0,
                capturedAtEpochMillis = now,
                nowEpochMillis = now
            )
        )
        assertNull(
            validateTricycleSubmissionLocation(
                latitude = 15.0,
                longitude = 181.0,
                accuracyMeters = 5.0,
                capturedAtEpochMillis = now,
                nowEpochMillis = now
            )
        )
    }

    @Test
    fun unreasonableAccuracy_isRejected() {
        val now = 1_800_000_000_000L
        val result = validateTricycleSubmissionLocation(
            latitude = 15.2145,
            longitude = 120.5891,
            accuracyMeters = 100_001.0,
            capturedAtEpochMillis = now,
            nowEpochMillis = now
        )

        assertNull(result)
    }
}
