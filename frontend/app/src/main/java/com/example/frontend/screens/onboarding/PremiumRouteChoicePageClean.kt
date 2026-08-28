package com.example.frontend.screens.onboarding

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.scaleIn
import androidx.compose.animation.slideInHorizontally
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
import androidx.compose.ui.geometry.Offset
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

private data class CleanRoutePreview(
    val label: String,
    val detail: String,
    val badge: String,
    val accent: Color,
    val fill: Color
)

/**
 * Page 1 with the same premium entrance sequence but without translucent/scale compositing on
 * route cards. Fully opaque pastel fills prevent the temporary emphasis from painting a pale
 * rectangular band behind route labels on some Android devices/emulators.
 */
@Composable
fun PremiumRouteChoicePageClean(active: Boolean) {
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

    CleanRoutePageShell {
        CleanRouteMascotHero(
            mascotVisible = heroVisible,
            speechVisible = speechVisible
        )

        CleanRoutePageTitle(
            title = "Find the best route for you",
            subtitle = "Compare the fastest, cheapest, and balanced route based on your trip."
        )

        Spacer(modifier = Modifier.height(12.dp))
        CleanRouteChoiceStack(
            visibleCount = routeStage,
            balancedEmphasis = balancedEmphasis
        )
    }
}

@Composable
private fun CleanRouteChoiceStack(
    visibleCount: Int,
    balancedEmphasis: Boolean
) {
    val routes = remember {
        listOf(
            CleanRoutePreview(
                label = "FASTEST",
                detail = "22 min • Tricycle → Jeepney",
                badge = "₱65",
                accent = Color(0xFFE95E58),
                fill = Color(0xFFFFECE8)
            ),
            CleanRoutePreview(
                label = "CHEAPEST",
                detail = "43 min • Walk → 2 Jeepneys",
                badge = "₱26",
                accent = Color(0xFF4EBF83),
                fill = Color(0xFFEAF5E3)
            ),
            CleanRoutePreview(
                label = "BALANCED",
                detail = "29 min • Walk → Tricycle → Jeepney",
                badge = "₱42",
                accent = TukiGold,
                fill = Color(0xFFFFF3D6)
            )
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
                CleanRouteChoiceCard(
                    route = route,
                    emphasized = index == 2 && balancedEmphasis
                )
            }
        }
    }
}

@Composable
private fun CleanRouteChoiceCard(
    route: CleanRoutePreview,
    emphasized: Boolean
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = route.fill,
        shape = RoundedCornerShape(21.dp),
        shadowElevation = if (emphasized) 3.dp else 0.dp,
        border = BorderStroke(
            width = if (emphasized) 1.5.dp else 1.dp,
            color = route.accent.copy(alpha = if (emphasized) 0.46f else 0.24f)
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
            Spacer(modifier = Modifier.size(12.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = route.label,
                    color = if (emphasized) TukiDeepTeal else TukiInk,
                    style = MaterialTheme.typography.labelSmall,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = route.detail,
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodySmall
                )
            }
            Spacer(modifier = Modifier.size(6.dp))
            Text(
                text = route.badge,
                color = TukiDeepTeal,
                style = MaterialTheme.typography.titleMedium
            )
        }
    }
}

@Composable
private fun CleanRouteMascotHero(
    mascotVisible: Boolean,
    speechVisible: Boolean
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(202.dp)
    ) {
        CleanRouteSceneMascot(
            active = mascotVisible,
            modifier = Modifier
                .align(Alignment.CenterStart)
                .offset(x = (-18).dp, y = 4.dp)
                .size(210.dp)
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
                    text = "Hi! I’m TUKI — your travel buddy.",
                    modifier = Modifier.padding(horizontal = 13.dp, vertical = 12.dp),
                    color = TukiInk,
                    style = MaterialTheme.typography.bodySmall
                )
            }
        }
    }
}

@Composable
private fun CleanRouteSceneMascot(
    active: Boolean,
    modifier: Modifier
) {
    val translationX by animateFloatAsState(
        targetValue = if (active) 0f else -96f,
        animationSpec = spring(dampingRatio = 0.72f, stiffness = 275f),
        label = "clean_route_mascot_entry"
    )
    val alpha by animateFloatAsState(
        targetValue = if (active) 1f else 0f,
        animationSpec = tween(220),
        label = "clean_route_mascot_alpha"
    )
    val scale by animateFloatAsState(
        targetValue = if (active) 1f else 0.86f,
        animationSpec = spring(dampingRatio = 0.70f, stiffness = 300f),
        label = "clean_route_mascot_scale"
    )

    TukiMascot(
        mood = TukiMascotMood.WELCOME,
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
private fun CleanRoutePageShell(content: @Composable () -> Unit) {
    Box(modifier = Modifier.fillMaxSize()) {
        CleanRouteDecor()
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
private fun CleanRouteDecor() {
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
private fun CleanRoutePageTitle(
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
