package com.example.frontend.screens.onboarding

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideInVertically
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
import androidx.compose.foundation.layout.widthIn
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
import androidx.compose.runtime.mutableStateOf
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

private data class PremiumRoutePreview(
    val label: String,
    val detail: String,
    val badge: String,
    val accent: Color
)

private enum class PremiumJourneyMode {
    WALK,
    TRICYCLE,
    JEEPNEY,
    DESTINATION
}

/**
 * Premium motion pass for onboarding pages 1-4.
 *
 * The flight/welcome intro intentionally remains in TukiFlightIntro.kt and is not changed here.
 * Each page uses the exact page-specific TUKI artwork already wired through TukiMascot.
 */
@Composable
fun PremiumRouteChoicePage(active: Boolean) {
    var heroVisible by remember { mutableStateOf(false) }
    var speechVisible by remember { mutableStateOf(false) }
    var routeStage by remember { mutableIntStateOf(0) }
    var balancedEmphasis by remember { mutableStateOf(false) }

    LaunchedEffect(active) {
        heroVisible = false
        speechVisible = false
        routeStage = 0
        balancedEmphasis = false

        if (active) {
            delay(90)
            heroVisible = true
            delay(260)
            speechVisible = true
            delay(260)
            routeStage = 1
            delay(160)
            routeStage = 2
            delay(160)
            routeStage = 3
            delay(180)
            balancedEmphasis = true
            delay(420)
            balancedEmphasis = false
        }
    }

    PremiumPageShell {
        PremiumMascotHero(
            mascotVisible = heroVisible,
            speechVisible = speechVisible,
            mood = TukiMascotMood.WELCOME,
            speech = "Hi! I’m TUKI — your travel buddy.",
            enterFromX = -96f
        )

        PremiumPageTitle(
            title = "Find the best route for you",
            subtitle = "Compare the fastest, cheapest, and balanced route based on your trip."
        )

        Spacer(modifier = Modifier.height(12.dp))
        PremiumRouteChoiceStack(
            visibleCount = routeStage,
            balancedEmphasis = balancedEmphasis
        )
    }
}

@Composable
private fun PremiumRouteChoiceStack(
    visibleCount: Int,
    balancedEmphasis: Boolean
) {
    val routes = remember {
        listOf(
            PremiumRoutePreview("FASTEST", "22 min • Tricycle → Jeepney", "₱65", Color(0xFFE95E58)),
            PremiumRoutePreview("CHEAPEST", "43 min • Walk → 2 Jeepneys", "₱26", Color(0xFF4EBF83)),
            PremiumRoutePreview("BALANCED", "29 min • Walk → Tricycle → Jeepney", "₱42", TukiGold)
        )
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .widthIn(max = 520.dp),
        verticalArrangement = Arrangement.spacedBy(9.dp)
    ) {
        routes.forEachIndexed { index, route ->
            AnimatedVisibility(
                visible = visibleCount > index,
                enter = fadeIn(tween(220)) + slideInHorizontally(
                    animationSpec = spring(dampingRatio = 0.78f, stiffness = 340f),
                    initialOffsetX = { if (index % 2 == 0) it / 4 else -it / 4 }
                )
            ) {
                PremiumRouteChoiceCard(
                    route = route,
                    emphasized = index == 2 && balancedEmphasis
                )
            }
        }
    }
}

