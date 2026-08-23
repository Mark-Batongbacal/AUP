package com.example.frontend.screens

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.MapScreen
import com.example.frontend.MapVisualStyle
import com.example.frontend.core.location.RouteCoordinate
import com.example.frontend.model.CommuteStep
import com.example.frontend.navigation.joinedNavigationLegs
import kotlinx.coroutines.launch
import org.maplibre.android.geometry.LatLng
import kotlin.math.roundToInt

private val NavBg = com.example.frontend.ui.theme.TukiCream
private val NavSurface = com.example.frontend.ui.theme.TukiSurfaceRaised
private val NavDark = com.example.frontend.ui.theme.TukiInk
private val NavTeal = com.example.frontend.ui.theme.TukiTeal
private val NavMuted = com.example.frontend.ui.theme.TukiMuted
private val NavOrange = com.example.frontend.ui.theme.TukiGold
private val NavIconBlue = com.example.frontend.ui.theme.TukiSky
private val NavTip = com.example.frontend.ui.theme.TukiForestSurface
private const val RoutePreviewListIndex = 3

@Composable
fun NavigationScreen(
    origin: String,
    destination: String,
    steps: List<CommuteStep>,
    totalMinutes: Int? = null,
    totalFare: Double? = null,
    legCount: Int? = null,
    legRoutePoints: List<List<LatLng>> = emptyList(),
    routeStartPoint: LatLng? = null,
    routeFinalDestination: LatLng? = null,
    isStartingNavigation: Boolean = false,
    navigationStartError: String? = null,
    hasActiveTrip: Boolean = false,
    activeTripDescription: String? = null,
    onBack: () -> Unit = {},
    onStartTracking: () -> Unit = {},
    onResumeActiveTrip: () -> Unit = {},
    onReplaceActiveTrip: () -> Unit = {}
) {
    val shownMinutes = totalMinutes ?: steps.sumOf { it.minutes }
    val shownFare = totalFare ?: steps.sumOf { it.fare ?: 0.0 }
    val shownLegs = legCount ?: steps.size
    val routeListState = rememberLazyListState()
    val scope = rememberCoroutineScope()
    var showReplacementConfirmation by remember { mutableStateOf(false) }
    var selectedLegIndex by remember(origin, destination) { mutableStateOf<Int?>(null) }
    val fullRoutePoints = remember(legRoutePoints) {
        joinedNavigationLegs(
            legRoutePoints.map { leg ->
                leg.map { point -> RouteCoordinate(point.latitude, point.longitude) }
            }
        ).map { point -> LatLng(point.latitude, point.longitude) }
    }
    val selectedLegPoints = selectedLegIndex
        ?.let { legRoutePoints.getOrNull(it) }
        ?.takeIf { it.size >= 2 }
    val displayedRoutePoints = selectedLegPoints ?: fullRoutePoints
    val displayedStart = selectedLegPoints?.firstOrNull()
        ?: routeStartPoint
        ?: displayedRoutePoints.firstOrNull()
    val displayedDestination = selectedLegPoints?.lastOrNull()
        ?: routeFinalDestination
        ?: displayedRoutePoints.lastOrNull()
    val renderedRoutePoints = selectedLegPoints
        ?: legRoutePoints.firstOrNull { points -> points.size >= 2 }
        ?: displayedRoutePoints
    val contextualLegs = if (selectedLegPoints != null) {
        legRoutePoints.filterIndexed { index, points ->
            index != selectedLegIndex && points.size >= 2
        }
    } else {
        legRoutePoints.filter { points -> points.size >= 2 }.drop(1)
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(NavBg)
    ) {
        LazyColumn(
            state = routeListState,
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
                        fontWeight = FontWeight.ExtraBold,
                        fontFamily = com.example.frontend.ui.theme.TukiDisplayFontFamily
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

            if (displayedRoutePoints.isNotEmpty()) {
                item {
                    RoutePreviewCard(
                        routePoints = renderedRoutePoints,
                        routeBoundsPoints = displayedRoutePoints,
                        contextualLegs = contextualLegs,
                        startPoint = displayedStart,
                        destinationPoint = displayedDestination,
                        finalDestination = routeFinalDestination,
                        selectedStep = selectedLegIndex?.let { index -> steps.getOrNull(index) },
                        onShowFullRoute = { selectedLegIndex = null }
                    )
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
                item {
                    RouteTimelineSteps(
                        steps = steps,
                        selectedLegIndex = selectedLegIndex,
                        legRoutePoints = legRoutePoints,
                        onLegSelected = { index ->
                            selectedLegIndex = if (selectedLegIndex == index) null else index
                            scope.launch { routeListState.animateScrollToItem(RoutePreviewListIndex) }
                        }
                    )
                }
            }

            navigationStartError?.let { message ->
                item { Text(message, color = com.example.frontend.ui.theme.TukiDanger, fontSize = 13.sp, fontWeight = FontWeight.SemiBold) }
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
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    color = NavTip
                ) {
                    Column(Modifier.padding(horizontal = 16.dp, vertical = 13.dp)) {
                        Text(
                            "Current trip is still active",
                            color = NavDark,
                            fontSize = 14.sp,
                            fontWeight = FontWeight.ExtraBold
                        )
                        activeTripDescription?.takeIf { it.isNotBlank() }?.let { description ->
                            Spacer(Modifier.height(3.dp))
                            Text(
                                description,
                                color = NavMuted,
                                fontSize = 12.sp,
                                maxLines = 2,
                                overflow = TextOverflow.Ellipsis
                            )
                        }
                    }
                }
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
                    onClick = { showReplacementConfirmation = true },
                    enabled = !isStartingNavigation,
                    modifier = Modifier.fillMaxWidth().height(54.dp),
                    shape = RoundedCornerShape(18.dp)
                ) {
                    Text(
                        "End Current & Start This Trip",
                        color = NavOrange,
                        fontSize = 14.sp,
                        fontWeight = FontWeight.ExtraBold
                    )
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

    if (showReplacementConfirmation) {
        AlertDialog(
            onDismissRequest = {
                if (!isStartingNavigation) showReplacementConfirmation = false
            },
            title = { Text("Start this trip instead?") },
            text = {
                Text(
                    "Your current trip will end, then TUKI will immediately start the route you selected."
                )
            },
            confirmButton = {
                TextButton(
                    enabled = !isStartingNavigation,
                    onClick = {
                        showReplacementConfirmation = false
                        onReplaceActiveTrip()
                    }
                ) {
                    Text("End & Start New", color = com.example.frontend.ui.theme.TukiDanger)
                }
            },
            dismissButton = {
                TextButton(
                    enabled = !isStartingNavigation,
                    onClick = { showReplacementConfirmation = false }
                ) {
                    Text("Keep Current Trip", color = NavTeal)
                }
            }
        )
    }
}

@Composable
private fun RoutePreviewCard(
    routePoints: List<LatLng>,
    routeBoundsPoints: List<LatLng>,
    contextualLegs: List<List<LatLng>>,
    startPoint: LatLng?,
    destinationPoint: LatLng?,
    finalDestination: LatLng?,
    selectedStep: CommuteStep?,
    onShowFullRoute: () -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        color = NavSurface,
        shadowElevation = 2.dp
    ) {
        Column(Modifier.padding(10.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth().padding(start = 5.dp, bottom = 6.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(Modifier.weight(1f)) {
                    Text(
                        if (selectedStep == null) "Your complete route" else routeStepTitle(selectedStep),
                        color = NavDark,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.ExtraBold,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        if (selectedStep == null) "Tap a step to inspect its route" else "Selected travel segment",
                        color = NavMuted,
                        fontSize = 10.sp
                    )
                }
                if (selectedStep != null) {
                    TextButton(onClick = onShowFullRoute) {
                        Text("Full route", color = NavTeal, fontSize = 11.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }
            MapScreen(
                routePoints = routePoints,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(218.dp)
                    .clip(RoundedCornerShape(15.dp)),
                startPoint = startPoint,
                selectedDestination = destinationPoint,
                finalDestination = finalDestination,
                futureRouteSegments = if (selectedStep == null) contextualLegs else emptyList(),
                transitRoutes = emptyList(),
                todaPoints = emptyList(),
                visualStyle = MapVisualStyle.LiveTrip,
                showDeviceLocation = false,
                fitRouteBounds = true,
                routeBoundsPoints = routeBoundsPoints
            )
        }
    }
}

@Composable
private fun RouteMetric(icon: String, value: String, modifier: Modifier = Modifier) {
    Row(modifier, horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        Text(icon, color = NavDark, fontSize = 16.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.width(6.dp))
        Text(
            value,
            color = NavDark,
            fontSize = 12.sp,
            fontWeight = FontWeight.ExtraBold,
            fontFamily = com.example.frontend.ui.theme.TukiUtilityFontFamily
        )
    }
}

@Composable
private fun RouteDivider() {
    Box(Modifier.width(1.dp).height(20.dp).background(NavMuted.copy(alpha = 0.25f)))
}

@Composable
private fun RouteTimelineSteps(
    steps: List<CommuteStep>,
    selectedLegIndex: Int?,
    legRoutePoints: List<List<LatLng>>,
    onLegSelected: (Int) -> Unit
) {
    Box(Modifier.fillMaxWidth()) {
        Box(
            Modifier
                .matchParentSize()
                .padding(start = 8.dp, top = 20.dp, bottom = 20.dp)
        ) {
            Box(Modifier.width(2.dp).fillMaxHeight().background(NavOrange))
        }
        Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
            steps.forEachIndexed { index, step ->
                RouteTimelineCard(
                    step = step,
                    selected = selectedLegIndex == index,
                    selectable = (legRoutePoints.getOrNull(index)?.size ?: 0) >= 2,
                    onClick = { onLegSelected(index) }
                )
            }
        }
    }
}

@Composable
private fun RouteTimelineCard(
    step: CommuteStep,
    selected: Boolean,
    selectable: Boolean,
    onClick: () -> Unit
) {
    Row(Modifier.fillMaxWidth()) {
        Box(Modifier.width(18.dp).padding(top = 18.dp), contentAlignment = Alignment.TopCenter) {
            Box(Modifier.size(10.dp).background(NavOrange, CircleShape))
        }
        Spacer(Modifier.width(3.dp))
        Surface(
            modifier = Modifier.weight(1f).clickable(enabled = selectable, onClick = onClick),
            shape = RoundedCornerShape(18.dp),
            color = if (selected) NavTip else NavSurface,
            border = if (selected) BorderStroke(1.dp, NavTeal.copy(alpha = 0.45f)) else null,
            shadowElevation = 1.dp
        ) {
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
                Text(
                    "⌖",
                    color = if (selectable) NavTeal else NavMuted.copy(alpha = 0.45f),
                    fontSize = 18.sp
                )
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
