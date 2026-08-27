package com.example.frontend.screens.onboarding

import androidx.compose.animation.AnimatedVisibility
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
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
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

private data class RoutePreview(
    val label: String,
    val detail: String,
    val badge: String,
    val accent: Color
)

private enum class JourneyMode {
    WALK,
    TRICYCLE,
    JEEPNEY,
    DESTINATION
}

@Composable
fun RouteChoicePage(active: Boolean) {
    OnboardingPageShell {
        MascotHero(
            active = active,
            mood = TukiMascotMood.WELCOME,
            speech = "Hi! I’m TUKI — your travel buddy.",
            enterFromX = -70f
        )

        PageTitle(
            title = "Find the best route for you",
            subtitle = "Compare the fastest, cheapest, and balanced route based on your trip."
        )

        Spacer(modifier = Modifier.height(18.dp))
        RouteChoiceStack(active = active)
    }
}

@Composable
private fun RouteChoiceStack(active: Boolean) {
    val routes = remember {
        listOf(
            RoutePreview("FASTEST", "22 min • Tricycle → Jeepney", "₱65", Color(0xFFE95E58)),
            RoutePreview("CHEAPEST", "43 min • Walk → 2 Jeepneys", "₱26", Color(0xFF4EBF83)),
            RoutePreview("BALANCED", "29 min • Walk → Tricycle → Jeepney", "₱42", TukiGold)
        )
    }
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            delay(160)
            routes.indices.forEach { index ->
                stage = index + 1
                delay(145)
            }
        }
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .widthIn(max = 520.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        routes.forEachIndexed { index, route ->
            AnimatedVisibility(
                visible = stage > index,
                enter = fadeIn(tween(240)) + slideInHorizontally(
                    animationSpec = spring(dampingRatio = 0.78f, stiffness = 330f),
                    initialOffsetX = { if (index % 2 == 0) it / 4 else -it / 4 }
                )
            ) {
                RouteChoiceCard(route)
            }
        }
    }
}

@Composable
private fun RouteChoiceCard(route: RoutePreview) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = route.accent.copy(alpha = 0.12f),
        shape = RoundedCornerShape(22.dp),
        border = BorderStroke(1.dp, route.accent.copy(alpha = 0.20f))
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(route.accent.copy(alpha = 0.18f), CircleShape),
                contentAlignment = Alignment.Center
            ) {
                Box(
                    modifier = Modifier
                        .size(11.dp)
                        .background(route.accent, CircleShape)
                )
            }
            Spacer(modifier = Modifier.width(12.dp))
            Column(modifier = Modifier.fillMaxWidth(0.72f)) {
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
            Spacer(modifier = Modifier.width(8.dp))
            Text(
                text = route.badge,
                color = TukiDeepTeal,
                style = MaterialTheme.typography.titleMedium
            )
        }
    }
}

