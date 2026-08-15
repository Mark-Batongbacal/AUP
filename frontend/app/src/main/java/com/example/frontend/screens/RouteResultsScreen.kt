package com.example.frontend.screens

// ============================================================================
// BACKEND TEAM: this screen currently runs entirely on mock data so the
// frontend has something to click through. Search the file for "BACKEND"
// to find every spot that needs to be swapped for a real API call.
// ============================================================================

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
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
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
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RouteOption
import kotlinx.coroutines.delay

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

/**
 * Shown after the user types a destination on Home and hits search.
 * Lists the possible ways to get there.
 */
@Composable
fun RouteResultsScreen(
    origin: String,
    destinationQuery: String,
    onBack: () -> Unit = {},
    onRouteSelect: (RouteOption) -> Unit = {}
) {
    var isLoading by remember { mutableStateOf(true) }
    var routeOptions by remember { mutableStateOf<List<RouteOption>>(emptyList()) }

    LaunchedEffect(destinationQuery) {
        isLoading = true

        // ------------------------------------------------------------------
        // BACKEND TEAM: replace fetchMockRouteOptions(...) with the real
        // route-planning API call, e.g.:
        //
        //   routeOptions = routeRepository.getRoutes(
        //       origin = origin,
        //       destination = destinationQuery
        //   )
        //
        // Keep it a suspend call inside this LaunchedEffect so the loading
        // spinner below keeps working as-is.
        // ------------------------------------------------------------------
        routeOptions = fetchMockRouteOptions(origin, destinationQuery)

        isLoading = false
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .padding(horizontal = 30.dp, vertical = 30.dp)
    ) {
        Text(
            text = "\u2190 Back",
            color = TukiTeal,
            fontSize = 16.sp,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.clickable(onClick = onBack)
        )

        Spacer(modifier = Modifier.height(20.dp))

        Text(
            text = destinationQuery,
            color = TukiDark,
            fontSize = 24.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(4.dp))

        Text(
            text = "from $origin",
            color = TukiGray,
            fontSize = 15.sp,
            fontWeight = FontWeight.SemiBold
        )

        Spacer(modifier = Modifier.height(24.dp))

        if (isLoading) {
            Box(
                modifier = Modifier.fillMaxWidth(),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Spacer(modifier = Modifier.height(60.dp))
                    CircularProgressIndicator(color = TukiTeal)
                    Spacer(modifier = Modifier.height(12.dp))
                    Text(text = "Finding routes...", color = TukiGray, fontSize = 14.sp)
                }
            }
        } else if (routeOptions.isEmpty()) {
            Text(
                text = "No routes found for \"$destinationQuery\" yet.",
                color = TukiGray,
                fontSize = 15.sp
            )
        } else {
            LazyColumn {
                items(routeOptions, key = { it.id }) { option ->
                    RouteOptionCard(option = option, onClick = { onRouteSelect(option) })
                    Spacer(modifier = Modifier.height(14.dp))
                }
            }
        }
    }
}

@Composable
private fun RouteOptionCard(option: RouteOption, onClick: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(color = TukiCream2, shape = RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(16.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = option.label,
                color = TukiDark,
                fontSize = 17.sp,
                fontWeight = FontWeight.Bold
            )
            Text(
                text = "\u20B1${option.totalFare}",
                color = TukiOrange,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
        }

        Spacer(modifier = Modifier.height(4.dp))

        Text(
            text = "${option.steps.size} legs \u00B7 ${option.totalMinutes} min",
            color = TukiTeal,
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold
        )

        Spacer(modifier = Modifier.height(10.dp))

        Row(verticalAlignment = Alignment.CenterVertically) {
            option.steps.forEachIndexed { index, step ->
                Text(
                    text = step.mode,
                    color = TukiDark,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.SemiBold
                )
                if (index != option.steps.lastIndex) {
                    Text(
                        text = "  \u2192  ",
                        color = TukiGray,
                        fontSize = 12.sp
                    )
                }
            }
        }
    }
}

// ============================================================================
// BACKEND TEAM: everything below this line is placeholder data so the
// screen is clickable/demoable without a live API. Delete this whole
// block once fetchMockRouteOptions(...) above is wired to the real thing.
// ============================================================================
private suspend fun fetchMockRouteOptions(origin: String, destination: String): List<RouteOption> {
    delay(900) // pretend network latency so the loading state is visible

    return listOf(
        RouteOption(
            id = "1",
            label = "Fastest",
            totalMinutes = 22,
            totalFare = 35.0,
            steps = listOf(
                CommuteStep(mode = "Jeepney", from = origin, to = "San Fernando Terminal", minutes = 14, fare = 15.0),
                CommuteStep(mode = "Tricycle", from = "San Fernando Terminal", to = destination, minutes = 8, fare = 20.0)
            )
        ),
        RouteOption(
            id = "2",
            label = "Cheapest",
            totalMinutes = 35,
            totalFare = 22.0,
            steps = listOf(
                CommuteStep(mode = "Jeepney", from = origin, to = "Dolores Crossing", minutes = 20, fare = 12.0),
                CommuteStep(mode = "Walk", from = "Dolores Crossing", to = "Guagua Terminal", minutes = 5, fare = null),
                CommuteStep(mode = "Jeepney", from = "Guagua Terminal", to = destination, minutes = 10, fare = 10.0)
            )
        ),
        RouteOption(
            id = "3",
            label = "Fewest transfers",
            totalMinutes = 28,
            totalFare = 40.0,
            steps = listOf(
                CommuteStep(mode = "Bus", from = origin, to = destination, minutes = 28, fare = 40.0)
            )
        )
    )
}
