package com.example.frontend.screens

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

private val RecentBg = Color(0xFFF8F5EC)
private val RecentCard = Color(0xFFFFFBF0)
private val RecentDark = Color(0xFF153E4B)
private val RecentTeal = Color(0xFF2C8E95)
private val RecentMuted = Color(0xFF7A898E)
private val RecentTabBg = Color(0xFFECEAE3)
private val RecentGreenBg = Color(0xFFE8F2D9)
private val RecentGreen = Color(0xFF5C9A4A)
private val RecentRedBg = Color(0xFFFFE2DE)
private val RecentRed = Color(0xFFDF5D58)
private val RecentOrange = Color(0xFFF4BF52)
private val RecentIconCream = Color(0xFFFFF0C7)
private val RecentIconBlue = Color(0xFFE7F1F3)

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
    val uniqueCommutes = remember(commutes) { commutes.distinctBy { it.uniqueRecentIdentity() } }
    val filtered = remember(uniqueCommutes, filter) {
        when (filter) {
            RecentFilter.All -> uniqueCommutes
            RecentFilter.Completed -> uniqueCommutes.filter { it.status.equals("Completed", true) }
            RecentFilter.Cancelled -> uniqueCommutes.filter { it.status.equals("Cancelled", true) }
        }
    }

    Column(Modifier.fillMaxSize().background(RecentBg)) {
        LazyColumn(
            modifier = Modifier.weight(1f).fillMaxWidth(),
            contentPadding = PaddingValues(start = 20.dp, end = 20.dp, top = 24.dp, bottom = 18.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            item {
                Text("Recent Trips", color = RecentDark, fontSize = 27.sp, fontWeight = FontWeight.ExtraBold)
                Spacer(Modifier.height(12.dp))
                RecentTabs(selected = filter, onSelected = { filter = it })
                Spacer(Modifier.height(6.dp))
            }

            when {
                isLoading -> item {
                    Box(Modifier.fillMaxWidth().padding(vertical = 60.dp), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(color = RecentTeal)
                    }
                }
                !errorMessage.isNullOrBlank() -> item {
                    Text(errorMessage, color = MaterialTheme.colorScheme.error, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                }
                !favoriteErrorMessage.isNullOrBlank() -> item {
                    Text(favoriteErrorMessage, color = MaterialTheme.colorScheme.error, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                }
                filtered.isEmpty() -> item {
                    Surface(
                        Modifier.fillMaxWidth().padding(top = 18.dp),
                        color = RecentCard,
                        shape = RoundedCornerShape(20.dp)
                    ) {
                        Column(Modifier.padding(22.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(if (isGuest) "Sign in to view your recent journeys." else "No trips in this category yet.", color = RecentMuted, fontSize = 14.sp)
                        }
                    }
                }
                else -> itemsIndexed(filtered, key = { index, commute -> commute.recentListKey(index) }) { _, commute ->
                    val recommendationId = commute.recommendationId
                    RecentTripCard(
                        commute = commute,
                        isFavorite = recommendationId != null && recommendationId in favoriteRecommendationIds,
                        favoriteWorking = recommendationId != null && recommendationId in favoriteWorkingRecommendationIds,
                        canFavorite = !isGuest && !recommendationId.isNullOrBlank(),
                        onFavoriteClick = { onToggleFavorite(commute) },
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
}

@Composable
private fun RecentTabs(selected: RecentFilter, onSelected: (RecentFilter) -> Unit) {
    Row(
        Modifier.fillMaxWidth().background(RecentTabBg, RoundedCornerShape(22.dp)).padding(3.dp),
        horizontalArrangement = Arrangement.spacedBy(2.dp)
    ) {
        RecentFilter.entries.forEach { item ->
            Surface(
                modifier = Modifier.weight(1f).height(38.dp).clickable { onSelected(item) },
                shape = RoundedCornerShape(19.dp),
                color = if (selected == item) RecentTeal else Color.Transparent
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text(item.name, color = if (selected == item) Color.White else RecentDark, fontSize = 12.sp, fontWeight = FontWeight.Bold)
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
        color = RecentCard,
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
                    color = if (completed) RecentIconCream else RecentIconBlue
                ) { Box(contentAlignment = Alignment.Center) { Text(icon, fontSize = 21.sp) } }

                Spacer(Modifier.width(11.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        "${commute.origin} → ${commute.destination}",
                        color = RecentDark,
                        fontSize = 14.sp,
                        fontWeight = FontWeight.ExtraBold,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Spacer(Modifier.height(2.dp))
                    Text(
                        "${formatRecentDate(commute.endedAt)} • ${commute.minutes} min • ₱${commute.totalFare.roundToInt()}",
                        color = RecentMuted,
                        fontSize = 11.sp,
                        fontWeight = FontWeight.SemiBold,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Spacer(Modifier.height(6.dp))
                    Surface(
                        shape = RoundedCornerShape(11.dp),
                        color = if (completed) RecentGreenBg else RecentRedBg
                    ) {
                        Text(
                            commute.status.ifBlank { if (completed) "Completed" else "Cancelled" },
                            Modifier.padding(horizontal = 12.dp, vertical = 4.dp),
                            color = if (completed) RecentGreen else RecentRed,
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Bold
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
                    CircularProgressIndicator(Modifier.size(18.dp), color = RecentTeal, strokeWidth = 2.dp)
                } else {
                    Text(if (isFavorite) "★" else "☆", color = RecentOrange, fontSize = 28.sp)
                }
            }
            Text(
                "›",
                modifier = Modifier.clickable(onClick = onClick).padding(start = 2.dp),
                color = RecentDark,
                fontSize = 28.sp,
                fontWeight = FontWeight.Medium
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
