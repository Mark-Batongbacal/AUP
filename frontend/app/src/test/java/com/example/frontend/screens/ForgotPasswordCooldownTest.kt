package com.example.frontend.screens

import org.junit.Assert.assertEquals
import org.junit.Test

class ForgotPasswordCooldownTest {
    @Test
    fun remainingSeconds_roundsUpPartialSeconds() {
        assertEquals(
            180,
            forgotPasswordResendSecondsRemaining(
                cooldownUntilMillis = 180_000L,
                nowMillis = 1L
            )
        )
        assertEquals(
            1,
            forgotPasswordResendSecondsRemaining(
                cooldownUntilMillis = 1_001L,
                nowMillis = 2L
            )
        )
    }

    @Test
    fun remainingSeconds_isZeroAfterExpiry() {
        assertEquals(0, forgotPasswordResendSecondsRemaining(1_000L, 1_000L))
        assertEquals(0, forgotPasswordResendSecondsRemaining(1_000L, 2_000L))
    }

    @Test
    fun resendLabel_formatsMinutesAndSeconds() {
        assertEquals("3:00", forgotPasswordResendLabel(180))
        assertEquals("2:05", forgotPasswordResendLabel(125))
        assertEquals("0:09", forgotPasswordResendLabel(9))
    }
}
