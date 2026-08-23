package com.example.frontend.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.Surface
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
import androidx.compose.material3.MaterialTheme
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiSurfaceRaised

enum class TukiTab { HOME, RECENT, FAVORITES, PROFILE }

@Composable
fun BottomBar(
    selectedTab: TukiTab,
    onHomeClick: () -> Unit = {},
    onRecentClick: () -> Unit = {},
    onFavoritesClick: () -> Unit = {},
    onProfileClick: () -> Unit = {}
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp),
        color = TukiSurfaceRaised,
        shadowElevation = 8.dp
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 13.dp)
        ) {
            BottomBarItem(R.drawable.home, "Home", selectedTab == TukiTab.HOME, onHomeClick, Modifier.weight(1f))
            BottomBarItem(R.drawable.recent, "Recent", selectedTab == TukiTab.RECENT, onRecentClick, Modifier.weight(1f))
            BottomBarItem(R.drawable.favorite, "Favorites", selectedTab == TukiTab.FAVORITES, onFavoritesClick, Modifier.weight(1f))
            BottomBarItem(R.drawable.profile, "Profile", selectedTab == TukiTab.PROFILE, onProfileClick, Modifier.weight(1f))
        }
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
    val tint = if (selected) TukiTeal else TukiMuted
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = modifier.clickable(onClick = onClick).padding(vertical = 2.dp)
    ) {
        Icon(
            painter = painterResource(iconRes),
            contentDescription = label,
            tint = tint,
            modifier = Modifier.size(23.dp)
        )
        Spacer(Modifier.height(4.dp))
        Text(label, color = tint, style = MaterialTheme.typography.labelSmall)
    }
}
