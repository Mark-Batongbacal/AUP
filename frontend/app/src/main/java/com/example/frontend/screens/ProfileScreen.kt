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
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.users.UpdateUserProfileRequest
import com.example.frontend.data.users.UserProfileDto

private val TukiTeal = Color(0xFF15919B)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

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

    var page by remember { mutableStateOf(ProfilePage.OVERVIEW) }
    var loadedProfile by remember { mutableStateOf<UserProfileDto?>(null) }

    LaunchedEffect(Unit) {
        when (val result = dataProvider.userRepository.getCurrentUser()) {
            is ApiResult.Success -> loadedProfile = result.data
            is ApiResult.Failure -> Unit
        }
    }

    val displayName = loadedProfile?.let { profile ->
        listOfNotNull(
            profile.firstName?.trim()?.takeIf { it.isNotEmpty() },
            profile.lastName?.trim()?.takeIf { it.isNotEmpty() }
        ).joinToString(" ")
    }?.takeIf { it.isNotBlank() } ?: userName

    val displayEmail = loadedProfile?.email?.takeIf { it.isNotBlank() } ?: userEmail
    val displayPhone = loadedProfile?.phoneNumber.orEmpty()

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
                onBack = { page = ProfilePage.OVERVIEW },
                onSaveLanguage = { page = ProfilePage.OVERVIEW }
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

    val accountRows = listOf(
        ProfileAccountRow(
            R.drawable.edit_profile,
            "Edit Profile",
            "Name, email, phone",
            { page = ProfilePage.EDIT_PROFILE }
        ),
        ProfileAccountRow(
            R.drawable.privacy,
            "Privacy & Security",
            "Password, data settings",
            { page = ProfilePage.PRIVACY_SECURITY }
        ),
        ProfileAccountRow(
            R.drawable.language,
            "Language",
            "English",
            { page = ProfilePage.LANGUAGE }
        )
    )

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .navigationBarsPadding()
    ) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 30.dp),
            contentPadding = androidx.compose.foundation.layout.PaddingValues(top = 30.dp, bottom = 20.dp)
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
                            fontSize = 30.sp,
                            fontWeight = FontWeight.ExtraBold
                        )
                    }

                    Spacer(modifier = Modifier.height(14.dp))
                    Text(
                        text = displayName,
                        color = TukiDark,
                        fontSize = 21.sp,
                        fontWeight = FontWeight.ExtraBold
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(text = displayEmail, color = TukiGray, fontSize = 15.sp)
                    Spacer(modifier = Modifier.height(24.dp))
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
                    color = TukiDark,
                    fontSize = 14.sp,
                    fontWeight = FontWeight.ExtraBold
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
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(TukiCream2, RoundedCornerShape(14.dp))
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

@Composable
private fun ProfileStatCard(stat: ProfileStat, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .background(TukiCream2, RoundedCornerShape(14.dp))
            .padding(vertical = 16.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = stat.value,
            color = TukiDark,
            fontSize = 22.sp,
            fontWeight = FontWeight.ExtraBold
        )
        Spacer(modifier = Modifier.height(2.dp))
        Text(
            text = stat.label,
            color = TukiGray,
            fontSize = 11.sp,
            fontWeight = FontWeight.SemiBold
        )
    }
}

@Composable
private fun AccountRowItem(row: ProfileAccountRow) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiCream2, RoundedCornerShape(14.dp))
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
                color = TukiDark,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(text = row.subtitle, color = TukiGray, fontSize = 13.sp)
        }

        Text(
            text = "\u203A",
            color = TukiGray,
            fontSize = 20.sp,
            fontWeight = FontWeight.Bold
        )
    }
}
