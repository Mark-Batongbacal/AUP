package com.example.frontend.screens.onboarding

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.slideInHorizontally
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.frontend.ui.motion.TukiMascot
import com.example.frontend.ui.motion.TukiMascotMood
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay
import kotlin.math.min

private enum class CleanJourneyMode {
    WALK,
    TRICYCLE,
    JEEPNEY,
    DESTINATION
}

/**
 * Page 2 variant that keeps the same premium sequencing while avoiding translucent/scale
 * compositing on the active card. This prevents the temporary highlight from painting a light
 * rectangular band behind the label text on some devices/emulators.
 */
@Composable
fun PremiumStepByStepPageClean(active: Boolean) {
    CleanPageShell {
        CleanPageTitle(
            title = "Follow your trip step by step",
            subtitle = "TUKI guides you through walking, tricycles, jeepneys, and transfers."
        )

        Spacer(modifier = Modifier.height(8.dp))
        CleanJourneyStory(active = active)

        Spacer(modifier = Modifier.height(10.dp))
        Surface(
            modifier = Modifier.fillMaxWidth(),
            color = TukiTeal.copy(alpha = 0.065f),
            shape = RoundedCornerShape(21.dp)
        ) {
            Row(
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 13.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(18.dp)
                        .background(TukiTeal.copy(alpha = 0.12f), CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Box(
                        modifier = Modifier
                            .size(7.dp)
                            .background(TukiTeal, CircleShape)
                    )
                }
                Spacer(modifier = Modifier.width(10.dp))
                Column {
                    Text(
                        text = "Different rides. Same destination.",
                        color = TukiInk,
                        style = MaterialTheme.typography.titleMedium
                    )
                    Text(
                        text = "Every transfer stays clear, so you always know what comes next.",
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }
    }
}

@Composable
private fun CleanJourneyStory(active: Boolean) {
    val modes = remember {
        listOf(
            CleanJourneyMode.WALK to "WALK",
            CleanJourneyMode.TRICYCLE to "TRICYCLE",
            CleanJourneyMode.JEEPNEY to "JEEPNEY",
            CleanJourneyMode.DESTINATION to "DESTINATION"
        )
    }
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            delay(170)
            modes.indices.forEach { index ->
                stage = index + 1
                delay(if (index == modes.lastIndex) 560 else 360)
            }
            stage = modes.size + 1
        }
    }

    val visibleSteps = stage.coerceAtMost(modes.size)
    val routeProgress by animateFloatAsState(
        targetValue = if (active) visibleSteps / modes.size.toFloat() else 0f,
        animationSpec = tween(360),
        label = "clean_journey_route_progress"
    )
    val guideProgress by animateFloatAsState(
        targetValue = if (active) routeProgress else 0f,
        animationSpec = spring(dampingRatio = 0.82f, stiffness = 280f),
        label = "clean_journey_guide_progress"
    )

    BoxWithConstraints(
        modifier = Modifier
            .fillMaxWidth()
            .height(310.dp)
    ) {
        val cardWidth = maxWidth * 0.61f

        Canvas(modifier = Modifier.fillMaxSize()) {
            val p0 = Offset(size.width * 0.30f, size.height * 0.11f)
            val p1 = Offset(size.width * 0.25f, size.height * 0.37f)
            val p2 = Offset(size.width * 0.33f, size.height * 0.62f)
            val p3 = Offset(size.width * 0.28f, size.height * 0.88f)

            val basePath = Path().apply {
                moveTo(p0.x, p0.y)
                cubicTo(
                    size.width * 0.40f, size.height * 0.19f,
                    size.width * 0.18f, size.height * 0.29f,
                    p1.x, p1.y
                )
                cubicTo(
                    size.width * 0.16f, size.height * 0.47f,
                    size.width * 0.43f, size.height * 0.53f,
                    p2.x, p2.y
                )
                cubicTo(
                    size.width * 0.43f, size.height * 0.72f,
                    size.width * 0.18f, size.height * 0.80f,
                    p3.x, p3.y
                )
            }

            drawPath(
                path = basePath,
                color = TukiTeal.copy(alpha = 0.12f),
                style = Stroke(width = 5.dp.toPx(), cap = StrokeCap.Round)
            )

            if (routeProgress >= 0.25f) {
                val first = Path().apply {
                    moveTo(p0.x, p0.y)
                    cubicTo(
                        size.width * 0.40f, size.height * 0.19f,
                        size.width * 0.18f, size.height * 0.29f,
                        p1.x, p1.y
                    )
                }
                drawPath(first, TukiTeal.copy(alpha = 0.68f), style = Stroke(4.dp.toPx(), cap = StrokeCap.Round))
            }
            if (routeProgress >= 0.50f) {
                val second = Path().apply {
                    moveTo(p1.x, p1.y)
                    cubicTo(
                        size.width * 0.16f, size.height * 0.47f,
                        size.width * 0.43f, size.height * 0.53f,
                        p2.x, p2.y
                    )
                }
                drawPath(second, TukiTeal.copy(alpha = 0.68f), style = Stroke(4.dp.toPx(), cap = StrokeCap.Round))
            }
            if (routeProgress >= 0.75f) {
                val third = Path().apply {
                    moveTo(p2.x, p2.y)
                    cubicTo(
                        size.width * 0.43f, size.height * 0.72f,
                        size.width * 0.18f, size.height * 0.80f,
                        p3.x, p3.y
                    )
                }
                drawPath(third, TukiTeal.copy(alpha = 0.68f), style = Stroke(4.dp.toPx(), cap = StrokeCap.Round))
            }

            val points = listOf(p0, p1, p2, p3)
            points.forEachIndexed { index, point ->
                val reached = visibleSteps > index
                drawCircle(
                    color = if (reached) TukiOrange.copy(alpha = 0.16f) else TukiTeal.copy(alpha = 0.06f),
                    radius = if (reached) 13.dp.toPx() else 9.dp.toPx(),
                    center = point
                )
                drawCircle(
                    color = if (reached) TukiOrange else TukiTeal.copy(alpha = 0.24f),
                    radius = 5.5.dp.toPx(),
                    center = point
                )
                drawCircle(Color.White, 2.4.dp.toPx(), point)
            }
        }

        CleanSceneMascot(
            active = active,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(x = (-10).dp, y = 38.dp)
                .size(170.dp)
                .graphicsLayer {
                    translationY = ((guideProgress - 0.50f) * 48f) * density
                }
        )

        Column(
            modifier = Modifier
                .align(Alignment.CenterEnd)
                .width(cardWidth),
            verticalArrangement = Arrangement.spacedBy(11.dp)
        ) {
            modes.forEachIndexed { index, (mode, label) ->
                AnimatedVisibility(
                    visible = visibleSteps > index,
                    enter = fadeIn(tween(210)) + slideInHorizontally(
                        animationSpec = spring(dampingRatio = 0.82f, stiffness = 350f),
                        initialOffsetX = { it / 4 }
                    )
                ) {
                    CleanJourneyStopCard(
                        mode = mode,
                        label = label,
                        highlighted = stage in 1..modes.size && stage - 1 == index
                    )
                }
            }
        }
    }
}

@Composable
private fun CleanJourneyStopCard(
    mode: CleanJourneyMode,
    label: String,
    highlighted: Boolean
) {
    // Keep the card fill fully opaque. The previous translucent highlighted Surface combined with
    // a graphics layer could render as a pale rectangular strip underneath the label on Android.
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color.White,
        shape = RoundedCornerShape(18.dp),
        shadowElevation = if (highlighted) 4.dp else 2.dp,
        border = BorderStroke(
            width = if (highlighted) 1.5.dp else 1.dp,
            color = if (highlighted) TukiTeal.copy(alpha = 0.48f) else TukiInk.copy(alpha = 0.06f)
        )
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            CleanJourneyModeIcon(mode = mode, modifier = Modifier.size(28.dp))
            Spacer(modifier = Modifier.width(10.dp))
            Text(
                text = label,
                color = if (highlighted) TukiDeepTeal else TukiInk,
                style = MaterialTheme.typography.labelLarge,
                fontWeight = if (highlighted) FontWeight.SemiBold else FontWeight.Normal
            )
        }
    }
}

