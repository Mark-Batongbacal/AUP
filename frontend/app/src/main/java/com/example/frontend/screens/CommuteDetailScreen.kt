package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
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

private val DetailBg = Color(0xFFF8F5EC)
private val DetailSurface = Color(0xFFFFFBF0)
private val DetailDark = Color(0xFF153E4B)
private val DetailTeal = Color(0xFF2C8E95)
private val DetailMuted = Color(0xFF7A898E)
private val DetailOrange = Color(0xFFF4BF52)
private val DetailIconBlue = Color(0xFFE7F2F3)
private val DetailTip = Color(0xFFE8F0EB)

@Composable
fun CommuteDetailScreen(
    commute: RecentCommute,
    legGeometries: List<List<LatLng>> = emptyList(),
    isGeometryLoading: Boolean = false,
    onBack: () -> Unit = {},
    onRepeatTrip: () -> Unit = {}
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize().background(DetailBg),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 20.dp, bottom = 22.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(40.dp).clickable(onClick = onBack), contentAlignment = Alignment.Center) {
                    Text("←", color = DetailDark, fontSize = 26.sp, fontWeight = FontWeight.Bold)
                }
                Text("Route Details", Modifier.weight(1f), color = DetailDark, fontSize = 23.sp, fontWeight = FontWeight.ExtraBold)
            }
        }

        item {
            Text("${commute.origin} →\n${commute.destination}", color = DetailDark, fontSize = 17.sp, lineHeight = 23.sp, fontWeight = FontWeight.ExtraBold)
        }

        item {
            Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = DetailSurface, shadowElevation = 1.dp) {
                Row(Modifier.fillMaxWidth().padding(horizontal = 10.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                    SummaryMetric("◷", "${commute.minutes} min", Modifier.weight(1f))
                    VerticalDivider()
                    SummaryMetric("₱", "₱${commute.totalFare.roundToInt()}", Modifier.weight(1f))
                    VerticalDivider()
                    SummaryMetric("◇", "${commute.legs} legs", Modifier.weight(1f))
                }
            }
        }

        item { Text("Step-by-step guide", color = DetailDark, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold) }

        if (commute.steps.isEmpty()) {
            item {
                Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = DetailSurface) {
                    Text("No step-by-step breakdown was saved for this trip.", Modifier.padding(18.dp), color = DetailMuted, fontSize = 13.sp)
                }
            }
        } else {
            item { TimelineSteps(commute.steps) }
        }

        item {
            Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = DetailTip) {
                Row(Modifier.padding(15.dp), verticalAlignment = Alignment.Top) {
                    Surface(Modifier.size(26.dp), shape = CircleShape, color = DetailTeal) {
                        Box(contentAlignment = Alignment.Center) { Text("i", color = Color.White, fontWeight = FontWeight.Bold, fontSize = 13.sp) }
                    }
                    Spacer(Modifier.width(10.dp))
                    Text("Tip: Prepare exact fare or have small bills for a smoother ride.", color = DetailDark, fontSize = 12.sp, lineHeight = 17.sp, fontWeight = FontWeight.SemiBold)
                }
            }
        }

        item { Spacer(Modifier.height(34.dp)) }

        item {
            Button(
                onClick = onRepeatTrip,
                modifier = Modifier.fillMaxWidth().height(54.dp),
                colors = ButtonDefaults.buttonColors(containerColor = DetailTeal),
                shape = RoundedCornerShape(18.dp)
            ) { Text("Start Trip  →", color = Color.White, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold) }
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
            Box(Modifier.width(2.dp).fillMaxHeight().background(DetailOrange))
        }
        Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
            steps.forEach { step -> StepTimelineCard(step = step) }
        }
    }
}

@Composable
private fun SummaryMetric(icon: String, value: String, modifier: Modifier = Modifier) {
    Row(modifier, horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        Text(icon, color = DetailDark, fontSize = 16.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.width(6.dp))
        Text(value, color = DetailDark, fontSize = 12.sp, fontWeight = FontWeight.ExtraBold)
    }
}

@Composable
private fun VerticalDivider() {
    Box(Modifier.width(1.dp).height(20.dp).background(DetailMuted.copy(alpha = 0.25f)))
}

@Composable
private fun StepTimelineCard(step: CommuteStep) {
    Row(Modifier.fillMaxWidth()) {
        Box(Modifier.width(18.dp).padding(top = 18.dp), contentAlignment = Alignment.TopCenter) {
            Box(Modifier.size(10.dp).background(DetailOrange, CircleShape))
        }
        Spacer(Modifier.width(3.dp))
        Surface(Modifier.weight(1f), shape = RoundedCornerShape(18.dp), color = DetailSurface, shadowElevation = 1.dp) {
            Row(Modifier.padding(14.dp), verticalAlignment = Alignment.Top) {
                Surface(Modifier.size(48.dp), shape = RoundedCornerShape(14.dp), color = DetailIconBlue) {
                    Box(contentAlignment = Alignment.Center) { Text(stepIcon(step.mode), fontSize = 23.sp) }
                }
                Spacer(Modifier.width(12.dp))
                Column(Modifier.weight(1f)) {
                    Text(stepTitle(step), color = DetailDark, fontSize = 14.sp, fontWeight = FontWeight.ExtraBold)
                    Spacer(Modifier.height(2.dp))
                    Text(stepMeta(step), color = DetailMuted, fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
                    step.instructions?.takeIf { it.isNotBlank() }?.let { instruction ->
                        Spacer(Modifier.height(7.dp))
                        instruction.lines().filter { it.isNotBlank() }.take(2).forEach { line ->
                            Text("• ${line.trim().removePrefix("•").trim()}", color = DetailMuted, fontSize = 10.sp, lineHeight = 15.sp)
                        }
                    }
                    if (step.instructions.isNullOrBlank()) {
                        Spacer(Modifier.height(7.dp))
                        Text("• ${step.from}", color = DetailMuted, fontSize = 10.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Text("• ${step.to}", color = DetailMuted, fontSize = 10.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
                    }
                }
                Text("⌖", color = Color(0xFF4D8DFF), fontSize = 18.sp)
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
