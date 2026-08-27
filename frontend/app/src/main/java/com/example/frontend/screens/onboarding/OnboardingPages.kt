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
import androidx.compose.foundation.Image
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
import androidx.compose.foundation.layout.weight
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
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R
import com.example.frontend.ui.motion.TukiMascot
import com.example.frontend.ui.motion.TukiMascotMood
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiInk
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
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 26.dp, vertical = 4.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        TukiMascot(
            mood = TukiMascotMood.GUIDE,
            modifier = Modifier.size(136.dp)
        )

        Text(
            text = "Three routes. One smarter choice.",
            color = Color.White,
            style = MaterialTheme.typography.displayMedium,
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = "Compare the fastest, cheapest, and balanced way to get there — then TUKI guides every leg.",
            color = Color.White.copy(alpha = 0.78f),
            style = MaterialTheme.typography.bodyLarge,
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.height(22.dp))

        RouteChoiceStack(active = active)
        Spacer(modifier = Modifier.height(8.dp))
    }
}

@Composable
private fun RouteChoiceStack(active: Boolean) {
    val routes = remember {
        listOf(
            RoutePreview("FASTEST", "22 min • Trike → Jeepney", "₱65", TukiOrange),
            RoutePreview("CHEAPEST", "43 min • Walk → 2 Jeepneys", "₱26", TukiGold),
            RoutePreview("BALANCED", "29 min • Walk → Trike → Jeepney", "₱42", Color(0xFF62D5D7))
        )
    }
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            delay(160)
            stage = 1
            delay(130)
            stage = 2
            delay(130)
            stage = 3
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
                enter = fadeIn(tween(280)) + slideInHorizontally(
                    animationSpec = tween(360),
                    initialOffsetX = { if (index % 2 == 0) it / 3 else -it / 3 }
                )
            ) {
                RouteChoiceCard(route = route, featured = index == 2)
            }
        }
    }
}

@Composable
private fun RouteChoiceCard(route: RoutePreview, featured: Boolean) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color.White.copy(alpha = if (featured) 1f else 0.94f),
        shape = RoundedCornerShape(22.dp),
        shadowElevation = if (featured) 10.dp else 5.dp,
        border = if (featured) BorderStroke(2.dp, route.accent.copy(alpha = 0.75f)) else null
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(42.dp)
                    .background(route.accent.copy(alpha = 0.14f), CircleShape),
                contentAlignment = Alignment.Center
            ) {
                Box(
                    modifier = Modifier
                        .size(if (featured) 14.dp else 11.dp)
                        .background(route.accent, CircleShape)
                )
            }

            Spacer(modifier = Modifier.width(13.dp))

            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = route.label,
                    color = TukiInk,
                    style = MaterialTheme.typography.labelSmall,
                    fontWeight = FontWeight.Bold
                )
                Spacer(modifier = Modifier.height(3.dp))
                Text(
                    text = route.detail,
                    color = TukiInk.copy(alpha = 0.68f),
                    style = MaterialTheme.typography.bodySmall
                )
            }

            Text(
                text = route.badge,
                color = TukiDeepTeal,
                style = MaterialTheme.typography.titleMedium
            )
        }
    }
}