@Composable
fun StepByStepPage(active: Boolean) {
    OnboardingPageShell {
        PageTitle(
            title = "Follow your trip step by step",
            subtitle = "TUKI guides you through walking, tricycles, jeepneys, and transfers."
        )

        Spacer(modifier = Modifier.height(12.dp))
        JourneyStory(active = active)

        Spacer(modifier = Modifier.height(14.dp))
        Surface(
            modifier = Modifier.fillMaxWidth(),
            color = TukiTeal.copy(alpha = 0.07f),
            shape = RoundedCornerShape(22.dp)
        ) {
            Row(
                modifier = Modifier.padding(16.dp),
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
                        text = "TUKI keeps every transfer clear so you always know what comes next.",
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }
    }
}

@Composable
private fun JourneyStory(active: Boolean) {
    val modes = remember {
        listOf(
            JourneyMode.WALK to "WALK",
            JourneyMode.TRICYCLE to "TRICYCLE",
            JourneyMode.JEEPNEY to "JEEPNEY",
            JourneyMode.DESTINATION to "DESTINATION"
        )
    }
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            modes.indices.forEach { index ->
                delay(if (index == 0) 170 else 220)
                stage = index + 1
            }
        }
    }

    val routeProgress by animateFloatAsState(
        targetValue = if (active) stage / modes.size.toFloat() else 0f,
        animationSpec = tween(320),
        label = "journey_route_progress"
    )

    BoxWithConstraints(
        modifier = Modifier
            .fillMaxWidth()
            .height(300.dp)
    ) {
        val routeX = maxWidth * 0.34f
        val cardWidth = maxWidth * 0.58f

        Canvas(modifier = Modifier.fillMaxSize()) {
            val points = listOf(
                Offset(size.width * 0.34f, size.height * 0.12f),
                Offset(size.width * 0.29f, size.height * 0.36f),
                Offset(size.width * 0.36f, size.height * 0.61f),
                Offset(size.width * 0.31f, size.height * 0.86f)
            )

            points.zipWithNext().forEach { (start, end) ->
                drawLine(
                    color = TukiTeal.copy(alpha = 0.13f),
                    start = start,
                    end = end,
                    strokeWidth = 5.dp.toPx(),
                    cap = StrokeCap.Round
                )
            }

            val segmentCount = points.size - 1
            val scaledProgress = routeProgress * segmentCount
            points.zipWithNext().forEachIndexed { index, (start, end) ->
                val local = (scaledProgress - index).coerceIn(0f, 1f)
                if (local > 0f) {
                    drawLine(
                        color = TukiTeal,
                        start = start,
                        end = Offset(
                            x = start.x + ((end.x - start.x) * local),
                            y = start.y + ((end.y - start.y) * local)
                        ),
                        strokeWidth = 5.dp.toPx(),
                        cap = StrokeCap.Round
                    )
                }
            }

            points.forEachIndexed { index, point ->
                drawCircle(
                    color = if (stage > index) TukiOrange else TukiTeal.copy(alpha = 0.22f),
                    radius = 6.dp.toPx(),
                    center = point
                )
                drawCircle(
                    color = Color.White,
                    radius = 2.5.dp.toPx(),
                    center = point
                )
            }
        }

        SceneMascot(
            active = active,
            mood = TukiMascotMood.GUIDE,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(y = 42.dp)
                .size(150.dp),
            enterFromX = -90f
        )

        Column(
            modifier = Modifier
                .align(Alignment.CenterEnd)
                .width(cardWidth),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            modes.forEachIndexed { index, (mode, label) ->
                AnimatedVisibility(
                    visible = stage > index,
                    enter = fadeIn(tween(200)) + slideInHorizontally(
                        animationSpec = spring(dampingRatio = 0.82f, stiffness = 360f),
                        initialOffsetX = { it / 3 }
                    )
                ) {
                    JourneyStopCard(mode = mode, label = label, active = stage > index)
                }
            }
        }

        Box(
            modifier = Modifier
                .offset(x = routeX - 3.dp, y = 22.dp)
                .size(6.dp)
                .background(TukiTeal, CircleShape)
        )
    }
}

@Composable
private fun JourneyStopCard(
    mode: JourneyMode,
    label: String,
    active: Boolean
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color.White,
        shape = RoundedCornerShape(18.dp),
        shadowElevation = 2.dp,
        border = BorderStroke(
            1.dp,
            if (active) TukiTeal.copy(alpha = 0.12f) else TukiInk.copy(alpha = 0.06f)
        )
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            JourneyModeIcon(
                mode = mode,
                modifier = Modifier.size(28.dp)
            )
            Spacer(modifier = Modifier.width(10.dp))
            Text(
                text = label,
                color = TukiInk,
                style = MaterialTheme.typography.labelLarge
            )
        }
    }
}

@Composable
fun ParaPoPage(active: Boolean) {
    OnboardingPageShell {
        PageTitle(
            title = "Never miss your stop",
            subtitle = "Get notified when you’re close to your drop-off point so you know when to get off."
        )

        Spacer(modifier = Modifier.height(8.dp))
        ParaPoHero(active = active)
        Spacer(modifier = Modifier.height(10.dp))
        ParaPoDemo(active = active)
    }
}

