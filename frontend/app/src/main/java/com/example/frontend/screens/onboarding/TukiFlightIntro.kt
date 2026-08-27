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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
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
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.sin

private const val INTRO_DURATION_MS = 4200

/**
 * A short branded story before onboarding page one.
 *
 * TUKI takes off, actively flaps through an abstract city-map scene, glides over a highlighted
 * commute, then eases into the same upper-left hero area used by page one. The route is scenery,
 * not a rail: TUKI flies freely across it so the character feels alive instead of dragged along a
 * line. Tap anywhere to skip without affecting the real onboarding page count.
 */
@Composable
fun TukiFlightIntro(
    onHandoffStarted: () -> Unit,
    onFinished: () -> Unit
) {
    val progressAnim = remember { Animatable(0f) }
    var handingOff by remember { mutableStateOf(false) }
    var finished by remember { mutableStateOf(false) }

    val sceneAlpha by animateFloatAsState(
        targetValue = if (handingOff) 0f else 1f,
        animationSpec = tween(520),
        label = "flight_scene_alpha"
    )
    val sceneScale by animateFloatAsState(
        targetValue = if (handingOff) 1.025f else 1f,
        animationSpec = tween(520),
        label = "flight_scene_scale"
    )

    fun startHandoff() {
        if (handingOff || finished) return
        handingOff = true
        onHandoffStarted()
    }

    LaunchedEffect(Unit) {
        delay(160)
        progressAnim.animateTo(
            targetValue = 1f,
            animationSpec = tween(INTRO_DURATION_MS, easing = FastOutSlowInEasing)
        )
        startHandoff()
    }

    LaunchedEffect(handingOff) {
        if (handingOff && !finished) {
            delay(540)
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
                detectTapGestures { if (!finished) startHandoff() }
            }
            .graphicsLayer {
                alpha = sceneAlpha
                scaleX = sceneScale
                scaleY = sceneScale
            }
    ) {
        val progress = progressAnim.value.coerceIn(0f, 1f)
        val flightPath = FlightPath(
            start = Point(-0.24f, 0.70f),
            control1 = Point(0.03f, 0.28f),
            control2 = Point(0.98f, 0.48f),
            end = Point(0.29f, 0.225f)
        )
        val position = cubicPoint(progress, flightPath)
        val tangent = cubicDerivative(progress, flightPath)
        val heading = Math.toDegrees(atan2(tangent.y.toDouble(), tangent.x.toDouble())).toFloat()

        val takeoff = (progress / 0.18f).coerceIn(0f, 1f)
        val landing = ((progress - 0.82f) / 0.18f).coerceIn(0f, 1f)
        val flap = sin(progress * PI.toFloat() * if (progress < 0.24f || progress > 0.76f) 24f else 14f)
        val bodyBob = sin(progress * PI.toFloat() * 12f) * (1f - landing) * 7f
        val bank = (heading * 0.20f).coerceIn(-13f, 13f) * (1f - landing)
        val mascotScale = 0.86f + (0.12f * takeoff) + (0.08f * landing)
        val mascotMood = when {
            progress < 0.19f -> TukiMascotMood.CELEBRATE
            progress < 0.69f -> TukiMascotMood.GUIDE
            progress < 0.86f -> TukiMascotMood.CELEBRATE
            else -> TukiMascotMood.WELCOME
        }

        Canvas(modifier = Modifier.fillMaxSize()) {
            drawAmbientScene(progress)
            drawCommuteStory(progress)
            drawWindStreaks(progress, position)
            drawFlightShadow(progress, position)
        }

        Text(
            text = when {
                progress < 0.34f -> "Every trip starts somewhere."
                progress < 0.72f -> "TUKI finds a smarter way through."
                else -> "Ready? Let’s find your ride."
            },
            modifier = Modifier
                .align(Alignment.TopCenter)
                .statusBarsPadding()
                .padding(top = 68.dp, start = 36.dp, end = 36.dp)
                .graphicsLayer {
                    alpha = ((progress - 0.05f) / 0.13f).coerceIn(0f, 1f) *
                        ((0.93f - progress) / 0.08f).coerceIn(0f, 1f)
                },
            color = TukiInk,
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.SemiBold,
            textAlign = TextAlign.Center
        )

        TukiMascot(
            mood = mascotMood,
            modifier = Modifier
                .offset(
                    x = (maxWidth * position.x) - 88.dp,
                    y = (maxHeight * position.y) - 88.dp
                )
                .size(176.dp)
                .graphicsLayer {
                    translationY = bodyBob * density
                    rotationZ = bank + (flap * 2.0f * (1f - landing))
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
                    alpha = ((progress - 0.22f) / 0.18f).coerceIn(0f, 0.56f)
                },
            color = TukiMuted,
            style = MaterialTheme.typography.bodySmall
        )
    }
}

