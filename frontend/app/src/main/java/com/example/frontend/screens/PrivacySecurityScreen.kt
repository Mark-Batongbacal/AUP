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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import java.time.Duration
import java.time.Instant
import kotlinx.coroutines.launch

private val PrivacyIconInk = Color(0xFF153E4B)
private val PrivacyIconSurface = Color(0xFFFFF0D5)

sealed interface DeleteAccountResult {
    data object Success : DeleteAccountResult
    data class Error(val message: String) : DeleteAccountResult
}

@Composable
fun PrivacySecurityScreen(
    initial2FAEnabled: Boolean = false,
    onBack: () -> Unit = {},
    onChangePasswordClick: () -> Unit = {},
    on2FAToggle: (Boolean) -> Unit = {},
    onConfirmDeleteAccount: suspend () -> DeleteAccountResult = {
        DeleteAccountResult.Error("Account deletion isn't wired up yet.")
    },
    onAccountDeleted: () -> Unit = {}
) {
    val context = LocalContext.current
    val dataProvider = remember(context) { TukiDataProvider(context.applicationContext) }
    var is2FAEnabled by remember { mutableStateOf(initial2FAEnabled) }
    var showDeleteDialog by remember { mutableStateOf(false) }
    var isDeleting by remember { mutableStateOf(false) }
    var deleteError by remember { mutableStateOf<String?>(null) }
    var passwordMetadataLoaded by remember { mutableStateOf(false) }
    var lastPasswordChangedAt by remember { mutableStateOf<String?>(null) }
    val coroutineScope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        when (val result = dataProvider.userRepository.getCurrentUser()) {
            is ApiResult.Success -> lastPasswordChangedAt = result.data.lastPasswordChangedAt
            is ApiResult.Failure -> lastPasswordChangedAt = null
        }
        passwordMetadataLoaded = true
    }

    val lastPasswordChange = when {
        !passwordMetadataLoaded -> "Checking password activity…"
        lastPasswordChangedAt == null -> "No local password change recorded"
        else -> lastPasswordChangeLabel(lastPasswordChangedAt)
    }

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
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .background(TukiSurfaceRaised, RoundedCornerShape(12.dp))
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text("‹", color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
            Spacer(modifier = Modifier.width(16.dp))
            Text("Privacy & security", color = TukiInk, style = MaterialTheme.typography.displaySmall)
        }

        Spacer(modifier = Modifier.height(28.dp))
        SectionLabel("PASSWORD")
        Spacer(modifier = Modifier.height(8.dp))
        SettingCard(
            iconText = "🔑",
            title = "Change password",
            subtitle = lastPasswordChange,
            titleColor = TukiInk,
            onClick = onChangePasswordClick,
            trailingContent = { Text("›", color = TukiMuted, style = MaterialTheme.typography.titleLarge) }
        )

        Spacer(modifier = Modifier.height(24.dp))
        SectionLabel("SECURITY")
        Spacer(modifier = Modifier.height(8.dp))
        SettingCard(
            iconText = "⛨",
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
                        uncheckedTrackColor = TukiMuted.copy(alpha = 0.35f)
                    )
                )
            }
        )

        Spacer(modifier = Modifier.height(24.dp))
        SectionLabel("DATA")
        Spacer(modifier = Modifier.height(8.dp))
        SettingCard(
            iconText = "♲",
            title = "Delete account",
            subtitle = "Permanently remove your data",
            titleColor = TukiDanger,
            onClick = {
                deleteError = null
                showDeleteDialog = true
            },
            trailingContent = { Text("›", color = TukiMuted, style = MaterialTheme.typography.titleLarge) }
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
            title = { Text("Delete your account?", color = TukiInk, style = MaterialTheme.typography.titleMedium) },
            text = {
                Column {
                    Text(
                        "This will permanently delete your account and all of your data, including trip history and favorites. This can't be undone.",
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodyMedium
                    )
                    deleteError?.let { message ->
                        Spacer(modifier = Modifier.height(12.dp))
                        Text(message, color = TukiDanger, style = MaterialTheme.typography.labelSmall)
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { confirmDelete() }, enabled = !isDeleting) {
                    if (isDeleting) {
                        CircularProgressIndicator(modifier = Modifier.size(16.dp), strokeWidth = 2.dp, color = TukiDanger)
                    } else {
                        Text("Delete", color = TukiDanger, style = MaterialTheme.typography.labelLarge)
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
                    Text("Cancel", color = TukiInk, style = MaterialTheme.typography.labelLarge)
                }
            },
            containerColor = TukiSurfaceRaised
        )
    }
}

internal fun lastPasswordChangeLabel(
    timestamp: String?,
    now: Instant = Instant.now()
): String {
    val changedAt = timestamp
        ?.let { runCatching { Instant.parse(it) }.getOrNull() }
        ?: return "Last change unavailable"
    val elapsed = Duration.between(changedAt, now)
    if (elapsed.isNegative || elapsed.seconds < 60) return "Last changed just now"

    val minutes = elapsed.toMinutes()
    if (minutes < 60) return "Last changed $minutes ${if (minutes == 1L) "minute" else "minutes"} ago"

    val hours = elapsed.toHours()
    if (hours < 24) return "Last changed $hours ${if (hours == 1L) "hour" else "hours"} ago"

    val days = elapsed.toDays()
    if (days < 30) return "Last changed $days ${if (days == 1L) "day" else "days"} ago"

    if (days < 365) {
        val months = (days / 30).coerceAtLeast(1)
        return "Last changed $months ${if (months == 1L) "month" else "months"} ago"
    }

    val years = (days / 365).coerceAtLeast(1)
    return "Last changed $years ${if (years == 1L) "year" else "years"} ago"
}

@Composable
private fun SectionLabel(text: String) {
    Text(text, color = TukiMuted, style = MaterialTheme.typography.labelSmall, letterSpacing = 1.sp)
}

@Composable
private fun SettingCard(
    iconText: String,
    title: String,
    subtitle: String,
    titleColor: Color,
    onClick: () -> Unit,
    trailingContent: @Composable () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiSurfaceRaised, RoundedCornerShape(18.dp))
            .clickable(onClick = onClick)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(44.dp)
                .background(PrivacyIconSurface, RoundedCornerShape(12.dp)),
            contentAlignment = Alignment.Center
        ) {
            Text(iconText, color = PrivacyIconInk, fontSize = 20.sp, fontWeight = FontWeight.Bold)
        }

        Spacer(modifier = Modifier.width(14.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(title, color = titleColor, style = MaterialTheme.typography.titleMedium)
            Spacer(modifier = Modifier.height(2.dp))
            Text(subtitle, color = TukiMuted, style = MaterialTheme.typography.bodySmall)
        }
        trailingContent()
    }
}
