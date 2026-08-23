package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
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
import com.example.frontend.model.RecentCommute
import org.maplibre.android.geometry.LatLng
import kotlin.math.roundToInt

import androidx.compose.material3.MaterialTheme
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiForest
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiForestSurface

@Composable
fun CommuteDetailScreen(
    commute: RecentCommute,
    legGeometries: List<List<LatLng>> = emptyList(),
    isGeometryLoading: Boolean = false,
    onBack: () -> Unit = {},
    onRepeatTrip: () -> Unit = {}
) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 12.dp, bottom = 22.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(40.dp).clickable(onClick = onBack), contentAlignment = Alignment.Center) {
                    Text("←", color = TukiInk, style = MaterialTheme.typography.displaySmall)
                }
                Text(
                    "Route Details",
                    Modifier.weight(1f),
                    color = TukiInk,
                    style = MaterialTheme.typography.displaySmall
                )
            }
        }

        item {
            Text("${commute.origin} →\n${commute.destination}", color = TukiInk, style = MaterialTheme.typography.titleLarge)
        }

        item {
            Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiSurfaceRaised, shadowElevation = 1.dp) {
                Row(Modifier.fillMaxWidth().padding(horizontal = 10.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                    SummaryMetric("◷", "${commute.minutes} min", Modifier.weight(1f))
                    VerticalDivider()
                    SummaryMetric("₱", "₱${commute.totalFare.roundToInt()}", Modifier.weight(1f))
                    VerticalDivider()
                    SummaryMetric("◇", "${commute.legs} legs", Modifier.weight(1f))
                }
            }
        }

        item { Text("Step-by-step guide", color = TukiInk, style = MaterialTheme.typography.titleMedium) }

        if (commute.steps.isEmpty()) {
            item {
                Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiSurfaceRaised) {
                    Text("No step-by-step breakdown was saved for this trip.", Modifier.padding(18.dp), color = TukiMuted, style = MaterialTheme.typography.bodySmall)
                }
            }
        } else {
            item { TimelineSteps(commute.steps) }
        }

        item {
            Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiForestSurface) {
                Row(Modifier.padding(15.dp), verticalAlignment = Alignment.Top) {
                    Surface(Modifier.size(26.dp), shape = CircleShape, color = TukiTeal) {
                        Box(contentAlignment = Alignment.Center) { Text("i", color = Color.White, style = MaterialTheme.typography.labelLarge) }
                    }
                    Spacer(Modifier.width(10.dp))
                    Text("Tip: Prepare exact fare or have small bills for a smoother ride.", color = TukiInk, style = MaterialTheme.typography.bodySmall)
                }
            }
        }

        item { Spacer(Modifier.height(34.dp)) }

        item {
            Button(
                onClick = onRepeatTrip,
                modifier = Modifier.fillMaxWidth().height(54.dp),
                colors = ButtonDefaults.buttonColors(containerColor = TukiTeal),
                shape = RoundedCornerShape(18.dp)
            ) { Text("Start Trip  →", color = Color.White, style = MaterialTheme.typography.titleMedium) }
        }
    }
}

@Composable
private fun TimelineSteps(steps: List<CommuteStep>) {
    Box(Modifier.fillMaxWidth()) {
        Box(
            Modifier
                .matchParentSize()
                .padding(start = 8.dp, top = 20.dp, bottom = 20.dp)
        ) {
            Box(Modifier.width(2.dp).fillMaxHeight().background(TukiOrange))
        }
        Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
            steps.forEach { step -> StepTimelineCard(step = step) }
        }
    }
}

@Composable
private fun SummaryMetric(icon: String, value: String, modifier: Modifier = Modifier) {
    Row(modifier, horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        Text(icon, color = TukiInk, style = MaterialTheme.typography.titleMedium)
        Spacer(Modifier.width(6.dp))
        Text(
            value,
            color = TukiInk,
            style = MaterialTheme.typography.labelLarge
        )
    }
}

@Composable
private fun VerticalDivider() {
    Box(Modifier.width(1.dp).height(20.dp).background(TukiMuted.copy(alpha = 0.25f)))
}

@Composable
private fun StepTimelineCard(step: CommuteStep) {
    Row(Modifier.fillMaxWidth()) {
        Box(Modifier.width(18.dp).padding(top = 18.dp), contentAlignment = Alignment.TopCenter) {
            Box(Modifier.size(10.dp).background(TukiOrange, CircleShape))
        }
        Spacer(Modifier.width(3.dp))
        Surface(Modifier.weight(1f), shape = RoundedCornerShape(18.dp), color = TukiSurfaceRaised, shadowElevation = 1.dp) {
            Row(Modifier.padding(14.dp), verticalAlignment = Alignment.Top) {
                Surface(Modifier.size(48.dp), shape = RoundedCornerShape(14.dp), color = TukiSky) {
                    Box(contentAlignment = Alignment.Center) { Text(stepIcon(step.mode), style = MaterialTheme.typography.titleLarge) }
                }
                Spacer(Modifier.width(12.dp))
                Column(Modifier.weight(1f)) {
                    Text(stepTitle(step), color = TukiInk, style = MaterialTheme.typography.titleMedium)
                    Spacer(Modifier.height(2.dp))
                    Text(stepMeta(step), color = TukiMuted, style = MaterialTheme.typography.labelSmall)
                    step.instructions?.takeIf { it.isNotBlank() }?.let { instruction ->
                        Spacer(Modifier.height(7.dp))
                        instruction.lines().filter { it.isNotBlank() }.take(2).forEach { line ->
                            Text("• ${line.trim().removePrefix("•").trim()}", color = TukiMuted, style = MaterialTheme.typography.bodySmall)
                        }
                    }
                    if (step.instructions.isNullOrBlank()) {
                        Spacer(Modifier.height(7.dp))
                        Text("• ${step.from}", color = TukiMuted, style = MaterialTheme.typography.bodySmall, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Text("• ${step.to}", color = TukiMuted, style = MaterialTheme.typography.bodySmall, maxLines = 2, overflow = TextOverflow.Ellipsis)
                    }
                }
                Text("⌖", color = TukiTeal, style = MaterialTheme.typography.titleMedium)
            }
        }
    }
}

private fun stepIcon(mode: String): String = when {
    mode.contains("walk", true) -> "🚶"
    mode.contains("trike", true) || mode.contains("tricycle", true) -> "🛺"
    mode.contains("jeep", true) || mode.contains("bus", true) -> "🚌"
    else -> "📍"
}

private fun stepTitle(step: CommuteStep): String = when {
    step.mode.contains("walk", true) -> "Walk to ${step.to}"
    step.mode.contains("trike", true) || step.mode.contains("tricycle", true) -> "Ride Tricycle"
    step.mode.contains("jeep", true) || step.mode.contains("bus", true) -> "Ride Jeepney"
    else -> step.mode
}

private fun stepMeta(step: CommuteStep): String {
    val second = when {
        step.mode.contains("walk", true) && step.distanceMeters != null -> "${step.distanceMeters.roundToInt()} m"
        step.fare != null -> "₱${step.fare.roundToInt()}"
        step.distanceMeters != null -> "${step.distanceMeters.roundToInt()} m"
        else -> null
    }
    return listOfNotNull("${step.minutes} mins", second).joinToString(" • ")
}
