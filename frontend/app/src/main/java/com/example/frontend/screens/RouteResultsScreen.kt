package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.core.location.LocationDetectionFailureMessage
import com.example.frontend.core.location.LocationNotSupportedShortMessage
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.location.isLocationSupported
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.places.PlacesRepository
import com.example.frontend.data.routing.JourneyPlanRequest
import com.example.frontend.data.routing.RoutingRepository
import com.example.frontend.data.routing.TransitMode
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RouteOption
import com.example.frontend.model.RoutePoint
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
import com.example.frontend.ui.theme.TukiOutline

@Composable
fun RouteResultsScreen(
    origin: String,
    destinationQuery: String,
    routingRepository: RoutingRepository,
    placesRepository: PlacesRepository,
    originLatitude: Double? = null,
    originLongitude: Double? = null,
    destinationLatitude: Double? = null,
    destinationLongitude: Double? = null,
    onBack: () -> Unit = {},
    onRouteSelect: (RouteOption) -> Unit = {},
    onSuggestToda: () -> Unit = {}
) {
    val context = LocalContext.current
    var isLoading by remember { mutableStateOf(true) }
    var routeOptions by remember { mutableStateOf<List<RouteOption>>(emptyList()) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var showUnsupportedLocationDialog by remember { mutableStateOf(false) }
    var selectedTab by remember { mutableStateOf(0) }

    LaunchedEffect(
        destinationQuery,
        originLatitude,
        originLongitude,
        destinationLatitude,
        destinationLongitude
    ) {
        isLoading = true
        errorMessage = null
        routeOptions = emptyList()
        selectedTab = 0

        val deviceLocation = if (originLatitude == null || originLongitude == null) {
            context.currentDeviceLocation()
        } else {
            null
        }

        val resolvedOriginLatitude = originLatitude ?: deviceLocation?.latitude
        val resolvedOriginLongitude = originLongitude ?: deviceLocation?.longitude

        if (resolvedOriginLatitude == null || resolvedOriginLongitude == null) {
            errorMessage = LocationDetectionFailureMessage
            isLoading = false
            return@LaunchedEffect
        }

        if (!isLocationSupported(resolvedOriginLatitude, resolvedOriginLongitude)) {
            showUnsupportedLocationDialog = true
            errorMessage = LocationNotSupportedShortMessage
            isLoading = false
            return@LaunchedEffect
        }

        var resolvedDestinationLatitude = destinationLatitude
        var resolvedDestinationLongitude = destinationLongitude
        var resolvedDestinationName = destinationQuery

        if (resolvedDestinationLatitude == null || resolvedDestinationLongitude == null) {
            when (
                val placeResult = placesRepository.searchPlaces(
                    query = destinationQuery,
                    focusLatitude = resolvedOriginLatitude,
                    focusLongitude = resolvedOriginLongitude
                )
            ) {
                is ApiResult.Success -> {
                    val place = placeResult.data.firstOrNull()
                    if (place == null) {
                        errorMessage = "No matching destination found for \"$destinationQuery\"."
                        isLoading = false
                        return@LaunchedEffect
                    }
                    resolvedDestinationLatitude = place.latitude
                    resolvedDestinationLongitude = place.longitude
                    resolvedDestinationName = place.name
                }

                is ApiResult.Failure -> {
                    errorMessage = placeResult.message
                    isLoading = false
                    return@LaunchedEffect
                }
            }
        }

        val finalDestinationLatitude = resolvedDestinationLatitude
        val finalDestinationLongitude = resolvedDestinationLongitude
        if (finalDestinationLatitude == null || finalDestinationLongitude == null) {
            errorMessage = "Destination coordinates are unavailable."
            isLoading = false
            return@LaunchedEffect
        }

        if (!isLocationSupported(finalDestinationLatitude, finalDestinationLongitude)) {
            showUnsupportedLocationDialog = true
            errorMessage = LocationNotSupportedShortMessage
            isLoading = false
            return@LaunchedEffect
        }

        when (
            val result = routingRepository.planJourneys(
                JourneyPlanRequest(
                    originLatitude = resolvedOriginLatitude,
                    originLongitude = resolvedOriginLongitude,
                    destinationName = resolvedDestinationName,
                    destinationLatitude = finalDestinationLatitude,
                    destinationLongitude = finalDestinationLongitude
                )
            )
        ) {
            is ApiResult.Success -> {
                routeOptions = result.data.map { planned ->
                    val plan = planned.journey
                    val recommendationTags = plan.source.recommendationType
                        .split(',')
                        .map { it.trim().lowercase() }
                        .filter { it.isNotBlank() }
                    val walkMeters = (
                        plan.source.originAccess.walkDistanceMeters +
                            plan.source.destinationAccess.walkDistanceMeters +
                            plan.source.transferWalkDistancesMeters.sum()
                        ).roundToInt()

                    // Never invent endpoint-only geometry. Missing geometry is resolved
                    // through the navigation geometry API instead.
                    val legRoutePoints = plan.legs.map { leg ->
                        leg.geometry.orEmpty().map { point ->
                            RoutePoint(point.latitude, point.longitude)
                        }
                    }
                    val legEndPoints = plan.legs.map { leg ->
                        RoutePoint(leg.destination.latitude, leg.destination.longitude)
                    }
                    val routePoints = buildList {
                        legRoutePoints.forEach { legPoints ->
                            legPoints.forEach { point ->
                                if (lastOrNull() != point) add(point)
                            }
                        }
                    }

                    RouteOption(
                        id = planned.recommendationId,
                        label = formatRecommendationLabel(recommendationTags),
                        totalMinutes = (plan.source.totalTimeSeconds / 60).roundToInt(),
                        totalFare = plan.source.totalFarePesos,
                        walkMeters = walkMeters,
                        transfers = plan.source.transferCount,
                        generalCost = plan.source.generalizedCostPesos,
                        isRecommended = "efficient" in recommendationTags,
                        routePoints = routePoints,
                        legRoutePoints = legRoutePoints,
                        legEndPoints = legEndPoints,
                        legRouteIds = plan.legs.map { leg ->
                            if (leg.mode == TransitMode.Jeepney) leg.routeId?.toLongOrNull() else null
                        },
                        steps = plan.legs.mapIndexed { legIndex, leg ->
                            val mode = when (leg.mode) {
                                TransitMode.Walk -> "Walk"
                                TransitMode.Trike -> "Tricycle"
                                TransitMode.Jeepney -> "Jeepney"
                                is TransitMode.Unknown -> "Transit"
                            }
                            CommuteStep(
                                mode = mode,
                                from = when {
                                    legIndex == 0 -> origin
                                    leg.routeName?.isNotBlank() == true -> leg.routeName
                                    else -> "Transfer point"
                                },
                                to = when {
                                    legIndex == plan.legs.lastIndex -> destinationQuery
                                    leg.routeName?.isNotBlank() == true -> leg.routeName
                                    else -> "Transfer point"
                                },
                                minutes = (leg.durationSeconds / 60).roundToInt(),
                                fare = leg.farePesos
                            )
                        }
                    )
                }
            }

            is ApiResult.Failure -> errorMessage = result.message
        }

        isLoading = false
    }

    if (showUnsupportedLocationDialog) {
        LocationNotSupportedDialog {
            showUnsupportedLocationDialog = false
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .navigationBarsPadding()
            .verticalScroll(rememberScrollState())
            .padding(top = 8.dp, bottom = 16.dp)
    ) {
        RouteResultsHeader(onBack = onBack)

        Spacer(modifier = Modifier.height(14.dp))

        CurrentAndDestinationCard(
            origin = origin,
            destinationQuery = destinationQuery,
            modifier = Modifier.padding(horizontal = 22.dp)
        )

        Spacer(modifier = Modifier.height(16.dp))

        RouteTabs(
            selectedTab = selectedTab,
            onTabSelected = { selectedTab = it },
            modifier = Modifier.padding(horizontal = 16.dp)
        )

        Spacer(modifier = Modifier.height(14.dp))

        when {
            isLoading -> LoadingRoutes()

            errorMessage != null -> RouteMessageCard(
                title = "We couldn't find routes",
                message = errorMessage.orEmpty()
            )

            routeOptions.isEmpty() -> RouteMessageCard(
                title = "No routes found",
                message = "There are no route recommendations for \"$destinationQuery\" yet."
            )

            else -> {
                val visibleRoutes = if (selectedTab == 0) {
                    routeOptions.take(3)
                } else {
                    routeOptions
                }

                RouteCarouselSection(
                    routes = visibleRoutes,
                    onRouteSelect = onRouteSelect
                )

                Spacer(modifier = Modifier.height(22.dp))
                SuggestTodaBanner(
                    onClick = onSuggestToda,
                    modifier = Modifier.padding(horizontal = 22.dp)
                )
            }
        }
    }
}

@Composable
private fun RouteResultsHeader(onBack: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 22.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(36.dp)
                .clickable(onClick = onBack),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "←",
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
        }

        Spacer(modifier = Modifier.width(10.dp))

        Text(
            text = "Where are you going?",
            color = TukiInk,
            style = MaterialTheme.typography.displaySmall
        )
    }
}