@Composable
private fun ParaPoHero(active: Boolean) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(165.dp)
    ) {
        SceneMascot(
            active = active,
            mood = TukiMascotMood.ALERT,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .offset(x = (-34).dp)
                .size(165.dp),
            enterFromX = -60f
        )

        AnimatedVisibility(
            visible = active,
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(top = 12.dp, end = 8.dp),
            enter = fadeIn(tween(180)) + scaleIn(
                initialScale = 0.72f,
                animationSpec = spring(dampingRatio = 0.58f, stiffness = 420f)
            )
        ) {
            Surface(
                color = Color(0xFFFF6D57),
                shape = RoundedCornerShape(22.dp, 22.dp, 22.dp, 7.dp),
                shadowElevation = 5.dp
            ) {
                Column(modifier = Modifier.padding(horizontal = 15.dp, vertical = 10.dp)) {
                    Text(
                        text = "PARA PO!",
                        color = Color.White,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = "Your stop is near!",
                        color = Color.White.copy(alpha = 0.92f),
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }
    }
}

@Composable
private fun ParaPoDemo(active: Boolean) {
    val infiniteTransition = rememberInfiniteTransition(label = "para_po_demo")
    val animatedProgress by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(3600, easing = LinearEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "demo_vehicle_progress"
    )
    val pulse by infiniteTransition.animateFloat(
        initialValue = 0.88f,
        targetValue = 1.25f,
        animationSpec = infiniteRepeatable(
            animation = tween(620),
            repeatMode = RepeatMode.Reverse
        ),
        label = "destination_pulse"
    )
    val progress = if (active) animatedProgress else 0f
    val alertVisible = active && progress > 0.70f

    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color.White,
        shape = RoundedCornerShape(26.dp),
        shadowElevation = 3.dp
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            BoxWithConstraints(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(126.dp)
            ) {
                val vehicleSize = 44.dp
                val startPadding = 18.dp
                val endPadding = 24.dp
                val travelWidth = (maxWidth - vehicleSize - startPadding - endPadding).coerceAtLeast(0.dp)

                Canvas(modifier = Modifier.fillMaxSize()) {
                    val startX = 18.dp.toPx()
                    val endX = size.width - 18.dp.toPx()
                    val trackY = 58.dp.toPx()

                    drawLine(
                        color = TukiTeal.copy(alpha = 0.16f),
                        start = Offset(startX, trackY),
                        end = Offset(endX, trackY),
                        strokeWidth = 6.dp.toPx(),
                        cap = StrokeCap.Round
                    )
                    drawLine(
                        color = TukiOrange,
                        start = Offset(startX, trackY),
                        end = Offset(startX + ((endX - startX) * progress), trackY),
                        strokeWidth = 6.dp.toPx(),
                        cap = StrokeCap.Round
                    )

                    listOf(0f, 0.5f, 1f).forEachIndexed { index, point ->
                        val x = startX + ((endX - startX) * point)
                        val reached = progress >= point
                        drawCircle(
                            color = if (reached) TukiOrange else TukiTeal,
                            radius = if (index == 2) 7.dp.toPx() else 5.5.dp.toPx(),
                            center = Offset(x, trackY)
                        )
                    }

                    val pulseRadius = 18.dp.toPx() * pulse
                    drawCircle(
                        color = Color(0xFFFF6D57).copy(alpha = if (alertVisible) 0.14f else 0.05f),
                        radius = pulseRadius,
                        center = Offset(endX, trackY)
                    )
                }

                Surface(
                    modifier = Modifier
                        .offset(
                            x = startPadding + (travelWidth * progress),
                            y = 36.dp
                        )
                        .size(vehicleSize),
                    color = Color.White,
                    shape = CircleShape,
                    shadowElevation = 3.dp,
                    border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.16f))
                ) {
                    JourneyModeIcon(
                        mode = JourneyMode.JEEPNEY,
                        modifier = Modifier.padding(8.dp)
                    )
                }

                Text(
                    text = "AUF",
                    modifier = Modifier.align(Alignment.BottomStart),
                    color = TukiInk,
                    style = MaterialTheme.typography.labelSmall
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
                enter = fadeIn(tween(180)) + slideInVertically(initialOffsetY = { it / 3 }),
                exit = fadeOut(tween(140))
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    color = TukiOrange.copy(alpha = 0.10f),
                    shape = RoundedCornerShape(18.dp),
                    border = BorderStroke(1.dp, TukiOrange.copy(alpha = 0.22f))
                ) {
                    Row(
                        modifier = Modifier.padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        BellIcon(
                            active = alertVisible,
                            modifier = Modifier.size(28.dp)
                        )
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "Vibration alert",
                                color = TukiInk,
                                style = MaterialTheme.typography.labelLarge
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
fun AskTukiPage(active: Boolean) {
    OnboardingPageShell {
        PageTitle(
            title = "Travel within your budget",
            subtitle = "Tell TUKI where you’re going and how much you want to spend."
        )

        Spacer(modifier = Modifier.height(10.dp))
        BudgetChatExperience(active = active)
    }
}

@Composable
private fun BudgetChatExperience(active: Boolean) {
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            delay(220)
            stage = 1
            delay(520)
            stage = 2
            delay(520)
            stage = 3
            delay(360)
            stage = 4
        }
    }

    BoxWithConstraints(
        modifier = Modifier
            .fillMaxWidth()
            .height(360.dp)
    ) {
        val chatWidth = maxWidth * 0.68f

        SceneMascot(
            active = active,
            mood = if (stage >= 3) TukiMascotMood.CELEBRATE else TukiMascotMood.THINKING,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(y = 26.dp)
                .size(168.dp),
            enterFromX = -80f
        )

        Column(
            modifier = Modifier
                .align(Alignment.TopEnd)
                .width(chatWidth),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            AnimatedVisibility(
                visible = stage >= 1,
                enter = fadeIn(tween(220)) + slideInHorizontally(initialOffsetX = { it / 4 })
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
                exit = fadeOut(tween(120))
            ) {
                ThinkingDots()
            }

            AnimatedVisibility(
                visible = stage >= 3,
                enter = fadeIn(tween(220)) + slideInHorizontally(initialOffsetX = { -it / 4 })
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
                enter = fadeIn(tween(260)) + slideInVertically(initialOffsetY = { it / 3 })
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    color = Color.White,
                    shape = RoundedCornerShape(20.dp),
                    shadowElevation = 3.dp,
                    border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.10f))
                ) {
                    Column(modifier = Modifier.padding(14.dp)) {
                        Text(
                            text = "BALANCED ROUTE",
                            color = TukiDeepTeal,
                            style = MaterialTheme.typography.labelSmall,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = "₱42  •  29 min",
                            color = TukiInk,
                            style = MaterialTheme.typography.titleLarge
                        )
                        Spacer(modifier = Modifier.height(9.dp))
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(7.dp)
                        ) {
                            JourneyModeIcon(JourneyMode.WALK, Modifier.size(20.dp))
                            Text("→", color = TukiMuted)
                            JourneyModeIcon(JourneyMode.TRICYCLE, Modifier.size(20.dp))
                            Text("→", color = TukiMuted)
                            JourneyModeIcon(JourneyMode.JEEPNEY, Modifier.size(20.dp))
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun ThinkingDots() {
    val infiniteTransition = rememberInfiniteTransition(label = "tuki_thinking_dots")
    val pulse1 by infiniteTransition.animateFloat(
        initialValue = 0.55f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(tween(480), RepeatMode.Reverse),
        label = "thinking_dot_1"
    )
    val pulse2 by infiniteTransition.animateFloat(
        initialValue = 1f,
        targetValue = 0.55f,
        animationSpec = infiniteRepeatable(tween(480), RepeatMode.Reverse),
        label = "thinking_dot_2"
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
private fun OnboardingPageShell(content: @Composable () -> Unit) {
    Box(modifier = Modifier.fillMaxSize()) {
        OnboardingDecor()
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 26.dp, vertical = 8.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            content()
            Spacer(modifier = Modifier.height(12.dp))
        }
    }
}

@Composable
private fun OnboardingDecor() {
    Canvas(modifier = Modifier.fillMaxSize()) {
        val minDimension = min(size.width, size.height)
        drawCircle(
            color = TukiTeal.copy(alpha = 0.035f),
            radius = minDimension * 0.22f,
            center = Offset(-size.width * 0.02f, size.height * 0.26f)
        )
        drawCircle(
            color = TukiGold.copy(alpha = 0.045f),
            radius = minDimension * 0.18f,
            center = Offset(size.width * 1.03f, size.height * 0.58f)
        )
        drawCircle(
            color = TukiOrange.copy(alpha = 0.028f),
            radius = minDimension * 0.14f,
            center = Offset(size.width * 0.22f, size.height * 0.92f)
        )
    }
}

@Composable
private fun MascotHero(
    active: Boolean,
    mood: TukiMascotMood,
    speech: String,
    enterFromX: Float
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(190.dp)
    ) {
        SceneMascot(
            active = active,
            mood = mood,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(x = (-10).dp, y = 10.dp)
                .size(185.dp),
            enterFromX = enterFromX
        )

        AnimatedVisibility(
            visible = active,
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(top = 8.dp),
            enter = fadeIn(tween(240)) + scaleIn(
                initialScale = 0.88f,
                animationSpec = spring(dampingRatio = 0.68f, stiffness = 360f)
            )
        ) {
            Surface(
                modifier = Modifier.widthIn(max = 188.dp),
                color = Color.White,
                shape = RoundedCornerShape(21.dp, 21.dp, 21.dp, 7.dp),
                shadowElevation = 3.dp
            ) {
                Text(
                    text = speech,
                    modifier = Modifier.padding(14.dp),
                    color = TukiInk,
                    style = MaterialTheme.typography.bodySmall
                )
            }
        }
    }
}

@Composable
private fun SceneMascot(
    active: Boolean,
    mood: TukiMascotMood,
    modifier: Modifier,
    enterFromX: Float
) {
    val translationX by animateFloatAsState(
        targetValue = if (active) 0f else enterFromX,
        animationSpec = spring(dampingRatio = 0.72f, stiffness = 250f),
        label = "scene_mascot_entry_${mood.name}"
    )
    val alpha by animateFloatAsState(
        targetValue = if (active) 1f else 0f,
        animationSpec = tween(220),
        label = "scene_mascot_alpha_${mood.name}"
    )
    val scale by animateFloatAsState(
        targetValue = if (active) 1f else 0.88f,
        animationSpec = spring(dampingRatio = 0.72f, stiffness = 260f),
        label = "scene_mascot_scale_${mood.name}"
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
private fun PageTitle(
    title: String,
    subtitle: String
) {
    Text(
        text = title,
        color = TukiInk,
        style = MaterialTheme.typography.displaySmall,
        textAlign = TextAlign.Center
    )
    Spacer(modifier = Modifier.height(6.dp))
    Text(
        text = subtitle,
        color = TukiMuted,
        style = MaterialTheme.typography.bodyMedium,
        textAlign = TextAlign.Center
    )
}

@Composable
private fun JourneyModeIcon(
    mode: JourneyMode,
    modifier: Modifier = Modifier
) {
    Canvas(modifier = modifier) {
        val w = size.width
        val h = size.height
        val teal = TukiTeal
        val orange = TukiOrange
        val ink = TukiDeepTeal

        when (mode) {
            JourneyMode.WALK -> {
                drawCircle(
                    color = orange,
                    radius = w * 0.10f,
                    center = Offset(w * 0.50f, h * 0.18f)
                )
                drawLine(teal, Offset(w * 0.50f, h * 0.30f), Offset(w * 0.46f, h * 0.58f), w * 0.07f, StrokeCap.Round)
                drawLine(teal, Offset(w * 0.47f, h * 0.40f), Offset(w * 0.30f, h * 0.52f), w * 0.06f, StrokeCap.Round)
                drawLine(teal, Offset(w * 0.48f, h * 0.42f), Offset(w * 0.66f, h * 0.52f), w * 0.06f, StrokeCap.Round)
                drawLine(ink, Offset(w * 0.46f, h * 0.58f), Offset(w * 0.31f, h * 0.83f), w * 0.065f, StrokeCap.Round)
                drawLine(ink, Offset(w * 0.46f, h * 0.58f), Offset(w * 0.65f, h * 0.80f), w * 0.065f, StrokeCap.Round)
            }

            JourneyMode.TRICYCLE -> {
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

            JourneyMode.JEEPNEY -> {
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

            JourneyMode.DESTINATION -> {
                drawCircle(
                    color = orange.copy(alpha = 0.18f),
                    radius = w * 0.28f,
                    center = Offset(w * 0.50f, h * 0.42f)
                )
                drawCircle(
                    color = orange,
                    radius = w * 0.17f,
                    center = Offset(w * 0.50f, h * 0.38f)
                )
                drawCircle(
                    color = Color.White,
                    radius = w * 0.065f,
                    center = Offset(w * 0.50f, h * 0.38f)
                )
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
private fun BellIcon(
    active: Boolean,
    modifier: Modifier = Modifier
) {
    val infiniteTransition = rememberInfiniteTransition(label = "bell_motion")
    val rotation by infiniteTransition.animateFloat(
        initialValue = if (active) -8f else 0f,
        targetValue = if (active) 8f else 0f,
        animationSpec = infiniteRepeatable(tween(180), RepeatMode.Reverse),
        label = "bell_rotation"
    )

    Canvas(
        modifier = modifier.graphicsLayer {
            rotationZ = rotation
        }
    ) {
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
