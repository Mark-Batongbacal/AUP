package com.example.frontend.screens

import androidx.activity.compose.LocalOnBackPressedDispatcherOwner
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.LocalTukiDataProvider
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab
import com.example.frontend.core.network.ApiResult
import com.example.frontend.model.FavoriteRoute
import kotlin.math.roundToInt

import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiSky

private data class FavoriteHistorySummary(
    val recommendationType: String,
    val minutes: Int,
    val totalFare: Double,
    val walkingMeters: Int
)

@Composable
fun FavoritesScreen(
    favorites: List<FavoriteRoute> = emptyList(),
    isGuest: Boolean = false,
    isLoading: Boolean = false,
    errorMessage: String? = null,
    removingFavoriteIds: Set<String> = emptySet(),
    onBack: (() -> Unit)? = null,
    onRouteClick: (FavoriteRoute) -> Unit = {},
    onRemoveFavorite: (FavoriteRoute) -> Unit = {},
    onHomeClick: () -> Unit = {},
    onRecentClick: () -> Unit = {},
    onProfileClick: () -> Unit = {}
) {
    val backDispatcher = LocalOnBackPressedDispatcherOwner.current?.onBackPressedDispatcher
    val dataProvider = LocalTukiDataProvider.current
    val favoriteRecommendationIds = remember(favorites) {
        favorites.mapNotNull { it.recommendationId.takeIf(String::isNotBlank) }.distinct().sorted()
    }
    var historySummaries by remember(favoriteRecommendationIds) {
        mutableStateOf<Map<String, FavoriteHistorySummary>>(emptyMap())
    }
    var historyLookupComplete by remember(favoriteRecommendationIds) {
        mutableStateOf(favoriteRecommendationIds.isEmpty())
    }
    var pendingRemoval by remember { mutableStateOf<FavoriteRoute?>(null) }
    var openedFavorite by remember { mutableStateOf<FavoriteRoute?>(null) }

    LaunchedEffect(dataProvider, favoriteRecommendationIds) {
        if (dataProvider == null || favoriteRecommendationIds.isEmpty()) {
            historySummaries = emptyMap()
            historyLookupComplete = true
            return@LaunchedEffect
        }

        historyLookupComplete = false
        historySummaries = when (val result = dataProvider.tripRepository.getHistory()) {
            is ApiResult.Success -> result.data.mapNotNull { item ->
                val recommendation = item.recommendation ?: return@mapNotNull null
                recommendation.recommendationId to FavoriteHistorySummary(
                    recommendationType = recommendation.recommendationType,
                    minutes = recommendation.totalMinutes.toDouble().roundToInt().coerceAtLeast(0),
                    totalFare = recommendation.totalFare.toDouble(),
                    walkingMeters = recommendation.walkingDistanceMeters.toDouble().roundToInt().coerceAtLeast(0)
                )
            }.toMap()
            is ApiResult.Failure -> emptyMap()
        }
        historyLookupComplete = true
    }

    val uniqueFavorites = remember(favorites, historySummaries) {
        favorites.map { route ->
            val summary = historySummaries[route.recommendationId]
            if (summary == null) {
                route
            } else {
                route.copy(
                    recommendationType = summary.recommendationType.takeIf { it.isNotBlank() }
                        ?: route.recommendationType,
                    minutes = summary.minutes,
                    totalFare = summary.totalFare,
                    walkingMeters = summary.walkingMeters
                )
            }
        }.distinctBy { it.uniqueFavoriteIdentity() }
    }

    openedFavorite?.let { favorite ->
        FavoriteRouteDetailsHost(
            favorite = favorite,
            onBack = { openedFavorite = null },
            onRepeatTrip = {
                onRouteClick(favorite)
            }
        )
        return
    }

    Column(Modifier.fillMaxSize().background(TukiCream)) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(horizontal = 20.dp),
            contentPadding = PaddingValues(top = 12.dp, bottom = 20.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            item {
                Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        Modifier.size(38.dp).clickable { onBack?.invoke() ?: backDispatcher?.onBackPressed() },
                        contentAlignment = Alignment.Center
                    ) {
                        Text("←", color = TukiInk, style = MaterialTheme.typography.displaySmall)
                    }
                    Text(
                        "Favorites",
                        Modifier.weight(1f),
                        color = TukiInk,
                        style = MaterialTheme.typography.displaySmall,
                        textAlign = TextAlign.Center
                    )
                    Spacer(Modifier.size(38.dp))
                }
                Spacer(Modifier.height(5.dp))
                Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("🌟", fontSize = 57.sp)
                    Text(
                        "Save your favorite routes\nfor quick access",
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodyMedium,
                        textAlign = TextAlign.Center
                    )
                }
                Spacer(Modifier.height(10.dp))
            }

            if (!errorMessage.isNullOrBlank()) {
                item { Text(errorMessage, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.labelSmall) }
            }

            when {
                isGuest -> item { EmptyFavoriteCard("Sign in to save and view your favorite routes.") }
                isLoading || (!historyLookupComplete && favorites.isNotEmpty()) -> item {
                    Box(Modifier.fillMaxWidth().padding(vertical = 28.dp), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(color = TukiTeal)
                    }
                }
                uniqueFavorites.isEmpty() -> item { EmptyFavoriteCard("No favorite routes yet.\nTap the star on a route to save it here.") }
                else -> itemsIndexed(uniqueFavorites, key = { index, route -> route.favoriteListKey(index) }) { _, route ->
                    FavoriteRouteCard(
                        route = route,
                        removing = route.id in removingFavoriteIds,
                        onClick = {
                            openedFavorite = route
                            onRouteClick(route)
                        },
                        onRemove = { pendingRemoval = route }
                    )
                }
            }

            item {
                Spacer(Modifier.height(10.dp))
                Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiGold.copy(alpha = 0.12f)) {
                    Row(Modifier.padding(16.dp), verticalAlignment = Alignment.Top) {
                        Surface(Modifier.size(27.dp), shape = CircleShape, color = TukiGold) {
                            Box(contentAlignment = Alignment.Center) { Text("i", color = Color.White, style = MaterialTheme.typography.labelLarge) }
                        }
                        Spacer(Modifier.width(11.dp))
                        Column {
                            Text("How to add favorites?", color = TukiInk, style = MaterialTheme.typography.titleSmall)
                            Spacer(Modifier.height(5.dp))
                            Text("Tap the star on any route to save it here.", color = TukiMuted, style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
            }
        }

        BottomBar(
            selectedTab = TukiTab.FAVORITES,
            onHomeClick = onHomeClick,
            onRecentClick = onRecentClick,
            onFavoritesClick = {},
            onProfileClick = onProfileClick
        )
    }

    pendingRemoval?.let { route ->
        val removing = route.id in removingFavoriteIds
        AlertDialog(
            onDismissRequest = {
                if (!removing) pendingRemoval = null
            },
            title = { Text("Remove from favorites?") },
            text = {
                Text(
                    "Are you sure you want to remove ${route.origin} → ${route.destination} from your favorites?"
                )
            },
            confirmButton = {
                TextButton(
                    enabled = !removing,
                    onClick = {
                        pendingRemoval = null
                        onRemoveFavorite(route)
                    }
                ) {
                    Text("Remove", color = MaterialTheme.colorScheme.error, fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                TextButton(
                    enabled = !removing,
                    onClick = { pendingRemoval = null }
                ) {
                    Text("Keep Favorite", color = TukiTeal)
                }
            }
        )
    }
}

@Composable
private fun FavoriteRouteCard(route: FavoriteRoute, removing: Boolean, onClick: () -> Unit, onRemove: () -> Unit) {
    Surface(
        modifier = Modifier.fillMaxWidth().clickable(enabled = !removing, onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = Color.White,
        shadowElevation = 2.dp
    ) {
        Row(Modifier.padding(horizontal = 11.dp, vertical = 11.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(Modifier.size(44.dp), shape = RoundedCornerShape(14.dp), color = TukiTeal.copy(alpha = 0.12f)) {
                Box(contentAlignment = Alignment.Center) { Text(routeIcon(route.recommendationType), style = MaterialTheme.typography.titleLarge) }
            }
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    "${route.origin} → ${route.destination}",
                    color = TukiInk,
                    style = MaterialTheme.typography.titleMedium,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Spacer(Modifier.height(6.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                    FavoritePill(formatRecommendation(route.recommendationType), TukiSky.copy(alpha = 0.35f), TukiInk)
                    FavoritePill("${route.minutes} min", TukiSky.copy(alpha = 0.2f), TukiInk)
                    FavoritePill("₱${route.totalFare.roundToInt()}", TukiSky.copy(alpha = 0.2f), TukiInk)
                }
            }
            Spacer(Modifier.width(6.dp))
            Box(
                Modifier
                    .size(40.dp)
                    .clickable(enabled = !removing, onClick = onRemove),
                contentAlignment = Alignment.Center
            ) {
                if (removing) CircularProgressIndicator(Modifier.size(18.dp), color = TukiTeal, strokeWidth = 2.dp)
                else Text("★", color = TukiOrange, style = MaterialTheme.typography.displaySmall)
            }
        }
    }
}

@Composable
private fun FavoritePill(text: String, color: Color, textColor: Color) {
    Surface(shape = RoundedCornerShape(11.dp), color = color) {
        Text(
            text,
            Modifier.padding(horizontal = 9.dp, vertical = 4.dp),
            color = textColor,
            style = MaterialTheme.typography.labelSmall,
            maxLines = 1
        )
    }
}

@Composable
private fun EmptyFavoriteCard(message: String) {
    Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = Color.White) {
        Text(message, Modifier.padding(22.dp), color = TukiMuted, style = MaterialTheme.typography.bodyMedium, textAlign = TextAlign.Center)
    }
}

private fun formatRecommendation(type: String): String {
    val tags = type.split(',').map { it.trim().lowercase() }
    return when {
        "fastest" in tags -> "Fastest"
        "cheapest" in tags -> "Cheapest"
        "efficient" in tags || "balanced" in tags -> "Balanced"
        type.isBlank() -> "Route"
        else -> type.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
    }
}

private fun routeIcon(type: String): String = when (formatRecommendation(type)) {
    "Fastest" -> "⚡"
    "Cheapest" -> "₱"
    else -> "🛺"
}

private fun FavoriteRoute.uniqueFavoriteIdentity(): String =
    recommendationId.takeIf { it.isNotBlank() }
        ?: id.takeIf { it.isNotBlank() }
        ?: listOf(origin, destination, recommendationType).joinToString("|")

private fun FavoriteRoute.favoriteListKey(index: Int): String =
    "${uniqueFavoriteIdentity()}-$index"
