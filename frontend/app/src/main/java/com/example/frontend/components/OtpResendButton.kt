package com.example.frontend.components

import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.text.font.FontWeight
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay

private const val OtpResendCooldownSeconds = 180

@Composable
fun OtpResendButton(
    sendGeneration: Int,
    enabled: Boolean = true,
    onResend: () -> Unit
) {
    var secondsRemaining by remember(sendGeneration) {
        mutableIntStateOf(if (sendGeneration > 0) OtpResendCooldownSeconds else 0)
    }

    LaunchedEffect(secondsRemaining, sendGeneration) {
        if (secondsRemaining > 0) {
            delay(1_000)
            secondsRemaining -= 1
        }
    }

    val minutes = secondsRemaining / 60
    val seconds = secondsRemaining % 60
    val label = if (secondsRemaining > 0) {
        "Resend in $minutes:${seconds.toString().padStart(2, '0')}"
    } else {
        "Resend OTP"
    }

    TextButton(
        onClick = onResend,
        enabled = enabled && secondsRemaining == 0
    ) {
        Text(
            text = label,
            color = if (enabled && secondsRemaining == 0) TukiTeal else TukiMuted,
            fontWeight = FontWeight.Bold
        )
    }
}
