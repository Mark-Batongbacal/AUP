package com.example.frontend.screens.onboarding

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.frontend.ui.motion.TukiMascot
import com.example.frontend.ui.motion.TukiMascotMood
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay
import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.sin

/**
 * Cinematic lead-in to onboarding.
 *
 * TUKI flies through a miniature transport scene, follows a drawn route, and finishes close to the
 * hero position used by the first onboarding page. It is deliberately not counted as an onboarding
 * page: there is no progress segment and a tap anywhere immediately hands off to page one.
 */
@Composable
fun TukiFlightIntro(
    onHandoffStarted: () -> Unit,
    onFinished: () -> Unit
) {
    val flightProgress = remember { Animatable(0f) }
    var handingOff by remember { mutableStateOf(false) }
    var finished by remember { mutableStateOf(false) }

    val sceneAlpha by animateFloatAsState(
        targetValue = if (handingOff) 0f else 1f,
        animationSpec = tween(420),
        label = "tuki_intro_scene_alpha"
    )
    val sceneryScale by animateFloatAsState(
        targetValue = if (handingOff) 1.035f else 1f,
        animationSpec = tween(420),
        label = "tuki_intro_scene_scale"
    )

    fun beginHandoff() {
        if (handingOff || finished) return
        handingOff = true
        onHandoffStarted()
    }

    LaunchedEffect(Unit) {
        delay(180)
        flightProgress.animateTo(
            targetValue = 1f,
            animationSpec = tween(
                durationMillis = 2550,
                easing = FastOutSlowInEasing
            )
        )
        beginHandoff()
        delay(440)
        finished = true
        onFinished()
    }

    LaunchedEffect(handingOff) {
        if (handingOff && !finished) {
            delay(460)
            if (!finished) {
                finished = true
                onFinished()
            }
        }
    }

    BoxWithConstraints(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .pointerInput(Unit) {
                detectTapGestures {
                    if (!finished) {
                        beginHandoff()
                    }
                }
            }
            .graphicsLayer {
                alpha = sceneAlpha
                scaleX = sceneryScale
                scaleY = sceneryScale
            }
    ) {
        val progress = flightProgress.value.coerceIn(0f, 1f)
        val point = cubicPoint(
            t = progress,
            p0 = NormalizedPoint(-0.16f, 0.72f),
            p1 = NormalizedPoint(0.20f, 0.48f),
            p2 = NormalizedPoint(0.92f, 0.42f),
            p3 = NormalizedPoint(0.30f, 0.235f)
        )
        val tangent = cubicDerivative(
            t = progress,
            p0 = NormalizedPoint(-0.16f, 0.72f),
            p1 = NormalizedPoint(0.20f, 0.48f),
            p2 = NormalizedPoint(0.92f, 0.42f),
            p3 = NormalizedPoint(0.30f, 0.235f)
        )
        val bank = (tangent.y * 18f).coerceIn(-10f, 10f)
        val wingBob = sin(progress * PI.toFloat() * 8f) * 5f
        val mascotScale = when {
            progress < 0.12f -> 0.88f + (progress / 0.12f) * 0.12f
            progress > 0.90f -> 1f + ((progress - 0.90f) / 0.10f) * 0.06f
            else -> 1f
        }

        Canvas(
            modifier = Modifier
                .fillMaxSize()
                .graphicsLayer { alpha = sceneAlpha }
        ) {
            drawIntroScenery(progress)
        }

        Text(
            text = "Finding a smarter way…",
            modifier = Modifier
                .align(Alignment.TopCenter)
                .statusBarsPadding()
                .padding(top = 72.dp)
                .graphicsLayer {
                    alpha = ((progress - 0.08f) / 0.20f).coerceIn(0f, 1f) *
                        ((0.84f - progress) / 0.18f).coerceIn(0f, 1f)
                },
            color = TukiInk,
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.SemiBold
        )

        TukiMascot(
            mood = if (progress > 0.84f) TukiMascotMood.WELCOME else TukiMascotMood.GUIDE,
            modifier = Modifier
                .offset(
                    x = (maxWidth * point.x) - 76.dp,
                    y = (maxHeight * point.y) - 76.dp
                )
                .size(152.dp)
                .graphicsLayer {
                    rotationZ = bank + wingBob
                    scaleX = mascotScale
                    scaleY = mascotScale
                },
            showHalo = false,
            contentDescription = "TUKI flying into onboarding"
        )

        Text(
            text = "Tap to continue",
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .navigationBarsPadding()
                .padding(bottom = 28.dp)
                .graphicsLayer {
                    alpha = ((progress - 0.24f) / 0.24f).coerceIn(0f, 0.70f)
                },
            color = TukiMuted,
            style = MaterialTheme.typography.bodySmall
        )
    }
}

