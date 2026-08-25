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
import androidx.compose.material3.MaterialTheme
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
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.core.location.LocationDetectionFailureMessage
import com.example.frontend.core.location.LocationNotSupportedShortMessage
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.location.isLocationSupported
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.places.PlacesRepository
import com.example.frontend.data.routing.JourneyPlanRequest
import com.example.frontend.data.routing.RoutingRepository
import com.example.frontend.data.routing.toRouteOption
import com.example.frontend.model.RouteOption
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiForest
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiOutline
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import kotlin.math.roundToInt

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
    var routeCycleRequest by remember { mutableStateOf(0) }

    LaunchedEffect(destinationQuery, originLatitude, originLongitude, destinationLatitude, destinationLongitude) {
        isLoading = true
        errorMessage = null
        routeOptions = emptyList()
        selectedTab = 0
        routeCycleRequest = 0

        val deviceLocation = if (originLatitude == null || originLongitude == null) context.currentDeviceLocation() else null
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
                        errorMessage = if (TukiInterfaceText.isFilipino) {
                            "Walang nahanap na destinasyon para sa \"$destinationQuery\"."
                        } else {
                            "No matching destination found for \"$destinationQuery\"."
                        }
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
            errorMessage = if (TukiInterfaceText.isFilipino) "Hindi available ang coordinates ng destinasyon." else "Destination coordinates are unavailable."
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
                    planned.toRouteOption(origin, destinationQuery)
                }
            }
            is ApiResult.Failure -> errorMessage = result.message
        }

        isLoading = false
    }

    if (showUnsupportedLocationDialog) {
        LocationNotSupportedDialog { showUnsupportedLocationDialog = false }
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
            canCycleRoutes = !isLoading && errorMessage == null && routeOptions.size > 1,
            onCycleRoute = { routeCycleRequest += 1 },
            modifier = Modifier.padding(horizontal = 22.dp)
        )
        Spacer(modifier = Modifier.height(16.dp))
        RouteTabs(selectedTab = selectedTab, onTabSelected = { selectedTab = it }, modifier = Modifier.padding(horizontal = 16.dp))
        Spacer(modifier = Modifier.height(14.dp))

        when {
            isLoading -> LoadingRoutes()
            errorMessage != null -> RouteMessageCard(
                title = if (TukiInterfaceText.isFilipino) "Hindi kami nakahanap ng ruta" else "We couldn't find routes",
                message = errorMessage.orEmpty()
            )
            routeOptions.isEmpty() -> RouteMessageCard(
                title = if (TukiInterfaceText.isFilipino) "Walang rutang nahanap" else "No routes found",
                message = if (TukiInterfaceText.isFilipino) {
                    "Wala pang route recommendation para sa \"$destinationQuery\"."
                } else {
                    "There are no route recommendations for \"$destinationQuery\" yet."
                }
            )
            else -> {
                val visibleRoutes = if (selectedTab == 0) routeOptions.take(3) else routeOptions
                RouteCarouselSection(
                    routes = visibleRoutes,
                    cycleRequest = routeCycleRequest,
                    onRouteSelect = onRouteSelect
                )
                Spacer(modifier = Modifier.height(22.dp))
                SuggestTodaBanner(onClick = onSuggestToda, modifier = Modifier.padding(horizontal = 22.dp))
            }
        }
    }
}

@Composable
private fun RouteResultsHeader(onBack: () -> Unit) {
    Row(modifier = Modifier.fillMaxWidth().padding(horizontal = 22.dp), verticalAlignment = Alignment.CenterVertically) {
        Box(modifier = Modifier.size(36.dp).clickable(onClick = onBack), contentAlignment = Alignment.Center) {
            Text(text = "←", color = TukiInk, style = MaterialTheme.typography.displaySmall)
        }
        Spacer(modifier = Modifier.width(10.dp))
        Text(text = TukiInterfaceText.whereAreYouGoing, color = TukiInk, style = MaterialTheme.typography.displaySmall)
    }
}

