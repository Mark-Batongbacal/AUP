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
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.example.frontend.MapScreen
import com.example.frontend.MapVisualStyle
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RecentCommute
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiForestSurface
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import org.maplibre.android.geometry.LatLng
import kotlin.math.roundToInt

@Composable
fun CommuteDetailScreen(
    commute: RecentCommute,
    legGeometries: List<List<LatLng>> = emptyList(),
    isGeometryLoading: Boolean = false,
    onBack: () -> Unit = {},
    onRepeatTrip: () -> Unit = {}
) {
    var selectedLegIndex by remember(commute.id) { mutableStateOf<Int?>(null) }
    val usableLegs = legGeometries.map { points -> points.takeIf { it.size >= 2 }.orEmpty() }
    val selectedLeg = selectedLegIndex?.let { index -> usableLegs.getOrNull(index) }?.takeIf { it.size >= 2 }
    val allRoutePoints = usableLegs.filter { it.size >= 2 }.flatten()
    val primaryRoute = selectedLeg ?: usableLegs.firstOrNull { it.size >= 2 } ?: emptyList()
    val contextualLegs = if (selectedLeg != null) emptyList() else usableLegs.filter { it.size >= 2 }.drop(1)
    val mapBounds = selectedLeg ?: allRoutePoints
    val mapStart = selectedLeg?.firstOrNull()
        ?: allRoutePoints.firstOrNull()
        ?: commute.originLatitude?.let { lat -> commute.originLongitude?.let { lon -> LatLng(lat, lon) } }
    val mapDestination = selectedLeg?.lastOrNull()
        ?: allRoutePoints.lastOrNull()
        ?: commute.destinationLatitude?.let { lat -> commute.destinationLongitude?.let { lon -> LatLng(lat, lon) } }
    val finalDestination = commute.destinationLatitude?.let { lat -> commute.destinationLongitude?.let { lon -> LatLng(lat, lon) } }

    LazyColumn(
        modifier = Modifier.fillMaxSize().background(TukiCream).statusBarsPadding(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 12.dp, bottom = 22.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(40.dp).clickable(onClick = onBack), contentAlignment = Alignment.Center) {
                    Text("←", color = TukiInk, style = MaterialTheme.typography.displaySmall)
                }
                Text(TukiInterfaceText.routeDetails, Modifier.weight(1f), color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
        }

        item { Text("${commute.origin} →\n${commute.destination}", color = TukiInk, style = MaterialTheme.typography.titleLarge) }

        item {
            Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiSurfaceRaised, shadowElevation = 1.dp) {
                Row(Modifier.fillMaxWidth().padding(horizontal = 10.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                    SummaryMetric("◷", "${commute.minutes} min", Modifier.weight(1f))
                    VerticalDivider()
                    SummaryMetric("₱", "₱${commute.totalFare.roundToInt()}", Modifier.weight(1f))
                    VerticalDivider()
                    SummaryMetric("◇", "${commute.legs} ${if (TukiInterfaceText.isFilipino) "hakbang" else "legs"}", Modifier.weight(1f))
                }
            }
        }

        if (isGeometryLoading || primaryRoute.isNotEmpty()) {
            item {
                HistoryRoutePreview(
                    routePoints = primaryRoute,
                    routeBoundsPoints = mapBounds,
                    contextualLegs = contextualLegs,
                    startPoint = mapStart,
                    destinationPoint = mapDestination,
                    finalDestination = finalDestination,
                    selectedStep = selectedLegIndex?.let { index -> commute.steps.getOrNull(index) },
                    isLoading = isGeometryLoading,
                    onShowFullRoute = { selectedLegIndex = null }
                )
            }
        }

        item { Text(TukiInterfaceText.stepByStepGuide, color = TukiInk, style = MaterialTheme.typography.titleMedium) }

        if (commute.steps.isEmpty()) {
            item {
                Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiSurfaceRaised) {
                    Text(
                        if (TukiInterfaceText.isFilipino) "Walang na-save na Step-by-step guide para sa biyaheng ito."
                        else "No step-by-step breakdown was saved for this trip.",
                        Modifier.padding(18.dp),
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        } else {
            item {
                TimelineSteps(
                    steps = commute.steps,
                    selectedLegIndex = selectedLegIndex,
                    selectableLegs = usableLegs,
                    onLegSelected = { index ->
                        if ((usableLegs.getOrNull(index)?.size ?: 0) >= 2) {
                            selectedLegIndex = if (selectedLegIndex == index) null else index
                        }
                    }
                )
            }
        }

        item {
            Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiForestSurface) {
                Row(Modifier.padding(15.dp), verticalAlignment = Alignment.Top) {
                    Surface(Modifier.size(26.dp), shape = CircleShape, color = TukiTeal) {
                        Box(contentAlignment = Alignment.Center) { Text("i", color = Color.White, style = MaterialTheme.typography.labelLarge) }
                    }
                    Spacer(Modifier.width(10.dp))
                    Text(TukiInterfaceText.tipPrepareFare, color = TukiInk, style = MaterialTheme.typography.bodySmall)
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
            ) { Text("${TukiInterfaceText.startTrip}  →", color = Color.White, style = MaterialTheme.typography.titleMedium) }
        }
    }
}

@Composable
private fun HistoryRoutePreview(
    routePoints: List<LatLng>,
    routeBoundsPoints: List<LatLng>,
    contextualLegs: List<List<LatLng>>,
    startPoint: LatLng?,
    destinationPoint: LatLng?,
    finalDestination: LatLng?,
    selectedStep: CommuteStep?,
    isLoading: Boolean,
    onShowFullRoute: () -> Unit
) {
    Surface(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(20.dp), color = TukiSurfaceRaised, shadowElevation = 2.dp) {
        Column(Modifier.padding(10.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth().padding(start = 5.dp, bottom = 6.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(Modifier.weight(1f)) {
                    Text(
                        if (selectedStep == null) {
                            if (TukiInterfaceText.isFilipino) "Natapos mong ruta" else "Your completed route"
                        } else stepTitle(selectedStep),
                        color = TukiInk,
                        style = MaterialTheme.typography.titleMedium,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        if (selectedStep == null) TukiInterfaceText.tapStepInspect else TukiInterfaceText.selectedTravelSegment,
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
                if (selectedStep != null) {
                    TextButton(onClick = onShowFullRoute) {
                        Text(TukiInterfaceText.fullRoute, color = TukiTeal, style = MaterialTheme.typography.labelLarge)
                    }
                }
            }

            if (isLoading && routePoints.isEmpty()) {
                Box(modifier = Modifier.fillMaxWidth().height(260.dp), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator(color = TukiTeal)
                }
            } else if (routePoints.size >= 2) {
                MapScreen(
                    routePoints = routePoints,
                    modifier = Modifier.fillMaxWidth().height(260.dp).clip(RoundedCornerShape(15.dp)),
                    startPoint = startPoint,
                    selectedDestination = destinationPoint,
                    finalDestination = finalDestination,
                    futureRouteSegments = contextualLegs,
                    transitRoutes = emptyList(),
                    todaPoints = emptyList(),
                    visualStyle = MapVisualStyle.LiveTrip,
                    showDeviceLocation = false,
                    fitRouteBounds = true,
                    routeBoundsPoints = routeBoundsPoints.ifEmpty { routePoints },
                    routeInteractionControlsEnabled = true
                )
            }
        }
    }
}

@Composable
private fun TimelineSteps(
    steps: List<CommuteStep>,
    selectedLegIndex: Int?,
    selectableLegs: List<List<LatLng>>,
    onLegSelected: (Int) -> Unit
) {
    Box(Modifier.fillMaxWidth()) {
        Box(Modifier.matchParentSize().padding(start = 8.dp, top = 20.dp, bottom = 20.dp)) {
            Box(Modifier.width(2.dp).fillMaxHeight().background(TukiOrange))
        }
        Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
            steps.forEachIndexed { index, step ->
                StepTimelineCard(
                    step = step,
                    selected = selectedLegIndex == index,
                    selectable = (selectableLegs.getOrNull(index)?.size ?: 0) >= 2,
                    onClick = { onLegSelected(index) }
                )
            }
        }
    }
}

@Composable
private fun SummaryMetric(icon: String, value: String, modifier: Modifier = Modifier) {
    Row(modifier, horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        Text(icon, color = TukiInk, style = MaterialTheme.typography.titleMedium)
        Spacer(Modifier.width(6.dp))
        Text(value, color = TukiInk, style = MaterialTheme.typography.labelLarge)
    }
}

@Composable
private fun VerticalDivider() {
    Box(Modifier.width(1.dp).height(20.dp).background(TukiMuted.copy(alpha = 0.25f)))
}

@Composable
private fun StepTimelineCard(
    step: CommuteStep,
    selected: Boolean,
    selectable: Boolean,
    onClick: () -> Unit
) {
    Row(Modifier.fillMaxWidth()) {
        Box(Modifier.width(18.dp).padding(top = 18.dp), contentAlignment = Alignment.TopCenter) {
            Box(Modifier.size(10.dp).background(TukiOrange, CircleShape))
        }
        Spacer(Modifier.width(3.dp))
        Surface(
            modifier = Modifier.weight(1f).clickable(enabled = selectable, onClick = onClick),
            shape = RoundedCornerShape(18.dp),
            color = if (selected) TukiForestSurface else TukiSurfaceRaised,
            shadowElevation = 1.dp
        ) {
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
                Text("⌖", color = if (selectable) TukiTeal else TukiMuted.copy(alpha = 0.45f), style = MaterialTheme.typography.titleMedium)
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
    step.mode.contains("walk", true) -> "${TukiInterfaceText.walkTo} ${step.to}"
    step.mode.contains("trike", true) || step.mode.contains("tricycle", true) -> TukiInterfaceText.rideTricycle
    step.mode.contains("jeep", true) || step.mode.contains("bus", true) -> TukiInterfaceText.rideJeepney
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
