package com.example.frontend.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.example.frontend.LocalTukiDataProvider
import com.example.frontend.R
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.screens.ContributionsHost
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal

enum class TukiTab { HOME, RECENT, CONTRIBUTIONS, FAVORITES, PROFILE }

@Composable
fun BottomBar(
    selectedTab: TukiTab,
    onHomeClick: () -> Unit = {},
    onRecentClick: () -> Unit = {},
    onContributionsClick: (() -> Unit)? = null,
    onFavoritesClick: () -> Unit = {},
    onProfileClick: () -> Unit = {}
) {
    var showContributions by remember { mutableStateOf(false) }
    val dataProvider = LocalTukiDataProvider.current

    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp),
        color = TukiSurfaceRaised,
        shadowElevation = 8.dp
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .navigationBarsPadding()
                .height(64.dp)
                .padding(horizontal = 4.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            BottomBarItem(
                R.drawable.home,
                TukiInterfaceText.home,
                selectedTab == TukiTab.HOME,
                onHomeClick,
                Modifier.weight(1f)
            )
            BottomBarItem(
                R.drawable.recent,
                TukiInterfaceText.recent,
                selectedTab == TukiTab.RECENT,
                onRecentClick,
                Modifier.weight(1f)
            )
            BottomBarItem(
                R.drawable.ic_contributions,
                "Contribute",
                selectedTab == TukiTab.CONTRIBUTIONS,
                {
                    if (onContributionsClick != null) {
                        onContributionsClick()
                    } else if (dataProvider != null) {
                        showContributions = true
                    }
                },
                Modifier.weight(1f)
            )
            BottomBarItem(
                R.drawable.favorite,
                TukiInterfaceText.favorites,
                selectedTab == TukiTab.FAVORITES,
                onFavoritesClick,
                Modifier.weight(1f)
            )
            BottomBarItem(
                R.drawable.profile,
                TukiInterfaceText.profile,
                selectedTab == TukiTab.PROFILE,
                onProfileClick,
                Modifier.weight(1f)
            )
        }
    }

    if (showContributions && dataProvider != null) {
        Dialog(
            onDismissRequest = { showContributions = false },
            properties = DialogProperties(
                usePlatformDefaultWidth = false,
                decorFitsSystemWindows = false
            )
        ) {
            Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
                ContributionsHost(
                    dataProvider = dataProvider,
                    onDismiss = { showContributions = false },
                    onHomeClick = onHomeClick,
                    onRecentClick = onRecentClick,
                    onFavoritesClick = onFavoritesClick,
                    onProfileClick = onProfileClick
                )
            }
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
        modifier = modifier
            .fillMaxHeight()
            .clickable(onClick = onClick)
            .padding(horizontal = 2.dp, vertical = 7.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Icon(
            painter = painterResource(iconRes),
            contentDescription = label,
            tint = tint,
            modifier = Modifier.size(22.dp)
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = label,
            color = tint,
            style = MaterialTheme.typography.labelSmall,
            maxLines = 1,
            textAlign = TextAlign.Center,
            modifier = Modifier.fillMaxWidth()
        )
    }
}
