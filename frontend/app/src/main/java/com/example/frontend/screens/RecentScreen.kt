package com.example.frontend.screens

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
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab
import com.example.frontend.model.RecentCommute
import java.time.Instant
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import kotlin.math.roundToInt

import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiForest
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiTealSurface
import com.example.frontend.ui.theme.TukiForestSurface
import com.example.frontend.ui.theme.TukiSurfaceRaised

private enum class RecentFilter { All, Completed, Cancelled }

@Composable
fun RecentScreen(
    commutes: List<RecentCommute> = emptyList(),
    isGuest: Boolean = false,
    isLoading: Boolean = false,
    errorMessage: String? = null,
    favoriteRecommendationIds: Set<String> = emptySet(),
    favoriteWorkingRecommendationIds: Set<String> = emptySet(),
    favoriteErrorMessage: String? = null,
    onToggleFavorite: (RecentCommute) -> Unit = {},
    onCommuteClick: (RecentCommute) -> Unit = {},
    onHomeClick: () -> Unit = {},
    onFavoritesClick: () -> Unit = {},
    onProfileClick: () -> Unit = {}
) {
    var filter by rememberSaveable { mutableStateOf(RecentFilter.All) }
    var pendingFavoriteRemoval by remember { mutableStateOf<RecentCommute?>(null) }
    val uniqueCommutes = remember(commutes) { commutes.distinctBy { it.uniqueRecentIdentity() } }
    val filtered = remember(uniqueCommutes, filter) {
        when (filter) {
            RecentFilter.All -> uniqueCommutes
            RecentFilter.Completed -> uniqueCommutes.filter { it.status.equals("Completed", true) }
            RecentFilter.Cancelled -> uniqueCommutes.filter { it.status.equals("Cancelled", true) }
        }
    }

    Column(Modifier.fillMaxSize().background(TukiCream)) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(horizontal = 20.dp),
            contentPadding = PaddingValues(top = 12.dp, bottom = 18.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            item {
                Text(
                    "Recent Trips",
                    color = TukiInk,
                    style = MaterialTheme.typography.displaySmall
                )
                Spacer(Modifier.height(12.dp))
                RecentTabs(selected = filter, onSelected = { filter = it })
                Spacer(Modifier.height(6.dp))
            }

            when {
                isLoading -> item {
                    Box(Modifier.fillMaxWidth().padding(vertical = 60.dp), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(color = TukiTeal)
                    }
                }
                !errorMessage.isNullOrBlank() -> item {
                    Text(errorMessage, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.labelLarge)
                }
                !favoriteErrorMessage.isNullOrBlank() -> item {
                    Text(favoriteErrorMessage, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.labelLarge)
                }
                filtered.isEmpty() -> item {
                    Surface(
                        Modifier.fillMaxWidth().padding(top = 18.dp),
                        color = TukiSurfaceRaised,
                        shape = RoundedCornerShape(20.dp)
                    ) {
                        Column(Modifier.padding(22.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(if (isGuest) "Sign in to view your recent journeys." else "No trips in this category yet.", color = TukiMuted, style = MaterialTheme.typography.bodyMedium)
                        }
                    }
                }
                else -> itemsIndexed(filtered, key = { index, commute -> commute.recentListKey(index) }) { _, commute ->
                    val recommendationId = commute.recommendationId
                    val isFavorite = recommendationId != null && recommendationId in favoriteRecommendationIds
                    RecentTripCard(
                        commute = commute,
                        isFavorite = isFavorite,
                        favoriteWorking = recommendationId != null && recommendationId in favoriteWorkingRecommendationIds,
                        canFavorite = !isGuest && !recommendationId.isNullOrBlank(),
                        onFavoriteClick = {
                            if (isFavorite) {
                                pendingFavoriteRemoval = commute
                            } else {
                                onToggleFavorite(commute)
                            }
                        },
                        onClick = { onCommuteClick(commute) }
                    )
                }
            }
        }

        BottomBar(
            selectedTab = TukiTab.RECENT,
            onHomeClick = onHomeClick,
            onRecentClick = {},
            onFavoritesClick = onFavoritesClick,
            onProfileClick = onProfileClick
        )
    }

    pendingFavoriteRemoval?.let { commute ->
        val recommendationId = commute.recommendationId
        val working = recommendationId != null && recommendationId in favoriteWorkingRecommendationIds
        AlertDialog(
            onDismissRequest = {
                if (!working) pendingFavoriteRemoval = null
            },
            title = { Text("Remove from favorites?") },
            text = {
                Text(
                    "Are you sure you want to remove ${commute.origin} → ${commute.destination} from your favorites?"
                )
            },
            confirmButton = {
                TextButton(
                    enabled = !working,
                    onClick = {
                        pendingFavoriteRemoval = null
                        onToggleFavorite(commute)
                    }
                ) {
                    Text("Remove", color = MaterialTheme.colorScheme.error, fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                TextButton(
                    enabled = !working,
                    onClick = { pendingFavoriteRemoval = null }
                ) {
                    Text("Keep Favorite", color = TukiTeal)
                }
            }
        )
    }
}

@Composable
private fun RecentTabs(selected: RecentFilter, onSelected: (RecentFilter) -> Unit) {
    Row(
        Modifier.fillMaxWidth().background(TukiSky.copy(alpha = 0.35f), RoundedCornerShape(22.dp)).padding(3.dp),
        horizontalArrangement = Arrangement.spacedBy(2.dp)
    ) {
        RecentFilter.entries.forEach { item ->
            Surface(
                modifier = Modifier.weight(1f).height(38.dp).clickable { onSelected(item) },
                shape = RoundedCornerShape(19.dp),
                color = if (selected == item) TukiDeepTeal else Color.Transparent
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text(item.name, color = if (selected == item) Color.White else TukiInk, style = MaterialTheme.typography.labelLarge)
                }
            }
        }
    }
}

@Composable
private fun RecentTripCard(
    commute: RecentCommute,
    isFavorite: Boolean,
    favoriteWorking: Boolean,
    canFavorite: Boolean,
    onFavoriteClick: () -> Unit,
    onClick: () -> Unit
) {
    val completed = commute.status.equals("Completed", true)
    val icon = when {
        commute.steps.any { it.mode.contains("jeep", true) || it.mode.contains("bus", true) } -> "🚌"
        commute.steps.any { it.mode.contains("tricycle", true) || it.mode.contains("trike", true) } -> "🛺"
        commute.steps.all { it.mode.contains("walk", true) } -> "🚶"
        else -> "🛺"
    }

    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = TukiSurfaceRaised,
        shape = RoundedCornerShape(18.dp),
        shadowElevation = 2.dp
    ) {
        Row(Modifier.padding(horizontal = 12.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
            Row(
                modifier = Modifier.weight(1f).clickable(onClick = onClick),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Surface(
                    Modifier.size(46.dp),
                    shape = RoundedCornerShape(14.dp),
                    color = if (completed) TukiGold.copy(alpha = 0.15f) else TukiTealSurface
                ) { Box(contentAlignment = Alignment.Center) { Text(icon, style = MaterialTheme.typography.titleLarge) } }

                Spacer(Modifier.width(11.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        "${commute.origin} → ${commute.destination}",
                        color = TukiInk,
                        style = MaterialTheme.typography.titleMedium,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Spacer(Modifier.height(2.dp))
                    Text(
                        "${formatRecentDate(commute.endedAt)} • ${commute.minutes} min • ₱${commute.totalFare.roundToInt()}",
                        color = TukiMuted,
                        style = MaterialTheme.typography.labelSmall,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Spacer(Modifier.height(6.dp))
                    Surface(
                        shape = RoundedCornerShape(11.dp),
                        color = if (completed) TukiForestSurface else com.example.frontend.ui.theme.TukiDanger.copy(alpha = 0.12f)
                    ) {
                        Text(
                            commute.status.ifBlank { if (completed) "Completed" else "Cancelled" },
                            Modifier.padding(horizontal = 12.dp, vertical = 4.dp),
                            color = if (completed) TukiForest else com.example.frontend.ui.theme.TukiDanger,
                            style = MaterialTheme.typography.labelSmall
                        )
                    }
                }
            }
            Spacer(Modifier.width(8.dp))
            Box(
                modifier = Modifier.size(40.dp).clickable(enabled = canFavorite && !favoriteWorking, onClick = onFavoriteClick),
                contentAlignment = Alignment.Center
            ) {
                if (favoriteWorking) {
                    CircularProgressIndicator(Modifier.size(18.dp), color = TukiTeal, strokeWidth = 2.dp)
                } else {
                    Text(if (isFavorite) "★" else "☆", color = TukiOrange, style = MaterialTheme.typography.titleLarge)
                }
            }
            Text(
                "›",
                modifier = Modifier.clickable(onClick = onClick).padding(start = 2.dp),
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
        }
    }
}

private fun formatRecentDate(value: String?): String {
    if (value.isNullOrBlank()) return "Recent trip"
    val zone = ZoneId.systemDefault()
    val date = runCatching { Instant.parse(value).atZone(zone).toLocalDate() }
        .recoverCatching { OffsetDateTime.parse(value).atZoneSameInstant(zone).toLocalDate() }
        .recoverCatching { LocalDateTime.parse(value).atZone(ZoneOffset.UTC).withZoneSameInstant(zone).toLocalDate() }
        .getOrNull() ?: return "Recent trip"
    return date.format(DateTimeFormatter.ofPattern("MMM d, yyyy"))
}

private fun RecentCommute.uniqueRecentIdentity(): String =
    id.takeIf { it.isNotBlank() }
        ?: listOf(
            recommendationId.orEmpty(),
            origin,
            destination,
            endedAt.orEmpty(),
            status
        ).joinToString("|")

private fun RecentCommute.recentListKey(index: Int): String =
    "${uniqueRecentIdentity()}-$index"
