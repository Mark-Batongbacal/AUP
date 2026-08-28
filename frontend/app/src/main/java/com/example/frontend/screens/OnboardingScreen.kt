package com.example.frontend.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
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
import com.example.frontend.screens.onboarding.PremiumAskTukiPageRedesigned
import com.example.frontend.screens.onboarding.PremiumParaPoPage
import com.example.frontend.screens.onboarding.PremiumRouteChoicePageClean
import com.example.frontend.screens.onboarding.PremiumStepByStepPageClean
import com.example.frontend.screens.onboarding.TukiFlightIntro
import com.example.frontend.ui.motion.TukiMascot
import com.example.frontend.ui.motion.TukiMascotMood
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private const val ONBOARDING_PAGE_COUNT = 4
private const val LOGIN_HANDOFF_DURATION_MS = 1900L

@Composable
fun OnboardingScreen(
    onLetsRideClick: () -> Unit
) {
    val pagerState = rememberPagerState(pageCount = { ONBOARDING_PAGE_COUNT })
    val coroutineScope = rememberCoroutineScope()
    var introVisible by remember { mutableStateOf(true) }
    var onboardingRevealed by remember { mutableStateOf(true) }
    var isFinishing by remember { mutableStateOf(false) }

    val onboardingAlpha by animateFloatAsState(
        targetValue = when {
            isFinishing -> 0f
            onboardingRevealed -> 1f
            else -> 0f
        },
        animationSpec = tween(if (onboardingRevealed) 420 else 260),
        label = "onboarding_content_alpha"
    )
    val onboardingScale by animateFloatAsState(
        targetValue = when {
            isFinishing -> 0.985f
            onboardingRevealed -> 1f
            else -> 1.015f
        },
        animationSpec = tween(360),
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
            delay(LOGIN_HANDOFF_DURATION_MS)
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
                    alpha = onboardingAlpha
                    scaleX = onboardingScale
                    scaleY = onboardingScale
                }
        ) {
            OnboardingHeader(
                currentPage = pagerState.currentPage,
                enabled = onboardingRevealed && !isFinishing,
                onSkip = ::finishOnboarding
            )

            HorizontalPager(
                state = pagerState,
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                userScrollEnabled = onboardingRevealed && !isFinishing,
                beyondViewportPageCount = 1
            ) { page ->
                val active = onboardingRevealed &&
                    !introVisible &&
                    !isFinishing &&
                    pagerState.currentPage == page

                when (page) {
                    0 -> PremiumRouteChoicePageClean(active = active)
                    1 -> PremiumStepByStepPageClean(active = active)
                    2 -> PremiumParaPoPage(active = active)
                    else -> PremiumAskTukiPageRedesigned(active = active)
                }
            }

            OnboardingControls(
                currentPage = pagerState.currentPage,
                enabled = onboardingRevealed && !isFinishing,
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

        if (introVisible) {
            TukiFlightIntro(
                onHandoffStarted = {
                    onboardingRevealed = true
                },
                onFinished = {
                    onboardingRevealed = true
                    introVisible = false
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
                    targetValue = if (index == currentPage) 38.dp else 29.dp,
                    animationSpec = tween(240),
                    label = "progress_segment_width_$index"
                )
                val fillWidth by animateDpAsState(
                    targetValue = when {
                        index < currentPage -> segmentWidth
                        index == currentPage -> segmentWidth
                        else -> 0.dp
                    },
                    animationSpec = tween(
                        durationMillis = if (index == currentPage) 420 else 220
                    ),
                    label = "progress_segment_fill_$index"
                )

                Box(
                    modifier = Modifier
                        .width(segmentWidth)
                        .height(5.dp)
                        .background(
                            color = TukiTeal.copy(alpha = 0.12f),
                            shape = RoundedCornerShape(100.dp)
                        )
                ) {
                    Box(
                        modifier = Modifier
                            .align(Alignment.CenterStart)
                            .width(fillWidth)
                            .height(5.dp)
                            .background(
                                color = if (index < currentPage) {
                                    TukiTeal.copy(alpha = 0.66f)
                                } else {
                                    TukiTeal
                                },
                                shape = RoundedCornerShape(100.dp)
                            )
                    )
                }
            }
        }

        AnimatedVisibility(
            visible = currentPage < ONBOARDING_PAGE_COUNT - 1,
            enter = fadeIn(tween(150)),
            exit = fadeOut(tween(120))
        ) {
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
            .padding(start = 24.dp, end = 24.dp, top = 10.dp, bottom = 16.dp),
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
                text = if (currentPage == ONBOARDING_PAGE_COUNT - 1) "Let's Ride  →" else "Next  →",
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
                    .size(176.dp)
                    .graphicsLayer {
                        scaleX = 0.76f + (alpha * 0.24f)
                        scaleY = scaleX
                    },
                showHalo = false
            )
            Spacer(modifier = Modifier.height(14.dp))
            Text(
                text = "Ready to make every trip better?",
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = "Same guide. Greater journeys.",
                color = TukiMuted,
                style = MaterialTheme.typography.bodyMedium
            )
        }
    }
}