private data class NormalizedPoint(
    val x: Float,
    val y: Float
)

private fun cubicPoint(
    t: Float,
    p0: NormalizedPoint,
    p1: NormalizedPoint,
    p2: NormalizedPoint,
    p3: NormalizedPoint
): NormalizedPoint {
    val u = 1f - t
    val tt = t * t
    val uu = u * u
    val uuu = uu * u
    val ttt = tt * t

    return NormalizedPoint(
        x = (uuu * p0.x) + (3f * uu * t * p1.x) + (3f * u * tt * p2.x) + (ttt * p3.x),
        y = (uuu * p0.y) + (3f * uu * t * p1.y) + (3f * u * tt * p2.y) + (ttt * p3.y)
    )
}

private fun cubicDerivative(
    t: Float,
    p0: NormalizedPoint,
    p1: NormalizedPoint,
    p2: NormalizedPoint,
    p3: NormalizedPoint
): NormalizedPoint {
    val u = 1f - t
    return NormalizedPoint(
        x = (3f * u * u * (p1.x - p0.x)) +
            (6f * u * t * (p2.x - p1.x)) +
            (3f * t * t * (p3.x - p2.x)),
        y = (3f * u * u * (p1.y - p0.y)) +
            (6f * u * t * (p2.y - p1.y)) +
            (3f * t * t * (p3.y - p2.y))
    )
}

private fun DrawScope.drawIntroScenery(progress: Float) {
    drawCircle(
        color = TukiTeal.copy(alpha = 0.045f),
        radius = size.minDimension * 0.28f,
        center = Offset(-size.width * 0.02f, size.height * 0.32f)
    )
    drawCircle(
        color = TukiGold.copy(alpha = 0.055f),
        radius = size.minDimension * 0.22f,
        center = Offset(size.width * 1.02f, size.height * 0.56f)
    )

    val p0 = NormalizedPoint(-0.16f, 0.72f)
    val p1 = NormalizedPoint(0.20f, 0.48f)
    val p2 = NormalizedPoint(0.92f, 0.42f)
    val p3 = NormalizedPoint(0.30f, 0.235f)
    val steps = 54

    for (index in 0 until steps) {
        val startT = index / steps.toFloat()
        val endT = (index + 1) / steps.toFloat()
        val start = cubicPoint(startT, p0, p1, p2, p3)
        val end = cubicPoint(endT, p0, p1, p2, p3)
        val revealed = progress >= startT

        drawLine(
            color = if (revealed) TukiTeal.copy(alpha = 0.64f) else TukiTeal.copy(alpha = 0.08f),
            start = Offset(start.x * size.width, start.y * size.height),
            end = Offset(end.x * size.width, end.y * size.height),
            strokeWidth = if (revealed) 4.dp.toPx() else 3.dp.toPx(),
            cap = StrokeCap.Round
        )
    }

    drawMiniHome(Offset(size.width * 0.11f, size.height * 0.67f), progress > 0.08f)
    drawMiniTerminal(Offset(size.width * 0.58f, size.height * 0.49f), progress > 0.40f)
    drawMiniStop(Offset(size.width * 0.82f, size.height * 0.37f), progress > 0.62f)
    drawMiniDestination(Offset(size.width * 0.32f, size.height * 0.23f), progress > 0.87f)
}

