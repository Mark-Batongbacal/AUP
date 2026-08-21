package com.example.frontend.screens

import androidx.activity.compose.LocalOnBackPressedDispatcherOwner
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab
import com.example.frontend.model.FavoriteRoute
import kotlin.math.roundToInt

private val FavoriteBg = Color(0xFFF8F5EC)
private val FavoriteSurface = Color(0xFFFFFBF0)
private val FavoriteDark = Color(0xFF153E4B)
private val FavoriteTeal = Color(0xFF2C8E95)
private val FavoriteMuted = Color(0xFF7A898E)
private val FavoriteOrange = Color(0xFFF4BF52)
private val FavoriteTip = Color(0xFFFFF0C7)
private val FavoriteBlue = Color(0xFFE8F2F2)
private val FavoriteGreen = Color(0xFFE7F1D8)
private val FavoriteDanger = Color(0xFFDF5D58)

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
    val uniqueFavorites = remember(favorites) { favorites.distinctBy { it.uniqueFavoriteIdentity() } }

    Column(Modifier.fillMaxSize().background(FavoriteBg)) {
        LazyColumn(
            modifier = Modifier.weight(1f).fillMaxWidth(),
            contentPadding = PaddingValues(start = 20.dp, end = 20.dp, top = 20.dp, bottom = 20.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            item {
                Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        Modifier.size(38.dp).clickable { onBack?.invoke() ?: backDispatcher?.onBackPressed() },
                        contentAlignment = Alignment.Center
                    ) {
                        Text("←", color = FavoriteDark, fontSize = 25.sp, fontWeight = FontWeight.Bold)
                    }
                    Text(
                        "Favorites",
                        Modifier.weight(1f),
                        color = FavoriteDark,
                        fontSize = 25.sp,
                        fontWeight = FontWeight.ExtraBold,
                        textAlign = TextAlign.Center
                    )
                    Spacer(Modifier.size(38.dp))
                }
                Spacer(Modifier.height(5.dp))
                Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("🌟", fontSize = 57.sp)
                    Text(
                        "Save your favorite routes\nfor quick access",
                        color = FavoriteMuted,
                        fontSize = 14.sp,
                        lineHeight = 18.sp,
                        textAlign = TextAlign.Center,
                        fontWeight = FontWeight.SemiBold
                    )
                }
                Spacer(Modifier.height(10.dp))
            }

            if (!errorMessage.isNullOrBlank()) {
                item { Text(errorMessage, color = MaterialTheme.colorScheme.error, fontSize = 11.sp, fontWeight = FontWeight.SemiBold) }
            }

            when {
                isGuest -> item { EmptyFavoriteCard("Sign in to save and view your favorite routes.") }
                isLoading -> item {
                    Box(Modifier.fillMaxWidth().padding(vertical = 28.dp), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(color = FavoriteTeal)
                    }
                }
                uniqueFavorites.isEmpty() -> item { EmptyFavoriteCard("No favorite routes yet.\nTap the star on a route to save it here.") }
                else -> itemsIndexed(uniqueFavorites, key = { index, route -> route.favoriteListKey(index) }) { _, route ->
                    FavoriteRouteCard(
                        route = route,
                        removing = route.id in removingFavoriteIds,
                        onClick = { onRouteClick(route) },
                        onRemove = { onRemoveFavorite(route) }
                    )
                }
            }

            item {
                Spacer(Modifier.height(10.dp))
                Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = FavoriteTip) {
                    Row(Modifier.padding(16.dp), verticalAlignment = Alignment.Top) {
                        Surface(Modifier.size(27.dp), shape = CircleShape, color = Color(0xFFD9913C)) {
                            Box(contentAlignment = Alignment.Center) { Text("i", color = Color.White, fontWeight = FontWeight.Bold) }
                        }
                        Spacer(Modifier.width(11.dp))
                        Column {
                            Text("How to add favorites?", color = FavoriteDark, fontSize = 13.sp, fontWeight = FontWeight.ExtraBold)
                            Spacer(Modifier.height(5.dp))
                            Text("Tap the star on any route to save it here.", color = FavoriteMuted, fontSize = 12.sp, lineHeight = 17.sp)
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
}

@Composable
private fun FavoriteRouteCard(route: FavoriteRoute, removing: Boolean, onClick: () -> Unit, onRemove: () -> Unit) {
    Surface(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = FavoriteSurface,
        shadowElevation = 2.dp
    ) {
        Row(Modifier.padding(horizontal = 11.dp, vertical = 11.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(Modifier.size(44.dp), shape = RoundedCornerShape(14.dp), color = FavoriteGreen) {
                Box(contentAlignment = Alignment.Center) { Text(routeIcon(route.recommendationType), fontSize = 21.sp) }
            }
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    "${route.origin} → ${route.destination}",
                    color = FavoriteDark,
                    fontSize = 14.sp,
                    fontWeight = FontWeight.ExtraBold,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Spacer(Modifier.height(6.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                    FavoritePill(formatRecommendation(route.recommendationType), FavoriteBlue, FavoriteDark)
                    FavoritePill("${route.minutes} min", Color(0xFFE9ECE8), FavoriteDark)
                    FavoritePill("₱${route.totalFare.roundToInt()}", Color(0xFFE9ECE8), FavoriteDark)
                }
            }
            Spacer(Modifier.width(6.dp))
            Box(Modifier.size(40.dp).clickable(enabled = !removing, onClick = onRemove), contentAlignment = Alignment.Center) {
                if (removing) CircularProgressIndicator(Modifier.size(18.dp), color = FavoriteTeal, strokeWidth = 2.dp)
                else Text("★", color = FavoriteOrange, fontSize = 26.sp)
            }
        }
    }
}

@Composable
private fun FavoritePill(text: String, color: Color, textColor: Color) {
    Surface(shape = RoundedCornerShape(11.dp), color = color) {
        Text(text, Modifier.padding(horizontal = 9.dp, vertical = 4.dp), color = textColor, fontSize = 9.sp, fontWeight = FontWeight.Bold, maxLines = 1)
    }
}

@Composable
private fun EmptyFavoriteCard(message: String) {
    Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = FavoriteSurface) {
        Text(message, Modifier.padding(22.dp), color = FavoriteMuted, fontSize = 13.sp, lineHeight = 18.sp, textAlign = TextAlign.Center)
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
