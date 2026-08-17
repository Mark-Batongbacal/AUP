package com.example.frontend.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R

private val TukiTeal = Color(0xFF15919B)
private val TukiGray = Color(0xFF9AA6A9)

enum class TukiTab { HOME, RECENT, FAVORITES, PROFILE }

@Composable
fun BottomBar(
    selectedTab: TukiTab,
    onHomeClick: () -> Unit = {},
    onRecentClick: () -> Unit = {},
    onFavoritesClick: () -> Unit = {},
    onProfileClick: () -> Unit = {}
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(Color.White)
            .padding(horizontal = 24.dp, vertical = 14.dp)
    ) {
        BottomBarItem(
            iconRes = R.drawable.home,
            label = "Home",
            selected = selectedTab == TukiTab.HOME,
            onClick = onHomeClick,
            modifier = Modifier.weight(1f)
        )
        BottomBarItem(
            iconRes = R.drawable.recent,
            label = "Recent",
            selected = selectedTab == TukiTab.RECENT,
            onClick = onRecentClick,
            modifier = Modifier.weight(1f)
        )
        BottomBarItem(
            iconRes = R.drawable.favorite,
            label = "Favorites",
            selected = selectedTab == TukiTab.FAVORITES,
            onClick = onFavoritesClick,
            modifier = Modifier.weight(1f)
        )
        BottomBarItem(
            iconRes = R.drawable.profile,
            label = "Profile",
            selected = selectedTab == TukiTab.PROFILE,
            onClick = onProfileClick,
            modifier = Modifier.weight(1f)
        )
    }
}

@Composable
private fun BottomBarItem(
    iconRes: Int,
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val tint = if (selected) TukiTeal else TukiGray
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = modifier.clickable(onClick = onClick)
    ) {
        Icon(
            painter = painterResource(iconRes),
            contentDescription = label,
            tint = tint,
            modifier = Modifier.size(24.dp)
        )
        Spacer(modifier = Modifier.height(4.dp))
        Text(text = label, color = tint, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
    }
}
