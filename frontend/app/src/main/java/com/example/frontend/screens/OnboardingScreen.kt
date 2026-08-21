package com.example.frontend.screens

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
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
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R

private val TukiTeal = com.example.frontend.ui.theme.TukiTeal
private val TukiOrange = com.example.frontend.ui.theme.TukiOrange

@Composable
fun OnboardingScreen(
    onLetsRideClick: () -> Unit
) {
    val infiniteTransition = rememberInfiniteTransition(
        label = "tuki_bounce"
    )

    val logoOffset = infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = -14f,
        animationSpec = infiniteRepeatable(
            animation = tween(600),
            repeatMode = RepeatMode.Reverse
        ),
        label = "logo_jump"
    )

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiTeal)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(
                    start = 34.dp,
                    end = 34.dp,
                    top = 170.dp,
                    bottom = 45.dp
                ),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Image(
                painter = painterResource(R.drawable.tuki_logo),
                contentDescription = "TUKI logo",
                modifier = Modifier
                    .size(170.dp)
                    .graphicsLayer {
                        translationY = logoOffset.value * density
                    },
                contentScale = ContentScale.Fit
            )

            Spacer(modifier = Modifier.height(5.dp))

            Text(
                text = "TUKI.",
                color = Color.White,
                fontSize = 46.sp,
                fontWeight = FontWeight.ExtraBold,
                fontFamily = com.example.frontend.ui.theme.TukiDisplayFontFamily
            )

            Spacer(modifier = Modifier.height(38.dp))

            Text(
                text = "Commute smarter.",
                color = Color.White,
                fontSize = 21.sp
            )

            Text(
                text = "Move easier.",
                color = Color.White,
                fontSize = 21.sp
            )

            Spacer(modifier = Modifier.height(28.dp))

            Row(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .width(30.dp)
                        .height(10.dp)
                        .background(
                            color = TukiOrange,
                            shape = RoundedCornerShape(5.dp)
                        )
                )

                Box(
                    modifier = Modifier
                        .size(10.dp)
                        .background(
                            color = Color.White.copy(alpha = 0.3f),
                            shape = CircleShape
                        )
                )

                Box(
                    modifier = Modifier
                        .size(10.dp)
                        .background(
                            color = Color.White.copy(alpha = 0.3f),
                            shape = CircleShape
                        ))
            }

            Spacer(modifier = Modifier.height(40.dp))

            Button(
                onClick = onLetsRideClick,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(84.dp),
                shape = RoundedCornerShape(22.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = TukiOrange,
                    contentColor = Color.White
                )
            ) {
                Text(
                    text = "Let's Ride",
                    fontSize = 25.sp,
                    fontWeight = FontWeight.Bold,
                    fontFamily = com.example.frontend.ui.theme.TukiDisplayFontFamily
                )
            }
        }
    }
}