@Composable
fun ParaPoPage(active: Boolean) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 26.dp, vertical = 4.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        TukiMascot(
            mood = TukiMascotMood.ALERT,
            modifier = Modifier.size(116.dp)
        )

        Text(
            text = "Never miss your stop.",
            color = Color.White,
            style = MaterialTheme.typography.displayMedium,
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = "Live trip guidance follows your current leg and gives you a clear heads-up before it is time to say “Para po!”.",
            color = Color.White.copy(alpha = 0.78f),
            style = MaterialTheme.typography.bodyLarge,
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.height(24.dp))

        ParaPoDemo(active = active)
        Spacer(modifier = Modifier.height(10.dp))
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
        modifier = Modifier
            .fillMaxWidth()
            .widthIn(max = 540.dp),
        color = Color.White.copy(alpha = 0.96f),
        shape = RoundedCornerShape(28.dp),
        shadowElevation = 10.dp
    ) {
        Column(modifier = Modifier.padding(18.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = "LIVE TRIP",
                        color = TukiTeal,
                        style = MaterialTheme.typography.labelSmall
                    )
                    Text(
                        text = "AUF → SM City Clark",
                        color = TukiInk,
                        style = MaterialTheme.typography.titleMedium
                    )
                }
                Surface(
                    color = TukiTeal.copy(alpha = 0.12f),
                    shape = RoundedCornerShape(100.dp)
                ) {
                    Text(
                        text = "●  LIVE",
                        color = TukiDeepTeal,
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                        style = MaterialTheme.typography.labelSmall
                    )
                }
            }

            Spacer(modifier = Modifier.height(14.dp))

            BoxWithConstraints(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(126.dp)
            ) {
                val horizontalPadding = 22.dp
                val markerWidth = 58.dp
                val travelWidth = (maxWidth - markerWidth - horizontalPadding).coerceAtLeast(0.dp)

                Canvas(modifier = Modifier.fillMaxSize()) {
                    val startX = 18.dp.toPx()
                    val endX = size.width - 18.dp.toPx()
                    val trackY = 57.dp.toPx()

                    drawLine(
                        color = TukiTeal.copy(alpha = 0.18f),
                        start = Offset(startX, trackY),
                        end = Offset(endX, trackY),
                        strokeWidth = 8.dp.toPx(),
                        cap = StrokeCap.Round
                    )
                    drawLine(
                        color = TukiOrange,
                        start = Offset(startX, trackY),
                        end = Offset(
                            x = startX + ((endX - startX) * progress),
                            y = trackY
                        ),
                        strokeWidth = 8.dp.toPx(),
                        cap = StrokeCap.Round
                    )

                    listOf(0f, 0.5f, 1f).forEachIndexed { index, stopProgress ->
                        val x = startX + ((endX - startX) * stopProgress)
                        drawCircle(
                            color = if (progress >= stopProgress) TukiOrange else TukiTeal,
                            radius = if (index == 2) 8.dp.toPx() else 6.dp.toPx(),
                            center = Offset(x, trackY)
                        )
                        drawCircle(
                            color = Color.White,
                            radius = if (index == 2) 3.dp.toPx() else 2.dp.toPx(),
                            center = Offset(x, trackY)
                        )
                    }
                }

                Surface(
                    modifier = Modifier
                        .offset(
                            x = horizontalPadding / 2 + (travelWidth * progress),
                            y = 40.dp
                        )
                        .width(markerWidth)
                        .height(34.dp),
                    color = TukiDeepTeal,
                    shape = RoundedCornerShape(11.dp),
                    shadowElevation = 6.dp
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text(
                            text = "JEEP",
                            color = Color.White,
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }

                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .align(Alignment.BottomCenter),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text("AUF", color = TukiInk.copy(alpha = 0.64f), style = MaterialTheme.typography.bodySmall)
                    Text("Checkpoint", color = TukiInk.copy(alpha = 0.64f), style = MaterialTheme.typography.bodySmall)
                    Text("SM Clark", color = TukiInk, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold)
                }
            }

            AnimatedVisibility(
                visible = alertVisible,
                enter = fadeIn(tween(220)) + scaleIn(initialScale = 0.88f),
                exit = fadeOut(tween(150))
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    color = TukiOrange.copy(alpha = 0.12f),
                    shape = RoundedCornerShape(18.dp),
                    border = BorderStroke(1.dp, TukiOrange.copy(alpha = 0.28f))
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 14.dp, vertical = 11.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Box(
                            modifier = Modifier
                                .size(32.dp)
                                .background(TukiOrange, CircleShape),
                            contentAlignment = Alignment.Center
                        ) {
                            Text("!", color = Color.White, fontWeight = FontWeight.Bold)
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "PARA PO!",
                                color = TukiInk,
                                style = MaterialTheme.typography.labelLarge
                            )
                            Text(
                                text = "Your stop is coming up. Get ready to alight.",
                                color = TukiInk.copy(alpha = 0.66f),
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
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 26.dp, vertical = 4.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        TukiMascot(
            mood = if (active) TukiMascotMood.THINKING else TukiMascotMood.WELCOME,
            modifier = Modifier.size(116.dp)
        )

        Text(
            text = "Just ask TUKI.",
            color = Color.White,
            style = MaterialTheme.typography.displayMedium,
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = "Tell TUKI where you are going, your budget, or what matters most. The AI explains routes calculated from real transport data.",
            color = Color.White.copy(alpha = 0.78f),
            style = MaterialTheme.typography.bodyLarge,
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.height(22.dp))

        ChatPreview(active = active)
        Spacer(modifier = Modifier.height(10.dp))
    }
}

