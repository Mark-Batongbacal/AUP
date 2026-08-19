package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
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
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.places.PlacesRepository
import com.example.frontend.data.routing.JourneyPlanRequest
import com.example.frontend.data.routing.RoutingRepository
import com.example.frontend.data.routing.TransitMode
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RouteOption
import com.example.frontend.model.RoutePoint
import kotlin.math.roundToInt

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

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

        val deviceLocation = if (originLatitude == null || originLongitude == null) {
            context.currentDeviceLocation()
        } else {
            null
        }

        val resolvedOriginLatitude = originLatitude ?: deviceLocation?.latitude
        val resolvedOriginLongitude = originLongitude ?: deviceLocation?.longitude

        if (resolvedOriginLatitude == null || resolvedOriginLongitude == null) {
            errorMessage = "Current location is unavailable. Allow location access and try again."
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

                    val routePoints = buildList {
                        plan.legs.forEach { leg ->
                            val points = if (leg.geometry.isNotEmpty()) {
                                leg.geometry.map { point -> RoutePoint(point.latitude, point.longitude) }
                            } else {
                                listOf(
                                    RoutePoint(leg.origin.latitude, leg.origin.longitude),
                                    RoutePoint(leg.destination.latitude, leg.destination.longitude)
                                )
                            }

                            points.forEach { point ->
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

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .navigationBarsPadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp, vertical = 12.dp)
    ) {
        Text(
            text = "← Back",
            color = TukiTeal,
            fontSize = 16.sp,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.clickable(onClick = onBack)
        )

        Spacer(modifier = Modifier.height(16.dp))

        Text(
            text = "Where are you going?",
            color = TukiDark,
            fontSize = 22.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(14.dp))

        CurrentAndDestinationCard(origin = origin, destinationQuery = destinationQuery)

        Spacer(modifier = Modifier.height(18.dp))

        if (isLoading) {
            Box(
                modifier = Modifier.fillMaxWidth().padding(vertical = 32.dp),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    CircularProgressIndicator(color = TukiTeal)
                    Spacer(modifier = Modifier.height(12.dp))
                    Text("Finding routes...", color = TukiGray, fontSize = 14.sp)
                }
            }
        } else if (errorMessage != null) {
            Text(
                text = "Error: $errorMessage",
                color = Color.Red,
                fontSize = 15.sp,
                fontWeight = FontWeight.Bold
            )
        } else if (routeOptions.isEmpty()) {
            Text(
                text = "No routes found for \"$destinationQuery\" yet.",
                color = TukiGray,
                fontSize = 15.sp
            )
        } else {
            Text(
                text = "ROUTE OPTIONS · $origin → $destinationQuery".uppercase(),
                color = TukiGray,
                fontSize = 11.sp,
                fontWeight = FontWeight.Bold
            )

            Spacer(modifier = Modifier.height(10.dp))

            val pagerState = rememberPagerState(pageCount = { routeOptions.size })

            HorizontalPager(
                state = pagerState,
                modifier = Modifier.fillMaxWidth(),
                pageSpacing = 12.dp
            ) { page ->
                RouteOptionCard(
                    option = routeOptions[page],
                    onClick = { onRouteSelect(routeOptions[page]) }
                )
            }

            Spacer(modifier = Modifier.height(12.dp))
            PagerDots(pageCount = routeOptions.size, currentPage = pagerState.currentPage)
            Spacer(modifier = Modifier.height(16.dp))
            SuggestTodaBanner(onClick = onSuggestToda)
            Spacer(modifier = Modifier.height(12.dp))
        }
    }
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
private fun CurrentAndDestinationCard(origin: String, destinationQuery: String) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiCream2, RoundedCornerShape(14.dp))
            .padding(horizontal = 16.dp, vertical = 12.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(modifier = Modifier.size(9.dp).background(TukiTeal, CircleShape))
            Spacer(modifier = Modifier.width(10.dp))
            Text("$origin (current location)", color = TukiDark, fontSize = 14.sp, fontWeight = FontWeight.Bold)
        }
        Spacer(modifier = Modifier.height(8.dp))
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(modifier = Modifier.size(9.dp).background(TukiOrange, RoundedCornerShape(2.dp)))
            Spacer(modifier = Modifier.width(10.dp))
            Text(
                destinationQuery.ifBlank { "Somewhere" },
                color = TukiDark,
                fontSize = 14.sp,
                fontWeight = FontWeight.Medium
            )
        }
    }
}

