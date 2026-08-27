package com.example.frontend.ui.motion

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import com.example.frontend.R
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiTeal

/**
 * Reusable motion states for the TUKI mascot.
 *
 * Each onboarding mood now renders a distinct approved toucan pose instead of reusing the same
 * static logo. The screen API stays stable so this can later be swapped for Rive state machines.
 */
enum class TukiMascotMood {
    WELCOME,
    GUIDE,
    ALERT,
    THINKING,
    CELEBRATE
}

@Composable
fun TukiMascot(
    mood: TukiMascotMood,
    modifier: Modifier = Modifier,
    contentDescription: String = "TUKI mascot",
    showHalo: Boolean = true
) {
    var entered by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) { entered = true }

    val infiniteTransition = rememberInfiniteTransition(label = "tuki_mascot_${mood.name}")

    val drawable = when (mood) {
        TukiMascotMood.WELCOME -> R.drawable.tuki_mascot_intro
        TukiMascotMood.GUIDE -> R.drawable.tuki_mascot_guide
        TukiMascotMood.ALERT -> R.drawable.tuki_mascot_alert
        TukiMascotMood.THINKING -> R.drawable.tuki_mascot_guide
        TukiMascotMood.CELEBRATE -> R.drawable.tuki_mascot_alert
    }

    val floatDistance = when (mood) {
        TukiMascotMood.ALERT -> 3f
        TukiMascotMood.CELEBRATE -> 8f
        TukiMascotMood.THINKING -> 3.5f
        else -> 5f
    }
    val tiltDistance = when (mood) {
        TukiMascotMood.THINKING -> 3.4f
        TukiMascotMood.ALERT -> 1.6f
        TukiMascotMood.CELEBRATE -> 4.2f
        TukiMascotMood.WELCOME -> 2.2f
        TukiMascotMood.GUIDE -> 1.8f
    }
    val cycleDuration = when (mood) {
        TukiMascotMood.ALERT -> 560
        TukiMascotMood.CELEBRATE -> 720
        TukiMascotMood.THINKING -> 1450
        else -> 1250
    }

    val floatY by infiniteTransition.animateFloat(
        initialValue = -floatDistance,
        targetValue = floatDistance,
        animationSpec = infiniteRepeatable(
            animation = tween(cycleDuration),
            repeatMode = RepeatMode.Reverse
        ),
        label = "tuki_float"
    )
    val tilt by infiniteTransition.animateFloat(
        initialValue = -tiltDistance,
        targetValue = tiltDistance,
        animationSpec = infiniteRepeatable(
            animation = tween(cycleDuration + 220),
            repeatMode = RepeatMode.Reverse
        ),
        label = "tuki_tilt"
    )
    val pulse by infiniteTransition.animateFloat(
        initialValue = 0.985f,
        targetValue = when (mood) {
            TukiMascotMood.CELEBRATE -> 1.08f
            TukiMascotMood.ALERT -> 1.035f
            else -> 1.015f
        },
        animationSpec = infiniteRepeatable(
            animation = tween(if (mood == TukiMascotMood.ALERT) 520 else 1100),
            repeatMode = RepeatMode.Reverse
        ),
        label = "tuki_pulse"
    )

    val entryScale by animateFloatAsState(
        targetValue = if (entered) 1f else 0.76f,
        animationSpec = spring(
            dampingRatio = Spring.DampingRatioMediumBouncy,
            stiffness = Spring.StiffnessLow
        ),
        label = "tuki_entry_scale"
    )
    val entryAlpha by animateFloatAsState(
        targetValue = if (entered) 1f else 0f,
        animationSpec = tween(360),
        label = "tuki_entry_alpha"
    )

    val haloColor = when (mood) {
        TukiMascotMood.ALERT -> TukiOrange
        TukiMascotMood.CELEBRATE -> TukiGold
        TukiMascotMood.THINKING -> TukiTeal
        TukiMascotMood.WELCOME,
        TukiMascotMood.GUIDE -> TukiTeal
    }

    Box(
        modifier = modifier,
        contentAlignment = Alignment.Center
    ) {
        if (showHalo) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(8.dp)
                    .graphicsLayer {
                        scaleX = pulse
                        scaleY = pulse
                        alpha = 0.14f * entryAlpha
                    }
                    .background(haloColor, CircleShape)
            )
        }

        Image(
            painter = painterResource(drawable),
            contentDescription = contentDescription,
            modifier = Modifier
                .fillMaxSize()
                .padding(if (showHalo) 8.dp else 0.dp)
                .graphicsLayer {
                    translationY = floatY * density
                    rotationZ = tilt
                    val direction = if (mood == TukiMascotMood.THINKING) -1f else 1f
                    scaleX = direction * entryScale * pulse
                    scaleY = entryScale * pulse
                    alpha = entryAlpha
                },
            contentScale = ContentScale.Fit
        )
    }
}
