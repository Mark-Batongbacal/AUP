package com.example.frontend.screens

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.MapScreen
import com.example.frontend.TodaPointOverlay
import com.example.frontend.TransitRouteOverlay
import com.example.frontend.components.ParaPoOverlay
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.navigation.TripOptionsCoordinator
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
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
    onBack: () -> Unit = {},
    onEndTrip: () -> Unit = {},
    onConfirmBoarding: () -> Unit = {},
    onConfirmAlighting: () -> Unit = {},
    onArrivalAcknowledged: () -> Unit = {}
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val options = remember(context) { TripOptionsCoordinator(context) }

    var showParaPoOverlay by remember { mutableStateOf(false) }
    var showExitTripDialog by remember { mutableStateOf(false) }
    var showArrivalDialog by remember { mutableStateOf(false) }
    var showTripOptions by remember { mutableStateOf(false) }
    var optionSnapshot by remember { mutableStateOf<NavigationSnapshotDto?>(null) }
    var optionRoutePoints by remember { mutableStateOf<List<LatLng>>(emptyList()) }
    var optionError by remember { mutableStateOf<String?>(null) }
    var optionWorking by remember { mutableStateOf(false) }
    var hasRerouted by remember { mutableStateOf(false) }
    var activeDestinationName by remember(destination) { mutableStateOf(destination) }
    var activeFinalDestination by remember(finalDestination) { mutableStateOf(finalDestination) }

    val snapshot = optionSnapshot ?: navigationSnapshot
    val working = isNavigationActionInProgress || optionWorking
    val progressBucket = ((snapshot?.progressMeters ?: 0.0) / 50.0).toInt()

    LaunchedEffect(navigationSnapshot?.state) {
        if (navigationSnapshot?.state.equals("Arrived", ignoreCase = true)) {
            showArrivalDialog = true
            showTripOptions = false
        }
    }

    LaunchedEffect(
        snapshot?.sessionId,
        snapshot?.currentLegIndex,
        snapshot?.currentLeg?.routeId,
        snapshot?.currentLeg?.transportMode,
        progressBucket
    ) {
        val current = snapshot ?: return@LaunchedEffect
        if (current.sessionId.startsWith("guest-") || current.state.equals("Arrived", true) || current.state.equals("Cancelled", true)) {
            return@LaunchedEffect
        }
        when (val geometry = options.currentLegGeometry(current)) {
            is ApiResult.Success -> optionRoutePoints = geometry.data.points.map { LatLng(it.latitude, it.longitude) }
            is ApiResult.Failure -> if (optionRoutePoints.isEmpty()) optionError = geometry.message
        }
    }

    fun applyOption(
        destinationUpdate: DestinationSearchResultDto? = null,
        request: suspend () -> ApiResult<NavigationSnapshotDto>
    ) {
        if (working) return
        scope.launch {
            optionWorking = true
            optionError = null
            when (val result = request()) {
                is ApiResult.Success -> {
                    hasRerouted = true
                    optionSnapshot = result.data
                    destinationUpdate?.let {
                        activeDestinationName = it.name
                        activeFinalDestination = LatLng(it.latitude, it.longitude)
                    }
                    when (val geometry = options.currentLegGeometry(result.data)) {
                        is ApiResult.Success -> optionRoutePoints = geometry.data.points.map { LatLng(it.latitude, it.longitude) }
                        is ApiResult.Failure -> optionError = geometry.message
                    }
                    showTripOptions = false
                    scope.launch {
                        delay(6_000)
                        optionSnapshot = null
                    }
                }
                is ApiResult.Failure -> optionError = result.message
            }
            optionWorking = false
        }
    }

    val instruction = snapshot?.displayInstruction()
        ?: snapshot?.nextInstruction?.let { next ->
            val mode = next.transportMode?.lowercase()?.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
            listOfNotNull(next.type.takeIf { it.isNotBlank() }, mode, next.routeName?.takeIf { it.isNotBlank() }).joinToString(" · ")
        } ?: "Waiting for navigation guidance…"

    val remainingDistance = snapshot?.remainingDistanceMeters
    val distanceText = when {
        remainingDistance == null -> "Waiting for location update"
        remainingDistance >= 1000 -> "%.1f km remaining".format(remainingDistance / 1000.0)
        else -> "${remainingDistance.roundToInt()} m remaining"
    }
    val currentLegDistance = snapshot?.currentLeg?.distanceMeters
    val progressFraction = if (currentLegDistance != null && currentLegDistance > 0.0) {
        (snapshot.progressMeters / currentLegDistance).coerceIn(0.0, 1.0).toFloat()
    } else 0f
    val latitude = snapshot?.currentLatitude
    val longitude = snapshot?.currentLongitude
    val currentPosition = if (latitude != null && longitude != null) LatLng(latitude, longitude) else null
    val baseRoute = optionRoutePoints.takeIf { it.size >= 2 } ?: routePoints
    val visibleRoutePoints = remember(baseRoute, currentPosition) { routeFromCurrentPosition(baseRoute, currentPosition) }

    val requiresBoarding = snapshot?.requiresBoardingConfirmation == true
    val requiresAlighting = snapshot?.requiresAlightingConfirmation == true
    val preparingToAlight = snapshot?.state.equals("ApproachingAlightPoint", true) && !requiresAlighting
    val canUseParaPo = requiresAlighting || snapshot?.nextInstruction?.type?.contains("alight", ignoreCase = true) == true
    val hasActiveTrip = snapshot != null && !snapshot.state.equals("Arrived", true) && !snapshot.state.equals("Cancelled", true)
    val showFareState = snapshot != null && !snapshot.sessionId.startsWith("guest-")

    fun requestBack() { if (hasActiveTrip) showExitTripDialog = true else onBack() }
    BackHandler(enabled = hasActiveTrip) { showExitTripDialog = true }

    Box(Modifier.fillMaxSize()) {
        MapScreen(
            routePoints = visibleRoutePoints,
            startPoint = currentPosition,
            futureRouteSegments = if (hasRerouted) emptyList() else futureRouteSegments,
            selectedDestination = if (hasRerouted) snapshot?.currentLeg?.let { leg ->
                if (leg.endLatitude != null && leg.endLongitude != null) LatLng(leg.endLatitude, leg.endLongitude) else null
            } else legDestination,
            finalDestination = activeFinalDestination,
            transitRoutes = nearbyJeepneyRoutes,
            todaPoints = todaPoints,
            modifier = Modifier.fillMaxSize()
        )

        Column(Modifier.fillMaxWidth().padding(30.dp).background(Color.White, RoundedCornerShape(20.dp)).padding(20.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    Modifier.size(32.dp).background(TukiCream, RoundedCornerShape(8.dp)).clickable(onClick = ::requestBack),
                    contentAlignment = Alignment.Center
                ) { Text("‹", color = TukiDark, fontSize = 20.sp, fontWeight = FontWeight.Bold) }
                Spacer(Modifier.width(16.dp))
                Column(Modifier.weight(1f)) {
                    Text("Current Trip", color = TukiGray, fontSize = 13.sp, fontWeight = FontWeight.Bold)
                    Text("$origin → $activeDestinationName", color = TukiDark, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
                    snapshot?.currentLeg?.routeName?.takeIf { it.isNotBlank() }?.let {
                        Text(it, color = TukiTeal, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                    }
                }
                if (hasActiveTrip && !snapshot!!.sessionId.startsWith("guest-")) {
                    FilledTonalButton(
                        onClick = { showTripOptions = true }, enabled = !working,
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)
                    ) { Text("Options", fontWeight = FontWeight.Bold) }
                }
            }
        }

        Surface(
            Modifier.align(Alignment.BottomCenter).fillMaxWidth().padding(20.dp),
            shape = RoundedCornerShape(24.dp), color = Color.White, tonalElevation = 8.dp, shadowElevation = 8.dp
        ) {
            Column(Modifier.padding(24.dp)) {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text("NEXT STEP", color = TukiTeal, fontSize = 12.sp, fontWeight = FontWeight.ExtraBold)
                        Text(instruction, color = TukiDark, fontSize = 19.sp, fontWeight = FontWeight.ExtraBold)
                        Spacer(Modifier.height(4.dp))
                        Text(distanceText, color = TukiGray, fontSize = 14.sp)
                        snapshot?.landmark?.let {
                            Spacer(Modifier.height(4.dp))
                            Text("Near ${it.name}", color = TukiTeal, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        }
                        if (preparingToAlight) {
                            Spacer(Modifier.height(6.dp))
                            Text(
                                "Prepare to alight. Confirm Alight will become available when you're within 75 m of your stop.",
                                color = TukiOrange,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                        (optionError ?: navigationError)?.takeIf { it.isNotBlank() }?.let {
                            Spacer(Modifier.height(6.dp))
                            Text(it, color = MaterialTheme.colorScheme.error, fontSize = 12.sp)
                        }
                    }
                    Spacer(Modifier.width(12.dp))
                    Surface(
                        Modifier.size(64.dp).clickable(enabled = canUseParaPo && !working) { showParaPoOverlay = true },
                        shape = CircleShape,
                        color = if (canUseParaPo) TukiOrange.copy(alpha = 0.2f) else TukiGray.copy(alpha = 0.12f)
                    ) { Box(contentAlignment = Alignment.Center) { Text("🔔", fontSize = 28.sp) } }
                }

                if (showFareState) {
                    Spacer(Modifier.height(16.dp))
                    Surface(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(14.dp),
                        color = TukiCream
                    ) {
                        Row(
                            Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 12.dp),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            FareValue("Approx. fare spent", snapshot!!.approxFareSpent)
                            FareValue("Estimated remaining", snapshot.estimatedRemainingFare)
                        }
                    }
                }

                if (requiresBoarding || requiresAlighting) {
                    Spacer(Modifier.height(16.dp))
                    Button(
                        onClick = if (requiresBoarding) onConfirmBoarding else onConfirmAlighting,
                        enabled = !working, modifier = Modifier.fillMaxWidth(),
                        colors = ButtonDefaults.buttonColors(containerColor = if (requiresBoarding) TukiTeal else TukiOrange)
                    ) {
                        if (working) {
                            CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp, color = Color.White)
                            Spacer(Modifier.width(8.dp))
                        }
                        Text(if (requiresBoarding) "Confirm Board" else "Confirm Alight", fontWeight = FontWeight.Bold)
                    }
                }
                Spacer(Modifier.height(20.dp))
                LinearProgressIndicator(
                    progress = { progressFraction }, modifier = Modifier.fillMaxWidth().height(8.dp),
                    color = TukiTeal, trackColor = TukiTeal.copy(alpha = 0.1f), strokeCap = androidx.compose.ui.graphics.StrokeCap.Round
                )
                snapshot?.status?.takeIf { it.isNotBlank() }?.let {
                    Spacer(Modifier.height(8.dp))
                    Text(it.replace('_', ' '), color = TukiGray, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                }
            }
        }

        if (optionWorking) {
            Surface(
                modifier = Modifier.align(Alignment.Center),
                shape = RoundedCornerShape(18.dp),
                color = Color.White,
                shadowElevation = 10.dp
            ) {
                Row(Modifier.padding(horizontal = 22.dp, vertical = 18.dp), verticalAlignment = Alignment.CenterVertically) {
                    CircularProgressIndicator(Modifier.size(24.dp), color = TukiTeal, strokeWidth = 3.dp)
                    Spacer(Modifier.width(12.dp))
                    Text("Updating your trip…", color = TukiDark, fontWeight = FontWeight.Bold)
                }
            }
        }

        if (showParaPoOverlay) {
            Box(Modifier.fillMaxSize().background(Color.Black.copy(alpha = 0.4f)).clickable { showParaPoOverlay = false }, contentAlignment = Alignment.Center) {
                ParaPoOverlay(onDismiss = { showParaPoOverlay = false })
            }
        }
    }

    if (showTripOptions && snapshot != null) {
        TripOptionsSheet(
            isWorking = working,
            onDismiss = { showTripOptions = false },
            onRerouteNow = { applyOption { options.rerouteNow(snapshot.sessionId) } },
            onPreferenceChange = { preference -> applyOption { options.changePreference(snapshot.sessionId, preference) } },
            onBudgetChange = { budget, clear -> applyOption { options.changeBudget(snapshot.sessionId, budget, clear) } },
            onDestinationSearch = { query ->
                when (val result = options.searchDestinations(query, snapshot.currentLatitude, snapshot.currentLongitude)) {
                    is ApiResult.Success -> result.data
                    is ApiResult.Failure -> { optionError = result.message; emptyList() }
                }
            },
            onDestinationChange = { place -> applyOption(place) { options.changeDestination(snapshot.sessionId, place) } },
            onEndTrip = onEndTrip
        )
    }

    if (showExitTripDialog) {
        AlertDialog(
            onDismissRequest = { showExitTripDialog = false },
            title = { Text("Trip is still active") },
            text = { Text("Going back will not end the navigation session. Continue the trip or end it first?") },
            confirmButton = { TextButton(onClick = onEndTrip, enabled = !working) { Text("End Trip", color = MaterialTheme.colorScheme.error) } },
            dismissButton = { TextButton(onClick = { showExitTripDialog = false }, enabled = !working) { Text("Continue Trip") } }
        )
    }

    if (showArrivalDialog) {
        AlertDialog(
            onDismissRequest = {},
            title = { Text("You have arrived 🎉") },
            text = { Text("You've reached $activeDestinationName. Your trip has been completed automatically.") },
            confirmButton = {
                Button(onClick = { showArrivalDialog = false; onArrivalAcknowledged() }, colors = ButtonDefaults.buttonColors(containerColor = TukiTeal)) { Text("Done") }
            }
        )
    }
}

@Composable
private fun FareValue(label: String, value: BigDecimal) {
    Column {
        Text(label, color = TukiGray, fontSize = 11.sp, fontWeight = FontWeight.Bold)
        Text(value.asPeso(), color = TukiDark, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
    }
}

private fun BigDecimal.asPeso(): String = "₱${stripTrailingZeros().toPlainString()}"

private fun routeFromCurrentPosition(route: List<LatLng>, current: LatLng?): List<LatLng> {
    if (current == null || route.size < 2) return route
    val nearestIndex = route.indices.minByOrNull { index ->
        val point = route[index]
        (point.latitude - current.latitude).pow(2) + (point.longitude - current.longitude).pow(2)
    } ?: return route
    return buildList { add(current); addAll(route.drop(nearestIndex)) }
}
