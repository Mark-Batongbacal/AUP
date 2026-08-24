package com.example.frontend.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted

@Composable
fun ParaPoOverlay(
    onDismiss: () -> Unit = {}
) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 24.dp),
        shape = RoundedCornerShape(28.dp),
        color = Color.White,
        tonalElevation = 12.dp,
        shadowElevation = 12.dp
    ) {
        Column(
            modifier = Modifier.padding(32.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = "\uD83D\uDCA1", // Lightbulb icon
                fontSize = 48.sp
            )
            
            Spacer(modifier = Modifier.height(20.dp))
            
            Text(
                text = "You're arriving!",
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
            
            Spacer(modifier = Modifier.height(12.dp))
            
            Text(
                text = "Get ready to say \"Para Po\" to the driver. Check your belongings before alighting.",
                color = TukiMuted,
                style = MaterialTheme.typography.bodyLarge,
                textAlign = TextAlign.Center,
                lineHeight = 22.sp
            )
            
            Spacer(modifier = Modifier.height(32.dp))
            
            Button(
                onClick = onDismiss,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(56.dp),
                shape = RoundedCornerShape(16.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = TukiOrange,
                    contentColor = Color.White
                )
            ) {
                Text(
                    text = "I'm ready",
                    style = MaterialTheme.typography.titleLarge
                )
            }
        }
    }
}