@Composable
private fun CurrentAndDestinationCard(
    origin: String,
    destinationQuery: String,
    canCycleRoutes: Boolean,
    onCycleRoute: () -> Unit,
    modifier: Modifier = Modifier
) {
    val originLabel = if (origin.contains("current location", ignoreCase = true)) {
        if (TukiInterfaceText.isFilipino && origin.equals("Current location", true)) TukiInterfaceText.currentLocation else origin
    } else {
        "$origin (${TukiInterfaceText.currentLocation.lowercase()})"
    }

    Row(
        modifier = modifier.fillMaxWidth().background(TukiGold.copy(alpha = 0.1f), RoundedCornerShape(18.dp))
            .padding(start = 16.dp, end = 12.dp, top = 13.dp, bottom = 13.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            LocationRow(dotColor = TukiTeal, text = originLabel, bold = true)
            Spacer(modifier = Modifier.height(9.dp))
            LocationRow(
                dotColor = TukiOrange,
                text = destinationQuery.ifBlank { if (TukiInterfaceText.isFilipino) "Kahit saan" else "Somewhere" },
                bold = true
            )
        }
        Spacer(modifier = Modifier.width(8.dp))
        Box(
            modifier = Modifier
                .size(38.dp)
                .background(TukiSurfaceRaised.copy(alpha = if (canCycleRoutes) 0.92f else 0.45f), CircleShape)
                .clickable(enabled = canCycleRoutes, onClick = onCycleRoute),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "⇄",
                color = if (canCycleRoutes) TukiDeepTeal else TukiMuted.copy(alpha = 0.55f),
                fontSize = 20.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }
    }
}

@Composable
private fun LocationRow(dotColor: Color, text: String, bold: Boolean) {
    Row(verticalAlignment = Alignment.Top) {
        Box(modifier = Modifier.padding(top = 5.dp).size(10.dp).background(dotColor, CircleShape))
        Spacer(modifier = Modifier.width(10.dp))
        Text(text = text, modifier = Modifier.weight(1f), color = TukiInk, style = MaterialTheme.typography.labelLarge, maxLines = 2, overflow = TextOverflow.Ellipsis)
    }
}

@Composable
private fun RouteTabs(selectedTab: Int, onTabSelected: (Int) -> Unit, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier.fillMaxWidth().height(42.dp).background(TukiSky.copy(alpha = 0.35f), RoundedCornerShape(22.dp)).padding(2.dp)
    ) {
        RouteTab(
            text = if (TukiInterfaceText.isFilipino) "Top 3 Ruta" else "Top 3 Routes",
            selected = selectedTab == 0,
            modifier = Modifier.weight(1f),
            onClick = { onTabSelected(0) }
        )
        RouteTab(
            text = if (TukiInterfaceText.isFilipino) "Lahat ng Ruta" else "All Routes",
            selected = selectedTab == 1,
            modifier = Modifier.weight(1f),
            onClick = { onTabSelected(1) }
        )
    }
}

