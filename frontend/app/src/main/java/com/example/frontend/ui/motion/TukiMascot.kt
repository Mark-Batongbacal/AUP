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
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import com.example.frontend.R
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiTeal

enum class TukiMascotMood {
    WELCOME,
    GUIDE,
    ALERT,
    THINKING,
    CELEBRATE
}

/**
 * A lightweight mascot renderer whose public API is intentionally mood-based.
 * Feature screens choose the emotion they need; the renderer owns which approved TUKI pose best
 * communicates that emotion. This keeps screen code stable if the art or a future Rive rig changes.
 */
@Composable
fun TukiMascot(
    mood: TukiMascotMood,
    modifier: Modifier = Modifier,
    contentDescription: String = "TUKI mascot",
    showHalo: Boolean = true
) {
    var entered by remember(mood) { mutableStateOf(false) }
    LaunchedEffect(mood) { entered = true }

    val transition = rememberInfiniteTransition(label = "tuki_${mood.name}")
    val drawable = when (mood) {
        TukiMascotMood.WELCOME -> R.drawable.tuki_mascot_intro
        TukiMascotMood.GUIDE -> R.drawable.tuki_pose_hover_up
        TukiMascotMood.ALERT -> R.drawable.tuki_mascot_alert
        TukiMascotMood.THINKING -> R.drawable.tuki_mascot_guide
        TukiMascotMood.CELEBRATE -> R.drawable.tuki_pose_celebrate
    }

    val amplitude = when (mood) {
        TukiMascotMood.ALERT -> 3.5f
        TukiMascotMood.CELEBRATE -> 7f
        TukiMascotMood.GUIDE -> 5.5f
        else -> 4f
    }
    val duration = when (mood) {
        TukiMascotMood.ALERT -> 620
        TukiMascotMood.CELEBRATE -> 760
        TukiMascotMood.THINKING -> 1450
        else -> 1180
    }

    val floatY by transition.animateFloat(
        initialValue = -amplitude,
        targetValue = amplitude,
        animationSpec = infiniteRepeatable(tween(duration), RepeatMode.Reverse),
        label = "float"
    )
    val tilt by transition.animateFloat(
        initialValue = if (mood == TukiMascotMood.THINKING) -2.4f else -1.2f,
        targetValue = if (mood == TukiMascotMood.THINKING) 2.4f else 1.2f,
        animationSpec = infiniteRepeatable(tween(duration + 260), RepeatMode.Reverse),
        label = "tilt"
    )
    val breathe by transition.animateFloat(
        initialValue = 0.99f,
        targetValue = if (mood == TukiMascotMood.CELEBRATE) 1.055f else 1.018f,
        animationSpec = infiniteRepeatable(tween(duration + 140), RepeatMode.Reverse),
        label = "breathe"
    )

    val entryScale by animateFloatAsState(
        targetValue = if (entered) 1f else 0.82f,
        animationSpec = spring(
            dampingRatio = Spring.DampingRatioMediumBouncy,
            stiffness = Spring.StiffnessLow
        ),
        label = "entry_scale"
    )
    val entryAlpha by animateFloatAsState(
        targetValue = if (entered) 1f else 0f,
        animationSpec = tween(260),
        label = "entry_alpha"
    )

    val haloColor = when (mood) {
        TukiMascotMood.ALERT -> TukiOrange
        TukiMascotMood.CELEBRATE -> TukiGold
        else -> TukiTeal
    }

    Box(modifier = modifier, contentAlignment = Alignment.Center) {
        if (showHalo) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(8.dp)
                    .graphicsLayer {
                        scaleX = breathe
                        scaleY = breathe
                        alpha = 0.13f * entryAlpha
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
                    val horizontalDirection = if (mood == TukiMascotMood.THINKING) -1f else 1f
                    scaleX = horizontalDirection * entryScale * breathe
                    scaleY = entryScale * breathe
                    alpha = entryAlpha
                },
            contentScale = ContentScale.Fit
        )
    }
}
