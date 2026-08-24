package com.example.frontend.screens

import android.location.Location
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.speech.tts.TextToSpeech
import androidx.activity.compose.BackHandler
import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectVerticalDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.CompositingStrategy
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.LiveTripMapScreen
import com.example.frontend.TodaPointOverlay
import com.example.frontend.TransitRouteOverlay
import com.example.frontend.components.ParaPoOverlay
import com.example.frontend.core.localization.AppLanguagePreference
import com.example.frontend.core.location.NavigationSyncSignal
import com.example.frontend.core.location.RouteCoordinate
import com.example.frontend.core.location.hasDeviceLocationPermission
import com.example.frontend.core.location.navigationLocationUpdates
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.navigation.NavigationLegDto
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.navigation.LocalLegProximity
import com.example.frontend.navigation.LocalNavigationEngine
import com.example.frontend.navigation.LocalNavigationSpeech
import com.example.frontend.navigation.LocalServerSyncReason
import com.example.frontend.navigation.TripOptionsCoordinator
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.launch
import org.maplibre.android.geometry.LatLng
import java.math.BigDecimal
import java.math.RoundingMode
import kotlin.math.max
import kotlin.math.roundToInt

private val TripScreen = com.example.frontend.ui.theme.TukiCream
private val TripCream = com.example.frontend.ui.theme.TukiGoldSurface
private val TripSurface = com.example.frontend.ui.theme.TukiSurfaceRaised
private val TripTile = com.example.frontend.ui.theme.TukiSky.copy(alpha = 0.30f)
private val TripDark = com.example.frontend.ui.theme.TukiInk
private val TripTeal = com.example.frontend.ui.theme.TukiTeal
private val TripOrange = com.example.frontend.ui.theme.TukiOrange
private val TripGray = com.example.frontend.ui.theme.TukiMuted
private val TripSoftTeal = com.example.frontend.ui.theme.TukiTealSurface
private val TripDanger = com.example.frontend.ui.theme.TukiDanger
private const val TripFreshFixMaxAgeMillis = 30_000L

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

    var showParaPo by remember { mutableStateOf(false) }
    var showEndDialog by remember { mutableStateOf(false) }
    var showArrival by remember { mutableStateOf(false) }
    var showOptions by remember { mutableStateOf(false) }
    var showNavigationAi by remember { mutableStateOf(false) }
    var optionSnapshot by remember { mutableStateOf<NavigationSnapshotDto?>(null) }
    var stableLegRoute by remember { mutableStateOf<List<LatLng>>(emptyList()) }
    var stableLegRouteKey by remember { mutableStateOf<String?>(null) }
    var optionError by remember { mutableStateOf<String?>(null) }
    var optionWorking by remember { mutableStateOf(false) }
    var hasRerouted by remember { mutableStateOf(false) }
    var activeDestinationName by remember(destination) { mutableStateOf(destination) }
    var activeFinalDestination by remember(finalDestination) { mutableStateOf(finalDestination) }
    var ttsReady by remember { mutableStateOf(false) }
    var instructionCollapsed by remember { mutableStateOf(false) }
    var recenterRequestKey by remember { mutableStateOf(0) }
    var legOverviewRequestKey by remember { mutableStateOf(0) }
    var localLandmarkNotice by remember { mutableStateOf<String?>(null) }
    var navigationLanguage by remember { mutableStateOf(AppLanguagePreference.current()) }

    val tts = remember(context) {
        TextToSpeech(context) { status -> ttsReady = status == TextToSpeech.SUCCESS }
    }
    DisposableEffect(tts) { onDispose { tts.stop(); tts.shutdown() } }

    val snapshot = optionSnapshot ?: navigationSnapshot
    val working = isNavigationActionInProgress || optionWorking
    val currentLegIndex = (snapshot?.currentLegIndex ?: 0).coerceAtLeast(0)
    val geometryKey = snapshot?.let(::navigationGeometryKey)
    val serverRerouted = snapshot?.status.equals("REROUTE_SUCCEEDED", true)
    val effectiveRerouted = hasRerouted || serverRerouted

    LaunchedEffect(snapshot?.sessionId) {
        navigationLanguage = options.refreshPreferredLanguage()
    }

    LaunchedEffect(navigationSnapshot?.state) {
        if (navigationSnapshot?.state.equals("Arrived", true)) {
            showArrival = true
            showOptions = false
            showNavigationAi = false
        }
    }

    LaunchedEffect(
        snapshot?.sessionId,
        geometryKey,
        snapshot?.state
    ) {
        val current = snapshot ?: return@LaunchedEffect
        val key = geometryKey ?: return@LaunchedEffect
        if (current.sessionId.startsWith("guest-") || current.state.equals("Arrived", true) || current.state.equals("Cancelled", true)) return@LaunchedEffect
        if (stableLegRouteKey == key && stableLegRoute.size >= 2) return@LaunchedEffect

        when (val geometry = options.currentLegGeometry(current)) {
            is ApiResult.Success -> {
                stableLegRoute = geometry.data.points.map { LatLng(it.latitude, it.longitude) }
                stableLegRouteKey = key
                if (serverRerouted) hasRerouted = true
            }
            is ApiResult.Failure -> if (stableLegRoute.isEmpty()) optionError = geometry.message
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
                        is ApiResult.Success -> {
                            stableLegRoute = geometry.data.points.map { LatLng(it.latitude, it.longitude) }
                            stableLegRouteKey = navigationGeometryKey(result.data)
                        }
                        is ApiResult.Failure -> optionError = geometry.message
                    }
                    showOptions = false
                    scope.launch { delay(6_000); optionSnapshot = null }
                }
                is ApiResult.Failure -> optionError = result.message
            }
            optionWorking = false
        }
    }

    val liveDeviceLocation by produceState<Location?>(initialValue = null, snapshot?.sessionId) {
        if (!context.hasDeviceLocationPermission()) return@produceState
        context.navigationLocationUpdates()
            .catch { }
            .collect { location ->
                val ageMillis = if (location.time > 0L) System.currentTimeMillis() - location.time else 0L
                if (ageMillis <= TripFreshFixMaxAgeMillis) value = location
            }
    }

    val baseRoute = stableLegRoute
        .takeIf { stableLegRouteKey == geometryKey && it.size >= 2 }
        ?: routePoints
    val routeCoordinates = remember(baseRoute) {
        baseRoute.map { RouteCoordinate(it.latitude, it.longitude) }
    }
    val localEngine = remember(snapshot?.sessionId, currentLegIndex, geometryKey) { LocalNavigationEngine() }
    val localProgress by produceState<com.example.frontend.navigation.LocalNavigationProgress?>(
        initialValue = null,
        liveDeviceLocation,
        routeCoordinates,
        currentLegIndex,
        snapshot?.currentLeg?.transportMode,
        snapshot?.currentLegInstructions,
        snapshot?.currentLegLandmarks
    ) {
        val location = liveDeviceLocation ?: return@produceState
        value = localEngine.update(
            raw = RouteCoordinate(location.latitude, location.longitude),
            accuracyMeters = location.accuracy.toDouble(),
            legIndex = currentLegIndex,
            transportMode = snapshot?.currentLeg?.transportMode,
            route = routeCoordinates,
            instructions = snapshot?.currentLegInstructions.orEmpty(),
            landmarks = snapshot?.currentLegLandmarks.orEmpty(),
            elapsedRealtimeNanos = location.elapsedRealtimeNanos.takeIf { it > 0L },
            speedMetersPerSecond = if (location.hasSpeed()) location.speed.toDouble() else null
        )
    }

    LaunchedEffect(localProgress?.serverSyncReason, currentLegIndex, snapshot?.sessionId) {
        val reason = localProgress?.serverSyncReason ?: return@LaunchedEffect
        val sessionId = snapshot?.sessionId ?: return@LaunchedEffect
        if (sessionId.startsWith("guest-")) return@LaunchedEffect
        when (reason) {
            LocalServerSyncReason.MISSED_LEG_TARGET -> applyOption {
                options.recoverMissedLegTarget(sessionId)
            }
            else -> NavigationSyncSignal.requestImmediateSync()
        }
    }

    LaunchedEffect(localProgress?.landmarkEvent, navigationLanguage) {
        val event = localProgress?.landmarkEvent ?: return@LaunchedEffect
        localLandmarkNotice = LocalNavigationSpeech.landmarkPassedText(event.name, navigationLanguage)
        delay(5_000)
        localLandmarkNotice = null
    }

    val leg = snapshot?.currentLeg
    val localGuidance = localProgress?.currentGuidance
        ?.takeIf {
            leg?.transportMode.equals("WALK", true) || leg?.transportMode.equals("WALKING", true)
        }
        ?.takeIf { !it.type.equals("Continue", true) }
    val localFollowingGuidance = localProgress?.followingGuidance
        ?.takeIf { !it.type.equals("Continue", true) }

    val remainingDistance = localProgress?.remainingMeters ?: snapshot?.remainingDistanceMeters
    val renderedTemplate = LocalNavigationSpeech.renderTemplate(
        snapshot?.spokenInstructionTemplate,
        remainingDistance
    )
    val instruction = localLandmarkNotice
        ?: localGuidance?.let { LocalNavigationSpeech.guidanceText(it, navigationLanguage) }
        ?: renderedTemplate
        ?: snapshot?.displayInstruction()
        ?: snapshot?.nextInstruction?.let { next ->
            next.text?.takeIf { it.isNotBlank() }
                ?: listOfNotNull(
                    next.type.takeIf { it.isNotBlank() },
                    next.transportMode?.lowercase()?.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() },
                    next.routeName?.takeIf { it.isNotBlank() }
                ).joinToString(" · ")
        }
        ?: "Waiting for navigation guidance…"

    val following = localFollowingGuidance?.let { LocalNavigationSpeech.guidanceText(it, navigationLanguage) }
        ?: snapshot?.followingInstruction?.let { next ->
            next.text?.takeIf { it.isNotBlank() }
                ?: listOfNotNull(next.type.takeIf { it.isNotBlank() }, next.routeName?.takeIf { it.isNotBlank() }).joinToString(" · ").takeIf { it.isNotBlank() }
        }

    val progress = localProgress?.let { local ->
        val total = local.progressMeters + local.remainingMeters
        if (total > 0) (local.progressMeters / total).coerceIn(0.0, 1.0).toFloat() else 0f
    } ?: run {
        val currentLegDistance = snapshot?.currentLeg?.distanceMeters
        if (currentLegDistance != null && currentLegDistance > 0) {
            ((snapshot?.progressMeters ?: 0.0) / currentLegDistance).coerceIn(0.0, 1.0).toFloat()
        } else 0f
    }

    val gpsPosition = liveDeviceLocation?.let { LatLng(it.latitude, it.longitude) }
    val currentPosition = gpsPosition
        ?: snapshot?.let {
            if (it.currentLatitude != null && it.currentLongitude != null) LatLng(it.currentLatitude, it.currentLongitude) else null
        }
    val visibleRoute = if (localProgress != null) {
        localProgress!!.remainingRoute.map { LatLng(it.latitude, it.longitude) }
    } else {
        baseRoute
    }

    val requiresBoarding = snapshot?.requiresBoardingConfirmation == true
    val requiresAlighting = snapshot?.requiresAlightingConfirmation == true
    val transitMode = leg?.transportMode.equals("JEEPNEY", true) ||
        leg?.transportMode.equals("TRICYCLE", true) ||
        leg?.transportMode.equals("TRIKE", true)
    val localApproachingEnd = localProgress?.legProximity != null &&
        localProgress?.legProximity != LocalLegProximity.NORMAL
    val preparingToAlight = (snapshot?.state.equals("ApproachingAlightPoint", true) && !requiresAlighting) ||
        (transitMode && localApproachingEnd && !requiresAlighting)
    val canParaPo = requiresAlighting ||
        snapshot?.nextInstruction?.type?.contains("alight", true) == true ||
        (transitMode && localApproachingEnd)
    val activeTrip = snapshot != null && !snapshot.state.equals("Arrived", true) && !snapshot.state.equals("Cancelled", true)
    val guestTrip = snapshot?.sessionId?.startsWith("guest-") == true
    val modeIcon = transportIcon(leg?.transportMode)
    val targetName = nextStopName(snapshot, activeDestinationName)
    val legTitle = currentLegTitle(leg, targetName)
    val eta = estimateMinutes(remainingDistance, leg?.transportMode)?.let { "~$it min" } ?: "Updating"
    val distance = formatDistance(remainingDistance)
    val fare = leg?.fare?.takeIf { it > BigDecimal.ZERO }?.asPeso()
        ?: snapshot?.estimatedRemainingFare?.takeIf { it > BigDecimal.ZERO }?.asPeso()
        ?: "₱0"
    val totalLegs = max(1, currentLegIndex + 1 + futureRouteSegments.size)

    BackHandler(enabled = activeTrip) { onBack() }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(TripScreen)
            .graphicsLayer(compositingStrategy = CompositingStrategy.Offscreen)
    ) {
        LiveTripMapScreen(
            routePoints = visibleRoute,
            currentPosition = currentPosition,
            legDestination = if (effectiveRerouted) leg?.let { current ->
                if (current.endLatitude != null && current.endLongitude != null) LatLng(current.endLatitude, current.endLongitude) else null
            } else legDestination,
            finalDestination = activeFinalDestination,
            futureRouteSegments = if (effectiveRerouted) emptyList() else futureRouteSegments,
            nearbyJeepneyRoutes = nearbyJeepneyRoutes,
            todaPoints = todaPoints,
            recenterRequestKey = recenterRequestKey,
            gpsPosition = gpsPosition,
            fullLegRoutePoints = baseRoute,
            legOverviewRequestKey = legOverviewRequestKey,
            legIdentity = geometryKey ?: "${snapshot?.sessionId}:$currentLegIndex",
            overviewBottomPaddingDp = if (instructionCollapsed) 166f else 276f,
            modifier = Modifier.fillMaxSize()
        )

        Column(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .fillMaxWidth()
        ) {
            Surface(color = TripScreen.copy(alpha = 0.97f), shadowElevation = 1.dp) {
                Column(Modifier.fillMaxWidth().statusBarsPadding()) {
                    LiveTripHeader(
                        showOptions = activeTrip && !guestTrip,
                        activeTrip = activeTrip,
                        working = working,
                        onBack = onBack,
                        onOptions = { showOptions = true },
                        onEnd = { showEndDialog = true }
                    )
                }
            }
            Spacer(Modifier.height(8.dp))
            CurrentLegCard(
                icon = modeIcon,
                title = legTitle,
                eta = eta,
                fare = fare,
                status = when {
                    navigationError != null -> "Location update delayed"
                    snapshot == null -> "Connecting to navigation…"
                    else -> null
                }
            )
        }

        Column(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .navigationBarsPadding()
                .padding(horizontal = 10.dp, vertical = 8.dp),
            horizontalAlignment = Alignment.End
        ) {
            if (activeTrip && !guestTrip) {
                NavigationAiButton(
                    enabled = !working,
                    onClick = { showNavigationAi = true },
                    modifier = Modifier.padding(end = 16.dp, bottom = 8.dp)
                )
            }
            Row(
                modifier = Modifier.padding(end = 14.dp, bottom = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                LegOverviewButton(
                    enabled = baseRoute.size >= 2,
                    onClick = { legOverviewRequestKey += 1 }
                )
                RecenterButton(
                    enabled = gpsPosition != null,
                    onClick = { recenterRequestKey += 1 }
                )
            }
            InstructionPanel(
                instruction = instruction,
                following = following,
                icon = modeIcon,
                distance = distance,
                eta = eta,
                fare = fare,
                progress = progress,
                totalLegs = totalLegs,
                currentLeg = currentLegIndex,
                canSpeak = ttsReady,
                canParaPo = canParaPo,
                requiresBoarding = requiresBoarding,
                requiresAlighting = requiresAlighting,
                preparingToAlight = preparingToAlight,
                working = working,
                status = snapshot?.status,
                optionError = optionError,
                collapsed = instructionCollapsed,
                onCollapsedChange = { instructionCollapsed = it },
                onSpeak = {
                    if (ttsReady) tts.speak(instruction, TextToSpeech.QUEUE_FLUSH, null, "tuki-navigation")
                },
                onParaPo = { showParaPo = true },
                onBoard = onConfirmBoarding,
                onAlight = onConfirmAlighting
            )
        }

        if (optionWorking) {
            Surface(
                modifier = Modifier.align(Alignment.Center),
                shape = RoundedCornerShape(18.dp),
                color = TripSurface,
                shadowElevation = 10.dp
            ) {
                Row(Modifier.padding(horizontal = 20.dp, vertical = 16.dp), verticalAlignment = Alignment.CenterVertically) {
                    CircularProgressIndicator(Modifier.size(22.dp), color = TripTeal, strokeWidth = 3.dp)
                    Spacer(Modifier.width(12.dp))
                    Text("Updating your trip…", color = TripDark, fontWeight = FontWeight.Bold)
                }
            }
        }
    }

    if (showParaPo) {
        Box(
            Modifier.fillMaxSize().background(TripDark.copy(alpha = 0.4f)).clickable { showParaPo = false },
            contentAlignment = Alignment.Center
        ) { ParaPoOverlay(onDismiss = { showParaPo = false }) }
    }

    if (showOptions && snapshot != null) {
        TripOptionsSheet(
            isWorking = working,
            onDismiss = { showOptions = false },
            onRerouteNow = { reason ->
                applyOption {
                    options.rerouteNow(
                        sessionId = snapshot.sessionId,
                        reason = reason.code,
                        avoidTransportMode = reason.avoidTransportMode
                    )
                }
            },
            onPreferenceChange = { preference -> applyOption { options.changePreference(snapshot.sessionId, preference) } },
            onLoadPreferencePreviews = {
                val destinationPoint = activeFinalDestination
                when (
                    val result = options.loadPreferencePreviews(
                        originLatitude = currentPosition?.latitude ?: snapshot.currentLatitude,
                        originLongitude = currentPosition?.longitude ?: snapshot.currentLongitude,
                        destinationName = activeDestinationName,
                        destinationLatitude = destinationPoint?.latitude,
                        destinationLongitude = destinationPoint?.longitude
                    )
                ) {
                    is ApiResult.Success -> result.data
                    is ApiResult.Failure -> {
                        optionError = result.message
                        emptyList()
                    }
                }
            },
            onBudgetChange = { budget, clear -> applyOption { options.changeBudget(snapshot.sessionId, budget, clear) } },
            onDestinationSearch = { query ->
                when (val result = options.searchDestinations(query, currentPosition?.latitude ?: snapshot.currentLatitude, currentPosition?.longitude ?: snapshot.currentLongitude)) {
                    is ApiResult.Success -> result.data
                    is ApiResult.Failure -> { optionError = result.message; emptyList() }
                }
            },
            onDestinationChange = { place -> applyOption(place) { options.changeDestination(snapshot.sessionId, place) } }
        )
    }

    if (showNavigationAi && snapshot != null && !guestTrip) {
        NavigationAiSheet(
            onDismiss = { showNavigationAi = false },
            ask = { message ->
                options.askNavigationAssistant(
                    sessionId = snapshot.sessionId,
                    message = message,
                    latitude = currentPosition?.latitude ?: snapshot.currentLatitude,
                    longitude = currentPosition?.longitude ?: snapshot.currentLongitude
                )
            }
        )
    }

    if (showEndDialog) {
        AlertDialog(
            onDismissRequest = { if (!working) showEndDialog = false },
            title = { Text("End this trip?") },
            text = { Text("Your active navigation will be stopped.") },
            confirmButton = {
                TextButton(onClick = { showEndDialog = false; onEndTrip() }, enabled = !working) {
                    Text("End Trip", color = TripDanger, fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                TextButton(onClick = { showEndDialog = false }, enabled = !working) { Text("Continue Trip", color = TripTeal) }
            }
        )
    }

    if (showArrival) {
        val summary = snapshot?.tripSummary
        AlertDialog(
            onDismissRequest = {},
            title = { Text("You have arrived 🎉") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("You've reached ${summary?.destinationName ?: activeDestinationName}.")
                    summary?.durationMinutes?.let { SummaryRow("Travel time", "$it min") }
                    summary?.let {
                        SummaryRow("Approx. fare spent", it.approxFareSpent.asPeso())
                        SummaryRow("Transit legs", it.transitLegs.toString())
                        SummaryRow("Transfers", it.transfers.toString())
                    }
                }
            },
            confirmButton = {
                Button(
                    onClick = { showArrival = false; onArrivalAcknowledged() },
                    colors = ButtonDefaults.buttonColors(containerColor = TripTeal)
                ) { Text("Done") }
            }
        )
    }
}

@Composable
private fun LiveTripHeader(
    showOptions: Boolean,
    activeTrip: Boolean,
    working: Boolean,
    onBack: () -> Unit,
    onOptions: () -> Unit,
    onEnd: () -> Unit
) {
    Row(
        Modifier.fillMaxWidth().height(62.dp).padding(horizontal = 18.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(Modifier.size(38.dp).clickable(onClick = onBack), contentAlignment = Alignment.Center) {
            Text("←", color = TripDark, fontSize = 24.sp, fontWeight = FontWeight.Bold)
        }
        Text(
            "Live Trip",
            Modifier.weight(1f),
            color = TripDark,
            fontSize = 20.sp,
            fontWeight = FontWeight.ExtraBold,
            fontFamily = com.example.frontend.ui.theme.TukiDisplayFontFamily,
            textAlign = TextAlign.Center
        )
        if (showOptions) {
            Surface(
                Modifier.size(36.dp).clickable(enabled = !working, onClick = onOptions),
                shape = CircleShape,
                color = TripTile
            ) { Box(contentAlignment = Alignment.Center) { Text("⋯", color = TripDark, fontSize = 20.sp, fontWeight = FontWeight.Bold) } }
            Spacer(Modifier.width(8.dp))
        }
        Surface(
            Modifier.height(36.dp).clickable(enabled = activeTrip && !working, onClick = onEnd),
            shape = RoundedCornerShape(18.dp),
            color = if (activeTrip) TripDanger else TripGray.copy(alpha = 0.4f)
        ) {
            Box(Modifier.padding(horizontal = 16.dp), contentAlignment = Alignment.Center) {
                Text("End Trip", color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun CurrentLegCard(icon: String, title: String, eta: String, fare: String, status: String?) {
    Surface(
        Modifier.fillMaxWidth().padding(horizontal = 18.dp),
        shape = RoundedCornerShape(20.dp),
        color = TripSurface.copy(alpha = 0.96f),
        shadowElevation = 6.dp
    ) {
        Row(Modifier.padding(horizontal = 14.dp, vertical = 13.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(Modifier.size(46.dp), shape = RoundedCornerShape(14.dp), color = TripSoftTeal) {
                Box(contentAlignment = Alignment.Center) { Text(icon, fontSize = 22.sp) }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(title, color = TripDark, fontSize = 14.sp, fontWeight = FontWeight.ExtraBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.height(3.dp))
                Text(
                    "$eta remaining  •  $fare",
                    color = TripGray,
                    fontSize = 11.sp,
                    fontWeight = FontWeight.SemiBold,
                    fontFamily = com.example.frontend.ui.theme.TukiUtilityFontFamily
                )
                status?.let {
                    Spacer(Modifier.height(2.dp))
                    Text(it, color = TripOrange, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@Composable
private fun LegOverviewButton(
    enabled: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Surface(
        modifier = modifier
            .height(43.dp)
            .clickable(enabled = enabled, onClick = onClick),
        shape = RoundedCornerShape(22.dp),
        color = if (enabled) TripSurface else TripTile,
        border = BorderStroke(1.dp, com.example.frontend.ui.theme.TukiOutline),
        shadowElevation = 7.dp
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Text("⌖", color = if (enabled) TripTeal else TripGray, fontSize = 17.sp)
            Text(
                "View leg",
                color = if (enabled) TripDark else TripGray,
                fontSize = 12.sp,
                fontWeight = FontWeight.Bold
            )
        }
    }
}

@Composable
private fun RecenterButton(
    enabled: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Surface(
        modifier = modifier
            .size(52.dp)
            .clickable(enabled = enabled, onClick = onClick),
        shape = CircleShape,
        color = if (enabled) TripSurface else TripTile,
        border = BorderStroke(1.dp, com.example.frontend.ui.theme.TukiOutline),
        shadowElevation = 10.dp,
        tonalElevation = 2.dp
    ) {
        Box(contentAlignment = Alignment.Center) {
            Text(
                "◎",
                color = if (enabled) TripDark else TripGray.copy(alpha = 0.65f),
                fontSize = 28.sp
            )
        }
    }
}

@Composable
private fun InstructionPanel(
    instruction: String,
    following: String?,
    icon: String,
    distance: String,
    eta: String,
    fare: String,
    progress: Float,
    totalLegs: Int,
    currentLeg: Int,
    canSpeak: Boolean,
    canParaPo: Boolean,
    requiresBoarding: Boolean,
    requiresAlighting: Boolean,
    preparingToAlight: Boolean,
    working: Boolean,
    status: String?,
    optionError: String?,
    collapsed: Boolean,
    onCollapsedChange: (Boolean) -> Unit,
    onSpeak: () -> Unit,
    onParaPo: () -> Unit,
    onBoard: () -> Unit,
    onAlight: () -> Unit,
    modifier: Modifier = Modifier
) {
    var dragDistance by remember { mutableStateOf(0f) }

    Surface(
        modifier.fillMaxWidth().animateContentSize(),
        shape = RoundedCornerShape(28.dp),
        color = TripSurface,
        shadowElevation = 8.dp
    ) {
        Column(Modifier.padding(horizontal = 16.dp, vertical = 10.dp)) {
            Box(
                Modifier
                    .fillMaxWidth()
                    .height(22.dp)
                    .pointerInput(collapsed) {
                        detectVerticalDragGestures(
                            onDragStart = { dragDistance = 0f },
                            onVerticalDrag = { change, amount ->
                                change.consume()
                                dragDistance += amount
                            },
                            onDragEnd = {
                                when {
                                    dragDistance > 28f -> onCollapsedChange(true)
                                    dragDistance < -28f -> onCollapsedChange(false)
                                }
                                dragDistance = 0f
                            }
                        )
                    }
                    .clickable { onCollapsedChange(!collapsed) },
                contentAlignment = Alignment.Center
            ) {
                Box(
                    Modifier.width(42.dp).height(4.dp).background(
                        TripGray.copy(alpha = 0.45f),
                        RoundedCornerShape(4.dp)
                    )
                )
            }

            TripProgressDots(totalLegs, currentLeg)
            Spacer(Modifier.height(if (collapsed) 7.dp else 10.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(Modifier.size(44.dp), shape = RoundedCornerShape(14.dp), color = TripSoftTeal) {
                    Box(contentAlignment = Alignment.Center) { Text(icon, fontSize = 21.sp) }
                }
                Spacer(Modifier.width(11.dp))
                Column(Modifier.weight(1f)) {
                    Text("Next Instruction", color = TripGray, fontSize = 10.sp, fontWeight = FontWeight.ExtraBold)
                    Text(
                        instruction,
                        color = TripDark,
                        fontSize = 14.sp,
                        fontWeight = FontWeight.ExtraBold,
                        maxLines = if (collapsed) 2 else 3,
                        overflow = TextOverflow.Ellipsis
                    )
                }
                Spacer(Modifier.width(10.dp))
                Surface(
                    Modifier.size(44.dp).clickable(enabled = canSpeak, onClick = onSpeak),
                    shape = CircleShape,
                    color = if (canSpeak) TripCream else TripTile
                ) { Box(contentAlignment = Alignment.Center) { Text("🔊", fontSize = 20.sp) } }
            }

            if (!collapsed) {
                following?.let {
                    Spacer(Modifier.height(7.dp))
                    Text("Then: $it", color = TripGray, fontSize = 10.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                }
                if (preparingToAlight) {
                    Spacer(Modifier.height(7.dp))
                    Surface(shape = RoundedCornerShape(10.dp), color = TripOrange.copy(alpha = 0.10f)) {
                        Text(
                            "Prepare to alight — your stop is getting close.",
                            Modifier.padding(horizontal = 10.dp, vertical = 7.dp),
                            color = TripOrange,
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }

                Spacer(Modifier.height(10.dp))
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    TripMetric("Distance", distance, Modifier.weight(1f))
                    TripMetric("ETA", eta, Modifier.weight(1f))
                    TripMetric("Fare", fare, Modifier.weight(1f))
                }

                if (canParaPo || requiresBoarding || requiresAlighting) {
                    Spacer(Modifier.height(10.dp))
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        if (canParaPo) {
                            Surface(
                                Modifier.weight(1f).height(42.dp).clickable(enabled = !working, onClick = onParaPo),
                                shape = RoundedCornerShape(14.dp),
                                color = TripOrange.copy(alpha = 0.12f)
                            ) { Box(contentAlignment = Alignment.Center) { Text("🔔  Para Po", color = TripOrange, fontSize = 12.sp, fontWeight = FontWeight.ExtraBold) } }
                        }
                        if (requiresBoarding || requiresAlighting) {
                            Button(
                                onClick = if (requiresBoarding) onBoard else onAlight,
                                enabled = !working,
                                modifier = Modifier.weight(if (canParaPo) 1.35f else 1f).height(42.dp),
                                shape = RoundedCornerShape(14.dp),
                                colors = ButtonDefaults.buttonColors(containerColor = if (requiresBoarding) TripTeal else TripOrange)
                            ) {
                                if (working) {
                                    CircularProgressIndicator(Modifier.size(15.dp), strokeWidth = 2.dp, color = Color.White)
                                    Spacer(Modifier.width(6.dp))
                                }
                                Text(if (requiresBoarding) "Confirm Board" else "Confirm Alight", fontSize = 11.sp, fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                }

                if (!optionError.isNullOrBlank()) {
                    Spacer(Modifier.height(6.dp))
                    Text(optionError, color = MaterialTheme.colorScheme.error, fontSize = 9.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
                } else if (!status.isNullOrBlank()) {
                    Spacer(Modifier.height(6.dp))
                    Text(status.replace('_', ' '), color = TripGray, fontSize = 9.sp, fontWeight = FontWeight.Bold)
                }
                Spacer(Modifier.height(7.dp))
                LinearProgressIndicator(
                    progress = { progress },
                    modifier = Modifier.fillMaxWidth().height(4.dp),
                    color = TripTeal,
                    trackColor = TripTeal.copy(alpha = 0.10f),
                    strokeCap = androidx.compose.ui.graphics.StrokeCap.Round
                )
            } else {
                Spacer(Modifier.height(5.dp))
            }
        }
    }
}

@Composable
private fun TripProgressDots(totalLegs: Int, currentLeg: Int) {
    val count = totalLegs.coerceIn(1, 6)
    val active = currentLeg.coerceIn(0, count - 1)
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        repeat(count) { index ->
            if (index > 0) {
                Box(
                    Modifier.width(22.dp).height(2.dp).background(
                        if (index <= active) TripTeal.copy(alpha = 0.55f) else TripGray.copy(alpha = 0.22f),
                        RoundedCornerShape(2.dp)
                    )
                )
            }
            Box(
                Modifier.size(if (index == active) 9.dp else 7.dp).background(
                    when {
                        index == active -> TripTeal
                        index < active -> TripTeal.copy(alpha = 0.55f)
                        else -> TripGray.copy(alpha = 0.28f)
                    },
                    CircleShape
                )
            )
        }
    }
}

@Composable
private fun TripMetric(label: String, value: String, modifier: Modifier = Modifier) {
    Column(
        modifier.background(TripTile, RoundedCornerShape(14.dp)).padding(horizontal = 8.dp, vertical = 10.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(label, color = TripGray, fontSize = 9.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(2.dp))
        Text(
            value,
            color = TripDark,
            fontSize = 13.sp,
            fontWeight = FontWeight.ExtraBold,
            fontFamily = com.example.frontend.ui.theme.TukiUtilityFontFamily,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
    }
}

@Composable
private fun SummaryRow(label: String, value: String) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, color = TripGray, fontWeight = FontWeight.SemiBold)
        Text(
            value,
            color = TripDark,
            fontWeight = FontWeight.ExtraBold,
            fontFamily = com.example.frontend.ui.theme.TukiUtilityFontFamily
        )
    }
}

private fun navigationGeometryKey(snapshot: NavigationSnapshotDto): String? {
    val leg = snapshot.currentLeg ?: return null
    return listOf(
        leg.legIndex.toString(),
        leg.routeId?.toString().orEmpty(),
        leg.transportMode.uppercase(),
        leg.startLatitude?.toString().orEmpty(),
        leg.startLongitude?.toString().orEmpty(),
        leg.endLatitude?.toString().orEmpty(),
        leg.endLongitude?.toString().orEmpty()
    ).joinToString(":")
}

private fun nextStopName(snapshot: NavigationSnapshotDto?, fallback: String): String {
    snapshot ?: return fallback
    val type = snapshot.nextInstruction?.type.orEmpty().lowercase()
    val state = snapshot.state.lowercase()
    val board = snapshot.boardInfo?.landmark?.name?.takeIf { it.isNotBlank() }
    val alight = snapshot.alightInfo?.landmark?.name?.takeIf { it.isNotBlank() }
    val to = snapshot.currentLeg?.toName?.takeIf { it.isNotBlank() }
    val from = snapshot.currentLeg?.fromName?.takeIf { it.isNotBlank() }
    return when {
        snapshot.requiresBoardingConfirmation || "board" in type || "pickup" in type || "walkingtopickup" in state -> board ?: to ?: from ?: fallback
        snapshot.requiresAlightingConfirmation || "alight" in type || "approachingalight" in state -> alight ?: to ?: fallback
        else -> to ?: alight ?: board ?: fallback
    }
}

private fun currentLegTitle(leg: NavigationLegDto?, fallback: String): String {
    if (leg == null) return "Preparing your next step"
    val target = leg.toName?.takeIf { it.isNotBlank() } ?: fallback
    return when (leg.transportMode.uppercase()) {
        "WALK", "WALKING" -> "Walk to $target"
        "TRIKE", "TRICYCLE" -> "Ride Tricycle to $target"
        "JEEP", "JEEPNEY" -> "Ride Jeepney to $target"
        else -> "Continue to $target"
    }
}

private fun transportIcon(mode: String?): String = when (mode?.uppercase()) {
    "WALK", "WALKING" -> "🚶"
    "TRIKE", "TRICYCLE" -> "🛺"
    "JEEP", "JEEPNEY" -> "🚌"
    else -> "📍"
}

private fun formatDistance(meters: Double?): String = when {
    meters == null -> "—"
    meters >= 1000 -> "%.1f km".format(meters / 1000.0)
    else -> "${meters.roundToInt()} m"
}

private fun estimateMinutes(meters: Double?, mode: String?): Int? {
    val distance = meters?.takeIf { it > 0 } ?: return null
    val speed = when (mode?.uppercase()) {
        "WALK", "WALKING" -> 1.25
        "TRIKE", "TRICYCLE" -> 5.5
        "JEEP", "JEEPNEY" -> 4.2
        else -> 3.5
    }
    return max(1, (distance / speed / 60.0).roundToInt())
}

private fun BigDecimal.asPeso(): String = "₱${setScale(0, RoundingMode.HALF_UP).toPlainString()}"