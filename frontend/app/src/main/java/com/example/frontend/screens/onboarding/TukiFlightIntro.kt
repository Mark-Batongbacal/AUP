package com.example.frontend.screens.onboarding

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectTapGestures
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
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.frontend.ui.motion.TukiMascot
import com.example.frontend.ui.motion.TukiMascotMood
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay
import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.sin

/**
 * Cinematic lead-in to onboarding.
 *
 * The intro deliberately uses a clean map-inspired scene rather than decorative transport props.
 * TUKI swoops, banks, flaps and settles into the same visual area used by onboarding page one.
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
        animationSpec = tween(430),
        label = "tuki_intro_scene_alpha"
    )
    val sceneScale by animateFloatAsState(
        targetValue = if (handingOff) 1.018f else 1f,
        animationSpec = tween(430),
        label = "tuki_intro_scene_scale"
    )

    fun beginHandoff() {
        if (handingOff || finished) return
        handingOff = true
        onHandoffStarted()
    }

    LaunchedEffect(Unit) {
        delay(120)
        flightProgress.animateTo(
            targetValue = 1f,
            animationSpec = tween(
                durationMillis = 2350,
                easing = FastOutSlowInEasing
            )
        )
        beginHandoff()
    }

    LaunchedEffect(handingOff) {
        if (handingOff && !finished) {
            delay(470)
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
                    if (!finished) beginHandoff()
                }
            }
            .graphicsLayer {
                alpha = sceneAlpha
                scaleX = sceneScale
                scaleY = sceneScale
            }
    ) {
        val progress = flightProgress.value.coerceIn(0f, 1f)
        val flightPath = FlightPath(
            p0 = NormalizedPoint(-0.20f, 0.68f),
            p1 = NormalizedPoint(0.06f, 0.30f),
            p2 = NormalizedPoint(1.03f, 0.50f),
            p3 = NormalizedPoint(0.30f, 0.235f)
        )
        val point = cubicPoint(progress, flightPath)
        val tangent = cubicDerivative(progress, flightPath)

        // Faster wing beats during take-off and landing, slower gliding through the middle.
        val flapFrequency = when {
            progress < 0.22f -> 18f
            progress > 0.80f -> 20f
            else -> 11f
        }
        val flap = sin(progress * PI.toFloat() * flapFrequency)
        val flapStrength = abs(flap)
        val lift = sin(progress * PI.toFloat() * 7.5f) * 8f
        val heading = Math.toDegrees(atan2(tangent.y.toDouble(), tangent.x.toDouble())).toFloat()
        val bank = (heading * 0.22f).coerceIn(-14f, 14f)
        val landingFactor = ((progress - 0.82f) / 0.18f).coerceIn(0f, 1f)
        val takeoffFactor = (progress / 0.14f).coerceIn(0f, 1f)
        val flightScale = 0.88f + (0.12f * takeoffFactor) + (0.045f * landingFactor)

        Canvas(modifier = Modifier.fillMaxSize()) {
            drawCleanMapScene(progress)
            drawFlightTrail(
                progress = progress,
                path = flightPath,
                current = point
            )
        }

        Text(
            text = "Your trip starts with a better route.",
            modifier = Modifier
                .align(Alignment.TopCenter)
                .statusBarsPadding()
                .padding(top = 76.dp)
                .graphicsLayer {
                    alpha = ((progress - 0.06f) / 0.18f).coerceIn(0f, 1f) *
                        ((0.82f - progress) / 0.18f).coerceIn(0f, 1f)
                },
            color = TukiInk,
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.SemiBold
        )

        TukiMascot(
            mood = if (progress > 0.86f) TukiMascotMood.WELCOME else TukiMascotMood.GUIDE,
            modifier = Modifier
                .offset(
                    x = (maxWidth * point.x) - 80.dp,
                    y = (maxHeight * point.y) - 80.dp
                )
                .size(160.dp)
                .graphicsLayer {
                    // Squash/stretch + body bob makes the flattened mascot read as flapping rather than sliding.
                    translationY = (lift - (flapStrength * 5f)) * density
                    rotationZ = bank + (flap * 3.4f * (1f - landingFactor))
                    scaleX = flightScale * (1f + (flapStrength * 0.035f))
                    scaleY = flightScale * (1f - (flapStrength * 0.065f))
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
                    alpha = ((progress - 0.26f) / 0.20f).coerceIn(0f, 0.62f)
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

private data class FlightPath(
    val p0: NormalizedPoint,
    val p1: NormalizedPoint,
    val p2: NormalizedPoint,
    val p3: NormalizedPoint
)

private fun cubicPoint(t: Float, path: FlightPath): NormalizedPoint {
    val u = 1f - t
    val tt = t * t
    val uu = u * u
    val uuu = uu * u
    val ttt = tt * t

    return NormalizedPoint(
        x = (uuu * path.p0.x) + (3f * uu * t * path.p1.x) +
            (3f * u * tt * path.p2.x) + (ttt * path.p3.x),
        y = (uuu * path.p0.y) + (3f * uu * t * path.p1.y) +
            (3f * u * tt * path.p2.y) + (ttt * path.p3.y)
    )
}

private fun cubicDerivative(t: Float, path: FlightPath): NormalizedPoint {
    val u = 1f - t
    return NormalizedPoint(
        x = (3f * u * u * (path.p1.x - path.p0.x)) +
            (6f * u * t * (path.p2.x - path.p1.x)) +
            (3f * t * t * (path.p3.x - path.p2.x)),
        y = (3f * u * u * (path.p1.y - path.p0.y)) +
            (6f * u * t * (path.p2.y - path.p1.y)) +
            (3f * t * t * (path.p3.y - path.p2.y))
    )
}

private fun DrawScope.drawCleanMapScene(progress: Float) {
    // Soft brand shapes keep the page visually related to the rest of onboarding.
    drawCircle(
        color = TukiTeal.copy(alpha = 0.035f),
        radius = size.minDimension * 0.30f,
        center = Offset(-size.width * 0.04f, size.height * 0.36f)
    )
    drawCircle(
        color = TukiOrange.copy(alpha = 0.035f),
        radius = size.minDimension * 0.24f,
        center = Offset(size.width * 1.04f, size.height * 0.64f)
    )

    // Faint roads: intentionally abstract so the scene suggests navigation without looking like random props.
    val road1 = Path().apply {
        moveTo(-size.width * 0.10f, size.height * 0.82f)
        cubicTo(
            size.width * 0.18f, size.height * 0.70f,
            size.width * 0.20f, size.height * 0.38f,
            size.width * 0.52f, size.height * 0.32f
        )
        cubicTo(
            size.width * 0.76f, size.height * 0.27f,
            size.width * 0.78f, size.height * 0.15f,
            size.width * 1.08f, size.height * 0.10f
        )
    }
    val road2 = Path().apply {
        moveTo(size.width * 0.03f, size.height * 0.20f)
        cubicTo(
            size.width * 0.28f, size.height * 0.30f,
            size.width * 0.48f, size.height * 0.58f,
            size.width * 0.92f, size.height * 0.54f
        )
    }
    drawPath(
        path = road1,
        color = TukiInk.copy(alpha = 0.035f),
        style = Stroke(width = 18.dp.toPx(), cap = StrokeCap.Round)
    )
    drawPath(
        path = road2,
        color = TukiInk.copy(alpha = 0.026f),
        style = Stroke(width = 12.dp.toPx(), cap = StrokeCap.Round)
    )

    // One meaningful trip route with an origin and destination — no decorative cars or unrelated signs.
    val routeStart = Offset(size.width * 0.16f, size.height * 0.72f)
    val routeMiddle = Offset(size.width * 0.66f, size.height * 0.54f)
    val routeEnd = Offset(size.width * 0.31f, size.height * 0.235f)
    val route = Path().apply {
        moveTo(routeStart.x, routeStart.y)
        cubicTo(
            size.width * 0.32f, size.height * 0.66f,
            size.width * 0.55f, size.height * 0.67f,
            routeMiddle.x, routeMiddle.y
        )
        cubicTo(
            size.width * 0.77f, size.height * 0.44f,
            size.width * 0.49f, size.height * 0.33f,
            routeEnd.x, routeEnd.y
        )
    }
    drawPath(
        path = route,
        color = TukiTeal.copy(alpha = 0.12f),
        style = Stroke(width = 5.dp.toPx(), cap = StrokeCap.Round)
    )

    val routeReveal = ((progress - 0.12f) / 0.70f).coerceIn(0f, 1f)
    if (routeReveal > 0f) {
        val steps = 46
        for (index in 0 until steps) {
            val startT = index / steps.toFloat()
            if (startT > routeReveal) break
            val endT = ((index + 1) / steps.toFloat()).coerceAtMost(routeReveal)
            val start = routePoint(startT, routeStart, routeMiddle, routeEnd)
            val end = routePoint(endT, routeStart, routeMiddle, routeEnd)
            drawLine(
                color = TukiTeal.copy(alpha = 0.72f),
                start = start,
                end = end,
                strokeWidth = 4.dp.toPx(),
                cap = StrokeCap.Round
            )
        }
    }

    drawOriginDot(routeStart, active = progress > 0.12f)
    drawDestinationPin(routeEnd, active = progress > 0.78f)
}

private fun routePoint(
    t: Float,
    start: Offset,
    middle: Offset,
    end: Offset
): Offset {
    return if (t <= 0.5f) {
        val local = t / 0.5f
        val control1 = Offset(start.x + 90f, start.y - 10f)
        val control2 = Offset(middle.x - 70f, middle.y + 80f)
        cubicOffset(local, start, control1, control2, middle)
    } else {
        val local = (t - 0.5f) / 0.5f
        val control1 = Offset(middle.x + 80f, middle.y - 70f)
        val control2 = Offset(end.x + 90f, end.y + 70f)
        cubicOffset(local, middle, control1, control2, end)
    }
}

private fun cubicOffset(t: Float, p0: Offset, p1: Offset, p2: Offset, p3: Offset): Offset {
    val u = 1f - t
    val tt = t * t
    val uu = u * u
    return Offset(
        x = (uu * u * p0.x) + (3f * uu * t * p1.x) + (3f * u * tt * p2.x) + (tt * t * p3.x),
        y = (uu * u * p0.y) + (3f * uu * t * p1.y) + (3f * u * tt * p2.y) + (tt * t * p3.y)
    )
}

private fun DrawScope.drawFlightTrail(
    progress: Float,
    path: FlightPath,
    current: NormalizedPoint
) {
    if (progress < 0.04f || progress > 0.92f) return

    val currentPx = Offset(current.x * size.width, current.y * size.height)
    val trailOffsets = listOf(0.035f, 0.065f, 0.095f)
    trailOffsets.forEachIndexed { index, offset ->
        val t = (progress - offset).coerceAtLeast(0f)
        val previous = cubicPoint(t, path)
        val previousPx = Offset(previous.x * size.width, previous.y * size.height)
        drawLine(
            color = TukiOrange.copy(alpha = 0.26f - (index * 0.06f)),
            start = previousPx,
            end = Offset(
                x = previousPx.x + ((currentPx.x - previousPx.x) * 0.55f),
                y = previousPx.y + ((currentPx.y - previousPx.y) * 0.55f)
            ),
            strokeWidth = (5f - index).dp.toPx(),
            cap = StrokeCap.Round
        )
    }

    // Air streaks around TUKI sell actual flight without adding unrelated scenery.
    repeat(3) { index ->
        val phase = progress * 13f + index
        val dx = cos(phase * PI.toFloat()) * 14.dp.toPx()
        val dy = sin(phase * PI.toFloat()) * 8.dp.toPx()
        drawLine(
            color = TukiTeal.copy(alpha = 0.13f),
            start = Offset(currentPx.x - 70.dp.toPx() + dx, currentPx.y + dy),
            end = Offset(currentPx.x - 42.dp.toPx() + dx, currentPx.y + dy),
            strokeWidth = 2.5.dp.toPx(),
            cap = StrokeCap.Round
        )
    }
}

private fun DrawScope.drawOriginDot(center: Offset, active: Boolean) {
    drawCircle(
        color = TukiTeal.copy(alpha = if (active) 0.16f else 0.07f),
        radius = 18.dp.toPx(),
        center = center
    )
    drawCircle(
        color = TukiTeal.copy(alpha = if (active) 1f else 0.35f),
        radius = 7.dp.toPx(),
        center = center
    )
}

private fun DrawScope.drawDestinationPin(center: Offset, active: Boolean) {
    val alpha = if (active) 1f else 0.22f
    drawCircle(
        color = TukiOrange.copy(alpha = 0.14f * alpha),
        radius = 27.dp.toPx(),
        center = center
    )
    drawCircle(
        color = TukiOrange.copy(alpha = alpha),
        radius = 13.dp.toPx(),
        center = center
    )
    drawCircle(
        color = Color.White.copy(alpha = alpha),
        radius = 5.dp.toPx(),
        center = center
    )
    drawLine(
        color = TukiOrange.copy(alpha = alpha),
        start = Offset(center.x, center.y + 11.dp.toPx()),
        end = Offset(center.x, center.y + 30.dp.toPx()),
        strokeWidth = 5.dp.toPx(),
        cap = StrokeCap.Round
    )
}
