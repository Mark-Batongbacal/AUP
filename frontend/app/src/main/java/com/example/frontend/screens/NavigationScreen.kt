package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.model.CommuteStep

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

@Composable
fun NavigationScreen(
    origin: String,
    destination: String,
    steps: List<CommuteStep>,
    isStartingNavigation: Boolean = false,
    navigationStartError: String? = null,
    hasActiveTrip: Boolean = false,
    onBack: () -> Unit = {},
    onStartTracking: () -> Unit = {},
    onResumeActiveTrip: () -> Unit = {},
    onEndActiveTrip: () -> Unit = {}
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .padding(horizontal = 30.dp, vertical = 30.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(Color.White, RoundedCornerShape(12.dp))
                    .clickable(enabled = !isStartingNavigation, onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(text = "\u2039", color = TukiDark, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            }
            Spacer(modifier = Modifier.width(14.dp))
            Text(text = "Route Details", color = TukiDark, fontSize = 22.sp, fontWeight = FontWeight.ExtraBold)
        }

        Spacer(modifier = Modifier.height(24.dp))

        Text(
            text = "$origin \u2192 $destination",
            color = TukiDark,
            fontSize = 18.sp,
            fontWeight = FontWeight.Bold
        )

        Spacer(modifier = Modifier.height(24.dp))

        LazyColumn(
            modifier = Modifier.weight(1f),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            items(steps) { step ->
                NavigationStepRow(step)
            }
        }

        navigationStartError?.let { message ->
            Text(
                text = message,
                color = Color.Red,
                fontSize = 13.sp,
                fontWeight = FontWeight.SemiBold,
                modifier = Modifier.padding(bottom = 10.dp)
            )
        }

        if (hasActiveTrip) {
            Button(
                onClick = onResumeActiveTrip,
                enabled = !isStartingNavigation,
                modifier = Modifier.fillMaxWidth().height(52.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = TukiTeal,
                    contentColor = Color.White
                )
            ) {
                Text("Resume Active Trip", fontWeight = FontWeight.Bold)
            }
            Spacer(modifier = Modifier.height(8.dp))
            OutlinedButton(
                onClick = onEndActiveTrip,
                enabled = !isStartingNavigation,
                modifier = Modifier.fillMaxWidth().height(52.dp)
            ) {
                Text("End Active Trip", color = TukiOrange, fontWeight = FontWeight.Bold)
            }
            Spacer(modifier = Modifier.height(8.dp))
        }

        Spacer(modifier = Modifier.height(14.dp))

        Button(
            onClick = onStartTracking,
            enabled = !isStartingNavigation && !hasActiveTrip,
            modifier = Modifier
                .fillMaxWidth()
                .height(60.dp),
            shape = RoundedCornerShape(20.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = TukiTeal,
                contentColor = Color.White
            )
        ) {
            if (isStartingNavigation) {
                CircularProgressIndicator(
                    modifier = Modifier.size(22.dp),
                    strokeWidth = 2.dp,
                    color = Color.White
                )
                Spacer(modifier = Modifier.width(10.dp))
                Text(text = "Working...", fontSize = 18.sp, fontWeight = FontWeight.Bold)
            } else {
                Text(text = "Start Trip", fontSize = 20.sp, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun NavigationStepRow(step: CommuteStep) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(Color.White, RoundedCornerShape(16.dp))
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .background(TukiTeal.copy(alpha = 0.1f), RoundedCornerShape(10.dp)),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = when(step.mode.lowercase()) {
                    "jeepney" -> "\uD83D\uDE90"
                    "tricycle" -> "\uD83D\uDEF4"
                    "walk" -> "\uD83D\uDEB6"
                    else -> "\uD83D\uDE8C"
                },
                fontSize = 20.sp
            )
        }

        Spacer(modifier = Modifier.width(16.dp))

        Column {
            Text(
                text = "${step.mode} to ${step.to}",
                color = TukiDark,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
            Text(
                text = "${step.minutes} mins \u00B7 ${step.fare?.let { "\u20B1$it" } ?: "Free"}",
                color = TukiGray,
                fontSize = 14.sp
            )
        }
    }
}
