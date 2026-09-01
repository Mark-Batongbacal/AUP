package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.contributions.TricyclePointSubmissionDto
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter

private enum class LocationContributionPage { OVERVIEW, SUBMIT_TRICYCLE, MY_CONTRIBUTIONS }

@Composable
fun LocationAwareContributionsHost(
    dataProvider: TukiDataProvider,
    onDismiss: () -> Unit,
    onHomeClick: () -> Unit,
    onRecentClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onProfileClick: () -> Unit
) {
    var page by remember { mutableStateOf(LocationContributionPage.OVERVIEW) }
    var isGuest by remember { mutableStateOf(false) }
    var profileLoaded by remember { mutableStateOf(false) }

    LaunchedEffect(dataProvider) {
        isGuest = when (val result = dataProvider.userRepository.getCurrentUser()) {
            is ApiResult.Success -> result.data.role.equals("Guest", ignoreCase = true)
            is ApiResult.Failure -> false
        }
        profileLoaded = true
    }

    when (page) {
        LocationContributionPage.OVERVIEW -> LocationContributionsOverviewScreen(
            isGuest = isGuest,
            profileLoaded = profileLoaded,
            onSuggestTricycle = { if (!isGuest) page = LocationContributionPage.SUBMIT_TRICYCLE },
            onMyContributions = { if (!isGuest) page = LocationContributionPage.MY_CONTRIBUTIONS },
            onHomeClick = { onDismiss(); onHomeClick() },
            onRecentClick = { onDismiss(); onRecentClick() },
            onFavoritesClick = { onDismiss(); onFavoritesClick() },
            onProfileClick = { onDismiss(); onProfileClick() }
        )

        LocationContributionPage.SUBMIT_TRICYCLE -> LocationAwareTricycleSubmissionScreen(
            dataProvider = dataProvider,
            onBack = { page = LocationContributionPage.OVERVIEW },
            onHomeClick = { onDismiss(); onHomeClick() },
            onRecentClick = { onDismiss(); onRecentClick() },
            onFavoritesClick = { onDismiss(); onFavoritesClick() },
            onProfileClick = { onDismiss(); onProfileClick() }
        )

        LocationContributionPage.MY_CONTRIBUTIONS -> LocationMyContributionsScreen(
            dataProvider = dataProvider,
            onBack = { page = LocationContributionPage.OVERVIEW },
            onHomeClick = { onDismiss(); onHomeClick() },
            onRecentClick = { onDismiss(); onRecentClick() },
            onFavoritesClick = { onDismiss(); onFavoritesClick() },
            onProfileClick = { onDismiss(); onProfileClick() }
        )
    }
}