@Composable
private fun RouteOptionCard(option: RouteOption, onClick: () -> Unit) {
    val icon = when {
        option.isRecommended -> "⭐"
        option.label.contains("Fast", ignoreCase = true) -> "⚡"
        option.label.contains("Cheap", ignoreCase = true) -> "₱"
        else -> "🚌"
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiDark, RoundedCornerShape(18.dp))
            .padding(20.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(icon, color = Color.White, fontSize = 16.sp)
                Spacer(modifier = Modifier.width(8.dp))
                Text(option.label, color = Color.White, fontSize = 18.sp, fontWeight = FontWeight.Bold)
            }

            if (option.isRecommended) {
                Box(
                    modifier = Modifier
                        .background(TukiOrange, RoundedCornerShape(10.dp))
                        .padding(horizontal = 10.dp, vertical = 5.dp)
                ) {
                    Text("RECOMMENDED", color = Color.White, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                }
            }
        }

        if (option.description.isNotBlank()) {
            Spacer(modifier = Modifier.height(6.dp))
            Text(option.description, color = Color.White.copy(alpha = 0.7f), fontSize = 13.sp)
        }

        Spacer(modifier = Modifier.height(16.dp))

        Row(modifier = Modifier.fillMaxWidth()) {
            StatBox("~${option.totalMinutes} min", "EST. TIME", Modifier.weight(1f))
            Spacer(modifier = Modifier.width(10.dp))
            StatBox("₱${option.totalFare.toInt()}", "EST. FARE", Modifier.weight(1f))
        }

        Spacer(modifier = Modifier.height(10.dp))

        Row(modifier = Modifier.fillMaxWidth()) {
            StatBox("${option.walkMeters} m", "WALK", Modifier.weight(1f))
            Spacer(modifier = Modifier.width(10.dp))
            StatBox(
                "${option.steps.size} legs",
                if (option.transfers == 1) "1 TRANSFER" else "${option.transfers} TRANSFERS",
                Modifier.weight(1f)
            )
        }

        Spacer(modifier = Modifier.height(14.dp))

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(Color.White.copy(alpha = 0.08f), RoundedCornerShape(12.dp))
                .padding(horizontal = 14.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column {
                Text("GEN. COST", color = Color.White.copy(alpha = 0.6f), fontSize = 10.sp, fontWeight = FontWeight.Bold)
                Text("Fare + time value", color = Color.White.copy(alpha = 0.5f), fontSize = 10.sp)
            }
            Text("₱${option.generalCost.toInt()}", color = TukiOrange, fontSize = 18.sp, fontWeight = FontWeight.ExtraBold)
        }

        Spacer(modifier = Modifier.height(10.dp))
        Text(
            "Estimates only — actual time and fare may vary with traffic and driver",
            color = Color.White.copy(alpha = 0.45f),
            fontSize = 10.sp
        )
        Spacer(modifier = Modifier.height(14.dp))

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(TukiOrange, RoundedCornerShape(14.dp))
                .clickable(onClick = onClick)
                .padding(vertical = 14.dp),
            horizontalArrangement = Arrangement.Center
        ) {
            Text("Select This Route", color = Color.White, fontSize = 15.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun StatBox(value: String, label: String, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .background(Color.White.copy(alpha = 0.08f), RoundedCornerShape(12.dp))
            .padding(horizontal = 12.dp, vertical = 10.dp)
    ) {
        Text(value, color = Color.White, fontSize = 15.sp, fontWeight = FontWeight.Bold)
        Spacer(modifier = Modifier.height(2.dp))
        Text(label, color = Color.White.copy(alpha = 0.55f), fontSize = 9.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun PagerDots(pageCount: Int, currentPage: Int) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
        repeat(pageCount) { index ->
            Box(
                modifier = Modifier
                    .padding(horizontal = 3.dp)
                    .size(if (index == currentPage) 8.dp else 6.dp)
                    .background(
                        if (index == currentPage) TukiTeal else TukiGray.copy(alpha = 0.4f),
                        CircleShape
                    )
            )
        }
    }
}

@Composable
private fun SuggestTodaBanner(onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiCream2, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier.size(26.dp).background(TukiTeal, CircleShape),
            contentAlignment = Alignment.Center
        ) {
            Text("+", color = Color.White, fontSize = 16.sp, fontWeight = FontWeight.Bold)
        }
        Spacer(modifier = Modifier.width(12.dp))
        Column {
            Text("Know a TODA we don't have? Suggest it", color = TukiDark, fontSize = 13.sp, fontWeight = FontWeight.Bold)
            Text("Reviewed by our team before it goes live", color = TukiGray, fontSize = 11.sp)
        }
    }
}
