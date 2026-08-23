package com.example.frontend.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
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
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.users.UpdateUserProfileRequest
import com.example.frontend.data.users.UserProfileDto
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiTeal
import java.time.Duration
import java.time.Instant
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

data class ProfileStat(val value: String, val label: String)

data class ProfileAccountRow(
    val iconRes: Int,
    val title: String,
    val subtitle: String,
    val onClick: () -> Unit
)

private enum class ProfilePage {
    OVERVIEW,
    EDIT_PROFILE,
    PRIVACY_SECURITY,
    CHANGE_PASSWORD,
    LANGUAGE
}

@Composable
fun ProfileScreen(
    userName: String = "Juan Dela Cruz",
    userEmail: String = "juan.delacruz@gmail.com",
    tripsTaken: Int = 18,
    favoritesCount: Int = 2,
    onBack: () -> Unit = {},
    onEditProfileClick: () -> Unit = {},
    onPrivacySecurityClick: () -> Unit = {},
    onLanguageClick: () -> Unit = {},
    onLogoutClick: () -> Unit = {},
    onHomeClick: () -> Unit = {},
    onRecentClick: () -> Unit = {},
    onFavoritesClick: () -> Unit = {}
) {
    val context = LocalContext.current
    val dataProvider = remember { TukiDataProvider(context.applicationContext) }
    val scope = rememberCoroutineScope()

    var page by remember { mutableStateOf(ProfilePage.OVERVIEW) }
    var loadedProfile by remember { mutableStateOf<UserProfileDto?>(null) }
    var guestClockTick by remember { mutableIntStateOf(0) }

    LaunchedEffect(Unit) {
        when (val result = dataProvider.userRepository.getCurrentUser()) {
            is ApiResult.Success -> loadedProfile = result.data
            is ApiResult.Failure -> Unit
        }
    }

    val isGuest = loadedProfile?.role.equals("Guest", ignoreCase = true) ||
        userName.equals("Guest", ignoreCase = true)

    LaunchedEffect(isGuest) {
        if (!isGuest) return@LaunchedEffect
        while (true) {
            delay(60_000)
            guestClockTick++
        }
    }

    LaunchedEffect(isGuest, page) {
        if (isGuest && page in setOf(
                ProfilePage.EDIT_PROFILE,
                ProfilePage.PRIVACY_SECURITY,
                ProfilePage.CHANGE_PASSWORD
            )
        ) {
            page = ProfilePage.OVERVIEW
        }
    }

    val displayName = loadedProfile?.let { profile ->
        listOfNotNull(
            profile.firstName?.trim()?.takeIf { it.isNotEmpty() },
            profile.lastName?.trim()?.takeIf { it.isNotEmpty() }
        ).joinToString(" ")
    }?.takeIf { it.isNotBlank() } ?: userName

    val displayEmail = if (isGuest) {
        "Temporary guest account"
    } else {
        loadedProfile?.email?.takeIf { it.isNotBlank() } ?: userEmail
    }
    val displayPhone = loadedProfile?.phoneNumber.orEmpty()
    val currentLanguage = when (loadedProfile?.preferredLanguage?.trim()?.lowercase()) {
        "filipino", "tagalog" -> LanguageOption.FILIPINO
        else -> LanguageOption.ENGLISH
    }
    val guestRemaining = remember(isGuest, guestClockTick) {
        if (isGuest) guestRemainingText(dataProvider.sessionStore.validSession()?.expiresAt) else null
    }

    when (page) {
        ProfilePage.EDIT_PROFILE -> {
            EditProfileScreen(
                initialFullName = displayName,
                initialEmail = displayEmail,
                initialPhone = displayPhone,
                onBack = { page = ProfilePage.OVERVIEW },
                onSaveChanges = { fullName, phone ->
                    val parts = fullName.trim().split(Regex("\\s+"), limit = 2)
                    when (
                        val result = dataProvider.userRepository.updateCurrentUser(
                            UpdateUserProfileRequest(
                                firstName = parts.firstOrNull().orEmpty(),
                                lastName = parts.getOrNull(1).orEmpty(),
                                phoneNumber = phone
                            )
                        )
                    ) {
                        is ApiResult.Success -> EditProfileResult.Success(result.data)
                        is ApiResult.Failure -> EditProfileResult.Error(result.message)
                    }
                },
                onSaved = { profile ->
                    loadedProfile = profile
                    page = ProfilePage.OVERVIEW
                }
            )
            return
        }

        ProfilePage.PRIVACY_SECURITY -> {
            PrivacySecurityScreen(
                onBack = { page = ProfilePage.OVERVIEW },
                onChangePasswordClick = { page = ProfilePage.CHANGE_PASSWORD },
                on2FAToggle = { _ -> },
                onConfirmDeleteAccount = {
                    when (val result = dataProvider.userRepository.deleteCurrentUser()) {
                        is ApiResult.Success -> DeleteAccountResult.Success
                        is ApiResult.Failure -> DeleteAccountResult.Error(
                            result.message.ifBlank { "Couldn't delete your account. Please try again." }
                        )
                    }
                },
                onAccountDeleted = {
                    dataProvider.authRepository.logoutLocalSession()
                    loadedProfile = null
                    onLogoutClick()
                }
            )
            return
        }

        ProfilePage.CHANGE_PASSWORD -> {
            ChangePasswordScreen(
                onBack = { page = ProfilePage.PRIVACY_SECURITY },
                onRequestOtp = { currentPassword ->
                    when (val result = dataProvider.authRepository.requestChangePasswordOtp(currentPassword)) {
                        is ApiResult.Success -> ChangePasswordResult.Success
                        is ApiResult.Failure -> ChangePasswordResult.Error(
                            result.message.ifBlank { "Couldn't send the password change OTP." }
                        )
                    }
                },
                onChangePassword = { currentPassword, code, newPassword ->
                    when (
                        val result = dataProvider.authRepository.changePassword(
                            currentPassword,
                            code,
                            newPassword
                        )
                    ) {
                        is ApiResult.Success -> ChangePasswordResult.Success
                        is ApiResult.Failure -> ChangePasswordResult.Error(
                            result.message.ifBlank { "The password or OTP is invalid." }
                        )
                    }
                },
                onPasswordChanged = { page = ProfilePage.PRIVACY_SECURITY }
            )
            return
        }

        ProfilePage.LANGUAGE -> {
            LanguageScreen(
                initialLanguage = currentLanguage,
                onBack = { page = ProfilePage.OVERVIEW },
                onSaveLanguage = { selectedLanguage ->
                    scope.launch {
                        when (
                            val result = dataProvider.userRepository.updateCurrentUser(
                                UpdateUserProfileRequest(
                                    preferredLanguage = selectedLanguage.title
                                )
                            )
                        ) {
                            is ApiResult.Success -> {
                                loadedProfile = result.data
                                page = ProfilePage.OVERVIEW
                            }
                            is ApiResult.Failure -> Unit
                        }
                    }
                }
            )
            return
        }

        ProfilePage.OVERVIEW -> Unit
    }

    val initials = remember(displayName) {
        displayName.split(" ")
            .mapNotNull { it.firstOrNull()?.uppercaseChar() }
            .take(2)
            .joinToString("")
    }

    val accountRows = buildList {
        if (!isGuest) {
            add(
                ProfileAccountRow(
                    R.drawable.edit_profile,
                    "Edit Profile",
                    "Name, email, phone",
                    { page = ProfilePage.EDIT_PROFILE }
                )
            )
            add(
                ProfileAccountRow(
                    R.drawable.privacy,
                    "Privacy & Security",
                    "Password, data settings",
                    { page = ProfilePage.PRIVACY_SECURITY }
                )
            )
        }
        add(
            ProfileAccountRow(
                R.drawable.language,
                "Language",
                currentLanguage.title,
                { page = ProfilePage.LANGUAGE }
            )
        )
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
    ) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(horizontal = 30.dp),
            contentPadding = androidx.compose.foundation.layout.PaddingValues(top = 12.dp, bottom = 20.dp)
        ) {
            item {
                Column(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Box(
                        modifier = Modifier
                            .size(90.dp)
                            .background(TukiTeal, CircleShape),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = initials,
                            color = Color.White,
                            style = MaterialTheme.typography.displayMedium
                        )
                    }

                    Spacer(modifier = Modifier.height(14.dp))
                    Text(
                        text = displayName,
                        color = TukiInk,
                        style = MaterialTheme.typography.displaySmall
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(text = displayEmail, color = TukiMuted, style = MaterialTheme.typography.bodyLarge)
                    Spacer(modifier = Modifier.height(18.dp))
                }
            }

            if (isGuest) {
                item {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .background(TukiOrange.copy(alpha = 0.12f), RoundedCornerShape(14.dp))
                            .padding(16.dp)
                    ) {
                        Text(
                            text = "Guest Mode · ${guestRemaining ?: "24-hour access"}",
                            color = TukiInk,
                            style = MaterialTheme.typography.titleMedium
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = "Your guest access is temporary. Sign up for an account if you want to use TUKI without the guest time limit.",
                            color = TukiMuted,
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                    Spacer(modifier = Modifier.height(18.dp))
                }
            }

            item {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    ProfileStatCard(ProfileStat(tripsTaken.toString(), "TRIPS TAKEN"), Modifier.weight(1f))
                    ProfileStatCard(ProfileStat(favoritesCount.toString(), "FAVORITES"), Modifier.weight(1f))
                }
                Spacer(modifier = Modifier.height(28.dp))
            }

            item {
                Text(
                    text = "ACCOUNT",
                    color = TukiInk,
                    style = MaterialTheme.typography.labelSmall,
                    letterSpacing = 1.sp
                )
                Spacer(modifier = Modifier.height(12.dp))
            }

            items(accountRows) { row ->
                AccountRowItem(row)
                Spacer(modifier = Modifier.height(12.dp))
            }

            item {
                Text(
                    text = "Log out",
                    color = Color(0xFFB00020),
                    style = MaterialTheme.typography.labelLarge,
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(TukiSky.copy(alpha = 0.3f), RoundedCornerShape(14.dp))
                        .clickable(onClick = onLogoutClick)
                        .padding(16.dp)
                )
            }
        }

        BottomBar(
            selectedTab = TukiTab.PROFILE,
            onHomeClick = onHomeClick,
            onRecentClick = onRecentClick,
            onFavoritesClick = onFavoritesClick,
            onProfileClick = {}
        )
    }
}

