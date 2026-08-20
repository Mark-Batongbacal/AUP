package com.example.frontend.screens

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.MapScreen
import com.example.frontend.TodaPointOverlay
import com.example.frontend.TransitRouteOverlay
import com.example.frontend.components.ParaPoOverlay
import com.example.frontend.data.navigation.NavigationSnapshotDto
import org.maplibre.android.geometry.LatLng
import java.math.BigDecimal
import kotlin.math.pow
import kotlin.math.roundToInt

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

@Composable
fun TripTrackingScreen(
    origin: String,
    destination: String,
    routePoints: List<LatLng> = emptyList(),
    futureRouteSegments: List<List<LatLng>> = emptyList(),
    legDestination: LatLng? = null,
    finalDestination: LatLng? = null,
    nearbyJeepneyRoutes: List<TransitRouteOverlay> = emptyList(),
    todaPoints: List<TodaPointOverlay> = emptyList(),
    navigationSnapshot: NavigationSnapshotDto? = null,
    navigationError: String? = null,
    isNavigationActionInProgress: Boolean = false,
    tripOptionsEnabled: Boolean = true,
    onBack: () -> Unit = {},
    onEndTrip: () -> Unit = {},
    onRerouteNow: () -> Unit = {},
    onChangePreference: (String) -> Unit = {},
    onChangeBudget: (BigDecimal?, Boolean) -> Unit = { _, _ -> },
    onChangeDestination: (String) -> Unit = {},
    onConfirmBoarding: () -> Unit = {},
    onConfirmAlighting: () -> Unit = {},
    onArrivalAcknowledged: () -> Unit = {}
) {
    var showParaPoOverlay by remember { mutableStateOf(false) }
    var showExitTripDialog by remember { mutableStateOf(false) }
    var showArrivalDialog by remember { mutableStateOf(false) }
    var showTripOptions by remember { mutableStateOf(false) }

    LaunchedEffect(navigationSnapshot?.state) {
        if (navigationSnapshot?.state.equals("Arrived", ignoreCase = true)) {
            showArrivalDialog = true
            showTripOptions = false
        }
    }

    val instruction = navigationSnapshot?.displayInstruction()
        ?: navigationSnapshot?.nextInstruction?.let { next ->
            val mode = next.transportMode
                ?.lowercase()
                ?.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
            val routeName = next.routeName?.takeIf { it.isNotBlank() }
            listOfNotNull(next.type.takeIf { it.isNotBlank() }, mode, routeName)
                .joinToString(" · ")
        }
        ?: "Waiting for navigation guidance…"

    val remainingDistance = navigationSnapshot?.remainingDistanceMeters
    val distanceText = when {
        remainingDistance == null -> "Waiting for location update"
        remainingDistance >= 1000 -> "%.1f km remaining".format(remainingDistance / 1000.0)
        else -> "${remainingDistance.roundToInt()} m remaining"
    }

    val currentLegDistance = navigationSnapshot?.currentLeg?.distanceMeters
    val progressFraction = if (currentLegDistance != null && currentLegDistance > 0.0) {
        (navigationSnapshot.progressMeters / currentLegDistance).coerceIn(0.0, 1.0).toFloat()
    } else 0f

    val latitude = navigationSnapshot?.currentLatitude
    val longitude = navigationSnapshot?.currentLongitude
    val currentPosition = if (latitude != null && longitude != null) LatLng(latitude, longitude) else null
    val visibleRoutePoints = remember(routePoints, currentPosition) {
        routeFromCurrentPosition(routePoints, currentPosition)
    }

    val requiresBoarding = navigationSnapshot?.requiresBoardingConfirmation == true
    val requiresAlighting = navigationSnapshot?.requiresAlightingConfirmation == true
    val canUseParaPo = requiresAlighting ||
        navigationSnapshot?.nextInstruction?.type?.contains("alight", ignoreCase = true) == true
    val hasActiveTrip = navigationSnapshot != null &&
        !navigationSnapshot.state.equals("Arrived", ignoreCase = true) &&
        !navigationSnapshot.state.equals("Cancelled", ignoreCase = true)

    fun requestBack() {
        if (hasActiveTrip) showExitTripDialog = true else onBack()
    }

    BackHandler(enabled = hasActiveTrip) { showExitTripDialog = true }

    Box(modifier = Modifier.fillMaxSize()) {
        MapScreen(
            routePoints = visibleRoutePoints,
            startPoint = currentPosition,
            futureRouteSegments = futureRouteSegments,
            selectedDestination = legDestination,
            finalDestination = finalDestination,
            transitRoutes = nearbyJeepneyRoutes,
            todaPoints = todaPoints,
            modifier = Modifier.fillMaxSize()
        )

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(30.dp)
                .background(Color.White, RoundedCornerShape(20.dp))
                .padding(20.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(32.dp)
                        .background(TukiCream, RoundedCornerShape(8.dp))
                        .clickable(onClick = ::requestBack),
                    contentAlignment = Alignment.Center
                ) {
                    Text("‹", color = TukiDark, fontSize = 20.sp, fontWeight = FontWeight.Bold)
                }
                Spacer(Modifier.width(16.dp))
                Column(Modifier.weight(1f)) {
                    Text("Current Trip", color = TukiGray, fontSize = 13.sp, fontWeight = FontWeight.Bold)
                    Text("$origin → $destination", color = TukiDark, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
                    navigationSnapshot?.currentLeg?.routeName?.takeIf { it.isNotBlank() }?.let {
                        Text(it, color = TukiTeal, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                    }
                }
                if (hasActiveTrip && tripOptionsEnabled) {
                    FilledTonalButton(
                        onClick = { showTripOptions = true },
                        enabled = !isNavigationActionInProgress,
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)
                    ) { Text("Options", fontWeight = FontWeight.Bold) }
                }
            }
        }

        Surface(
            modifier = Modifier.align(Alignment.BottomCenter).fillMaxWidth().padding(20.dp),
            shape = RoundedCornerShape(24.dp),
            color = Color.White,
            tonalElevation = 8.dp,
            shadowElevation = 8.dp
        ) {
            Column(Modifier.padding(24.dp)) {
                Row(
                    Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(Modifier.weight(1f)) {
                        Text("NEXT STEP", color = TukiTeal, fontSize = 12.sp, fontWeight = FontWeight.ExtraBold)
                        Text(instruction, color = TukiDark, fontSize = 19.sp, fontWeight = FontWeight.ExtraBold)
                        Spacer(Modifier.height(4.dp))
                        Text(distanceText, color = TukiGray, fontSize = 14.sp)
                        navigationSnapshot?.landmark?.let {
                            Spacer(Modifier.height(4.dp))
                            Text("Near ${it.name}", color = TukiTeal, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        }
                        navigationError?.takeIf { it.isNotBlank() }?.let {
                            Spacer(Modifier.height(6.dp))
                            Text(it, color = MaterialTheme.colorScheme.error, fontSize = 12.sp)
                        }
                    }
                    Spacer(Modifier.width(12.dp))
                    Surface(
                        modifier = Modifier.size(64.dp).clickable(enabled = canUseParaPo && !isNavigationActionInProgress) {
                            showParaPoOverlay = true
                        },
                        shape = CircleShape,
                        color = if (canUseParaPo) TukiOrange.copy(alpha = 0.2f) else TukiGray.copy(alpha = 0.12f)
                    ) {
                        Box(contentAlignment = Alignment.Center) { Text("🔔", fontSize = 28.sp) }
                    }
                }

                if (requiresBoarding || requiresAlighting) {
                    Spacer(Modifier.height(16.dp))
                    Button(
                        onClick = if (requiresBoarding) onConfirmBoarding else onConfirmAlighting,
                        enabled = !isNavigationActionInProgress,
                        modifier = Modifier.fillMaxWidth(),
                        colors = ButtonDefaults.buttonColors(containerColor = if (requiresBoarding) TukiTeal else TukiOrange)
                    ) {
                        if (isNavigationActionInProgress) {
                            CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp, color = Color.White)
                            Spacer(Modifier.width(8.dp))
                        }
                        Text(if (requiresBoarding) "Confirm Board" else "Confirm Alight", fontWeight = FontWeight.Bold)
                    }
                }

                Spacer(Modifier.height(20.dp))
                LinearProgressIndicator(
                    progress = { progressFraction },
                    modifier = Modifier.fillMaxWidth().height(8.dp),
                    color = TukiTeal,
                    trackColor = TukiTeal.copy(alpha = 0.1f),
                    strokeCap = androidx.compose.ui.graphics.StrokeCap.Round
                )
                navigationSnapshot?.status?.takeIf { it.isNotBlank() }?.let {
                    Spacer(Modifier.height(8.dp))
                    Text(it.replace('_', ' '), color = TukiGray, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                }
            }
        }

        if (showParaPoOverlay) {
            Box(
                Modifier.fillMaxSize().background(Color.Black.copy(alpha = 0.4f)).clickable { showParaPoOverlay = false },
                contentAlignment = Alignment.Center
            ) { ParaPoOverlay(onDismiss = { showParaPoOverlay = false }) }
        }
    }

    if (showTripOptions) {
        TripOptionsSheet(
            isWorking = isNavigationActionInProgress,
            onDismiss = { showTripOptions = false },
            onRerouteNow = onRerouteNow,
            onPreferenceChange = onChangePreference,
            onBudgetChange = onChangeBudget,
            onDestinationChange = onChangeDestination,
            onEndTrip = onEndTrip
        )
    }

    if (showExitTripDialog) {
        AlertDialog(
            onDismissRequest = { showExitTripDialog = false },
            title = { Text("Trip is still active") },
            text = { Text("Going back will not end the navigation session. Continue the trip or end it first?") },
            confirmButton = {
                TextButton(onClick = onEndTrip, enabled = !isNavigationActionInProgress) {
                    Text("End Trip", color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = {
                TextButton(onClick = { showExitTripDialog = false }, enabled = !isNavigationActionInProgress) {
                    Text("Continue Trip")
                }
            }
        )
    }

    if (showArrivalDialog) {
        AlertDialog(
            onDismissRequest = {},
            title = { Text("You have arrived 🎉") },
            text = { Text("You've reached $destination. Your trip has been completed automatically.") },
            confirmButton = {
                Button(
                    onClick = { showArrivalDialog = false; onArrivalAcknowledged() },
                    colors = ButtonDefaults.buttonColors(containerColor = TukiTeal)
                ) { Text("Done") }
            }
        )
    }
}

private fun routeFromCurrentPosition(route: List<LatLng>, current: LatLng?): List<LatLng> {
    if (current == null || route.size < 2) return route
    val nearestIndex = route.indices.minByOrNull { index ->
        val point = route[index]
        (point.latitude - current.latitude).pow(2) + (point.longitude - current.longitude).pow(2)
    } ?: return route
    return buildList {
        add(current)
        addAll(route.drop(nearestIndex))
    }
}