@Composable
private fun PremiumRouteChoiceCard(
    route: PremiumRoutePreview,
    emphasized: Boolean
) {
    val scale by animateFloatAsState(
        targetValue = if (emphasized) 1.024f else 1f,
        animationSpec = spring(dampingRatio = 0.58f, stiffness = 420f),
        label = "premium_route_${route.label}_scale"
    )

    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .graphicsLayer {
                scaleX = scale
                scaleY = scale
            },
        color = route.accent.copy(alpha = if (emphasized) 0.15f else 0.11f),
        shape = RoundedCornerShape(21.dp),
        shadowElevation = if (emphasized) 3.dp else 0.dp,
        border = BorderStroke(
            1.dp,
            route.accent.copy(alpha = if (emphasized) 0.34f else 0.20f)
        )
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 13.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(route.accent.copy(alpha = 0.17f), CircleShape),
                contentAlignment = Alignment.Center
            ) {
                Box(
                    modifier = Modifier
                        .size(11.dp)
                        .background(route.accent, CircleShape)
                )
            }
            Spacer(modifier = Modifier.width(12.dp))
            Column(modifier = Modifier.fillMaxWidth(0.73f)) {
                Text(
                    text = route.label,
                    color = TukiInk,
                    style = MaterialTheme.typography.labelSmall,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = route.detail,
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodySmall
                )
            }
            Spacer(modifier = Modifier.width(6.dp))
            Text(
                text = route.badge,
                color = TukiDeepTeal,
                style = MaterialTheme.typography.titleMedium
            )
        }
    }
}