@Composable
private fun RouteTab(text: String, selected: Boolean, modifier: Modifier = Modifier, onClick: () -> Unit) {
    Box(
        modifier = modifier.fillMaxSize().background(if (selected) TukiDeepTeal else Color.Transparent, RoundedCornerShape(20.dp)).clickable(onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Text(text = text, color = if (selected) Color.White else TukiMuted, style = MaterialTheme.typography.labelLarge)
    }
}

@Composable
private fun LoadingRoutes() {
    Box(modifier = Modifier.fillMaxWidth().padding(vertical = 70.dp), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            CircularProgressIndicator(color = TukiTeal)
            Spacer(modifier = Modifier.height(14.dp))
            Text(
                text = if (TukiInterfaceText.isFilipino) "Hinahanap ang pinakamainam na ruta..." else "Finding the best routes...",
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
        modifier = Modifier.fillMaxWidth().padding(horizontal = 22.dp).background(TukiSurfaceRaised, RoundedCornerShape(18.dp)).padding(20.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(text = title, color = TukiInk, style = MaterialTheme.typography.titleLarge, textAlign = TextAlign.Center)
        Spacer(modifier = Modifier.height(6.dp))
        Text(text = message, color = TukiMuted, style = MaterialTheme.typography.bodyMedium, textAlign = TextAlign.Center)
    }
}

@Composable
private fun RouteCarouselSection(
    routes: List<RouteOption>,
    cycleRequest: Int,
    onRouteSelect: (RouteOption) -> Unit
) {
    if (routes.isEmpty()) return
    val pagerState = rememberPagerState(pageCount = { routes.size })
    var lastHandledCycleRequest by remember { mutableStateOf(cycleRequest) }

    LaunchedEffect(routes.size) {
        if (pagerState.currentPage > routes.lastIndex) pagerState.scrollToPage(routes.lastIndex.coerceAtLeast(0))
    }

    LaunchedEffect(cycleRequest, routes.size) {
        if (cycleRequest != lastHandledCycleRequest) {
            lastHandledCycleRequest = cycleRequest
            if (routes.size > 1) {
                val nextPage = (pagerState.currentPage + 1) % routes.size
                pagerState.animateScrollToPage(nextPage)
            }
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
    PagerDots(pageCount = routes.size, currentPage = pagerState.currentPage)
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

@Composable
private fun RouteOptionCard(option: RouteOption) {
    val cardColor = routeCardColor(option)
    val titleIcon = when {
        option.isRecommended -> "★"
        option.label.contains("Fast", ignoreCase = true) || option.label.contains("bilis", ignoreCase = true) -> "⚡"
        option.label.contains("Cheap", ignoreCase = true) || option.label.contains("mura", ignoreCase = true) -> "₱"
        else -> "●"
    }

    Box(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(top = 10.dp).background(cardColor, RoundedCornerShape(22.dp))
                .padding(start = 15.dp, end = 15.dp, top = 28.dp, bottom = 18.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.Center) {
                Text(
                    text = titleIcon,
                    color = if (option.isRecommended) TukiGold else Color.White,
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
                color = Color.White.copy(alpha = 0.82f),
                fontSize = 12.sp,
                fontWeight = FontWeight.Medium,
                textAlign = TextAlign.Center,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
            Spacer(modifier = Modifier.height(15.dp))

            Row(modifier = Modifier.fillMaxWidth()) {
                StatTile("◷", "~${option.totalMinutes} min", if (TukiInterfaceText.isFilipino) "Tinatayang oras" else "Est. time", Modifier.weight(1f))
                Spacer(modifier = Modifier.width(3.dp))
                StatTile("▣", "₱${option.totalFare.roundToInt()}", if (TukiInterfaceText.isFilipino) "Tinatayang pamasahe" else "Est. fare", Modifier.weight(1f))
            }
            Spacer(modifier = Modifier.height(3.dp))
            Row(modifier = Modifier.fillMaxWidth()) {
                StatTile("♙", "${option.walkMeters} m", if (TukiInterfaceText.isFilipino) "Lakad" else "Walk", Modifier.weight(1f))
                Spacer(modifier = Modifier.width(3.dp))
                StatTile("◉", "${option.steps.size} ${if (TukiInterfaceText.isFilipino) "hakbang" else "legs"}", transferLabel(option.transfers), Modifier.weight(1f))
            }
            Spacer(modifier = Modifier.height(4.dp))

            Row(
                modifier = Modifier.fillMaxWidth().background(TukiSky.copy(alpha = 0.2f), RoundedCornerShape(11.dp)).padding(horizontal = 14.dp, vertical = 11.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = if (TukiInterfaceText.isFilipino) "Kabuuang Cost" else "Gen. Cost",
                    color = Color.White,
                    style = MaterialTheme.typography.labelLarge
                )
                Text(text = "₱${option.generalCost.roundToInt()}", color = TukiOrange, style = MaterialTheme.typography.titleLarge)
            }

            Spacer(modifier = Modifier.height(12.dp))
            Text(
                text = if (TukiInterfaceText.isFilipino) {
                    "Tantiya lamang — maaaring magbago ang oras at pamasahe\ndahil sa trapiko at driver"
                } else {
                    "Estimates only — actual time and fare may vary\nwith traffic and driver"
                },
                color = Color.White.copy(alpha = 0.62f),
                fontSize = 10.sp,
                lineHeight = 16.sp,
                textAlign = TextAlign.Center
            )
        }

        if (option.isRecommended) {
            Box(
                modifier = Modifier.align(Alignment.TopCenter).background(TukiOrange, RoundedCornerShape(14.dp)).padding(horizontal = 14.dp, vertical = 5.dp)
            ) {
                Text(
                    text = if (TukiInterfaceText.isFilipino) "REKOMENDADO" else "RECOMMENDED",
                    color = Color.White,
                    fontSize = 10.sp,
                    fontWeight = FontWeight.ExtraBold
                )
            }
        }
    }
}

@Composable
private fun StatTile(symbol: String, value: String, label: String, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier.height(62.dp).background(TukiSky.copy(alpha = 0.2f), RoundedCornerShape(11.dp)).padding(horizontal = 10.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(text = symbol, color = Color.White.copy(alpha = 0.9f), style = MaterialTheme.typography.titleLarge)
        Spacer(modifier = Modifier.width(8.dp))
        Column {
            Text(text = value, color = Color.White, style = MaterialTheme.typography.labelLarge, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text(text = label, color = Color.White.copy(alpha = 0.72f), style = MaterialTheme.typography.bodySmall, maxLines = 1, overflow = TextOverflow.Ellipsis)
        }
    }
}

private fun primaryRouteTitle(option: RouteOption): String {
    return when {
        option.isRecommended -> if (TukiInterfaceText.isFilipino) "Pinakamainam" else "Best Overall"
        option.label.contains("Fast", ignoreCase = true) || option.label.contains("bilis", ignoreCase = true) -> if (TukiInterfaceText.isFilipino) "Pinakamabilis" else "Fastest"
        option.label.contains("Cheap", ignoreCase = true) || option.label.contains("mura", ignoreCase = true) -> if (TukiInterfaceText.isFilipino) "Pinakamura" else "Cheapest"
        else -> option.label.ifBlank { if (TukiInterfaceText.isFilipino) "Opsyon ng Ruta" else "Route Option" }
    }
}

private fun routeSubtitle(option: RouteOption): String {
    if (option.description.isNotBlank()) return option.description
    return when {
        option.isRecommended -> if (TukiInterfaceText.isFilipino) "Mabilis • Abot-kaya • Mas kaunting transfer" else "Fast • Affordable • Less Transfers"
        option.label.contains("Fast", ignoreCase = true) || option.label.contains("bilis", ignoreCase = true) -> if (TukiInterfaceText.isFilipino) "Pinakamabilis • Mas maikling oras ng biyahe" else "Fastest • Less travel time"
        option.label.contains("Cheap", ignoreCase = true) || option.label.contains("mura", ignoreCase = true) -> if (TukiInterfaceText.isFilipino) "Tipid • Pinakamababang pamasahe" else "Budget-friendly • Lowest fare"
        else -> if (TukiInterfaceText.isFilipino) "Praktikal • Beripikadong transport route" else "Practical • Verified transport route"
    }
}

private fun routeCardColor(option: RouteOption): Color {
    return when {
        option.isRecommended -> TukiDeepTeal
        option.label.contains("Fast", ignoreCase = true) || option.label.contains("bilis", ignoreCase = true) -> TukiDeepTeal
        option.label.contains("Cheap", ignoreCase = true) || option.label.contains("mura", ignoreCase = true) -> TukiForest
        else -> TukiDeepTeal
    }
}

private fun transferLabel(transfers: Int): String {
    return if (TukiInterfaceText.isFilipino) {
        if (transfers == 1) "1 transfer" else "$transfers transfer"
    } else {
        if (transfers == 1) "1 transfer" else "$transfers transfers"
    }
}

@Composable
private fun PagerDots(pageCount: Int, currentPage: Int) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        repeat(pageCount) { index ->
            Box(
                modifier = Modifier.padding(horizontal = 3.dp).size(if (index == currentPage) 8.dp else 7.dp)
                    .background(if (index == currentPage) TukiTeal else TukiOutline, CircleShape)
            )
        }
    }
}

@Composable
private fun SelectRouteButton(onClick: () -> Unit, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier.fillMaxWidth().height(52.dp).background(TukiOrange, RoundedCornerShape(17.dp)).clickable(onClick = onClick),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = if (TukiInterfaceText.isFilipino) "Piliin ang Rutang Ito" else "Select This Route",
            color = Color.White,
            fontSize = 16.sp,
            fontWeight = FontWeight.ExtraBold
        )
        Spacer(modifier = Modifier.width(10.dp))
        Text(text = "→", color = Color.White, fontSize = 21.sp, fontWeight = FontWeight.Medium)
    }
}

@Composable
private fun SuggestTodaBanner(onClick: () -> Unit, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier.fillMaxWidth().background(TukiGold.copy(alpha = 0.1f), RoundedCornerShape(16.dp)).clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(modifier = Modifier.size(28.dp).background(TukiTeal, CircleShape), contentAlignment = Alignment.Center) {
            Text("+", color = Color.White, style = MaterialTheme.typography.titleLarge)
        }
        Spacer(modifier = Modifier.width(12.dp))
        Column {
            Text(
                text = if (TukiInterfaceText.isFilipino) "May alam kang TODA na wala pa sa amin? I-suggest ito" else "Know a TODA we don't have? Suggest it",
                color = TukiInk,
                style = MaterialTheme.typography.titleMedium
            )
            Text(
                text = if (TukiInterfaceText.isFilipino) "Rerepasuhin muna ng team bago ito maging available" else "Reviewed by our team before it goes live",
                color = TukiMuted,
                style = MaterialTheme.typography.bodySmall
            )
        }
    }
}
