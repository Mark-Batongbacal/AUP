package com.example.frontend.data.contributions

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PreciseTricycleSubmissionLocationTest {
    private val now = 1_000_000L

    @Test
    fun fixWithin35MetersAndFresh_isAccepted() {
        assertTrue(
            isPreciseTricycleSubmissionFix(
                accuracyMeters = 18.5,
                locationEpochMillis = now - 2_000L,
                nowEpochMillis = now
            )
        )
    }

    @Test
    fun fixExactly35MetersAndFresh_isAccepted() {
        assertTrue(
            isPreciseTricycleSubmissionFix(
                accuracyMeters = 35.0,
                locationEpochMillis = now - 9_000L,
                nowEpochMillis = now
            )
        )
    }

    @Test
    fun fixWorseThan35Meters_isRejected() {
        assertFalse(
            isPreciseTricycleSubmissionFix(
                accuracyMeters = 35.1,
                locationEpochMillis = now - 1_000L,
                nowEpochMillis = now
            )
        )
    }

    @Test
    fun staleFix_isRejected() {
        assertFalse(
            isPreciseTricycleSubmissionFix(
                accuracyMeters = 10.0,
                locationEpochMillis = now - 10_001L,
                nowEpochMillis = now
            )
        )
    }

    @Test
    fun missingAccuracy_isRejected() {
        assertFalse(
            isPreciseTricycleSubmissionFix(
                accuracyMeters = null,
                locationEpochMillis = now - 1_000L,
                nowEpochMillis = now
            )
        )
    }

    @Test
    fun futureFix_isRejected() {
        assertFalse(
            isPreciseTricycleSubmissionFix(
                accuracyMeters = 10.0,
                locationEpochMillis = now + 1L,
                nowEpochMillis = now
            )
        )
    }
}
