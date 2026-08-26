package com.example.frontend.screens

import java.time.Instant
import org.junit.Assert.assertEquals
import org.junit.Test

class PrivacySecurityScreenTest {
    private val now = Instant.parse("2026-08-27T00:00:00Z")

    @Test
    fun passwordChangeWithinMinute_isJustNow() {
        assertEquals(
            "Last changed just now",
            lastPasswordChangeLabel("2026-08-26T23:59:30Z", now)
        )
    }

    @Test
    fun passwordChangeDaysAgo_usesRelativeDays() {
        assertEquals(
            "Last changed 3 days ago",
            lastPasswordChangeLabel("2026-08-24T00:00:00Z", now)
        )
    }

    @Test
    fun passwordChangeMonthsAgo_usesRelativeMonths() {
        assertEquals(
            "Last changed 3 months ago",
            lastPasswordChangeLabel("2026-05-27T00:00:00Z", now)
        )
    }

    @Test
    fun invalidTimestamp_isUnavailable() {
        assertEquals("Last change unavailable", lastPasswordChangeLabel("not-a-date", now))
    }
}