@Composable
private fun CurrentAndDestinationCard(
    origin: String,
    destinationQuery: String,
    modifier: Modifier = Modifier
) {
    val originLabel = if (origin.contains("current location", ignoreCase = true)) {
        origin
    } else {
        "$origin (current location)"
    }

    Row(
        modifier = modifier
            .fillMaxWidth()
            .background(TukiGold.copy(alpha = 0.1f), RoundedCornerShape(18.dp))
            .padding(start = 16.dp, end = 12.dp, top = 13.dp, bottom = 13.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            LocationRow(
                dotColor = TukiTeal,
                text = originLabel,
                bold = true
            )

            Spacer(modifier = Modifier.height(9.dp))

            LocationRow(
                dotColor = TukiOrange,
                text = destinationQuery.ifBlank { "Somewhere" },
                bold = true
            )
        }

        Spacer(modifier = Modifier.width(8.dp))

        Box(
            modifier = Modifier
                .size(32.dp)
                .background(Color.White.copy(alpha = 0.55f), CircleShape),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "⇅",
                color = TukiInk,
                style = MaterialTheme.typography.titleMedium
            )
        }
    }
}

@Composable
private fun LocationRow(
    dotColor: Color,
    text: String,
    bold: Boolean
) {
    Row(verticalAlignment = Alignment.Top) {
        Box(
            modifier = Modifier
                .padding(top = 5.dp)
                .size(10.dp)
                .background(dotColor, CircleShape)
        )
        Spacer(modifier = Modifier.width(10.dp))
        Text(
            text = text,
            modifier = Modifier.weight(1f),
            color = TukiInk,
            style = MaterialTheme.typography.labelLarge,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis
        )
    }
}

