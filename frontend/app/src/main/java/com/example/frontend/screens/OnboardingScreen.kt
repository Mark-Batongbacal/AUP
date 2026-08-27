package com.example.frontend.screens

import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import com.example.frontend.R
import com.example.frontend.screens.onboarding.AskTukiPage
import com.example.frontend.screens.onboarding.ParaPoPage
import com.example.frontend.screens.onboarding.RouteChoicePage
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private const val ONBOARDING_PAGE_COUNT = 3

@Composable
fun OnboardingScreen(
    onLetsRideClick: () -> Unit
) {
    val pagerState = rememberPagerState(pageCount = { ONBOARDING_PAGE_COUNT })
    val coroutineScope = rememberCoroutineScope()
    var isFinishing by remember { mutableStateOf(false) }

    val contentAlpha by animateFloatAsState(
        targetValue = if (isFinishing) 0f else 1f,
        animationSpec = tween(320),
        label = "onboarding_content_alpha"
    )
    val contentScale by animateFloatAsState(
        targetValue = if (isFinishing) 0.96f else 1f,
        animationSpec = tween(360),
        label = "onboarding_content_scale"
    )
    val handoffAlpha by animateFloatAsState(
        targetValue = if (isFinishing) 1f else 0f,
        animationSpec = tween(430),
        label = "login_handoff_alpha"
    )

    fun finishOnboarding() {
        if (isFinishing) return
        isFinishing = true
        coroutineScope.launch {
            delay(460)
            onLetsRideClick()
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    colors = listOf(
                        TukiDeepTeal,
                        TukiTeal,
                        Color(0xFF087783)
                    )
                )
            )
    ) {
        OnboardingBackdrop()

        Column(
            modifier = Modifier
                .fillMaxSize()
                .statusBarsPadding()
                .navigationBarsPadding()
                .graphicsLayer {
                    alpha = contentAlpha
                    scaleX = contentScale
                    scaleY = contentScale
                }
        ) {
            OnboardingTopBar(
                showSkip = pagerState.currentPage < ONBOARDING_PAGE_COUNT - 1,
                enabled = !isFinishing,
                onSkip = ::finishOnboarding
            )

            HorizontalPager(
                state = pagerState,
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                userScrollEnabled = !isFinishing,
                beyondViewportPageCount = 1
            ) { page ->
                val active = pagerState.currentPage == page
                when (page) {
                    0 -> RouteChoicePage(active = active)
                    1 -> ParaPoPage(active = active)
                    else -> AskTukiPage(active = active)
                }
            }

            OnboardingControls(
                currentPage = pagerState.currentPage,
                enabled = !isFinishing,
                onPrimaryClick = {
                    if (pagerState.currentPage == ONBOARDING_PAGE_COUNT - 1) {
                        finishOnboarding()
                    } else {
                        coroutineScope.launch {
                            pagerState.animateScrollToPage(pagerState.currentPage + 1)
                        }
                    }
                }
            )
        }

        if (handoffAlpha > 0f) {
            LoginHandoffOverlay(alpha = handoffAlpha)
        }
    }
}

@Composable
private fun OnboardingBackdrop() {
    Canvas(modifier = Modifier.fillMaxSize()) {
        drawCircle(
            color = TukiGold.copy(alpha = 0.08f),
            radius = size.minDimension * 0.52f,
            center = Offset(size.width * 1.08f, size.height * 0.16f)
        )
        drawCircle(
            color = Color.White.copy(alpha = 0.05f),
            radius = size.minDimension * 0.44f,
            center = Offset(-size.width * 0.08f, size.height * 0.72f)
        )
        drawCircle(
            color = TukiOrange.copy(alpha = 0.05f),
            radius = size.minDimension * 0.22f,
            center = Offset(size.width * 0.88f, size.height * 0.72f)
        )
    }
}

@Composable
private fun OnboardingTopBar(
    showSkip: Boolean,
    enabled: Boolean,
    onSkip: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(62.dp)
            .padding(horizontal = 22.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            modifier = Modifier.weight(1f),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Image(
                painter = painterResource(R.drawable.tuki_logo),
                contentDescription = null,
                modifier = Modifier.size(34.dp),
                contentScale = ContentScale.Fit
            )
            Spacer(modifier = Modifier.width(7.dp))
            Text(
                text = "TUKI",
                color = Color.White,
                style = MaterialTheme.typography.titleLarge
            )
        }

        if (showSkip) {
            TextButton(onClick = onSkip, enabled = enabled) {
                Text(
                    text = "Skip",
                    color = Color.White.copy(alpha = if (enabled) 0.86f else 0.45f),
                    style = MaterialTheme.typography.labelLarge
                )
            }
        }
    }
}

@Composable
private fun OnboardingControls(
    currentPage: Int,
    enabled: Boolean,
    onPrimaryClick: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 26.dp, vertical = 16.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Row(
            horizontalArrangement = Arrangement.spacedBy(7.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            repeat(ONBOARDING_PAGE_COUNT) { index ->
                val width by animateDpAsState(
                    targetValue = if (currentPage == index) 28.dp else 8.dp,
                    animationSpec = tween(260),
                    label = "page_indicator_width_$index"
                )
                Box(
                    modifier = Modifier
                        .width(width)
                        .height(8.dp)
                        .background(
                            color = if (currentPage == index) TukiGold else Color.White.copy(alpha = 0.28f),
                            shape = CircleShape
                        )
                )
            }
        }

        Spacer(modifier = Modifier.height(14.dp))

        Button(
            onClick = onPrimaryClick,
            enabled = enabled,
            modifier = Modifier
                .fillMaxWidth()
                .height(58.dp),
            shape = RoundedCornerShape(20.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = TukiOrange,
                contentColor = Color.White,
                disabledContainerColor = TukiOrange.copy(alpha = 0.55f),
                disabledContentColor = Color.White.copy(alpha = 0.72f)
            )
        ) {
            Text(
                text = if (currentPage == ONBOARDING_PAGE_COUNT - 1) "Let's Ride" else "Next",
                style = MaterialTheme.typography.titleLarge
            )
        }

        Spacer(modifier = Modifier.height(6.dp))
        Text(
            text = if (currentPage == ONBOARDING_PAGE_COUNT - 1) {
                "Your smarter commute starts here."
            } else {
                "Swipe to explore"
            },
            color = Color.White.copy(alpha = 0.62f),
            style = MaterialTheme.typography.bodySmall
        )
    }
}

@Composable
private fun LoginHandoffOverlay(alpha: Float) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .graphicsLayer { this.alpha = alpha }
            .background(TukiCream)
    ) {
        Row(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .statusBarsPadding()
                .padding(top = 12.dp)
                .graphicsLayer {
                    this.alpha = alpha
                    scaleX = 0.86f + (alpha * 0.14f)
                    scaleY = scaleX
                },
            verticalAlignment = Alignment.CenterVertically
        ) {
            Image(
                painter = painterResource(R.drawable.tuki_logo),
                contentDescription = null,
                modifier = Modifier.size(50.dp),
                contentScale = ContentScale.Fit
            )
            Spacer(modifier = Modifier.width(10.dp))
            Text(
                text = "TUKI.",
                color = TukiTeal,
                style = MaterialTheme.typography.displaySmall
            )
        }
    }
}
