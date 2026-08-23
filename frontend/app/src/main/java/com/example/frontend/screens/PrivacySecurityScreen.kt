package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.launch

import androidx.compose.material3.MaterialTheme
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiGold
import com.example.frontend.ui.theme.TukiGoldSurface

// Icon background tints
private val KeyBoxBg = Color(0xFFFCEAD8)
private val ShieldBoxBg = Color(0xFFE2F4F1)
private val DeleteBoxBg = Color(0xFFFDE8E8)

sealed interface DeleteAccountResult {
    data object Success : DeleteAccountResult
    data class Error(val message: String) : DeleteAccountResult
}

@Composable
fun PrivacySecurityScreen(
    lastPasswordChange: String = "Last changed 3 months ago",
    initial2FAEnabled: Boolean = false,
    onBack: () -> Unit = {},
    onChangePasswordClick: () -> Unit = {},
    on2FAToggle: (Boolean) -> Unit = {},
    onConfirmDeleteAccount: suspend () -> DeleteAccountResult = {
        DeleteAccountResult.Error("Account deletion isn't wired up yet.")
    },
    onAccountDeleted: () -> Unit = {}
) {
    var is2FAEnabled by remember { mutableStateOf(initial2FAEnabled) }
    var showDeleteDialog by remember { mutableStateOf(false) }
    var isDeleting by remember { mutableStateOf(false) }
    var deleteError by remember { mutableStateOf<String?>(null) }

    val coroutineScope = rememberCoroutineScope()

    fun confirmDelete() {
        if (isDeleting) return
        coroutineScope.launch {
            isDeleting = true
            deleteError = null
            when (val result = onConfirmDeleteAccount()) {
                is DeleteAccountResult.Success -> {
                    isDeleting = false
                    showDeleteDialog = false
                    onAccountDeleted()
                }
                is DeleteAccountResult.Error -> {
                    isDeleting = false
                    deleteError = result.message
                }
            }
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .navigationBarsPadding()
            .padding(horizontal = 24.dp, vertical = 20.dp)
    ) {
        // Header
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .background(TukiSky.copy(alpha = 0.35f), CircleShape)
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "\u2039",
                    color = TukiInk,
                    style = MaterialTheme.typography.displaySmall
                )
            }
            Spacer(modifier = Modifier.width(16.dp))
            Text(
                text = "Privacy & security",
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
        }

        Spacer(modifier = Modifier.height(28.dp))

        // PASSWORD SECTION
        SectionLabel(text = "PASSWORD")
        Spacer(modifier = Modifier.height(8.dp))
        SettingCard(
            iconText = "🔑",
            iconBgColor = TukiOrange.copy(alpha = 0.12f),
            title = "Change password",
            subtitle = lastPasswordChange,
            titleColor = TukiInk,
            onClick = onChangePasswordClick,
            trailingContent = {
                Text(text = "\u203A", color = TukiMuted, style = MaterialTheme.typography.titleLarge)
            }
        )

        Spacer(modifier = Modifier.height(24.dp))

        // SECURITY SECTION
        SectionLabel(text = "SECURITY")
        Spacer(modifier = Modifier.height(8.dp))
        SettingCard(
            iconText = "🛡️",
            iconBgColor = TukiTeal.copy(alpha = 0.12f),
            title = "Two-factor authentication",
            subtitle = "Add an extra layer of security",
            titleColor = TukiInk,
            onClick = {
                is2FAEnabled = !is2FAEnabled
                on2FAToggle(is2FAEnabled)
            },
            trailingContent = {
                Switch(
                    checked = is2FAEnabled,
                    onCheckedChange = {
                        is2FAEnabled = it
                        on2FAToggle(it)
                    },
                    colors = SwitchDefaults.colors(
                        checkedThumbColor = Color.White,
                        checkedTrackColor = TukiTeal,
                        uncheckedThumbColor = Color.White,
                        uncheckedTrackColor = Color(0xFFE0E0E0)
                    )
                )
            }
        )

        Spacer(modifier = Modifier.height(24.dp))

        // DATA SECTION
        SectionLabel(text = "DATA")
        Spacer(modifier = Modifier.height(8.dp))
        SettingCard(
            iconText = "🗑️",
            iconBgColor = TukiDanger.copy(alpha = 0.12f),
            title = "Delete account",
            subtitle = "Permanently remove your data",
            titleColor = TukiDanger,
            onClick = {
                deleteError = null
                showDeleteDialog = true
            },
            trailingContent = {
                Text(text = "\u203A", color = TukiMuted, style = MaterialTheme.typography.titleLarge)
            }
        )
    }

    if (showDeleteDialog) {
        AlertDialog(
            onDismissRequest = {
                if (!isDeleting) {
                    showDeleteDialog = false
                    deleteError = null
                }
            },
            title = {
                Text(text = "Delete your account?", color = TukiInk, style = MaterialTheme.typography.titleMedium)
            },
            text = {
                Column {
                    Text(
                        text = "This will permanently delete your account and all of your data, " +
                                "including trip history and favorites. This can't be undone.",
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodyMedium
                    )
                    deleteError?.let { message ->
                        Spacer(modifier = Modifier.height(12.dp))
                        Text(text = message, color = TukiDanger, style = MaterialTheme.typography.labelSmall)
                    }
                }
            },
            confirmButton = {
                TextButton(
                    onClick = { confirmDelete() },
                    enabled = !isDeleting
                ) {
                    if (isDeleting) {
                        CircularProgressIndicator(modifier = Modifier.size(16.dp), strokeWidth = 2.dp, color = TukiDanger)
                    } else {
                        Text(text = "Delete", color = TukiDanger, style = MaterialTheme.typography.labelLarge)
                    }
                }
            },
            dismissButton = {
                TextButton(
                    onClick = {
                        showDeleteDialog = false
                        deleteError = null
                    },
                    enabled = !isDeleting
                ) {
                    Text(text = "Cancel", color = TukiInk, style = MaterialTheme.typography.labelLarge)
                }
            },
            containerColor = TukiCream
        )
    }
}

@Composable
private fun SectionLabel(text: String) {
    Text(
        text = text,
        color = TukiMuted,
        style = MaterialTheme.typography.labelSmall,
        letterSpacing = 1.sp
    )
}

@Composable
private fun SettingCard(
    iconText: String,
    iconBgColor: Color,
    title: String,
    subtitle: String,
    titleColor: Color,
    onClick: () -> Unit,
    trailingContent: @Composable () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiSky.copy(alpha = 0.35f), RoundedCornerShape(18.dp))
            .clickable(onClick = onClick)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        // Icon Box
        Box(
            modifier = Modifier
                .size(44.dp)
                .background(iconBgColor, RoundedCornerShape(12.dp)),
            contentAlignment = Alignment.Center
        ) {
            Text(text = iconText, fontSize = 20.sp)
        }

        Spacer(modifier = Modifier.width(14.dp))

        // Titles
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                color = titleColor,
                style = MaterialTheme.typography.titleMedium
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = subtitle,
                color = TukiMuted,
                style = MaterialTheme.typography.bodySmall
            )
        }

        trailingContent()
    }
}