@Composable
private fun RouteTabs(
    selectedTab: Int,
    onTabSelected: (Int) -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .height(42.dp)
            .background(TukiSky.copy(alpha = 0.35f), RoundedCornerShape(22.dp))
            .padding(2.dp)
    ) {
        RouteTab(
            text = "Top 3 Routes",
            selected = selectedTab == 0,
            modifier = Modifier.weight(1f),
            onClick = { onTabSelected(0) }
        )
        RouteTab(
            text = "All Routes",
            selected = selectedTab == 1,
            modifier = Modifier.weight(1f),
            onClick = { onTabSelected(1) }
        )
    }
}

@Composable
private fun RouteTab(
    text: String,
    selected: Boolean,
    modifier: Modifier = Modifier,
    onClick: () -> Unit
) {
    Box(
        modifier = modifier
            .fillMaxSize()
            .background(
                color = if (selected) TukiDeepTeal else Color.Transparent,
                shape = RoundedCornerShape(20.dp)
            )
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = text,
            color = if (selected) Color.White else TukiMuted,
            style = MaterialTheme.typography.labelLarge
        )
    }
}

@Composable
private fun LoadingRoutes() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 70.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            CircularProgressIndicator(color = TukiTeal)
            Spacer(modifier = Modifier.height(14.dp))
            Text(
                text = "Finding the best routes...",
                color = TukiMuted,
                fontSize = 14.sp,
                fontWeight = FontWeight.Medium
            )
        }
    }
}

@Composable
private fun RouteMessageCard(title: String, message: String) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 22.dp)
            .background(Color.White.copy(alpha = 0.72f), RoundedCornerShape(18.dp))
            .padding(20.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = title,
            color = TukiInk,
            style = MaterialTheme.typography.titleLarge,
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.height(6.dp))
        Text(
            text = message,
            color = TukiMuted,
            style = MaterialTheme.typography.bodyMedium,
            textAlign = TextAlign.Center
        )
    }
}

