package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.model.CommuteStep
import kotlin.math.roundToInt

private val NavBg = Color(0xFFF8F5EC)
private val NavSurface = Color(0xFFFFFBF0)
private val NavDark = Color(0xFF153E4B)
private val NavTeal = Color(0xFF2C8E95)
private val NavMuted = Color(0xFF7A898E)
private val NavOrange = Color(0xFFF4BF52)
private val NavIconBlue = Color(0xFFE7F2F3)
private val NavTip = Color(0xFFE8F0EB)

@Composable
fun NavigationScreen(
    origin: String,
    destination: String,
    steps: List<CommuteStep>,
    totalMinutes: Int? = null,
    totalFare: Double? = null,
    legCount: Int? = null,
    isStartingNavigation: Boolean = false,
    navigationStartError: String? = null,
    hasActiveTrip: Boolean = false,
    onBack: () -> Unit = {},
    onStartTracking: () -> Unit = {},
    onResumeActiveTrip: () -> Unit = {},
    onEndActiveTrip: () -> Unit = {}
) {
    val shownMinutes = totalMinutes ?: steps.sumOf { it.minutes }
    val shownFare = totalFare ?: steps.sumOf { it.fare ?: 0.0 }
    val shownLegs = legCount ?: steps.size

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(NavBg)
    ) {
        LazyColumn(
            modifier = Modifier.weight(1f).fillMaxWidth(),
            contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 20.dp, bottom = 16.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            item {
                Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        Modifier.size(40.dp).clickable(enabled = !isStartingNavigation, onClick = onBack),
                        contentAlignment = Alignment.Center
                    ) {
                        Text("←", color = NavDark, fontSize = 26.sp, fontWeight = FontWeight.Bold)
                    }
                    Text(
                        "Route Details",
                        Modifier.weight(1f),
                        color = NavDark,
                        fontSize = 23.sp,
                        fontWeight = FontWeight.ExtraBold
                    )
                }
            }

            item {
                Text(
                    "$origin →\n$destination",
                    color = NavDark,
                    fontSize = 17.sp,
                    lineHeight = 23.sp,
                    fontWeight = FontWeight.ExtraBold
                )
            }

            item {
                Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = NavSurface, shadowElevation = 1.dp) {
                    Row(Modifier.fillMaxWidth().padding(horizontal = 10.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                        RouteMetric("◷", "${shownMinutes.coerceAtLeast(0)} min", Modifier.weight(1f))
                        RouteDivider()
                        RouteMetric("₱", "₱${shownFare.roundToInt().coerceAtLeast(0)}", Modifier.weight(1f))
                        RouteDivider()
                        RouteMetric("◇", "${shownLegs.coerceAtLeast(0)} legs", Modifier.weight(1f))
                    }
                }
            }

            item { Text("Step-by-step guide", color = NavDark, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold) }

            if (steps.isEmpty()) {
                item {
                    Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = NavSurface) {
                        Text("Choose a route again to see its step-by-step guide.", Modifier.padding(18.dp), color = NavMuted, fontSize = 13.sp)
                    }
                }
            } else {
                item { RouteTimelineSteps(steps) }
            }

            navigationStartError?.let { message ->
                item { Text(message, color = Color.Red, fontSize = 13.sp, fontWeight = FontWeight.SemiBold) }
            }

            item {
                Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = NavTip) {
                    Row(Modifier.padding(15.dp), verticalAlignment = Alignment.Top) {
                        Surface(Modifier.size(26.dp), shape = CircleShape, color = NavTeal) {
                            Box(contentAlignment = Alignment.Center) { Text("i", color = Color.White, fontWeight = FontWeight.Bold, fontSize = 13.sp) }
                        }
                        Spacer(Modifier.width(10.dp))
                        Text("Tip: Prepare exact fare or have small bills for a smoother ride.", color = NavDark, fontSize = 12.sp, lineHeight = 17.sp, fontWeight = FontWeight.SemiBold)
                    }
                }
            }
        }

        Column(
            Modifier
                .fillMaxWidth()
                .padding(start = 16.dp, end = 16.dp, bottom = 22.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            if (hasActiveTrip) {
                Button(
                    onClick = onResumeActiveTrip,
                    enabled = !isStartingNavigation,
                    modifier = Modifier.fillMaxWidth().height(54.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = NavTeal, contentColor = Color.White),
                    shape = RoundedCornerShape(18.dp)
                ) {
                    Text("Resume Active Trip", fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
                }
                OutlinedButton(
                    onClick = onEndActiveTrip,
                    enabled = !isStartingNavigation,
                    modifier = Modifier.fillMaxWidth().height(54.dp),
                    shape = RoundedCornerShape(18.dp)
                ) {
                    Text("End Active Trip", color = NavOrange, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
                }
            }

            Button(
                onClick = onStartTracking,
                enabled = !isStartingNavigation && !hasActiveTrip,
                modifier = Modifier.fillMaxWidth().height(54.dp),
                colors = ButtonDefaults.buttonColors(containerColor = NavTeal, contentColor = Color.White),
                shape = RoundedCornerShape(18.dp)
            ) {
                if (isStartingNavigation) {
                    CircularProgressIndicator(Modifier.size(20.dp), color = Color.White, strokeWidth = 2.dp)
                    Spacer(Modifier.width(10.dp))
                    Text("Working...", fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
                } else {
                    Text("Start Trip  →", fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
                }
            }
        }
    }
}

@Composable
private fun RouteMetric(icon: String, value: String, modifier: Modifier = Modifier) {
    Row(modifier, horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        Text(icon, color = NavDark, fontSize = 16.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.width(6.dp))
        Text(value, color = NavDark, fontSize = 12.sp, fontWeight = FontWeight.ExtraBold)
    }
}

@Composable
private fun RouteDivider() {
    Box(Modifier.width(1.dp).height(20.dp).background(NavMuted.copy(alpha = 0.25f)))
}

@Composable
private fun RouteTimelineSteps(steps: List<CommuteStep>) {
    Box(Modifier.fillMaxWidth()) {
        Box(
            Modifier
                .matchParentSize()
                .padding(start = 8.dp, top = 20.dp, bottom = 20.dp)
        ) {
            Box(Modifier.width(2.dp).fillMaxHeight().background(NavOrange))
        }
        Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
            steps.forEach { step -> RouteTimelineCard(step) }
        }
    }
}

@Composable
private fun RouteTimelineCard(step: CommuteStep) {
    Row(Modifier.fillMaxWidth()) {
        Box(Modifier.width(18.dp).padding(top = 18.dp), contentAlignment = Alignment.TopCenter) {
            Box(Modifier.size(10.dp).background(NavOrange, CircleShape))
        }
        Spacer(Modifier.width(3.dp))
        Surface(Modifier.weight(1f), shape = RoundedCornerShape(18.dp), color = NavSurface, shadowElevation = 1.dp) {
            Row(Modifier.padding(14.dp), verticalAlignment = Alignment.Top) {
                Surface(Modifier.size(48.dp), shape = RoundedCornerShape(14.dp), color = NavIconBlue) {
                    Box(contentAlignment = Alignment.Center) { Text(routeStepIcon(step.mode), fontSize = 23.sp) }
                }
                Spacer(Modifier.width(12.dp))
                Column(Modifier.weight(1f)) {
                    Text(routeStepTitle(step), color = NavDark, fontSize = 14.sp, fontWeight = FontWeight.ExtraBold)
                    Spacer(Modifier.height(2.dp))
                    Text(routeStepMeta(step), color = NavMuted, fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
                    step.instructions?.takeIf { it.isNotBlank() }?.let { instruction ->
                        Spacer(Modifier.height(7.dp))
                        instruction.lines().filter { it.isNotBlank() }.take(2).forEach { line ->
                            Text("• ${line.trim().removePrefix("•").trim()}", color = NavMuted, fontSize = 10.sp, lineHeight = 15.sp)
                        }
                    }
                    if (step.instructions.isNullOrBlank()) {
                        Spacer(Modifier.height(7.dp))
                        Text("• ${step.from}", color = NavMuted, fontSize = 10.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Text("• ${step.to}", color = NavMuted, fontSize = 10.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
                    }
                }
                Text("⌖", color = Color(0xFF4D8DFF), fontSize = 18.sp)
            }
        }
    }
}

private fun routeStepIcon(mode: String): String = when {
    mode.contains("walk", true) -> "🚶"
    mode.contains("trike", true) || mode.contains("tricycle", true) -> "🛺"
    mode.contains("jeep", true) || mode.contains("bus", true) -> "🚌"
    else -> "📍"
}

private fun routeStepTitle(step: CommuteStep): String = when {
    step.mode.contains("walk", true) -> "Walk to ${step.to}"
    step.mode.contains("trike", true) || step.mode.contains("tricycle", true) -> "Ride Tricycle"
    step.mode.contains("jeep", true) || step.mode.contains("bus", true) -> "Ride Jeepney"
    else -> step.mode
}

private fun routeStepMeta(step: CommuteStep): String {
    val second = when {
        step.mode.contains("walk", true) && step.distanceMeters != null -> "${step.distanceMeters.roundToInt()} m"
        step.fare != null -> "₱${step.fare.roundToInt()}"
        step.distanceMeters != null -> "${step.distanceMeters.roundToInt()} m"
        else -> null
    }
    return listOfNotNull("${step.minutes} mins", second).joinToString(" • ")
}
