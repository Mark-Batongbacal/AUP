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

private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiRed = Color(0xFFD64545)
private val TukiTeal = Color(0xFF15919B)
private val TukiError = Color(0xFFB00020)

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
                    .background(TukiCream2, CircleShape)
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "\u203A".let { "\u2039" },
                    color = TukiDark,
                    fontSize = 22.sp,
                    fontWeight = FontWeight.Bold
                )
            }
            Spacer(modifier = Modifier.width(16.dp))
            Text(
                text = "Privacy & security",
                color = TukiDark,
                fontSize = 24.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        Spacer(modifier = Modifier.height(28.dp))

        // PASSWORD SECTION
        SectionLabel(text = "PASSWORD")
        Spacer(modifier = Modifier.height(8.dp))
        SettingCard(
            iconText = "🔑",
            iconBgColor = KeyBoxBg,
            title = "Change password",
            subtitle = lastPasswordChange,
            titleColor = TukiDark,
            onClick = onChangePasswordClick,
            trailingContent = {
                Text(text = "\u203A", color = TukiGray, fontSize = 20.sp, fontWeight = FontWeight.Bold)
            }
        )

        Spacer(modifier = Modifier.height(24.dp))

        // SECURITY SECTION
        SectionLabel(text = "SECURITY")
        Spacer(modifier = Modifier.height(8.dp))
        SettingCard(
            iconText = "🛡️",
            iconBgColor = ShieldBoxBg,
            title = "Two-factor authentication",
            subtitle = "Add an extra layer of security",
            titleColor = TukiDark,
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
            iconBgColor = DeleteBoxBg,
            title = "Delete account",
            subtitle = "Permanently remove your data",
            titleColor = TukiRed,
            onClick = {
                deleteError = null
                showDeleteDialog = true
            },
            trailingContent = {
                Text(text = "\u203A", color = TukiGray, fontSize = 20.sp, fontWeight = FontWeight.Bold)
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
                Text(text = "Delete your account?", color = TukiDark, fontWeight = FontWeight.ExtraBold)
            },
            text = {
                Column {
                    Text(
                        text = "This will permanently delete your account and all of your data, " +
                                "including trip history and favorites. This can't be undone.",
                        color = TukiGray,
                        fontSize = 14.sp
                    )
                    deleteError?.let { message ->
                        Spacer(modifier = Modifier.height(12.dp))
                        Text(text = message, color = TukiError, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                    }
                }
            },
            confirmButton = {
                TextButton(
                    onClick = { confirmDelete() },
                    enabled = !isDeleting
                ) {
                    if (isDeleting) {
                        CircularProgressIndicator(modifier = Modifier.size(16.dp), strokeWidth = 2.dp, color = TukiRed)
                    } else {
                        Text(text = "Delete", color = TukiRed, fontWeight = FontWeight.Bold)
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
                    Text(text = "Cancel", color = TukiDark, fontWeight = FontWeight.SemiBold)
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
        color = TukiGray,
        fontSize = 12.sp,
        fontWeight = FontWeight.Bold
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
            .background(TukiCream2, RoundedCornerShape(18.dp))
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
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = subtitle,
                color = TukiGray,
                fontSize = 12.sp
            )
        }

        trailingContent()
    }
}