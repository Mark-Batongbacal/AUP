package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
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
import com.example.frontend.model.FavoriteRoute
import com.example.frontend.R
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.size
import androidx.compose.ui.res.painterResource


private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

//BACKEND: replace sampleFavorites (bottom of file) with the user's real starred routes

@Composable
fun FavoritesScreen(
    favorites: List<FavoriteRoute> = sampleFavorites,
    isGuest: Boolean = false,
    onRouteClick: (FavoriteRoute) -> Unit = {},
    onHomeClick: () -> Unit = {},
    onRecentClick: () -> Unit = {},
    onProfileClick: () -> Unit = {}
) {
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
                Text(text = "Favorites", color = TukiDark, fontSize = 27.sp, fontWeight = FontWeight.ExtraBold)
                Spacer(modifier = Modifier.height(24.dp))
            }

            item {
                Text(text = "STARRED ROUTES", color = TukiGray, fontSize = 13.sp, fontWeight = FontWeight.ExtraBold)
                Spacer(modifier = Modifier.height(10.dp))
            }

            if (isGuest) {
                item {
                    Text(
                        text = "Sign in to save favorite routes.",
                        color = TukiGray,
                        fontSize = 14.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                }
            } else if (favorites.isEmpty()) {
                item {
                    Text(
                        text = "No favorite routes yet.",
                        color = TukiGray,
                        fontSize = 14.sp
                    )
                }
            } else {
                items(favorites, key = { it.id }) { route ->
                    FavoriteRow(route = route, onClick = { onRouteClick(route) })
                    Spacer(modifier = Modifier.height(12.dp))
                }
            }

            item {
                Spacer(modifier = Modifier.height(10.dp))
                TipBanner()
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
private fun FavoriteRow(route: FavoriteRoute, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiCream2, RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(16.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column {
            Text(
                text = "${route.origin} to ${route.destination}",
                color = TukiDark,
                fontSize = 17.sp,
                fontWeight = FontWeight.Bold
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = "Used ${route.timesUsed} times \u00B7 ${route.note}",
                color = TukiGray,
                fontSize = 13.sp
            )
        }

        Image(
            painter = painterResource(R.drawable.favorite),
            contentDescription = "Star",
            modifier = Modifier.size(22.dp)
        )
    }
}

@Composable
private fun TipBanner() {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiTeal, RoundedCornerShape(18.dp))
            .padding(20.dp)
    ) {
        Text(text = "Tip", color = Color.White, fontSize = 18.sp, fontWeight = FontWeight.Bold)
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = "Tap the star on any route to save it here",
            color = Color.White.copy(alpha = 0.85f),
            fontSize = 14.sp
        )
    }
}

private val sampleFavorites = listOf(
    FavoriteRoute(id = "1", origin = "Porac", destination = "Angeles", timesUsed = 14, note = "daily commute"),
    FavoriteRoute(id = "2", origin = "Angeles", destination = "Porac", timesUsed = 12, note = "return trip"),
    FavoriteRoute(id = "3", origin = "Sta. Rita", destination = "Guagua Town", timesUsed = 6, note = "weekend market run")
)