@Composable
private fun ChatPreview(active: Boolean) {
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            delay(200)
            stage = 1
            delay(520)
            stage = 2
            delay(420)
            stage = 3
        }
    }

    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .widthIn(max = 540.dp),
        color = Color.White.copy(alpha = 0.96f),
        shape = RoundedCornerShape(28.dp),
        shadowElevation = 10.dp
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(9.dp)
                        .background(TukiTeal, CircleShape)
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = "ASK TUKI",
                    color = TukiDeepTeal,
                    style = MaterialTheme.typography.labelSmall
                )
            }

            Spacer(modifier = Modifier.height(14.dp))

            AnimatedVisibility(
                visible = stage >= 1,
                enter = fadeIn(tween(260)) + slideInHorizontally(initialOffsetX = { it / 4 })
            ) {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                    Surface(
                        modifier = Modifier.fillMaxWidth(0.86f),
                        color = TukiDeepTeal,
                        shape = RoundedCornerShape(20.dp, 20.dp, 6.dp, 20.dp)
                    ) {
                        Text(
                            text = "I only have ₱50. What's the best way to SM Clark?",
                            color = Color.White,
                            modifier = Modifier.padding(13.dp),
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(10.dp))

            AnimatedVisibility(
                visible = stage >= 2,
                enter = fadeIn(tween(260)) + slideInHorizontally(initialOffsetX = { -it / 4 })
            ) {
                Row(verticalAlignment = Alignment.Top) {
                    Image(
                        painter = painterResource(R.drawable.tuki_logo),
                        contentDescription = null,
                        modifier = Modifier.size(38.dp),
                        contentScale = ContentScale.Fit
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Surface(
                        modifier = Modifier.weight(1f),
                        color = TukiTeal.copy(alpha = 0.10f),
                        shape = RoundedCornerShape(6.dp, 20.dp, 20.dp, 20.dp)
                    ) {
                        Text(
                            text = "I found a balanced route within your budget.",
                            color = TukiInk,
                            modifier = Modifier.padding(13.dp),
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(10.dp))

            AnimatedVisibility(
                visible = stage >= 3,
                enter = fadeIn(tween(280)) + slideInVertically(initialOffsetY = { it / 3 })
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    color = TukiGold.copy(alpha = 0.16f),
                    shape = RoundedCornerShape(18.dp),
                    border = BorderStroke(1.dp, TukiGold.copy(alpha = 0.36f))
                ) {
                    Row(
                        modifier = Modifier.padding(14.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = "BALANCED ROUTE",
                                color = TukiInk,
                                style = MaterialTheme.typography.labelSmall
                            )
                            Text(
                                text = "Walk → Tricycle → Jeepney",
                                color = TukiInk.copy(alpha = 0.68f),
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                        Column(horizontalAlignment = Alignment.End) {
                            Text("₱42", color = TukiDeepTeal, style = MaterialTheme.typography.titleLarge)
                            Text("29 min", color = TukiInk.copy(alpha = 0.58f), style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
            }
        }
    }
}