@Composable
private fun LocationContributionsOverviewScreen(
    isGuest: Boolean,
    profileLoaded: Boolean,
    onSuggestTricycle: () -> Unit,
    onMyContributions: () -> Unit,
    onHomeClick: () -> Unit,
    onRecentClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onProfileClick: () -> Unit
) {
    Column(Modifier.fillMaxSize().background(TukiCream)) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(horizontal = 20.dp),
            contentPadding = PaddingValues(top = 18.dp, bottom = 20.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            item {
                Text("Contributions", color = TukiInk, style = MaterialTheme.typography.displaySmall)
                Spacer(Modifier.height(5.dp))
                Text("Help improve TUKI transport data.", color = TukiMuted, style = MaterialTheme.typography.bodyMedium)
            }

            if (profileLoaded && isGuest) {
                item {
                    Surface(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(18.dp),
                        color = TukiOrange.copy(alpha = 0.12f)
                    ) {
                        Column(Modifier.padding(16.dp)) {
                            Text("Registered account required", color = TukiInk, style = MaterialTheme.typography.titleMedium)
                            Spacer(Modifier.height(4.dp))
                            Text(
                                "Sign in or create an account before submitting transport information for verification.",
                                color = TukiMuted,
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                    }
                }
            }

            item {
                LocationContributionActionCard(
                    icon = "🛺",
                    title = "Suggest a Tricycle Point",
                    subtitle = "Report a missing TODA or tricycle terminal.",
                    enabled = !isGuest,
                    onClick = onSuggestTricycle
                )
            }

            item {
                LocationContributionActionCard(
                    icon = "📋",
                    title = "My Contributions",
                    subtitle = "View your submitted reports and statuses.",
                    enabled = !isGuest,
                    onClick = onMyContributions
                )
            }

            item {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(18.dp),
                    color = TukiSky.copy(alpha = 0.38f)
                ) {
                    Row(Modifier.padding(16.dp), verticalAlignment = Alignment.Top) {
                        Surface(Modifier.size(30.dp), shape = CircleShape, color = TukiTeal) {
                            Box(contentAlignment = Alignment.Center) {
                                Text("i", color = Color.White, fontWeight = FontWeight.Bold)
                            }
                        }
                        Spacer(Modifier.width(12.dp))
                        Column(Modifier.weight(1f)) {
                            Text("How it works", color = TukiInk, style = MaterialTheme.typography.titleMedium)
                            Spacer(Modifier.height(4.dp))
                            Text(
                                "Submissions are reviewed by TUKI administrators for accuracy before they become official transport data.",
                                color = TukiMuted,
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                    }
                }
            }
        }

        LocationContributionBottomBar(
            onHomeClick = onHomeClick,
            onRecentClick = onRecentClick,
            onFavoritesClick = onFavoritesClick,
            onProfileClick = onProfileClick
        )
    }
}

@Composable
private fun LocationContributionActionCard(
    icon: String,
    title: String,
    subtitle: String,
    enabled: Boolean,
    onClick: () -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxWidth().clickable(enabled = enabled, onClick = onClick),
        shape = RoundedCornerShape(20.dp),
        color = TukiSurfaceRaised,
        shadowElevation = 2.dp
    ) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(
                Modifier.size(58.dp),
                shape = RoundedCornerShape(18.dp),
                color = if (enabled) TukiTeal.copy(alpha = 0.12f) else TukiMuted.copy(alpha = 0.08f)
            ) {
                Box(contentAlignment = Alignment.Center) { Text(icon, fontSize = 28.sp) }
            }
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text(title, color = if (enabled) TukiInk else TukiMuted, style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(4.dp))
                Text(subtitle, color = TukiMuted, style = MaterialTheme.typography.bodyMedium)
            }
            Text("›", color = if (enabled) TukiTeal else TukiMuted, fontSize = 30.sp)
        }
    }
}

