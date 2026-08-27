package com.example.frontend.screens.onboarding

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
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
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
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

private data class RoutePreview(
    val label: String,
    val detail: String,
    val badge: String,
    val accent: Color
)

@Composable
fun RouteChoicePage(active: Boolean) {
    OnboardingPageShell {
        MascotHero(
            active = active,
            mood = TukiMascotMood.WELCOME,
            alignEnd = false,
            speech = "Hi! I’ll help you find the ride that fits your day."
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
            delay(180)
            repeat(routes.size) { index ->
                stage = index + 1
                delay(150)
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
                    animationSpec = tween(340),
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
        border = BorderStroke(1.dp, route.accent.copy(alpha = 0.18f))
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

        Spacer(modifier = Modifier.height(16.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            TukiMascot(
                mood = TukiMascotMood.GUIDE,
                modifier = Modifier.size(138.dp),
                showHalo = false
            )
            Spacer(modifier = Modifier.width(12.dp))
            JourneySteps(active = active)
        }

        Spacer(modifier = Modifier.height(18.dp))

        Surface(
            modifier = Modifier.fillMaxWidth(),
            color = TukiTeal.copy(alpha = 0.07f),
            shape = RoundedCornerShape(22.dp)
        ) {
            Row(
                modifier = Modifier.padding(16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("●", color = TukiTeal, fontSize = 20.sp)
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
private fun JourneySteps(active: Boolean) {
    val items = listOf(
        "WALK" to "🚶",
        "TRICYCLE" to "🛺",
        "JEEPNEY" to "🚌",
        "DESTINATION" to "📍"
    )
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            items.indices.forEach { index ->
                delay(if (index == 0) 120 else 170)
                stage = index + 1
            }
        }
    }

    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        items.forEachIndexed { index, item ->
            AnimatedVisibility(
                visible = stage > index,
                enter = fadeIn(tween(220)) + slideInHorizontally(initialOffsetX = { it / 3 })
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    color = Color.White,
                    shape = RoundedCornerShape(18.dp),
                    shadowElevation = 2.dp
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(item.second, fontSize = 20.sp)
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(
                            text = item.first,
                            color = TukiInk,
                            style = MaterialTheme.typography.labelLarge
                        )
                    }
                }
            }
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

        Spacer(modifier = Modifier.height(12.dp))

        Box(
            modifier = Modifier.fillMaxWidth(),
            contentAlignment = Alignment.Center
        ) {
            TukiMascot(
                mood = TukiMascotMood.ALERT,
                modifier = Modifier.size(150.dp),
                showHalo = false
            )
            Surface(
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .padding(top = 8.dp, end = 10.dp),
                color = Color(0xFFFF6D57),
                shape = RoundedCornerShape(20.dp, 20.dp, 20.dp, 6.dp),
                shadowElevation = 4.dp
            ) {
                Column(modifier = Modifier.padding(horizontal = 14.dp, vertical = 10.dp)) {
                    Text(
                        text = "PARA PO!",
                        color = Color.White,
                        style = MaterialTheme.typography.titleMedium
                    )
                    Text(
                        text = "Your stop is near!",
                        color = Color.White.copy(alpha = 0.90f),
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(12.dp))
        ParaPoDemo(active = active)
    }
}

@Composable
private fun ParaPoDemo(active: Boolean) {
    val infiniteTransition = rememberInfiniteTransition(label = "para_po_demo")
    val animatedProgress by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(3300, easing = LinearEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "demo_vehicle_progress"
    )
    val progress = if (active) animatedProgress else 0f
    val alertVisible = active && progress > 0.70f

    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color.White,
        shape = RoundedCornerShape(24.dp),
        shadowElevation = 2.dp
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            BoxWithConstraints(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(92.dp)
            ) {
                val markerWidth = 52.dp
                val travelWidth = (maxWidth - markerWidth - 24.dp).coerceAtLeast(0.dp)

                Canvas(modifier = Modifier.fillMaxSize()) {
                    val startX = 16.dp.toPx()
                    val endX = size.width - 16.dp.toPx()
                    val trackY = 40.dp.toPx()

                    drawLine(
                        color = TukiTeal.copy(alpha = 0.18f),
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
                    listOf(0f, 0.5f, 1f).forEach { point ->
                        val x = startX + ((endX - startX) * point)
                        drawCircle(
                            color = if (progress >= point) TukiOrange else TukiTeal,
                            radius = 6.dp.toPx(),
                            center = Offset(x, trackY)
                        )
                    }
                }

                Surface(
                    modifier = Modifier
                        .offset(
                            x = 12.dp + (travelWidth * progress),
                            y = 23.dp
                        )
                        .width(markerWidth)
                        .height(34.dp),
                    color = TukiDeepTeal,
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text("JEEP", color = Color.White, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }

            AnimatedVisibility(
                visible = alertVisible,
                enter = fadeIn(tween(180)) + scaleIn(initialScale = 0.90f),
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
                        Text("🔔", fontSize = 20.sp)
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "Vibration alert",
                                color = TukiInk,
                                style = MaterialTheme.typography.labelLarge
                            )
                            Text(
                                text = "We’ll gently alert you so you have time to prepare.",
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
            title = "Just ask by budget",
            subtitle = "Tell TUKI where you’re going and how much you want to spend."
        )

        Spacer(modifier = Modifier.height(8.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            TukiMascot(
                mood = if (active) TukiMascotMood.THINKING else TukiMascotMood.WELCOME,
                modifier = Modifier.size(132.dp),
                showHalo = false
            )
            Spacer(modifier = Modifier.width(10.dp))
            ChatPreview(active = active)
        }
    }
}

@Composable
private fun ChatPreview(active: Boolean) {
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            delay(180)
            stage = 1
            delay(480)
            stage = 2
            delay(360)
            stage = 3
        }
    }

    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        AnimatedVisibility(
            visible = stage >= 1,
            enter = fadeIn(tween(220)) + slideInHorizontally(initialOffsetX = { it / 4 })
        ) {
            Surface(
                color = Color(0xFFDCEEFF),
                shape = RoundedCornerShape(20.dp, 20.dp, 6.dp, 20.dp)
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
            visible = stage >= 2,
            enter = fadeIn(tween(220)) + slideInHorizontally(initialOffsetX = { -it / 4 })
        ) {
            Surface(
                color = Color(0xFFDDF4E8),
                shape = RoundedCornerShape(6.dp, 20.dp, 20.dp, 20.dp)
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
            visible = stage >= 3,
            enter = fadeIn(tween(260)) + slideInVertically(initialOffsetY = { it / 3 })
        ) {
            Surface(
                modifier = Modifier.fillMaxWidth(),
                color = Color.White,
                shape = RoundedCornerShape(20.dp),
                shadowElevation = 2.dp
            ) {
                Column(modifier = Modifier.padding(14.dp)) {
                    Text(
                        text = "BALANCED ROUTE",
                        color = TukiDeepTeal,
                        style = MaterialTheme.typography.labelSmall
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "₱42  •  29 min",
                        color = TukiInk,
                        style = MaterialTheme.typography.titleLarge
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "Walk  →  Tricycle  →  Jeepney",
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }
    }
}

@Composable
private fun OnboardingPageShell(content: @Composable () -> Unit) {
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

@Composable
private fun MascotHero(
    active: Boolean,
    mood: TukiMascotMood,
    alignEnd: Boolean,
    speech: String
) {
    val alignment = if (alignEnd) Alignment.CenterEnd else Alignment.CenterStart
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(180.dp)
    ) {
        TukiMascot(
            mood = mood,
            modifier = Modifier
                .align(alignment)
                .size(165.dp),
            showHalo = false
        )

        AnimatedVisibility(
            visible = active,
            modifier = Modifier
                .align(if (alignEnd) Alignment.TopStart else Alignment.TopEnd),
            enter = fadeIn(tween(260)) + scaleIn(initialScale = 0.92f)
        ) {
            Surface(
                modifier = Modifier.widthIn(max = 180.dp),
                color = Color.White,
                shape = RoundedCornerShape(20.dp, 20.dp, 20.dp, 6.dp),
                shadowElevation = 3.dp
            ) {
                Text(
                    text = speech,
                    modifier = Modifier.padding(13.dp),
                    color = TukiInk,
                    style = MaterialTheme.typography.bodySmall
                )
            }
        }
    }
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
