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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.routing.RoutingRepository
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RouteOption
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
    onBack: () -> Unit = {},
    onRouteSelect: (RouteOption) -> Unit = {},
    onSuggestToda: () -> Unit = {}
) {
    var isLoading by remember { mutableStateOf(true) }
    var routeOptions by remember { mutableStateOf<List<RouteOption>>(emptyList()) }
    var errorMessage by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(destinationQuery) {
        isLoading = true
        errorMessage = null

        // Using sample coordinates as real geocoding is not yet integrated in this view
        when (
            val result = routingRepository.planTrip(
                15.1453,
                120.5887,
                15.1174,
                120.5720
            )
        ) {
            is ApiResult.Success -> {
                routeOptions = result.data.map { plan ->
                    val walkMeters = (
                        plan.source.originAccess.walkDistanceMeters +
                            plan.source.destinationAccess.walkDistanceMeters +
                            plan.source.transferWalkDistancesMeters.sum()
                        ).roundToInt()

                    RouteOption(
                        id = plan.hashCode().toString(),
                        label = plan.source.recommendationType,
                        totalMinutes = (plan.source.totalTimeSeconds / 60).roundToInt(),
                        totalFare = plan.source.totalFarePesos,
                        walkMeters = walkMeters,
                        transfers = plan.source.transferCount,
                        generalCost = plan.source.generalizedCostPesos,
                        steps = plan.legs.map { leg ->
                            CommuteStep(
                                mode = leg.mode.toString(),
                                from = "Origin",
                                to = "Destination",
                                minutes = (leg.durationSeconds / 60).roundToInt(),
                                fare = leg.farePesos
                            )
                        }
                    )
                }
            }

            is ApiResult.Failure -> {
                errorMessage = result.message
            }
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

        CurrentAndDestinationCard(
            origin = origin,
            destinationQuery = destinationQuery
        )

        Spacer(modifier = Modifier.height(18.dp))

        if (isLoading) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 32.dp),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    CircularProgressIndicator(color = TukiTeal)
                    Spacer(modifier = Modifier.height(12.dp))
                    Text(
                        text = "Finding routes...",
                        color = TukiGray,
                        fontSize = 14.sp
                    )
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

            val pagerState = rememberPagerState(
                pageCount = { routeOptions.size }
            )

            HorizontalPager(
                state = pagerState,
                modifier = Modifier.fillMaxWidth(),
                pageSpacing = 12.dp
            ) { page ->
                RouteOptionCard(
                    option = routeOptions[page],
                    onClick = {
                        onRouteSelect(routeOptions[page])
                    }
                )
            }

            Spacer(modifier = Modifier.height(12.dp))

            PagerDots(
                pageCount = routeOptions.size,
                currentPage = pagerState.currentPage
            )

            Spacer(modifier = Modifier.height(16.dp))

            SuggestTodaBanner(onClick = onSuggestToda)

            Spacer(modifier = Modifier.height(12.dp))
        }
    }
}

@Composable
private fun CurrentAndDestinationCard(
    origin: String,
    destinationQuery: String
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                color = TukiCream2,
                shape = RoundedCornerShape(14.dp)
            )
            .padding(horizontal = 16.dp, vertical = 12.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(9.dp)
                    .background(TukiTeal, CircleShape)
            )
            Spacer(modifier = Modifier.width(10.dp))
            Text(
                text = "$origin (current location)",
                color = TukiDark,
                fontSize = 14.sp,
                fontWeight = FontWeight.Bold
            )
        }

        Spacer(modifier = Modifier.height(8.dp))

        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(9.dp)
                    .background(
                        TukiOrange,
                        RoundedCornerShape(2.dp)
                    )
            )
            Spacer(modifier = Modifier.width(10.dp))
            Text(
                text = destinationQuery.ifBlank { "Somewhere" },
                color = TukiDark,
                fontSize = 14.sp,
                fontWeight = FontWeight.Medium
            )
        }
    }
}