@Composable
private fun LocationMyContributionsScreen(
    dataProvider: TukiDataProvider,
    onBack: () -> Unit,
    onHomeClick: () -> Unit,
    onRecentClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onProfileClick: () -> Unit
) {
    var loading by remember { mutableStateOf(true) }
    var error by remember { mutableStateOf<String?>(null) }
    var submissions by remember { mutableStateOf<List<TricyclePointSubmissionDto>>(emptyList()) }

    LaunchedEffect(dataProvider) {
        loading = true
        error = null
        when (val result = dataProvider.tricycleSubmissionRepository.getMine()) {
            is ApiResult.Success -> submissions = result.data
            is ApiResult.Failure -> error = result.message
        }
        loading = false
    }

    Column(Modifier.fillMaxSize().background(TukiCream)) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(horizontal = 20.dp),
            contentPadding = PaddingValues(top = 10.dp, bottom = 20.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    TextButton(onClick = onBack, contentPadding = PaddingValues(0.dp)) {
                        Text("←", color = TukiInk, fontSize = 28.sp)
                    }
                    Spacer(Modifier.width(8.dp))
                    Column {
                        Text("My Contributions", color = TukiInk, style = MaterialTheme.typography.headlineSmall)
                        Text("Track your submitted transport reports.", color = TukiMuted, style = MaterialTheme.typography.bodySmall)
                    }
                }
            }

            when {
                loading -> item {
                    Box(Modifier.fillMaxWidth().padding(vertical = 50.dp), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(color = TukiTeal)
                    }
                }
                !error.isNullOrBlank() -> item {
                    Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp), color = TukiSky.copy(alpha = 0.28f)) {
                        Column(Modifier.padding(14.dp)) {
                            Text("Unable to load contributions", color = TukiInk, style = MaterialTheme.typography.titleSmall)
                            Text(error.orEmpty(), color = TukiMuted, style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
                submissions.isEmpty() -> item {
                    Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiSurfaceRaised) {
                        Column(Modifier.padding(22.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                            Text("📋", fontSize = 38.sp)
                            Spacer(Modifier.height(8.dp))
                            Text("No contributions yet", color = TukiInk, style = MaterialTheme.typography.titleMedium)
                            Spacer(Modifier.height(4.dp))
                            Text(
                                "Your submitted tricycle/TODA reports will appear here.",
                                color = TukiMuted,
                                style = MaterialTheme.typography.bodyMedium,
                                textAlign = TextAlign.Center
                            )
                        }
                    }
                }
                else -> items(submissions, key = { it.tricyclePointSubmissionId }) { submission ->
                    LocationContributionSubmissionCard(submission)
                }
            }
        }

        LocationContributionBottomBar(
            onHomeClick = onHomeClick,
            onRecentClick = onRecentClick,
            onFavoritesClick = onFavoritesClick,
            onProfileClick = onProfileClick
        )
    }
}

@Composable
private fun LocationContributionSubmissionCard(submission: TricyclePointSubmissionDto) {
    val title = submission.suggestedTodaName?.takeIf { it.isNotBlank() }
        ?: submission.suggestedLandmark?.takeIf { it.isNotBlank() }
        ?: "Tricycle point submission #${submission.tricyclePointSubmissionId}"
    val created = remember(submission.createdAt) {
        runCatching {
            OffsetDateTime.parse(submission.createdAt).format(DateTimeFormatter.ofPattern("MMM d, yyyy · h:mm a"))
        }.getOrElse { submission.createdAt }
    }

    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(18.dp),
        color = TukiSurfaceRaised,
        shadowElevation = 1.dp
    ) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(Modifier.size(48.dp), shape = RoundedCornerShape(15.dp), color = TukiTeal.copy(alpha = 0.12f)) {
                Box(contentAlignment = Alignment.Center) { Text("🛺", fontSize = 23.sp) }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(title, color = TukiInk, style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.height(3.dp))
                Text(created, color = TukiMuted, style = MaterialTheme.typography.bodySmall)
            }
            LocationStatusPill(submission.status)
        }
    }
}

@Composable
private fun LocationStatusPill(status: String) {
    val normalized = status.trim().lowercase()
    val background = when (normalized) {
        "approved" -> Color(0xFFE6F5E9)
        "rejected" -> Color(0xFFFFE8E6)
        "needschanges" -> Color(0xFFFFF1D7)
        else -> TukiSky.copy(alpha = 0.42f)
    }
    val foreground = when (normalized) {
        "approved" -> Color(0xFF238B45)
        "rejected" -> Color(0xFFC53B32)
        "needschanges" -> Color(0xFFA46600)
        else -> TukiTeal
    }

    Surface(shape = RoundedCornerShape(50), color = background) {
        Text(
            status.ifBlank { "Pending" },
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
            color = foreground,
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.Bold
        )
    }
}

@Composable
private fun LocationContributionBottomBar(
    onHomeClick: () -> Unit,
    onRecentClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onProfileClick: () -> Unit
) {
    BottomBar(
        selectedTab = TukiTab.CONTRIBUTIONS,
        onHomeClick = onHomeClick,
        onRecentClick = onRecentClick,
        onContributionsClick = {},
        onFavoritesClick = onFavoritesClick,
        onProfileClick = onProfileClick
    )
}
