package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RecentCommute

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

/**
 * Full breakdown of a past commute. Reached by tapping a card in
 * "Recent Commutes" on the Home screen.
 */
@Composable
fun CommuteDetailScreen(
    commute: RecentCommute,
    onBack: () -> Unit = {}
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .padding(horizontal = 30.dp, vertical = 30.dp)
    ) {
        Text(
            text = "\u2190 Back",
            color = TukiTeal,
            fontSize = 16.sp,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.clickable(onClick = onBack)
        )

        Spacer(modifier = Modifier.height(20.dp))

        Text(
            text = "${commute.origin} \u2192 ${commute.destination}",
            color = TukiDark,
            fontSize = 24.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(6.dp))

        Text(
            text = "${commute.legs} legs \u00B7 ${commute.minutes} min total",
            color = TukiTeal,
            fontSize = 16.sp,
            fontWeight = FontWeight.SemiBold
        )

        Spacer(modifier = Modifier.height(24.dp))

        if (commute.steps.isEmpty()) {
            Text(
                text = "No step-by-step breakdown saved for this trip yet.",
                color = TukiGray,
                fontSize = 15.sp
            )
        } else {
            LazyColumn {
                items(commute.steps) { step -> StepRow(step) }
            }
        }
    }
}

@Composable
private fun StepRow(step: CommuteStep) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiCream2, RoundedCornerShape(14.dp))
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        androidx.compose.foundation.layout.Box(
            modifier = Modifier
                .width(6.dp)
                .height(36.dp)
                .background(TukiOrange, RoundedCornerShape(3.dp))
        )
        Spacer(modifier = Modifier.width(12.dp))
        Column {
            Text(
                text = "${step.mode}: ${step.from} \u2192 ${step.to}",
                color = TukiDark,
                fontWeight = FontWeight.Bold,
                fontSize = 15.sp
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = "${step.minutes} min" + (step.fare?.let { " \u00B7 \u20B1$it" } ?: ""),
                color = TukiGray,
                fontSize = 13.sp
            )
        }
    }
    Spacer(modifier = Modifier.height(10.dp))
}