@Composable
private fun CleanSceneMascot(
    active: Boolean,
    modifier: Modifier
) {
    val translationX by animateFloatAsState(
        targetValue = if (active) 0f else -84f,
        animationSpec = spring(dampingRatio = 0.72f, stiffness = 275f),
        label = "clean_scene_mascot_entry"
    )
    val alpha by animateFloatAsState(
        targetValue = if (active) 1f else 0f,
        animationSpec = tween(220),
        label = "clean_scene_mascot_alpha"
    )
    val scale by animateFloatAsState(
        targetValue = if (active) 1f else 0.86f,
        animationSpec = spring(dampingRatio = 0.70f, stiffness = 300f),
        label = "clean_scene_mascot_scale"
    )

    TukiMascot(
        mood = TukiMascotMood.GUIDE,
        modifier = modifier.graphicsLayer {
            this.translationX = translationX * density
            this.alpha = alpha
            scaleX = scale
            scaleY = scale
        },
        showHalo = false
    )
}

@Composable
private fun CleanPageShell(content: @Composable () -> Unit) {
    Box(modifier = Modifier.fillMaxSize()) {
        CleanOnboardingDecor()
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp, vertical = 4.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            content()
            Spacer(modifier = Modifier.height(8.dp))
        }
    }
}

@Composable
private fun CleanOnboardingDecor() {
    Canvas(modifier = Modifier.fillMaxSize()) {
        val minDimension = min(size.width, size.height)
        drawCircle(
            color = TukiTeal.copy(alpha = 0.030f),
            radius = minDimension * 0.22f,
            center = Offset(-size.width * 0.02f, size.height * 0.26f)
        )
        drawCircle(
            color = TukiGold.copy(alpha = 0.038f),
            radius = minDimension * 0.18f,
            center = Offset(size.width * 1.03f, size.height * 0.58f)
        )
        drawCircle(
            color = TukiOrange.copy(alpha = 0.022f),
            radius = minDimension * 0.14f,
            center = Offset(size.width * 0.22f, size.height * 0.91f)
        )

        val accentPath = Path().apply {
            moveTo(size.width * 0.03f, size.height * 0.33f)
            cubicTo(
                size.width * 0.22f, size.height * 0.29f,
                size.width * 0.35f, size.height * 0.38f,
                size.width * 0.52f, size.height * 0.34f
            )
        }
        drawPath(
            path = accentPath,
            color = TukiTeal.copy(alpha = 0.035f),
            style = Stroke(width = 3.dp.toPx(), cap = StrokeCap.Round)
        )
    }
}

