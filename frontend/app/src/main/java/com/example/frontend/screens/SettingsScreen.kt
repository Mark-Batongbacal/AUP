package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

private val TukiTeal = Color(0xFF15919B)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiOrange = Color(0xFFFF9318)

@Composable
fun SettingsScreen(
    onBack: () -> Unit = {},
    onLogoutClick: () -> Unit = {}
) {
    var showChangePassword by remember { mutableStateOf(false) }

    if (showChangePassword) {
        ChangePasswordScreen(
            onBack = { showChangePassword = false },
            onPasswordChanged = { showChangePassword = false }
        )
        return
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 30.dp, vertical = 30.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiCream2, RoundedCornerShape(12.dp))
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(text = "\u2039", color = TukiDark, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            }
            Spacer(modifier = Modifier.width(14.dp))
            Text(
                text = "Settings",
                color = TukiDark,
                fontSize = 22.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        LazyColumn(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 30.dp),
            verticalArrangement = Arrangement.spacedBy(20.dp),
            contentPadding = PaddingValues(bottom = 40.dp)
        ) {
            item {
                SettingsSection(title = "ACCOUNT") {
                    SettingsRow(
                        title = "Change password",
                        subtitle = "Confirm changes with an email OTP",
                        onClick = { showChangePassword = true }
                    )
                }
            }

            item {
                SettingsSection(title = "GENERAL") {
                    SettingsRow(title = "Notifications", subtitle = "Manage alerts & sounds")
                    SettingsRow(title = "Dark Mode", subtitle = "On / Off")
                    SettingsRow(title = "Language", subtitle = "English")
                }
            }

            item {
                SettingsSection(title = "SUPPORT") {
                    SettingsRow(title = "Help Center", subtitle = "FAQs and guides")
                    SettingsRow(title = "Report a Problem", subtitle = "Tell us what's wrong")
                }
            }

            item {
                SettingsSection(title = "ABOUT") {
                    SettingsRow(title = "Privacy Policy", subtitle = "Data usage & safety")
                    SettingsRow(title = "Terms of Service", subtitle = "Usage rules")
                    SettingsRow(title = "App Version", subtitle = "1.0.0 (Beta)", hasChevron = false)
                }
            }

            item {
                Spacer(modifier = Modifier.height(10.dp))
                Button(
                    onClick = onLogoutClick,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(56.dp),
                    shape = RoundedCornerShape(16.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Color.White,
                        contentColor = Color.Red
                    ),
                    elevation = ButtonDefaults.buttonElevation(defaultElevation = 0.dp)
                ) {
                    Text(
                        text = "Log Out",
                        fontSize = 17.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }
        }
    }
}

@Composable
private fun SettingsSection(
    title: String,
    content: @Composable ColumnScope.() -> Unit
) {
    Column {
        Text(
            text = title,
            color = TukiGray,
            fontSize = 13.sp,
            fontWeight = FontWeight.ExtraBold,
            letterSpacing = 1.sp
        )
        Spacer(modifier = Modifier.height(12.dp))
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .background(TukiCream2, RoundedCornerShape(18.dp))
                .padding(vertical = 4.dp)
        ) {
            content()
        }
    }
}

@Composable
private fun SettingsRow(
    title: String,
    subtitle: String,
    hasChevron: Boolean = true,
    onClick: () -> Unit = {}
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 20.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                color = TukiDark,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = subtitle,
                color = TukiGray,
                fontSize = 13.sp
            )
        }
        if (hasChevron) {
            Text(
                text = "\u203A",
                color = TukiGray,
                fontSize = 20.sp,
                fontWeight = FontWeight.Bold
            )
        }
    }
}
