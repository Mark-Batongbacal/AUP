package com.example.frontend.screens

import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.airbnb.lottie.compose.*
import com.example.frontend.R
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay

@Composable
fun LoginSuccessAnimationScreen(
    onAnimationComplete: () -> Unit
) {
    var startExitAnimation by remember { mutableStateOf(false) }

    val composition by rememberLottieComposition(LottieCompositionSpec.RawRes(R.raw.tuki_loading))
    val progress by animateLottieCompositionAsState(
        composition = composition,
        iterations = LottieConstants.IterateForever
    )

    // Exit animation values
    val exitAlpha by animateFloatAsState(
        targetValue = if (startExitAnimation) 0f else 1f,
        animationSpec = tween(500),
        label = "exitAlpha"
    )
    val exitScale by animateFloatAsState(
        targetValue = if (startExitAnimation) 3f else 1f,
        animationSpec = tween(500, easing = FastOutSlowInEasing),
        label = "exitScale"
    )

    LaunchedEffect(Unit) {
        // Play the animation for 2.5 seconds
        delay(2500)
        startExitAnimation = true
        delay(500)
        onAnimationComplete()
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .alpha(exitAlpha),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier.scale(exitScale)
        ) {
            Box(
                contentAlignment = Alignment.Center,
                modifier = Modifier.size(200.dp)
            ) {
                // Background circle
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(Color.White, CircleShape)
                )

                LottieAnimation(
                    composition = composition,
                    progress = { progress },
                    modifier = Modifier.size(160.dp)
                )
            }

            Spacer(modifier = Modifier.height(32.dp))

            Text(
                text = if (TukiInterfaceText.isFilipino) "Tagumpay ang Login!" else "Login Successful!",
                color = TukiTeal,
                fontSize = 24.sp,
                fontWeight = FontWeight.Bold,
                style = MaterialTheme.typography.headlineMedium
            )

            Spacer(modifier = Modifier.height(8.dp))

            Text(
                text = if (TukiInterfaceText.isFilipino) "Inihahanda na ang iyong biyahe..." else "Preparing your journey...",
                color = TukiOrange,
                fontSize = 16.sp,
                fontWeight = FontWeight.Medium
            )
        }
    }
}