@Composable
private fun RouteCarouselSection(
    routes: List<RouteOption>,
    onRouteSelect: (RouteOption) -> Unit
) {
    if (routes.isEmpty()) return

    val pagerState = rememberPagerState(pageCount = { routes.size })

    LaunchedEffect(routes.size) {
        if (pagerState.currentPage > routes.lastIndex) {
            pagerState.scrollToPage(routes.lastIndex.coerceAtLeast(0))
        }
    }

    HorizontalPager(
        state = pagerState,
        modifier = Modifier.fillMaxWidth(),
        contentPadding = PaddingValues(horizontal = 38.dp),
        pageSpacing = 10.dp
    ) { page ->
        RouteOptionCard(option = routes[page])
    }

    Spacer(modifier = Modifier.height(13.dp))

    PagerDots(
        pageCount = routes.size,
        currentPage = pagerState.currentPage
    )

    Spacer(modifier = Modifier.height(16.dp))

    val selectedRoute = routes.getOrNull(pagerState.currentPage) ?: routes.first()
    SelectRouteButton(
        onClick = {
            com.example.frontend.TukiMapOverlayState.selectJourneyJeepneyRoutes(selectedRoute.legRouteIds)
            onRouteSelect(selectedRoute)
        },
        modifier = Modifier.padding(horizontal = 22.dp)
    )
}

private fun formatRecommendationLabel(tags: List<String>): String {
    val fastest = "fastest" in tags
    val cheapest = "cheapest" in tags
    val efficient = "efficient" in tags

    return when {
        efficient && fastest -> "Best Overall · Fastest"
        efficient && cheapest -> "Best Overall · Cheapest"
        efficient -> "Best Overall"
        fastest -> "Fastest"
        cheapest -> "Cheapest"
        else -> tags.joinToString(" · ") { tag ->
            tag.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
        }.ifBlank { "Route option" }
    }
}

