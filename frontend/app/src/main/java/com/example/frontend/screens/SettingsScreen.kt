package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.ui.theme.AppearancePreferences
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiThemeRuntime

@Composable
fun SettingsScreen(
    userName: String = "TUKI User",
    userEmail: String = "",
    tripsTaken: Int = 0,
    favoritesCount: Int = 0,
    onBack: () -> Unit = {},
    onLogoutClick: () -> Unit = {}
) {
    val context = LocalContext.current
    val isDarkMode = TukiThemeRuntime.darkMode

    LazyColumn(
        modifier = Modifier.fillMaxSize().background(TukiCream).statusBarsPadding(),
        contentPadding = PaddingValues(start = 24.dp, end = 24.dp, top = 12.dp, bottom = 32.dp)
    ) {
        item {
            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier.size(42.dp).clickable(onClick = onBack),
                    contentAlignment = Alignment.Center
                ) {
                    Text("←", color = TukiInk, fontSize = 25.sp, fontWeight = FontWeight.Bold)
                }
                Spacer(Modifier.width(8.dp))
                Text(TukiInterfaceText.settings, color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
            Spacer(Modifier.height(28.dp))
        }

        item {
            SettingsSectionLabel(TukiInterfaceText.appearance)
            Spacer(Modifier.height(10.dp))
            Surface(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(18.dp),
                color = TukiSurfaceRaised,
                shadowElevation = if (isDarkMode) 0.dp else 2.dp
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Box(
                        modifier = Modifier
                            .size(46.dp)
                            .background(Color(0xFFFFF0D5), RoundedCornerShape(14.dp)),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(if (isDarkMode) "☾" else "☀", color = TukiInk, fontSize = 22.sp)
                    }
                    Spacer(Modifier.width(14.dp))
                    Column(Modifier.weight(1f)) {
                        Text(
                            TukiInterfaceText.darkMode,
                            color = TukiInk,
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(Modifier.height(2.dp))
                        Text(
                            TukiInterfaceText.darkModeSubtitle,
                            color = TukiMuted,
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                    Switch(
                        checked = isDarkMode,
                        onCheckedChange = { enabled ->
                            AppearancePreferences.setDarkMode(context.applicationContext, enabled)
                            TukiThemeRuntime.darkMode = enabled
                        },
                        colors = SwitchDefaults.colors(
                            checkedThumbColor = Color.White,
                            checkedTrackColor = TukiTeal,
                            uncheckedThumbColor = Color.White,
                            uncheckedTrackColor = TukiMuted.copy(alpha = 0.45f)
                        )
                    )
                }
            }
            Spacer(Modifier.height(34.dp))
        }

        item {
            SettingsSectionLabel(TukiInterfaceText.support)
            Spacer(Modifier.height(10.dp))
            Surface(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(18.dp),
                color = TukiSurfaceRaised,
                shadowElevation = if (isDarkMode) 0.dp else 2.dp
            ) {
                Column {
                    SettingsActionRow(
                        TukiInterfaceText.helpCenter,
                        if (TukiInterfaceText.isFilipino) "Mga FAQ at gabay" else "FAQs and guides",
                        "?"
                    )
                    SettingsDivider()
                    SettingsActionRow(
                        TukiInterfaceText.sendFeedback,
                        if (TukiInterfaceText.isFilipino) "Tulungan kaming mapahusay ang TUKI" else "Help us improve TUKI",
                        "✉"
                    )
                    SettingsDivider()
                    SettingsActionRow(TukiInterfaceText.aboutTuki, "Version 1.0.0", "i")
                }
            }
            Spacer(Modifier.height(120.dp))
        }

        item {
            Button(
                onClick = onLogoutClick,
                modifier = Modifier.fillMaxWidth().height(54.dp),
                shape = RoundedCornerShape(18.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = if (isDarkMode) TukiDanger.copy(alpha = 0.16f) else TukiDanger.copy(alpha = 0.10f),
                    contentColor = TukiDanger
                ),
                elevation = ButtonDefaults.buttonElevation(defaultElevation = 0.dp)
            ) {
                Text(TukiInterfaceText.logOut, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun SettingsSectionLabel(text: String) {
    Text(
        text = text,
        color = TukiInk,
        style = MaterialTheme.typography.labelSmall,
        letterSpacing = 1.sp,
        fontWeight = FontWeight.Bold
    )
}

@Composable
private fun SettingsActionRow(
    title: String,
    subtitle: String,
    icon: String,
    onClick: () -> Unit = {}
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 15.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(44.dp)
                .background(Color(0xFFFFF0D5), RoundedCornerShape(13.dp)),
            contentAlignment = Alignment.Center
        ) {
            Text(icon, color = TukiInk, fontSize = 18.sp)
        }
        Spacer(Modifier.width(14.dp))
        Column(Modifier.weight(1f)) {
            Text(title, color = TukiInk, style = MaterialTheme.typography.titleMedium)
            Spacer(Modifier.height(2.dp))
            Text(subtitle, color = TukiMuted, style = MaterialTheme.typography.bodySmall)
        }
        Text("›", color = TukiMuted, fontSize = 22.sp)
    }
}

@Composable
private fun SettingsDivider() {
    Box(
        Modifier
            .fillMaxWidth()
            .padding(start = 74.dp)
            .height(1.dp)
            .background(TukiMuted.copy(alpha = 0.12f))
    )
}
