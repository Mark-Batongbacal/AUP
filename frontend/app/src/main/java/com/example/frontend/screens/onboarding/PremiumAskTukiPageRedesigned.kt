package com.example.frontend.screens.onboarding

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.scaleIn
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideInVertically
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.weight
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

private enum class BudgetRouteMode {
    WALK,
    TRICYCLE,
    JEEPNEY,
    DESTINATION
}

/**
 * Redesigned fourth onboarding page.
 *
 * The route result now owns a full-width block and TUKI sits in a dedicated row below it, so the
 * mascot can never be covered by the chat/result card. The motion remains staged and restrained.
 */
@Composable
fun PremiumAskTukiPageRedesigned(active: Boolean) {
    var stage by remember { mutableIntStateOf(0) }

    LaunchedEffect(active) {
        stage = 0
        if (active) {
            delay(220)
            stage = 1
            delay(560)
            stage = 2
            delay(640)
            stage = 3
            delay(420)
            stage = 4
        }
    }

    BudgetPageShell {
        BudgetPageTitle(
            title = "Travel within your budget",
            subtitle = "Tell TUKI where you’re going and how much you want to spend."
        )

        Spacer(modifier = Modifier.height(10.dp))

        AnimatedVisibility(
            visible = stage >= 1,
            enter = fadeIn(tween(220)) + slideInHorizontally(
                animationSpec = spring(dampingRatio = 0.82f, stiffness = 330f),
                initialOffsetX = { it / 4 }
            )
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.End
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth(0.76f),
                    color = Color(0xFFDCEEFF),
                    shape = RoundedCornerShape(22.dp, 22.dp, 7.dp, 22.dp)
                ) {
                    Text(
                        text = "I only have ₱50. What’s the best way to SM Clark?",
                        modifier = Modifier.padding(horizontal = 15.dp, vertical = 13.dp),
                        color = TukiInk,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(10.dp))

        AnimatedVisibility(
            visible = stage >= 2,
            enter = fadeIn(tween(220)) + slideInHorizontally(
                animationSpec = spring(dampingRatio = 0.82f, stiffness = 330f),
                initialOffsetX = { -it / 4 }
            )
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(9.dp)
            ) {
                Surface(
                    modifier = Modifier.size(42.dp),
                    color = TukiTeal.copy(alpha = 0.10f),
                    shape = CircleShape,
                    border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.12f))
                ) {
                    TukiMascot(
                        mood = if (stage >= 3) TukiMascotMood.CELEBRATE else TukiMascotMood.THINKING,
                        modifier = Modifier.padding(4.dp),
                        showHalo = false
                    )
                }

                Surface(
                    modifier = Modifier.widthIn(max = 270.dp),
                    color = Color(0xFFDDF4E8),
                    shape = RoundedCornerShape(7.dp, 20.dp, 20.dp, 20.dp)
                ) {
                    Text(
                        text = if (stage == 2) "Finding the best fit for your budget…" else "I found a great route for you!",
                        modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
                        color = TukiInk,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        AnimatedVisibility(
            visible = stage >= 3,
            enter = fadeIn(tween(260)) + slideInVertically(
                animationSpec = spring(dampingRatio = 0.76f, stiffness = 320f),
                initialOffsetY = { it / 4 }
            )
        ) {
            BudgetRouteResultCard()
        }

        Spacer(modifier = Modifier.height(10.dp))

        AnimatedVisibility(
            visible = stage >= 4,
            enter = fadeIn(tween(260)) + scaleIn(
                initialScale = 0.94f,
                animationSpec = spring(dampingRatio = 0.68f, stiffness = 300f)
            )
        ) {
            TukiResultCelebration()
        }

        Spacer(modifier = Modifier.height(12.dp))
    }
}

@Composable
private fun BudgetRouteResultCard() {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color.White,
        shape = RoundedCornerShape(24.dp),
        shadowElevation = 5.dp,
        border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.13f))
    ) {
        Column(modifier = Modifier.padding(horizontal = 17.dp, vertical = 15.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
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
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                        color = TukiDeepTeal,
                        style = MaterialTheme.typography.labelSmall,
                        fontWeight = FontWeight.SemiBold
                    )
                }
            }

            Spacer(modifier = Modifier.height(6.dp))
            Text(
                text = "₱42  •  29 min",
                color = TukiInk,
                style = MaterialTheme.typography.titleLarge
            )
            Spacer(modifier = Modifier.height(13.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.Top,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                BudgetRouteStep(
                    mode = BudgetRouteMode.WALK,
                    label = "Walk",
                    detail = "6 min"
                )
                BudgetArrow()
                BudgetRouteStep(
                    mode = BudgetRouteMode.TRICYCLE,
                    label = "Tricycle",
                    detail = "8 min"
                )
                BudgetArrow()
                BudgetRouteStep(
                    mode = BudgetRouteMode.JEEPNEY,
                    label = "Jeepney",
                    detail = "13 min"
                )
                BudgetArrow()
                BudgetRouteStep(
                    mode = BudgetRouteMode.DESTINATION,
                    label = "SM Clark",
                    detail = ""
                )
            }
        }
    }
}