@Composable
fun PremiumStepByStepPage(active: Boolean) {
    PremiumPageShell {
        PremiumPageTitle(
            title = "Follow your trip step by step",
            subtitle = "TUKI guides you through walking, tricycles, jeepneys, and transfers."
        )

        Spacer(modifier = Modifier.height(8.dp))
        PremiumJourneyStory(active = active)

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
private fun PremiumJourneyStory(active: Boolean) {
    val modes = remember {
        listOf(
            PremiumJourneyMode.WALK to "WALK",
            PremiumJourneyMode.TRICYCLE to "TRICYCLE",
            PremiumJourneyMode.JEEPNEY to "JEEPNEY",
            PremiumJourneyMode.DESTINATION to "DESTINATION"
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
            // Settled state: all steps remain visible without a permanently pulsing card.
            stage = modes.size + 1
        }
    }

    val visibleSteps = stage.coerceAtMost(modes.size)
    val routeProgress by animateFloatAsState(
        targetValue = if (active) visibleSteps / modes.size.toFloat() else 0f,
        animationSpec = tween(360),
        label = "premium_journey_route_progress"
    )
    val guideProgress by animateFloatAsState(
        targetValue = if (active) routeProgress else 0f,
        animationSpec = spring(dampingRatio = 0.82f, stiffness = 280f),
        label = "premium_journey_guide_progress"
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

            // Reveal the route one commute segment at a time so the line tells the same story as
            // the cards instead of being fully drawn before the steps appear.
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

        PremiumSceneMascot(
            active = active,
            mood = TukiMascotMood.GUIDE,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(x = (-10).dp, y = 38.dp)
                .size(170.dp)
                .graphicsLayer {
                    // TUKI follows the story gently, then settles after the destination is reached.
                    translationY = ((guideProgress - 0.50f) * 48f) * density
                },
            enterFromX = -84f
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
                    PremiumJourneyStopCard(
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
private fun PremiumJourneyStopCard(
    mode: PremiumJourneyMode,
    label: String,
    highlighted: Boolean
) {
    val scale by animateFloatAsState(
        targetValue = if (highlighted) 1.026f else 1f,
        animationSpec = spring(dampingRatio = 0.64f, stiffness = 420f),
        label = "premium_journey_card_$label"
    )

    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .graphicsLayer {
                scaleX = scale
                scaleY = scale
            },
        color = if (highlighted) TukiTeal.copy(alpha = 0.060f) else Color.White,
        shape = RoundedCornerShape(18.dp),
        shadowElevation = if (highlighted) 4.dp else 2.dp,
        border = BorderStroke(
            1.dp,
            if (highlighted) TukiTeal.copy(alpha = 0.42f) else TukiInk.copy(alpha = 0.06f)
        )
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            PremiumJourneyModeIcon(mode = mode, modifier = Modifier.size(28.dp))
            Spacer(modifier = Modifier.width(10.dp))
            Text(
                text = label,
                color = TukiInk,
                style = MaterialTheme.typography.labelLarge,
                fontWeight = if (highlighted) FontWeight.SemiBold else FontWeight.Normal
            )
        }
    }
}

@Composable
fun PremiumParaPoPage(active: Boolean) {
    var alertVisible by remember { mutableStateOf(false) }

    PremiumPageShell {
        PremiumPageTitle(
            title = "Never miss your stop",
            subtitle = "Get notified when you’re close to your drop-off point so you know when to get off."
        )

        Spacer(modifier = Modifier.height(5.dp))
        PremiumParaPoHero(
            active = active,
            alertVisible = alertVisible
        )
        Spacer(modifier = Modifier.height(6.dp))
        PremiumParaPoDemo(
            active = active,
            alertVisible = alertVisible,
            onAlertVisibleChange = { alertVisible = it }
        )
    }
}

@Composable
private fun PremiumParaPoHero(
    active: Boolean,
    alertVisible: Boolean
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(168.dp)
    ) {
        PremiumSceneMascot(
            active = active,
            mood = TukiMascotMood.ALERT,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .offset(x = (-38).dp)
                .size(178.dp),
            enterFromX = -64f
        )

        AnimatedVisibility(
            visible = active && alertVisible,
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(top = 7.dp, end = 2.dp),
            enter = fadeIn(tween(160)) + scaleIn(
                initialScale = 0.62f,
                animationSpec = spring(dampingRatio = 0.50f, stiffness = 470f)
            ),
            exit = fadeOut(tween(160))
        ) {
            Surface(
                color = Color(0xFFFF6D57),
                shape = RoundedCornerShape(23.dp, 23.dp, 23.dp, 6.dp),
                shadowElevation = 7.dp
            ) {
                Column(modifier = Modifier.padding(horizontal = 17.dp, vertical = 11.dp)) {
                    Text(
                        text = "PARA PO!",
                        color = Color.White,
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = "Your stop is near!",
                        color = Color.White.copy(alpha = 0.94f),
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }
    }
}

@Composable
private fun PremiumParaPoDemo(
    active: Boolean,
    alertVisible: Boolean,
    onAlertVisibleChange: (Boolean) -> Unit
) {
    val progress = remember { Animatable(0f) }

    LaunchedEffect(active) {
        if (!active) {
            onAlertVisibleChange(false)
            progress.snapTo(0f)
            return@LaunchedEffect
        }

        while (true) {
            onAlertVisibleChange(false)
            progress.snapTo(0f)
            delay(420)

            // Travel most of the route calmly before the actual alighting alert appears.
            progress.animateTo(
                targetValue = 0.72f,
                animationSpec = tween(2550, easing = LinearEasing)
            )
            onAlertVisibleChange(true)
            progress.animateTo(
                targetValue = 1f,
                animationSpec = tween(850, easing = LinearEasing)
            )

            // Hold the destination state long enough for the alert to be understood.
            delay(1550)
            onAlertVisibleChange(false)
            delay(500)
        }
    }

    val destinationScale by animateFloatAsState(
        targetValue = if (alertVisible) 1.45f else 0.88f,
        animationSpec = spring(dampingRatio = 0.42f, stiffness = 360f),
        label = "premium_para_destination_pulse"
    )

    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color.White,
        shape = RoundedCornerShape(26.dp),
        shadowElevation = 4.dp,
        border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.07f))
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            BoxWithConstraints(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(126.dp)
            ) {
                val vehicleSize = 48.dp
                val startPadding = 14.dp
                val endPadding = 22.dp
                val travelWidth = (maxWidth - vehicleSize - startPadding - endPadding).coerceAtLeast(0.dp)
                val currentProgress = if (active) progress.value else 0f

                Canvas(modifier = Modifier.fillMaxSize()) {
                    val startX = 16.dp.toPx()
                    val endX = size.width - 18.dp.toPx()
                    val trackY = 58.dp.toPx()

                    drawLine(
                        color = TukiTeal.copy(alpha = 0.14f),
                        start = Offset(startX, trackY),
                        end = Offset(endX, trackY),
                        strokeWidth = 6.dp.toPx(),
                        cap = StrokeCap.Round
                    )
                    drawLine(
                        color = TukiOrange,
                        start = Offset(startX, trackY),
                        end = Offset(startX + ((endX - startX) * currentProgress), trackY),
                        strokeWidth = 6.dp.toPx(),
                        cap = StrokeCap.Round
                    )

                    listOf(0f, 0.5f, 1f).forEachIndexed { index, point ->
                        val x = startX + ((endX - startX) * point)
                        val reached = currentProgress >= point
                        drawCircle(
                            color = if (reached) TukiOrange else TukiTeal,
                            radius = if (index == 2) 7.dp.toPx() else 5.dp.toPx(),
                            center = Offset(x, trackY)
                        )
                    }

                    drawCircle(
                        color = Color(0xFFFF6D57).copy(alpha = if (alertVisible) 0.18f else 0.05f),
                        radius = 19.dp.toPx() * destinationScale,
                        center = Offset(endX, trackY)
                    )
                }

                Surface(
                    modifier = Modifier
                        .offset(
                            x = startPadding + (travelWidth * currentProgress),
                            y = 34.dp
                        )
                        .size(vehicleSize),
                    color = Color.White,
                    shape = CircleShape,
                    shadowElevation = 5.dp,
                    border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.18f))
                ) {
                    PremiumJourneyModeIcon(
                        mode = PremiumJourneyMode.JEEPNEY,
                        modifier = Modifier.padding(8.dp)
                    )
                }

                Text(
                    text = "AUF",
                    modifier = Modifier.align(Alignment.BottomStart),
                    color = TukiInk,
                    style = MaterialTheme.typography.labelSmall,
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    text = "SM Clark",
                    modifier = Modifier.align(Alignment.BottomEnd),
                    color = TukiInk,
                    style = MaterialTheme.typography.labelSmall,
                    fontWeight = FontWeight.Bold
                )
            }

            AnimatedVisibility(
                visible = alertVisible,
                enter = fadeIn(tween(170)) + slideInVertically(
                    animationSpec = spring(dampingRatio = 0.68f, stiffness = 360f),
                    initialOffsetY = { it / 2 }
                ),
                exit = fadeOut(tween(170))
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    color = TukiOrange.copy(alpha = 0.09f),
                    shape = RoundedCornerShape(18.dp),
                    border = BorderStroke(1.dp, TukiOrange.copy(alpha = 0.28f))
                ) {
                    Row(
                        modifier = Modifier.padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        PremiumBellIcon(modifier = Modifier.size(30.dp))
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "Vibration alert",
                                color = TukiInk,
                                style = MaterialTheme.typography.labelLarge,
                                fontWeight = FontWeight.SemiBold
                            )
                            Text(
                                text = "TUKI gives you time to prepare before your drop-off.",
                                color = TukiMuted,
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun PremiumAskTukiPage(active: Boolean) {
    PremiumPageShell {
        PremiumPageTitle(
            title = "Travel within your budget",
            subtitle = "Tell TUKI where you’re going and how much you want to spend."
        )

        Spacer(modifier = Modifier.height(6.dp))
        PremiumBudgetChatExperience(active = active)
    }
}

@Composable
private fun PremiumBudgetChatExperience(active: Boolean) {
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            delay(260)
            stage = 1
            delay(600)
            stage = 2
            delay(820)
            stage = 3
            delay(440)
            stage = 4
        }
    }

    val reactionScale by animateFloatAsState(
        targetValue = if (stage >= 4) 1.055f else 1f,
        animationSpec = spring(dampingRatio = 0.48f, stiffness = 360f),
        label = "premium_budget_tuki_reaction"
    )

    BoxWithConstraints(
        modifier = Modifier
            .fillMaxWidth()
            .height(400.dp)
    ) {
        val chatWidth = maxWidth * 0.73f

        PremiumSceneMascot(
            active = active,
            mood = if (stage >= 3) TukiMascotMood.CELEBRATE else TukiMascotMood.THINKING,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(x = (-20).dp, y = 42.dp)
                .size(185.dp)
                .graphicsLayer {
                    scaleX = reactionScale
                    scaleY = reactionScale
                },
            enterFromX = -75f
        )

        PremiumCelebrationSparkles(
            visible = stage >= 4,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(x = 12.dp, y = (-22).dp)
                .size(126.dp)
        )

        Column(
            modifier = Modifier
                .align(Alignment.TopEnd)
                .width(chatWidth),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            AnimatedVisibility(
                visible = stage >= 1,
                enter = fadeIn(tween(210)) + slideInHorizontally(initialOffsetX = { it / 4 })
            ) {
                Surface(
                    color = Color(0xFFDCEEFF),
                    shape = RoundedCornerShape(20.dp, 20.dp, 7.dp, 20.dp)
                ) {
                    Text(
                        text = "I only have ₱50. What’s the best way to SM Clark?",
                        modifier = Modifier.padding(13.dp),
                        color = TukiInk,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }

            AnimatedVisibility(
                visible = stage == 2,
                enter = fadeIn(tween(180)) + scaleIn(initialScale = 0.90f),
                exit = fadeOut(tween(140))
            ) {
                PremiumThinkingDots()
            }

            AnimatedVisibility(
                visible = stage >= 3,
                enter = fadeIn(tween(210)) + slideInHorizontally(initialOffsetX = { -it / 4 })
            ) {
                Surface(
                    color = Color(0xFFDDF4E8),
                    shape = RoundedCornerShape(7.dp, 20.dp, 20.dp, 20.dp)
                ) {
                    Text(
                        text = "I found a great route for you!",
                        modifier = Modifier.padding(13.dp),
                        color = TukiInk,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }

            AnimatedVisibility(
                visible = stage >= 4,
                enter = fadeIn(tween(270)) + slideInVertically(
                    animationSpec = spring(dampingRatio = 0.72f, stiffness = 320f),
                    initialOffsetY = { it / 3 }
                )
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    color = Color.White,
                    shape = RoundedCornerShape(22.dp),
                    shadowElevation = 5.dp,
                    border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.13f))
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text(
                                text = "BALANCED ROUTE",
                                color = TukiDeepTeal,
                                style = MaterialTheme.typography.labelSmall,
                                fontWeight = FontWeight.Bold
                            )
                            Surface(
                                color = TukiTeal.copy(alpha = 0.10f),
                                shape = RoundedCornerShape(20.dp)
                            ) {
                                Text(
                                    text = "Best fit",
                                    modifier = Modifier.padding(horizontal = 9.dp, vertical = 4.dp),
                                    color = TukiDeepTeal,
                                    style = MaterialTheme.typography.labelSmall
                                )
                            }
                        }
                        Spacer(modifier = Modifier.height(5.dp))
                        Text(
                            text = "₱42  •  29 min",
                            color = TukiInk,
                            style = MaterialTheme.typography.titleLarge
                        )
                        Spacer(modifier = Modifier.height(10.dp))
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(7.dp)
                        ) {
                            PremiumJourneyModeIcon(PremiumJourneyMode.WALK, Modifier.size(21.dp))
                            Text("→", color = TukiMuted)
                            PremiumJourneyModeIcon(PremiumJourneyMode.TRICYCLE, Modifier.size(21.dp))
                            Text("→", color = TukiMuted)
                            PremiumJourneyModeIcon(PremiumJourneyMode.JEEPNEY, Modifier.size(21.dp))
                            Text("→", color = TukiMuted)
                            PremiumJourneyModeIcon(PremiumJourneyMode.DESTINATION, Modifier.size(21.dp))
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun PremiumThinkingDots() {
    val infiniteTransition = rememberInfiniteTransition(label = "premium_tuki_thinking_dots")
    val pulse1 by infiniteTransition.animateFloat(
        initialValue = 0.50f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(tween(460), RepeatMode.Reverse),
        label = "premium_thinking_dot_1"
    )
    val pulse2 by infiniteTransition.animateFloat(
        initialValue = 1f,
        targetValue = 0.50f,
        animationSpec = infiniteRepeatable(tween(460), RepeatMode.Reverse),
        label = "premium_thinking_dot_2"
    )

    Surface(
        color = Color.White,
        shape = RoundedCornerShape(18.dp),
        shadowElevation = 1.dp
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 11.dp),
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            listOf(pulse1, pulse2, pulse1).forEach { alpha ->
                Box(
                    modifier = Modifier
                        .size(7.dp)
                        .graphicsLayer { this.alpha = alpha }
                        .background(TukiTeal, CircleShape)
                )
            }
        }
    }
}

@Composable
private fun PremiumPageShell(content: @Composable () -> Unit) {
    Box(modifier = Modifier.fillMaxSize()) {
        PremiumOnboardingDecor()
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
private fun PremiumOnboardingDecor() {
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
private fun PremiumMascotHero(
    mascotVisible: Boolean,
    speechVisible: Boolean,
    mood: TukiMascotMood,
    speech: String,
    enterFromX: Float
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(202.dp)
    ) {
        PremiumSceneMascot(
            active = mascotVisible,
            mood = mood,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(x = (-18).dp, y = 4.dp)
                .size(210.dp),
            enterFromX = enterFromX
        )

        AnimatedVisibility(
            visible = speechVisible,
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(top = 22.dp, end = 2.dp),
            enter = fadeIn(tween(210)) + scaleIn(
                initialScale = 0.86f,
                animationSpec = spring(dampingRatio = 0.66f, stiffness = 370f)
            )
        ) {
            Surface(
                modifier = Modifier.widthIn(max = 185.dp),
                color = Color.White,
                shape = RoundedCornerShape(21.dp, 21.dp, 21.dp, 6.dp),
                shadowElevation = 4.dp,
                border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.07f))
            ) {
                Text(
                    text = speech,
                    modifier = Modifier.padding(horizontal = 13.dp, vertical = 12.dp),
                    color = TukiInk,
                    style = MaterialTheme.typography.bodySmall
                )
            }
        }
    }
}

@Composable
private fun PremiumSceneMascot(
    active: Boolean,
    mood: TukiMascotMood,
    modifier: Modifier,
    enterFromX: Float
) {
    val translationX by animateFloatAsState(
        targetValue = if (active) 0f else enterFromX,
        animationSpec = spring(dampingRatio = 0.72f, stiffness = 275f),
        label = "premium_scene_mascot_entry_${mood.name}"
    )
    val alpha by animateFloatAsState(
        targetValue = if (active) 1f else 0f,
        animationSpec = tween(220),
        label = "premium_scene_mascot_alpha_${mood.name}"
    )
    val scale by animateFloatAsState(
        targetValue = if (active) 1f else 0.86f,
        animationSpec = spring(dampingRatio = 0.70f, stiffness = 300f),
        label = "premium_scene_mascot_scale_${mood.name}"
    )

    TukiMascot(
        mood = mood,
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
private fun PremiumPageTitle(
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
private fun PremiumCelebrationSparkles(
    visible: Boolean,
    modifier: Modifier = Modifier
) {
    val alpha by animateFloatAsState(
        targetValue = if (visible) 1f else 0f,
        animationSpec = tween(320),
        label = "premium_celebration_sparkles_alpha"
    )
    val scale by animateFloatAsState(
        targetValue = if (visible) 1f else 0.72f,
        animationSpec = spring(dampingRatio = 0.56f, stiffness = 360f),
        label = "premium_celebration_sparkles_scale"
    )

    Canvas(
        modifier = modifier.graphicsLayer {
            this.alpha = alpha
            scaleX = scale
            scaleY = scale
        }
    ) {
        val centers = listOf(
            Offset(size.width * 0.18f, size.height * 0.22f),
            Offset(size.width * 0.78f, size.height * 0.16f),
            Offset(size.width * 0.86f, size.height * 0.66f)
        )
        centers.forEachIndexed { index, center ->
            val radius = (if (index == 1) 5.dp else 4.dp).toPx()
            drawLine(
                color = if (index % 2 == 0) TukiGold else TukiTeal,
                start = Offset(center.x - radius, center.y),
                end = Offset(center.x + radius, center.y),
                strokeWidth = 2.dp.toPx(),
                cap = StrokeCap.Round
            )
            drawLine(
                color = if (index % 2 == 0) TukiGold else TukiTeal,
                start = Offset(center.x, center.y - radius),
                end = Offset(center.x, center.y + radius),
                strokeWidth = 2.dp.toPx(),
                cap = StrokeCap.Round
            )
        }
    }
}

@Composable
private fun PremiumJourneyModeIcon(
    mode: PremiumJourneyMode,
    modifier: Modifier = Modifier
) {
    Canvas(modifier = modifier) {
        val w = size.width
        val h = size.height
        val teal = TukiTeal
        val orange = TukiOrange
        val ink = TukiDeepTeal

        when (mode) {
            PremiumJourneyMode.WALK -> {
                drawCircle(orange, w * 0.10f, Offset(w * 0.50f, h * 0.18f))
                drawLine(teal, Offset(w * 0.50f, h * 0.30f), Offset(w * 0.46f, h * 0.58f), w * 0.07f, StrokeCap.Round)
                drawLine(teal, Offset(w * 0.47f, h * 0.40f), Offset(w * 0.30f, h * 0.52f), w * 0.06f, StrokeCap.Round)
                drawLine(teal, Offset(w * 0.48f, h * 0.42f), Offset(w * 0.66f, h * 0.52f), w * 0.06f, StrokeCap.Round)
                drawLine(ink, Offset(w * 0.46f, h * 0.58f), Offset(w * 0.31f, h * 0.83f), w * 0.065f, StrokeCap.Round)
                drawLine(ink, Offset(w * 0.46f, h * 0.58f), Offset(w * 0.65f, h * 0.80f), w * 0.065f, StrokeCap.Round)
            }

            PremiumJourneyMode.TRICYCLE -> {
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

            PremiumJourneyMode.JEEPNEY -> {
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

            PremiumJourneyMode.DESTINATION -> {
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

@Composable
private fun PremiumBellIcon(modifier: Modifier = Modifier) {
    val infiniteTransition = rememberInfiniteTransition(label = "premium_bell_motion")
    val rotation by infiniteTransition.animateFloat(
        initialValue = -10f,
        targetValue = 10f,
        animationSpec = infiniteRepeatable(tween(155), RepeatMode.Reverse),
        label = "premium_bell_rotation"
    )

    Canvas(modifier = modifier.graphicsLayer { rotationZ = rotation }) {
        val w = size.width
        val h = size.height
        drawRoundRect(
            color = TukiOrange,
            topLeft = Offset(w * 0.25f, h * 0.24f),
            size = Size(w * 0.50f, h * 0.48f),
            cornerRadius = CornerRadius(w * 0.22f, w * 0.22f)
        )
        drawLine(
            color = TukiOrange,
            start = Offset(w * 0.18f, h * 0.72f),
            end = Offset(w * 0.82f, h * 0.72f),
            strokeWidth = w * 0.10f,
            cap = StrokeCap.Round
        )
        drawCircle(
            color = TukiDeepTeal,
            radius = w * 0.075f,
            center = Offset(w * 0.50f, h * 0.82f)
        )
    }
}