private fun guestRemainingText(expiresAt: String?): String {
    val expiration = expiresAt?.let { value -> runCatching { Instant.parse(value) }.getOrNull() }
        ?: return "24-hour access"
    val remaining = Duration.between(Instant.now(), expiration)
    if (remaining.isZero || remaining.isNegative) return "expired"

    val totalMinutes = remaining.toMinutes().coerceAtLeast(1)
    val hours = totalMinutes / 60
    val minutes = totalMinutes % 60
    return if (hours > 0) "${hours}h ${minutes}m remaining" else "${minutes}m remaining"
}

@Composable
private fun ProfileStatCard(stat: ProfileStat, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .background(TukiSky.copy(alpha = 0.3f), RoundedCornerShape(14.dp))
            .padding(vertical = 16.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = stat.value,
            color = TukiInk,
            style = MaterialTheme.typography.titleLarge
        )
        Spacer(modifier = Modifier.height(2.dp))
        Text(
            text = stat.label,
            color = TukiMuted,
            style = MaterialTheme.typography.labelSmall
        )
    }
}

@Composable
private fun AccountRowItem(row: ProfileAccountRow) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiSky.copy(alpha = 0.3f), RoundedCornerShape(14.dp))
            .clickable(onClick = row.onClick)
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier.size(40.dp),
            contentAlignment = Alignment.Center
        ) {
            Image(
                painter = painterResource(row.iconRes),
                contentDescription = row.title,
                modifier = Modifier.size(40.dp)
            )
        }

        Spacer(modifier = Modifier.width(14.dp))

        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = row.title,
                color = TukiInk,
                style = MaterialTheme.typography.titleMedium
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(text = row.subtitle, color = TukiMuted, style = MaterialTheme.typography.bodySmall)
        }

        Text(
            text = "\u203A",
            color = TukiMuted,
            style = MaterialTheme.typography.titleLarge
        )
    }
}