private fun DrawScope.drawMiniHome(center: Offset, active: Boolean) {
    val alpha = if (active) 0.95f else 0.20f
    drawRoundRect(
        color = Color.White.copy(alpha = alpha),
        topLeft = Offset(center.x - 26.dp.toPx(), center.y - 14.dp.toPx()),
        size = Size(52.dp.toPx(), 38.dp.toPx()),
        cornerRadius = CornerRadius(8.dp.toPx())
    )
    drawLine(
        color = TukiOrange.copy(alpha = alpha),
        start = Offset(center.x - 30.dp.toPx(), center.y - 12.dp.toPx()),
        end = Offset(center.x, center.y - 34.dp.toPx()),
        strokeWidth = 6.dp.toPx(),
        cap = StrokeCap.Round
    )
    drawLine(
        color = TukiOrange.copy(alpha = alpha),
        start = Offset(center.x, center.y - 34.dp.toPx()),
        end = Offset(center.x + 30.dp.toPx(), center.y - 12.dp.toPx()),
        strokeWidth = 6.dp.toPx(),
        cap = StrokeCap.Round
    )
}

private fun DrawScope.drawMiniTerminal(center: Offset, active: Boolean) {
    val alpha = if (active) 0.95f else 0.20f
    drawRoundRect(
        color = TukiTeal.copy(alpha = alpha),
        topLeft = Offset(center.x - 34.dp.toPx(), center.y - 17.dp.toPx()),
        size = Size(68.dp.toPx(), 34.dp.toPx()),
        cornerRadius = CornerRadius(10.dp.toPx())
    )
    drawRoundRect(
        color = TukiGold.copy(alpha = alpha),
        topLeft = Offset(center.x + 2.dp.toPx(), center.y - 30.dp.toPx()),
        size = Size(30.dp.toPx(), 18.dp.toPx()),
        cornerRadius = CornerRadius(5.dp.toPx())
    )
    drawCircle(TukiDeepTeal.copy(alpha = alpha), 8.dp.toPx(), Offset(center.x - 20.dp.toPx(), center.y + 18.dp.toPx()))
    drawCircle(TukiDeepTeal.copy(alpha = alpha), 8.dp.toPx(), Offset(center.x + 22.dp.toPx(), center.y + 18.dp.toPx()))
}

private fun DrawScope.drawMiniStop(center: Offset, active: Boolean) {
    val alpha = if (active) 0.95f else 0.20f
    drawLine(
        color = TukiDeepTeal.copy(alpha = alpha),
        start = Offset(center.x, center.y - 27.dp.toPx()),
        end = Offset(center.x, center.y + 28.dp.toPx()),
        strokeWidth = 5.dp.toPx(),
        cap = StrokeCap.Round
    )
    drawCircle(
        color = TukiOrange.copy(alpha = alpha),
        radius = 14.dp.toPx(),
        center = Offset(center.x, center.y - 28.dp.toPx())
    )
    drawCircle(
        color = Color.White.copy(alpha = alpha),
        radius = 5.dp.toPx(),
        center = Offset(center.x, center.y - 28.dp.toPx())
    )
}

private fun DrawScope.drawMiniDestination(center: Offset, active: Boolean) {
    val alpha = if (active) 1f else 0.16f
    drawCircle(
        color = TukiOrange.copy(alpha = 0.12f * alpha),
        radius = 30.dp.toPx(),
        center = center
    )
    drawCircle(
        color = TukiOrange.copy(alpha = alpha),
        radius = 14.dp.toPx(),
        center = center
    )
    drawCircle(
        color = Color.White.copy(alpha = alpha),
        radius = 5.dp.toPx(),
        center = center
    )
}