@Composable
private fun CleanPageTitle(
    title: String,
    subtitle: String
) {
    Text(
        text = title,
        color = TukiInk,
        style = MaterialTheme.typography.displaySmall,
        textAlign = TextAlign.Center
    )
    Spacer(modifier = Modifier.height(5.dp))
    Text(
        text = subtitle,
        color = TukiMuted,
        style = MaterialTheme.typography.bodyMedium,
        textAlign = TextAlign.Center
    )
}

@Composable
private fun CleanJourneyModeIcon(
    mode: CleanJourneyMode,
    modifier: Modifier = Modifier
) {
    Canvas(modifier = modifier) {
        val w = size.width
        val h = size.height
        val teal = TukiTeal
        val orange = TukiOrange
        val ink = TukiDeepTeal

        when (mode) {
            CleanJourneyMode.WALK -> {
                drawCircle(orange, w * 0.10f, Offset(w * 0.50f, h * 0.18f))
                drawLine(teal, Offset(w * 0.50f, h * 0.30f), Offset(w * 0.46f, h * 0.58f), w * 0.07f, StrokeCap.Round)
                drawLine(teal, Offset(w * 0.47f, h * 0.40f), Offset(w * 0.30f, h * 0.52f), w * 0.06f, StrokeCap.Round)
                drawLine(teal, Offset(w * 0.48f, h * 0.42f), Offset(w * 0.66f, h * 0.52f), w * 0.06f, StrokeCap.Round)
                drawLine(ink, Offset(w * 0.46f, h * 0.58f), Offset(w * 0.31f, h * 0.83f), w * 0.065f, StrokeCap.Round)
                drawLine(ink, Offset(w * 0.46f, h * 0.58f), Offset(w * 0.65f, h * 0.80f), w * 0.065f, StrokeCap.Round)
            }

            CleanJourneyMode.TRICYCLE -> {
                drawRoundRect(
                    color = teal,
                    topLeft = Offset(w * 0.20f, h * 0.38f),
                    size = Size(w * 0.52f, h * 0.32f),
                    cornerRadius = CornerRadius(w * 0.08f, w * 0.08f)
                )
                drawRoundRect(
                    color = orange,
                    topLeft = Offset(w * 0.48f, h * 0.25f),
                    size = Size(w * 0.28f, h * 0.20f),
                    cornerRadius = CornerRadius(w * 0.05f, w * 0.05f)
                )
                drawLine(ink, Offset(w * 0.17f, h * 0.38f), Offset(w * 0.30f, h * 0.24f), w * 0.05f, StrokeCap.Round)
                drawCircle(ink, w * 0.105f, Offset(w * 0.28f, h * 0.76f))
                drawCircle(ink, w * 0.105f, Offset(w * 0.68f, h * 0.76f))
                drawCircle(Color.White, w * 0.045f, Offset(w * 0.28f, h * 0.76f))
                drawCircle(Color.White, w * 0.045f, Offset(w * 0.68f, h * 0.76f))
            }

            CleanJourneyMode.JEEPNEY -> {
                drawRoundRect(
                    color = teal,
                    topLeft = Offset(w * 0.10f, h * 0.32f),
                    size = Size(w * 0.80f, h * 0.40f),
                    cornerRadius = CornerRadius(w * 0.09f, w * 0.09f)
                )
                drawRoundRect(
                    color = orange,
                    topLeft = Offset(w * 0.17f, h * 0.23f),
                    size = Size(w * 0.56f, h * 0.18f),
                    cornerRadius = CornerRadius(w * 0.05f, w * 0.05f)
                )
                drawRoundRect(Color.White.copy(alpha = 0.92f), Offset(w * 0.21f, h * 0.40f), Size(w * 0.17f, h * 0.16f), CornerRadius(w * 0.03f, w * 0.03f))
                drawRoundRect(Color.White.copy(alpha = 0.92f), Offset(w * 0.43f, h * 0.40f), Size(w * 0.17f, h * 0.16f), CornerRadius(w * 0.03f, w * 0.03f))
                drawRoundRect(Color.White.copy(alpha = 0.92f), Offset(w * 0.65f, h * 0.40f), Size(w * 0.14f, h * 0.16f), CornerRadius(w * 0.03f, w * 0.03f))
                drawCircle(ink, w * 0.09f, Offset(w * 0.28f, h * 0.76f))
                drawCircle(ink, w * 0.09f, Offset(w * 0.72f, h * 0.76f))
            }

            CleanJourneyMode.DESTINATION -> {
                drawCircle(orange.copy(alpha = 0.18f), w * 0.28f, Offset(w * 0.50f, h * 0.42f))
                drawCircle(orange, w * 0.17f, Offset(w * 0.50f, h * 0.38f))
                drawCircle(Color.White, w * 0.065f, Offset(w * 0.50f, h * 0.38f))
                drawLine(
                    color = orange,
                    start = Offset(w * 0.50f, h * 0.53f),
                    end = Offset(w * 0.50f, h * 0.86f),
                    strokeWidth = w * 0.08f,
                    cap = StrokeCap.Round
                )
            }
        }
    }
}