@Composable
private fun RouteOptionCard(
    option: RouteOption,
    onClick: () -> Unit
) {
    val icon = when {
        option.isRecommended -> "⭐"
        option.label.contains("Fast", ignoreCase = true) -> "⚡"
        option.label.contains("Cheap", ignoreCase = true) -> "₱"
        else -> "🚌"
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                color = TukiDark,
                shape = RoundedCornerShape(18.dp)
            )
            .padding(20.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = icon,
                    color = Color.White,
                    fontSize = 16.sp
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = option.label,
                    color = Color.White,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold
                )
            }

            if (option.isRecommended) {
                Box(
                    modifier = Modifier
                        .background(
                            color = TukiOrange,
                            shape = RoundedCornerShape(10.dp)
                        )
                        .padding(
                            horizontal = 10.dp,
                            vertical = 5.dp
                        )
                ) {
                    Text(
                        text = "RECOMMENDED",
                        color = Color.White,
                        fontSize = 10.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }
        }

        if (option.description.isNotBlank()) {
            Spacer(modifier = Modifier.height(6.dp))
            Text(
                text = option.description,
                color = Color.White.copy(alpha = 0.7f),
                fontSize = 13.sp
            )
        }

        Spacer(modifier = Modifier.height(16.dp))

        Row(modifier = Modifier.fillMaxWidth()) {
            StatBox(
                value = "~${option.totalMinutes} min",
                label = "EST. TIME",
                modifier = Modifier.weight(1f)
            )

            Spacer(modifier = Modifier.width(10.dp))

            StatBox(
                value = "₱${option.totalFare.toInt()}",
                label = "EST. FARE",
                modifier = Modifier.weight(1f)
            )
        }

        Spacer(modifier = Modifier.height(10.dp))

        Row(modifier = Modifier.fillMaxWidth()) {
            StatBox(
                value = "${option.walkMeters} m",
                label = "WALK",
                modifier = Modifier.weight(1f)
            )
            Spacer(modifier = Modifier.width(10.dp))
            StatBox(
                value = "${maxOf(option.transfers, option.steps.size)} legs",
                label = "TRANSFERS",
                modifier = Modifier.weight(1f)
            )
        }

        Spacer(modifier = Modifier.height(14.dp))

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(
                    color = Color.White.copy(alpha = 0.08f),
                    shape = RoundedCornerShape(12.dp)
                )
                .padding(horizontal = 14.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column {
                Text(
                    text = "GEN. COST",
                    color = Color.White.copy(alpha = 0.6f),
                    fontSize = 10.sp,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "Fare + time value",
                    color = Color.White.copy(alpha = 0.5f),
                    fontSize = 10.sp
                )
            }

            Text(
                text = "₱${option.generalCost.toInt()}",
                color = TukiOrange,
                fontSize = 18.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        Spacer(modifier = Modifier.height(10.dp))

        Text(
            text = "Estimates only — actual time and fare may vary with traffic and driver",
            color = Color.White.copy(alpha = 0.45f),
            fontSize = 10.sp
        )

        Spacer(modifier = Modifier.height(14.dp))

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(
                    color = TukiOrange,
                    shape = RoundedCornerShape(14.dp)
                )
                .clickable(onClick = onClick)
                .padding(vertical = 14.dp),
            horizontalArrangement = Arrangement.Center
        ) {
            Text(
                text = "Select This Route",
                color = Color.White,
                fontSize = 15.sp,
                fontWeight = FontWeight.Bold
            )
        }
    }
}

@Composable
private fun StatBox(
    value: String,
    label: String,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .background(
                color = Color.White.copy(alpha = 0.08f),
                shape = RoundedCornerShape(12.dp)
            )
            .padding(horizontal = 12.dp, vertical = 10.dp)
    ) {
        Text(
            text = value,
            color = Color.White,
            fontSize = 15.sp,
            fontWeight = FontWeight.Bold
        )
        Spacer(modifier = Modifier.height(2.dp))
        Text(
            text = label,
            color = Color.White.copy(alpha = 0.55f),
            fontSize = 9.sp,
            fontWeight = FontWeight.Bold
        )
    }
}

@Composable
private fun PagerDots(
    pageCount: Int,
    currentPage: Int
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.Center
    ) {
        repeat(pageCount) { index ->
            Box(
                modifier = Modifier
                    .padding(horizontal = 3.dp)
                    .size(
                        if (index == currentPage) {
                            8.dp
                        } else {
                            6.dp
                        }
                    )
                    .background(
                        color = if (index == currentPage) {
                            TukiTeal
                        } else {
                            TukiGray.copy(alpha = 0.4f)
                        },
                        shape = CircleShape
                    )
            )
        }
    }
}

@Composable
private fun SuggestTodaBanner(
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                color = TukiCream2,
                shape = RoundedCornerShape(14.dp)
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(26.dp)
                .background(TukiTeal, CircleShape),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "+",
                color = Color.White,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
        }

        Spacer(modifier = Modifier.width(12.dp))

        Column {
            Text(
                text = "Know a TODA we don't have? Suggest it",
                color = TukiDark,
                fontSize = 13.sp,
                fontWeight = FontWeight.Bold
            )
            Text(
                text = "Reviewed by our team before it goes live",
                color = TukiGray,
                fontSize = 11.sp
            )
        }
    }
}
