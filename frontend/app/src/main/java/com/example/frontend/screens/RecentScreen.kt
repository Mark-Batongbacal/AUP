package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab
import com.example.frontend.model.RecentCommute

private val TukiTeal = Color(0xFF15919B)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

@Composable
fun RecentScreen(
    commutes: List<RecentCommute> = emptyList(),
    isLoading: Boolean = false,
    errorMessage: String? = null,
    onCommuteClick: (RecentCommute) -> Unit = {},
    onHomeClick: () -> Unit = {},
    onFavoritesClick: () -> Unit = {},
    onProfileClick: () -> Unit = {}
) {
    val grouped = commutes.groupBy { it.dateGroup }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
    ) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 30.dp),
            contentPadding = androidx.compose.foundation.layout.PaddingValues(top = 30.dp, bottom = 20.dp)
        ) {
            item {
                Text(text = "Recent", color = TukiDark, fontSize = 27.sp, fontWeight = FontWeight.ExtraBold)
                Spacer(modifier = Modifier.height(24.dp))
            }

            when {
                isLoading -> {
                    item {
                        Box(
                            modifier = Modifier.fillMaxWidth().padding(vertical = 48.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            CircularProgressIndicator(color = TukiTeal)
                        }
                    }
                }

                !errorMessage.isNullOrBlank() -> {
                    item {
                        Text(
                            text = errorMessage,
                            color = MaterialTheme.colorScheme.error,
                            fontSize = 14.sp,
                            fontWeight = FontWeight.SemiBold
                        )
                    }
                }

                commutes.isEmpty() -> {
                    item {
                        Text(
                            text = "No trips yet. Your completed and active trips will appear here.",
                            color = TukiGray,
                            fontSize = 14.sp
                        )
                    }
                }

                else -> {
                    grouped.forEach { (section, sectionCommutes) ->
                        item {
                            Text(
                                text = section.uppercase(),
                                color = TukiGray,
                                fontSize = 13.sp,
                                fontWeight = FontWeight.ExtraBold
                            )
                            Spacer(modifier = Modifier.height(10.dp))
                        }

                        items(sectionCommutes, key = { it.id }) { commute ->
                            RecentRow(commute = commute, onClick = { onCommuteClick(commute) })
                            Spacer(modifier = Modifier.height(12.dp))
                        }

                        item { Spacer(modifier = Modifier.height(10.dp)) }
                    }
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
private fun RecentRow(commute: RecentCommute, onClick: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiCream2, RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(16.dp)
    ) {
        Text(
            text = "${commute.origin} to ${commute.destination}",
            color = TukiDark,
            fontSize = 17.sp,
            fontWeight = FontWeight.Bold
        )
        Spacer(modifier = Modifier.height(6.dp))
        Text(
            text = "${commute.legs} legs · ${commute.minutes} min",
            color = TukiTeal,
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold
        )
    }
}