@Composable
private fun BudgetRouteStep(
    mode: BudgetRouteMode,
    label: String,
    detail: String
) {
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = Modifier.widthIn(min = 48.dp, max = 66.dp)
    ) {
        BudgetRouteIcon(mode = mode, modifier = Modifier.size(29.dp))
        Spacer(modifier = Modifier.height(5.dp))
        Text(
            text = label,
            color = TukiInk,
            style = MaterialTheme.typography.labelSmall,
            textAlign = TextAlign.Center,
            fontWeight = FontWeight.SemiBold
        )
        if (detail.isNotBlank()) {
            Text(
                text = detail,
                color = TukiMuted,
                style = MaterialTheme.typography.labelSmall,
                textAlign = TextAlign.Center
            )
        }
    }
}

@Composable
private fun BudgetArrow() {
    Text(
        text = "→",
        modifier = Modifier.padding(top = 5.dp),
        color = TukiMuted,
        style = MaterialTheme.typography.titleMedium
    )
}

@Composable
private fun TukiResultCelebration() {
    val mascotOffsetX by animateFloatAsState(
        targetValue = 0f,
        animationSpec = spring(dampingRatio = 0.68f, stiffness = 280f),
        label = "budget_result_tuki_x"
    )

    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Box(
            modifier = Modifier
                .size(142.dp)
                .graphicsLayer { this.translationX = mascotOffsetX * density },
            contentAlignment = Alignment.Center
        ) {
            CelebrationMarks(modifier = Modifier.fillMaxSize())
            TukiMascot(
                mood = TukiMascotMood.CELEBRATE,
                modifier = Modifier.size(132.dp),
                showHalo = false
            )
        }

        Surface(
            modifier = Modifier.weight(1f),
            color = Color(0xFFEAF5E3),
            shape = RoundedCornerShape(20.dp, 20.dp, 20.dp, 7.dp),
            border = BorderStroke(1.dp, TukiTeal.copy(alpha = 0.10f))
        ) {
            Column(modifier = Modifier.padding(horizontal = 14.dp, vertical = 13.dp)) {
                Text(
                    text = "Same destination.",
                    color = TukiInk,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    text = "Smarter choices. Greater journeys.",
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodySmall
                )
            }
        }
    }
}

@Composable
private fun CelebrationMarks(modifier: Modifier = Modifier) {
    Canvas(modifier = modifier) {
        val marks = listOf(
            Offset(size.width * 0.20f, size.height * 0.18f),
            Offset(size.width * 0.79f, size.height * 0.14f),
            Offset(size.width * 0.88f, size.height * 0.55f)
        )

        marks.forEachIndexed { index, center ->
            val radius = (if (index == 1) 5.dp else 4.dp).toPx()
            val color = if (index % 2 == 0) TukiGold else TukiTeal
            drawLine(
                color = color,
                start = Offset(center.x - radius, center.y),
                end = Offset(center.x + radius, center.y),
                strokeWidth = 2.dp.toPx(),
                cap = StrokeCap.Round
            )
            drawLine(
                color = color,
                start = Offset(center.x, center.y - radius),
                end = Offset(center.x, center.y + radius),
                strokeWidth = 2.dp.toPx(),
                cap = StrokeCap.Round
            )
        }
    }
}

@Composable
private fun BudgetPageShell(content: @Composable () -> Unit) {
    Box(modifier = Modifier.fillMaxSize()) {
        BudgetPageDecor()
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp, vertical = 4.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            content()
        }
    }
}

@Composable
private fun BudgetPageDecor() {
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
private fun BudgetPageTitle(
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
private fun BudgetRouteIcon(
    mode: BudgetRouteMode,
    modifier: Modifier = Modifier
) {
    Canvas(modifier = modifier) {
        val w = size.width
        val h = size.height
        val teal = TukiTeal
        val orange = TukiOrange
        val ink = TukiDeepTeal

        when (mode) {
            BudgetRouteMode.WALK -> {
                drawCircle(orange, w * 0.10f, Offset(w * 0.50f, h * 0.18f))
                drawLine(teal, Offset(w * 0.50f, h * 0.30f), Offset(w * 0.46f, h * 0.58f), w * 0.07f, StrokeCap.Round)
                drawLine(teal, Offset(w * 0.47f, h * 0.40f), Offset(w * 0.30f, h * 0.52f), w * 0.06f, StrokeCap.Round)
                drawLine(teal, Offset(w * 0.48f, h * 0.42f), Offset(w * 0.66f, h * 0.52f), w * 0.06f, StrokeCap.Round)
                drawLine(ink, Offset(w * 0.46f, h * 0.58f), Offset(w * 0.31f, h * 0.83f), w * 0.065f, StrokeCap.Round)
                drawLine(ink, Offset(w * 0.46f, h * 0.58f), Offset(w * 0.65f, h * 0.80f), w * 0.065f, StrokeCap.Round)
            }

            BudgetRouteMode.TRICYCLE -> {
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
                drawCircle(ink, w * 0.105f, Offset(w * 0.28f, h * 0.76f))
                drawCircle(ink, w * 0.105f, Offset(w * 0.68f, h * 0.76f))
                drawCircle(Color.White, w * 0.045f, Offset(w * 0.28f, h * 0.76f))
                drawCircle(Color.White, w * 0.045f, Offset(w * 0.68f, h * 0.76f))
            }

            BudgetRouteMode.JEEPNEY -> {
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

            BudgetRouteMode.DESTINATION -> {
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
