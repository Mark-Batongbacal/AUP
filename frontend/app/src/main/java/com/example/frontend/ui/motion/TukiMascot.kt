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
 * Onboarding mascot renderer using the approved TUKI poses supplied by the product team.
 *
 * The previous generated/legacy poses were intentionally removed from onboarding because they
 * could look inconsistent with the approved mascot. The public API remains mood-based so feature
 * screens do not need to know which drawable is used for each emotion.
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

    // These two drawables were produced directly from the user-approved TUKI artwork.
    // The expressive first pose is used for greeting/alert/success moments, while the raised-wing
    // pose is used when TUKI is guiding or thinking.
    val drawable = when (mood) {
        TukiMascotMood.WELCOME -> R.drawable.tuki_pose_celebrate
        TukiMascotMood.GUIDE -> R.drawable.tuki_pose_hover_up
        TukiMascotMood.ALERT -> R.drawable.tuki_pose_celebrate
        TukiMascotMood.THINKING -> R.drawable.tuki_pose_hover_up
        TukiMascotMood.CELEBRATE -> R.drawable.tuki_pose_celebrate
    }

    val amplitude = when (mood) {
        TukiMascotMood.WELCOME -> 4.5f
        TukiMascotMood.GUIDE -> 5.5f
        TukiMascotMood.ALERT -> 7f
        TukiMascotMood.THINKING -> 3f
        TukiMascotMood.CELEBRATE -> 8f
    }
    val duration = when (mood) {
        TukiMascotMood.WELCOME -> 1180
        TukiMascotMood.GUIDE -> 1320
        TukiMascotMood.ALERT -> 560
        TukiMascotMood.THINKING -> 1550
        TukiMascotMood.CELEBRATE -> 720
    }
    val tiltAmount = when (mood) {
        TukiMascotMood.WELCOME -> 1.8f
        TukiMascotMood.GUIDE -> 2.4f
        TukiMascotMood.ALERT -> 3.2f
        TukiMascotMood.THINKING -> 2.8f
        TukiMascotMood.CELEBRATE -> 3.6f
    }

    val floatY by transition.animateFloat(
        initialValue = -amplitude,
        targetValue = amplitude,
        animationSpec = infiniteRepeatable(
            animation = tween(duration),
            repeatMode = RepeatMode.Reverse
        ),
        label = "float"
    )
    val tilt by transition.animateFloat(
        initialValue = -tiltAmount,
        targetValue = tiltAmount,
        animationSpec = infiniteRepeatable(
            animation = tween(duration + 260),
            repeatMode = RepeatMode.Reverse
        ),
        label = "tilt"
    )
    val breathe by transition.animateFloat(
        initialValue = 0.992f,
        targetValue = when (mood) {
            TukiMascotMood.ALERT -> 1.045f
            TukiMascotMood.CELEBRATE -> 1.06f
            else -> 1.018f
        },
        animationSpec = infiniteRepeatable(
            animation = tween(duration + 140),
            repeatMode = RepeatMode.Reverse
        ),
        label = "breathe"
    )

    val entryScale by animateFloatAsState(
        targetValue = if (entered) 1f else 0.80f,
        animationSpec = spring(
            dampingRatio = if (mood == TukiMascotMood.ALERT) 0.55f else Spring.DampingRatioMediumBouncy,
            stiffness = if (mood == TukiMascotMood.ALERT) 330f else Spring.StiffnessLow
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
                        alpha = 0.12f * entryAlpha
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
                    scaleX = entryScale * breathe
                    scaleY = entryScale * breathe
                    alpha = entryAlpha
                },
            contentScale = ContentScale.Fit
        )
    }
}