private data class Point(val x: Float, val y: Float)
private data class FlightPath(
    val start: Point,
    val control1: Point,
    val control2: Point,
    val end: Point
)

private fun cubicPoint(t: Float, path: FlightPath): Point {
    val u = 1f - t
    val uu = u * u
    val tt = t * t
    return Point(
        x = (uu * u * path.start.x) + (3f * uu * t * path.control1.x) +
            (3f * u * tt * path.control2.x) + (tt * t * path.end.x),
        y = (uu * u * path.start.y) + (3f * uu * t * path.control1.y) +
            (3f * u * tt * path.control2.y) + (tt * t * path.end.y)
    )
}

private fun cubicDerivative(t: Float, path: FlightPath): Point {
    val u = 1f - t
    return Point(
        x = (3f * u * u * (path.control1.x - path.start.x)) +
            (6f * u * t * (path.control2.x - path.control1.x)) +
            (3f * t * t * (path.end.x - path.control2.x)),
        y = (3f * u * u * (path.control1.y - path.start.y)) +
            (6f * u * t * (path.control2.y - path.control1.y)) +
            (3f * t * t * (path.end.y - path.control2.y))
    )
}

private fun DrawScope.drawAmbientScene(progress: Float) {
    // Brand-colored parallax atmosphere; intentionally soft so TUKI stays the focal point.
    val drift = sin(progress * PI.toFloat()) * 28.dp.toPx()
    drawCircle(
        color = TukiTeal.copy(alpha = 0.045f),
        radius = size.minDimension * 0.34f,
        center = Offset(-size.width * 0.08f + drift, size.height * 0.34f)
    )
    drawCircle(
        color = TukiGold.copy(alpha = 0.055f),
        radius = size.minDimension * 0.27f,
        center = Offset(size.width * 1.08f - drift, size.height * 0.64f)
    )

    // Faint map contours create place and depth without literal random cars/signage.
    repeat(3) { index ->
        val y = size.height * (0.38f + index * 0.15f)
        val contour = Path().apply {
            moveTo(-30.dp.toPx(), y)
            cubicTo(
                size.width * 0.22f, y - 70.dp.toPx(),
                size.width * 0.58f, y + 65.dp.toPx(),
                size.width + 30.dp.toPx(), y - 18.dp.toPx()
            )
        }
        drawPath(
            path = contour,
            color = TukiDeepTeal.copy(alpha = 0.032f),
            style = Stroke(width = (8 - index).dp.toPx(), cap = StrokeCap.Round)
        )
    }

    // Tiny ambient dots behave like map POIs and fade in sequentially.
    val dots = listOf(
        Offset(size.width * 0.18f, size.height * 0.57f),
        Offset(size.width * 0.47f, size.height * 0.49f),
        Offset(size.width * 0.76f, size.height * 0.42f)
    )
    dots.forEachIndexed { index, point ->
        val local = ((progress - (0.16f + index * 0.10f)) / 0.12f).coerceIn(0f, 1f)
        drawCircle(TukiTeal.copy(alpha = 0.14f * local), 12.dp.toPx() * local, point)
        drawCircle(TukiTeal.copy(alpha = 0.60f * local), 4.dp.toPx(), point)
    }
}