@Composable
private fun RouteOptionCard(option: RouteOption) {
    val cardColor = routeCardColor(option)
    val titleIcon = when {
        option.isRecommended -> "★"
        option.label.contains("Fast", ignoreCase = true) -> "⚡"
        option.label.contains("Cheap", ignoreCase = true) -> "₱"
        else -> "●"
    }

    Box(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 10.dp)
                .background(cardColor, RoundedCornerShape(22.dp))
                .padding(start = 15.dp, end = 15.dp, top = 28.dp, bottom = 18.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.Center
            ) {
                Text(
                    text = titleIcon,
                    color = if (option.isRecommended) com.example.frontend.ui.theme.TukiGold else Color.White,
                    fontSize = 22.sp,
                    fontWeight = FontWeight.Bold
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = primaryRouteTitle(option),
                    color = Color.White,
                    fontSize = 20.sp,
                    fontWeight = FontWeight.ExtraBold,
                    fontFamily = com.example.frontend.ui.theme.TukiDisplayFontFamily,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            Text(
                text = routeSubtitle(option),
                color = Color.White.copy(alpha = 0.72f),
                fontSize = 12.sp,
                fontWeight = FontWeight.Medium,
                textAlign = TextAlign.Center,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )

            Spacer(modifier = Modifier.height(15.dp))

            Row(modifier = Modifier.fillMaxWidth()) {
                StatTile(
                    symbol = "◷",
                    value = "~${option.totalMinutes} min",
                    label = "Est. time",
                    modifier = Modifier.weight(1f)
                )
                Spacer(modifier = Modifier.width(3.dp))
                StatTile(
                    symbol = "▣",
                    value = "₱${option.totalFare.roundToInt()}",
                    label = "Est. fare",
                    modifier = Modifier.weight(1f)
                )
            }

            Spacer(modifier = Modifier.height(3.dp))

            Row(modifier = Modifier.fillMaxWidth()) {
                StatTile(
                    symbol = "♙",
                    value = "${option.walkMeters} m",
                    label = "Walk",
                    modifier = Modifier.weight(1f)
                )
                Spacer(modifier = Modifier.width(3.dp))
                StatTile(
                    symbol = "◉",
                    value = "${option.steps.size} legs",
                    label = transferLabel(option.transfers),
                    modifier = Modifier.weight(1f)
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(TukiSky.copy(alpha = 0.2f), RoundedCornerShape(11.dp))
                    .padding(horizontal = 14.dp, vertical = 11.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Gen. Cost",
                    color = TukiInk,
                    style = MaterialTheme.typography.labelLarge
                )
                Text(
                    text = "₱${option.generalCost.roundToInt()}",
                    color = TukiOrange,
                    style = MaterialTheme.typography.titleLarge
                )
            }

            Spacer(modifier = Modifier.height(12.dp))

            Text(
                text = "Estimates only — actual time and fare may vary\nwith traffic and driver",
                color = Color.White.copy(alpha = 0.54f),
                fontSize = 10.sp,
                lineHeight = 16.sp,
                textAlign = TextAlign.Center
            )
        }

        if (option.isRecommended) {
            Box(
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .background(TukiOrange, RoundedCornerShape(14.dp))
                    .padding(horizontal = 14.dp, vertical = 5.dp)
            ) {
                Text(
                    text = "RECOMMENDED",
                    color = Color.White,
                    fontSize = 10.sp,
                    fontWeight = FontWeight.ExtraBold
                )
            }
        }
    }
}

@Composable
private fun StatTile(
    symbol: String,
    value: String,
    label: String,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .height(62.dp)
            .background(TukiSky.copy(alpha = 0.2f), RoundedCornerShape(11.dp))
            .padding(horizontal = 10.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = symbol,
            color = TukiTeal,
            style = MaterialTheme.typography.titleLarge
        )

        Spacer(modifier = Modifier.width(8.dp))

        Column {
            Text(
                text = value,
                color = TukiInk,
                style = MaterialTheme.typography.labelLarge,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Text(
                text = label,
                color = TukiMuted,
                style = MaterialTheme.typography.bodySmall,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

private fun primaryRouteTitle(option: RouteOption): String {
    return when {
        option.isRecommended -> "Best Overall"
        option.label.contains("Fast", ignoreCase = true) -> "Fastest"
        option.label.contains("Cheap", ignoreCase = true) -> "Cheapest"
        else -> option.label.ifBlank { "Route Option" }
    }
}

private fun routeSubtitle(option: RouteOption): String {
    if (option.description.isNotBlank()) return option.description

    return when {
        option.isRecommended -> "Fast • Affordable • Less Transfers"
        option.label.contains("Fast", ignoreCase = true) -> "Fastest • Less travel time"
        option.label.contains("Cheap", ignoreCase = true) -> "Budget-friendly • Lowest fare"
        else -> "Practical • Verified transport route"
    }
}

private fun routeCardColor(option: RouteOption): Color {
    return when {
        option.isRecommended -> TukiDeepTeal
        option.label.contains("Fast", ignoreCase = true) -> TukiDeepTeal
        option.label.contains("Cheap", ignoreCase = true) -> TukiForest
        else -> TukiDeepTeal
    }
}

private fun transferLabel(transfers: Int): String {
    return if (transfers == 1) "1 transfer" else "$transfers transfers"
}

@Composable
private fun PagerDots(pageCount: Int, currentPage: Int) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically
    ) {
        repeat(pageCount) { index ->
            Box(
                modifier = Modifier
                    .padding(horizontal = 3.dp)
                    .size(if (index == currentPage) 8.dp else 7.dp)
                    .background(
                        color = if (index == currentPage) TukiTeal else com.example.frontend.ui.theme.TukiOutline,
                        shape = CircleShape
                    )
            )
        }
    }
}

@Composable
private fun SelectRouteButton(
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .height(52.dp)
            .background(TukiOrange, RoundedCornerShape(17.dp))
            .clickable(onClick = onClick),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = "Select This Route",
            color = Color.White,
            fontSize = 16.sp,
            fontWeight = FontWeight.ExtraBold
        )
        Spacer(modifier = Modifier.width(10.dp))
        Text(
            text = "→",
            color = Color.White,
            fontSize = 21.sp,
            fontWeight = FontWeight.Medium
        )
    }
}

@Composable
private fun SuggestTodaBanner(
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .background(TukiGold.copy(alpha = 0.1f), RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(28.dp)
                .background(TukiTeal, CircleShape),
            contentAlignment = Alignment.Center
        ) {
            Text("+", color = Color.White, style = MaterialTheme.typography.titleLarge)
        }
        Spacer(modifier = Modifier.width(12.dp))
        Column {
            Text(
                text = "Know a TODA we don't have? Suggest it",
                color = TukiInk,
                style = MaterialTheme.typography.titleMedium
            )
            Text(
                text = "Reviewed by our team before it goes live",
                color = TukiMuted,
                style = MaterialTheme.typography.bodySmall
            )
        }
    }
}
