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
 * The current brand asset is a single transparent image, so this component deliberately animates
 * the mascot as a whole. Keeping the state API separate from the renderer means a future Rive
 * implementation can replace the renderer without changing feature screens.
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
    contentDescription: String = "TUKI mascot"
) {
    var entered by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) { entered = true }

    val infiniteTransition = rememberInfiniteTransition(label = "tuki_mascot_motion")

    val floatDistance = when (mood) {
        TukiMascotMood.ALERT -> 3f
        TukiMascotMood.CELEBRATE -> 7f
        else -> 5f
    }
    val tiltDistance = when (mood) {
        TukiMascotMood.THINKING -> 2.8f
        TukiMascotMood.ALERT -> 1.2f
        TukiMascotMood.CELEBRATE -> 3.5f
        else -> 1.8f
    }
    val cycleDuration = when (mood) {
        TukiMascotMood.ALERT -> 520
        TukiMascotMood.CELEBRATE -> 760
        TukiMascotMood.THINKING -> 1500
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
    val haloPulse by infiniteTransition.animateFloat(
        initialValue = 0.92f,
        targetValue = when (mood) {
            TukiMascotMood.ALERT -> 1.12f
            TukiMascotMood.CELEBRATE -> 1.08f
            else -> 1.02f
        },
        animationSpec = infiniteRepeatable(
            animation = tween(if (mood == TukiMascotMood.ALERT) 520 else 1200),
            repeatMode = RepeatMode.Reverse
        ),
        label = "tuki_halo"
    )

    val entryScale by animateFloatAsState(
        targetValue = if (entered) 1f else 0.72f,
        animationSpec = spring(
            dampingRatio = Spring.DampingRatioMediumBouncy,
            stiffness = Spring.StiffnessLow
        ),
        label = "tuki_entry_scale"
    )
    val entryAlpha by animateFloatAsState(
        targetValue = if (entered) 1f else 0f,
        animationSpec = tween(420),
        label = "tuki_entry_alpha"
    )

    val haloColor = when (mood) {
        TukiMascotMood.ALERT -> TukiOrange
        TukiMascotMood.CELEBRATE -> TukiGold
        TukiMascotMood.THINKING -> Color.White
        TukiMascotMood.WELCOME,
        TukiMascotMood.GUIDE -> TukiTeal
    }

    Box(
        modifier = modifier,
        contentAlignment = Alignment.Center
    ) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(8.dp)
                .graphicsLayer {
                    scaleX = haloPulse
                    scaleY = haloPulse
                    alpha = 0.16f * entryAlpha
                }
                .background(haloColor, CircleShape)
        )

        Image(
            painter = painterResource(R.drawable.tuki_logo),
            contentDescription = contentDescription,
            modifier = Modifier
                .fillMaxSize()
                .padding(10.dp)
                .graphicsLayer {
                    translationY = floatY * density
                    rotationZ = tilt
                    scaleX = entryScale
                    scaleY = entryScale
                    alpha = entryAlpha
                },
            contentScale = ContentScale.Fit
        )
    }
}