private fun DrawScope.drawCommuteStory(progress: Float) {
    val origin = Offset(size.width * 0.13f, size.height * 0.72f)
    val transfer = Offset(size.width * 0.67f, size.height * 0.56f)
    val destination = Offset(size.width * 0.31f, size.height * 0.24f)

    val route = Path().apply {
        moveTo(origin.x, origin.y)
        cubicTo(
            size.width * 0.31f, size.height * 0.68f,
            size.width * 0.56f, size.height * 0.67f,
            transfer.x, transfer.y
        )
        cubicTo(
            size.width * 0.80f, size.height * 0.43f,
            size.width * 0.54f, size.height * 0.33f,
            destination.x, destination.y
        )
    }
    drawPath(
        path = route,
        color = TukiTeal.copy(alpha = 0.10f),
        style = Stroke(width = 5.dp.toPx(), cap = StrokeCap.Round)
    )

    val reveal = ((progress - 0.14f) / 0.64f).coerceIn(0f, 1f)
    val points = sampleRoute(origin, transfer, destination, 64)
    val visibleCount = (points.size * reveal).toInt().coerceIn(1, points.size)
    for (index in 0 until visibleCount - 1) {
        drawLine(
            color = TukiTeal.copy(alpha = 0.76f),
            start = points[index],
            end = points[index + 1],
            strokeWidth = 4.dp.toPx(),
            cap = StrokeCap.Round
        )
    }

    drawCircle(TukiTeal.copy(alpha = 0.16f), 17.dp.toPx(), origin)
    drawCircle(TukiTeal, 6.dp.toPx(), origin)

    val transferAlpha = ((progress - 0.38f) / 0.14f).coerceIn(0f, 1f)
    drawCircle(TukiGold.copy(alpha = 0.16f * transferAlpha), 18.dp.toPx(), transfer)
    drawCircle(TukiGold.copy(alpha = transferAlpha), 6.dp.toPx(), transfer)

    val destinationAlpha = ((progress - 0.72f) / 0.16f).coerceIn(0f, 1f)
    val pulse = 1f + (0.18f * sin(progress * PI.toFloat() * 12f))
    drawCircle(
        TukiOrange.copy(alpha = 0.14f * destinationAlpha),
        26.dp.toPx() * pulse,
        destination
    )
    drawCircle(TukiOrange.copy(alpha = destinationAlpha), 10.dp.toPx(), destination)
    drawCircle(Color.White.copy(alpha = destinationAlpha), 4.dp.toPx(), destination)
}

private fun sampleRoute(
    start: Offset,
    transfer: Offset,
    destination: Offset,
    count: Int
): List<Offset> = List(count) { index ->
    val t = index / (count - 1).toFloat()
    if (t <= 0.5f) {
        val local = t * 2f
        cubicOffset(
            local,
            start,
            Offset(start.x + 80f, start.y - 25f),
            Offset(transfer.x - 60f, transfer.y + 70f),
            transfer
        )
    } else {
        val local = (t - 0.5f) * 2f
        cubicOffset(
            local,
            transfer,
            Offset(transfer.x + 80f, transfer.y - 90f),
            Offset(destination.x + 100f, destination.y + 80f),
            destination
        )
    }
}

private fun cubicOffset(t: Float, p0: Offset, p1: Offset, p2: Offset, p3: Offset): Offset {
    val u = 1f - t
    val uu = u * u
    val tt = t * t
    return Offset(
        x = (uu * u * p0.x) + (3f * uu * t * p1.x) + (3f * u * tt * p2.x) + (tt * t * p3.x),
        y = (uu * u * p0.y) + (3f * uu * t * p1.y) + (3f * u * tt * p2.y) + (tt * t * p3.y)
    )
}

private fun DrawScope.drawWindStreaks(progress: Float, position: Point) {
    if (progress < 0.08f || progress > 0.88f) return
    val center = Offset(position.x * size.width, position.y * size.height)
    repeat(4) { index ->
        val phase = progress * 15f + index * 1.3f
        val vertical = sin(phase * PI.toFloat()) * 18.dp.toPx()
        val length = (24 + index * 7).dp.toPx()
        val x = center.x - (78 + index * 12).dp.toPx()
        drawLine(
            color = if (index % 2 == 0) {
                TukiOrange.copy(alpha = 0.18f)
            } else {
                TukiTeal.copy(alpha = 0.14f)
            },
            start = Offset(x, center.y + vertical),
            end = Offset(x + length, center.y + vertical),
            strokeWidth = 2.5.dp.toPx(),
            cap = StrokeCap.Round
        )
    }
}

private fun DrawScope.drawFlightShadow(progress: Float, position: Point) {
    val altitude = 1f - (position.y.coerceIn(0.18f, 0.76f) - 0.18f) / 0.58f
    val landing = ((progress - 0.80f) / 0.20f).coerceIn(0f, 1f)
    val shadowCenter = Offset(
        x = position.x * size.width,
        y = (position.y * size.height) + (118.dp.toPx() * (0.72f + altitude * 0.25f))
    )
    drawOval(
        color = TukiOrange.copy(alpha = 0.055f + 0.07f * landing),
        topLeft = Offset(
            shadowCenter.x - (56.dp.toPx() * (0.78f + landing * 0.22f)),
            shadowCenter.y - 7.dp.toPx()
        ),
        size = androidx.compose.ui.geometry.Size(
            width = 112.dp.toPx() * (0.78f + landing * 0.22f),
            height = 14.dp.toPx()
        )
    )
}
