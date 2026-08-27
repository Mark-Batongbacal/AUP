package com.example.frontend.screens

import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.unit.dp
import com.example.frontend.screens.onboarding.AskTukiPage
import com.example.frontend.screens.onboarding.ParaPoPage
import com.example.frontend.screens.onboarding.RouteChoicePage
import com.example.frontend.screens.onboarding.StepByStepPage
import com.example.frontend.ui.motion.TukiMascot
import com.example.frontend.ui.motion.TukiMascotMood
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private const val ONBOARDING_PAGE_COUNT = 4

@Composable
fun OnboardingScreen(
    onLetsRideClick: () -> Unit
) {
    val pagerState = rememberPagerState(pageCount = { ONBOARDING_PAGE_COUNT })
    val coroutineScope = rememberCoroutineScope()
    var isFinishing by remember { mutableStateOf(false) }

    val contentAlpha by animateFloatAsState(
        targetValue = if (isFinishing) 0f else 1f,
        animationSpec = tween(260),
        label = "onboarding_content_alpha"
    )
    val contentScale by animateFloatAsState(
        targetValue = if (isFinishing) 0.985f else 1f,
        animationSpec = tween(300),
        label = "onboarding_content_scale"
    )
    val handoffAlpha by animateFloatAsState(
        targetValue = if (isFinishing) 1f else 0f,
        animationSpec = tween(360),
        label = "onboarding_handoff_alpha"
    )

    fun finishOnboarding() {
        if (isFinishing) return
        isFinishing = true
        coroutineScope.launch {
            delay(390)
            onLetsRideClick()
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
    ) {
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
            OnboardingHeader(
                currentPage = pagerState.currentPage,
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
                    1 -> StepByStepPage(active = active)
                    2 -> ParaPoPage(active = active)
                    else -> AskTukiPage(active = active)
                }
            }

            OnboardingControls(
                currentPage = pagerState.currentPage,
                enabled = !isFinishing,
                onBack = {
                    if (pagerState.currentPage > 0) {
                        coroutineScope.launch {
                            pagerState.animateScrollToPage(pagerState.currentPage - 1)
                        }
                    }
                },
                onNext = {
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
private fun OnboardingHeader(
    currentPage: Int,
    enabled: Boolean,
    onSkip: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(58.dp)
            .padding(horizontal = 22.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            modifier = Modifier.weight(1f),
            horizontalArrangement = Arrangement.spacedBy(7.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            repeat(ONBOARDING_PAGE_COUNT) { index ->
                val segmentWidth by animateDpAsState(
                    targetValue = if (index <= currentPage) 34.dp else 22.dp,
                    animationSpec = tween(220),
                    label = "progress_segment_$index"
                )
                Box(
                    modifier = Modifier
                        .width(segmentWidth)
                        .height(5.dp)
                        .background(
                            color = if (index <= currentPage) TukiTeal else TukiInk.copy(alpha = 0.10f),
                            shape = RoundedCornerShape(100.dp)
                        )
                )
            }
        }

        TextButton(
            onClick = onSkip,
            enabled = enabled
        ) {
            Text(
                text = "Skip",
                color = TukiMuted,
                style = MaterialTheme.typography.labelLarge
            )
        }
    }
}

@Composable
private fun OnboardingControls(
    currentPage: Int,
    enabled: Boolean,
    onBack: () -> Unit,
    onNext: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(start = 24.dp, end = 24.dp, top = 12.dp, bottom = 18.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        TextButton(
            onClick = onBack,
            enabled = enabled && currentPage > 0,
            modifier = Modifier.width(88.dp)
        ) {
            Text(
                text = if (currentPage == 0) "" else "Back",
                color = TukiInk,
                style = MaterialTheme.typography.labelLarge
            )
        }

        Button(
            onClick = onNext,
            enabled = enabled,
            modifier = Modifier
                .weight(1f)
                .height(58.dp),
            shape = RoundedCornerShape(20.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = TukiTeal,
                contentColor = Color.White,
                disabledContainerColor = TukiTeal.copy(alpha = 0.50f),
                disabledContentColor = Color.White.copy(alpha = 0.72f)
            )
        ) {
            Text(
                text = if (currentPage == ONBOARDING_PAGE_COUNT - 1) "Let's Ride" else "Next",
                style = MaterialTheme.typography.titleMedium
            )
        }
    }
}

@Composable
private fun LoginHandoffOverlay(alpha: Float) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .graphicsLayer { this.alpha = alpha }
            .background(TukiCream),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            TukiMascot(
                mood = TukiMascotMood.CELEBRATE,
                modifier = Modifier
                    .size(150.dp)
                    .graphicsLayer {
                        scaleX = 0.82f + (alpha * 0.18f)
                        scaleY = scaleX
                    },
                showHalo = false
            )
            Spacer(modifier = Modifier.height(14.dp))
            Text(
                text = "Ready when you are.",
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
            Text(
                text = "Your smarter commute starts here.",
                color = TukiMuted,
                style = MaterialTheme.typography.bodyMedium
            )
        }
    }
}
