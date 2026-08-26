package com.example.frontend.screens

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
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
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
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
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
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
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.launch
import java.io.ByteArrayOutputStream
import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter

private enum class ContributionPage { OVERVIEW, SUBMIT_TRICYCLE, MY_CONTRIBUTIONS }

private data class SelectedProofPhoto(
    val bytes: ByteArray,
    val contentType: String,
    val fileName: String
)

@Composable
fun ContributionsHost(
    dataProvider: TukiDataProvider,
    onDismiss: () -> Unit,
    onHomeClick: () -> Unit,
    onRecentClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onProfileClick: () -> Unit
) {
    var page by remember { mutableStateOf(ContributionPage.OVERVIEW) }
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
        ContributionPage.OVERVIEW -> ContributionsOverviewScreen(
            isGuest = isGuest,
            profileLoaded = profileLoaded,
            onSuggestTricycle = { if (!isGuest) page = ContributionPage.SUBMIT_TRICYCLE },
            onMyContributions = { if (!isGuest) page = ContributionPage.MY_CONTRIBUTIONS },
            onHomeClick = {
                onDismiss()
                onHomeClick()
            },
            onRecentClick = {
                onDismiss()
                onRecentClick()
            },
            onFavoritesClick = {
                onDismiss()
                onFavoritesClick()
            },
            onProfileClick = {
                onDismiss()
                onProfileClick()
            }
        )

        ContributionPage.SUBMIT_TRICYCLE -> TricycleSubmissionScreen(
            dataProvider = dataProvider,
            onBack = { page = ContributionPage.OVERVIEW },
            onHomeClick = {
                onDismiss()
                onHomeClick()
            },
            onRecentClick = {
                onDismiss()
                onRecentClick()
            },
            onFavoritesClick = {
                onDismiss()
                onFavoritesClick()
            },
            onProfileClick = {
                onDismiss()
                onProfileClick()
            }
        )

        ContributionPage.MY_CONTRIBUTIONS -> MyContributionsScreen(
            dataProvider = dataProvider,
            onBack = { page = ContributionPage.OVERVIEW },
            onHomeClick = {
                onDismiss()
                onHomeClick()
            },
            onRecentClick = {
                onDismiss()
                onRecentClick()
            },
            onFavoritesClick = {
                onDismiss()
                onFavoritesClick()
            },
            onProfileClick = {
                onDismiss()
                onProfileClick()
            }
        )
    }
}

@Composable
private fun ContributionsOverviewScreen(
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
                Text(
                    "Help improve TUKI transport data.",
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodyMedium
                )
            }

            if (profileLoaded && isGuest) {
                item {
                    Surface(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(18.dp),
                        color = TukiOrange.copy(alpha = 0.12f)
                    ) {
                        Column(Modifier.padding(16.dp)) {
                            Text(
                                "Registered account required",
                                color = TukiInk,
                                style = MaterialTheme.typography.titleMedium
                            )
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
                ContributionActionCard(
                    icon = "🛺",
                    title = "Suggest a Tricycle Point",
                    subtitle = "Report a missing TODA or tricycle terminal.",
                    enabled = !isGuest,
                    onClick = onSuggestTricycle
                )
            }

            item {
                ContributionActionCard(
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
                    Row(
                        Modifier.padding(16.dp),
                        verticalAlignment = Alignment.Top
                    ) {
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

        ContributionBottomBar(
            onHomeClick = onHomeClick,
            onRecentClick = onRecentClick,
            onFavoritesClick = onFavoritesClick,
            onProfileClick = onProfileClick
        )
    }
}

@Composable
private fun ContributionActionCard(
    icon: String,
    title: String,
    subtitle: String,
    enabled: Boolean,
    onClick: () -> Unit
) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(enabled = enabled, onClick = onClick),
        shape = RoundedCornerShape(20.dp),
        color = TukiSurfaceRaised,
        shadowElevation = 2.dp
    ) {
        Row(
            Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Surface(
                Modifier.size(58.dp),
                shape = RoundedCornerShape(18.dp),
                color = if (enabled) TukiTeal.copy(alpha = 0.12f) else TukiMuted.copy(alpha = 0.08f)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text(icon, fontSize = 28.sp)
                }
            }
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    title,
                    color = if (enabled) TukiInk else TukiMuted,
                    style = MaterialTheme.typography.titleMedium
                )
                Spacer(Modifier.height(4.dp))
                Text(subtitle, color = TukiMuted, style = MaterialTheme.typography.bodyMedium)
            }
            Text("›", color = if (enabled) TukiTeal else TukiMuted, fontSize = 30.sp)
        }
    }
}

@Composable
private fun TricycleSubmissionScreen(
    dataProvider: TukiDataProvider,
    onBack: () -> Unit,
    onHomeClick: () -> Unit,
    onRecentClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onProfileClick: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var selectedPhoto by remember { mutableStateOf<SelectedProofPhoto?>(null) }
    var proofImageUrl by remember { mutableStateOf<String?>(null) }
    var isUploading by remember { mutableStateOf(false) }
    var uploadError by remember { mutableStateOf<String?>(null) }
    var todaName by remember { mutableStateOf("") }
    var landmark by remember { mutableStateOf("") }

    fun upload(photo: SelectedProofPhoto) {
        selectedPhoto = photo
        proofImageUrl = null
        uploadError = null
        scope.launch {
            isUploading = true
            when (
                val result = dataProvider.tricycleSubmissionRepository.uploadProof(
                    imageBytes = photo.bytes,
                    contentType = photo.contentType,
                    fileName = photo.fileName
                )
            ) {
                is ApiResult.Success -> proofImageUrl = result.data.proofImageUrl
                is ApiResult.Failure -> uploadError = result.message
            }
            isUploading = false
        }
    }

    val cameraLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.TakePicturePreview()
    ) { bitmap: Bitmap? ->
        if (bitmap != null) {
            val output = ByteArrayOutputStream()
            bitmap.compress(Bitmap.CompressFormat.JPEG, 92, output)
            upload(
                SelectedProofPhoto(
                    bytes = output.toByteArray(),
                    contentType = "image/jpeg",
                    fileName = "tricycle-proof-camera.jpg"
                )
            )
        }
    }

    val galleryLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        if (uri != null) {
            runCatching {
                val bytes = context.contentResolver.openInputStream(uri)?.use { it.readBytes() }
                    ?: error("Unable to read the selected image.")
                val mime = context.contentResolver.getType(uri)
                    ?.takeIf { it in setOf("image/jpeg", "image/png", "image/webp") }
                    ?: "image/jpeg"
                val extension = when (mime) {
                    "image/png" -> "png"
                    "image/webp" -> "webp"
                    else -> "jpg"
                }
                upload(SelectedProofPhoto(bytes, mime, "tricycle-proof-gallery.$extension"))
            }.onFailure {
                uploadError = it.message ?: "Unable to read the selected image."
            }
        }
    }

    Column(Modifier.fillMaxSize().background(TukiCream)) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(horizontal = 20.dp),
            contentPadding = PaddingValues(top = 10.dp, bottom = 24.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    TextButton(onClick = onBack, contentPadding = PaddingValues(0.dp)) {
                        Text("←", color = TukiInk, fontSize = 28.sp)
                    }
                    Spacer(Modifier.width(8.dp))
                    Column {
                        Text("Suggest a Tricycle Point", color = TukiInk, style = MaterialTheme.typography.headlineSmall)
                        Text(
                            "Help us add a missing TODA or terminal.",
                            color = TukiMuted,
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }
            }

            item {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    PhotoActionButton("📷", "Take Photo", Modifier.weight(1f)) {
                        cameraLauncher.launch(null)
                    }
                    PhotoActionButton("▧", "Choose from Gallery", Modifier.weight(1f)) {
                        galleryLauncher.launch("image/*")
                    }
                }
            }

            selectedPhoto?.let { photo ->
                item {
                    val bitmap = remember(photo.bytes) {
                        BitmapFactory.decodeByteArray(photo.bytes, 0, photo.bytes.size)
                    }
                    Surface(
                        modifier = Modifier.fillMaxWidth().height(220.dp),
                        shape = RoundedCornerShape(18.dp),
                        color = TukiSky.copy(alpha = 0.28f)
                    ) {
                        if (bitmap != null) {
                            Image(
                                bitmap = bitmap.asImageBitmap(),
                                contentDescription = "Selected proof photo",
                                modifier = Modifier.fillMaxSize(),
                                contentScale = ContentScale.Crop
                            )
                        }
                    }
                }

                item {
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        TextButton(
                            modifier = Modifier.weight(1f),
                            onClick = { cameraLauncher.launch(null) }
                        ) { Text("↻ Retake Photo", color = TukiTeal) }
                        TextButton(
                            modifier = Modifier.weight(1f),
                            onClick = { galleryLauncher.launch("image/*") }
                        ) { Text("✎ Change Photo", color = TukiTeal) }
                    }
                }
            }

            item {
                when {
                    selectedPhoto == null -> SubmissionStatusCard(
                        title = "Photo required",
                        subtitle = "Take a photo or choose one from your gallery before continuing.",
                        success = false
                    )
                    isUploading -> SubmissionStatusCard(
                        title = "Uploading proof photo…",
                        subtitle = "Keep this screen open while TUKI prepares your submission.",
                        success = false,
                        showProgress = true
                    )
                    proofImageUrl != null -> SubmissionStatusCard(
                        title = "Proof photo ready",
                        subtitle = "Your photo was securely uploaded to TUKI.",
                        success = true
                    )
                    else -> SubmissionStatusCard(
                        title = "Photo upload failed",
                        subtitle = uploadError ?: "Please try another photo.",
                        success = false
                    )
                }
            }

            item {
                OutlinedTextField(
                    value = todaName,
                    onValueChange = { if (it.length <= 200) todaName = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("TODA Name (optional)") },
                    placeholder = { Text("e.g., San Fernando TODA") },
                    singleLine = true,
                    shape = RoundedCornerShape(14.dp)
                )
            }

            item {
                OutlinedTextField(
                    value = landmark,
                    onValueChange = { if (it.length <= 300) landmark = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Landmark (optional)") },
                    placeholder = { Text("e.g., Near public market") },
                    minLines = 2,
                    maxLines = 3,
                    shape = RoundedCornerShape(14.dp)
                )
            }

            item {
                SubmissionStatusCard(
                    title = "Automatic location capture",
                    subtitle = "TUKI will detect your current location automatically before final submission. Coordinates will stay hidden from the passenger interface.",
                    success = false
                )
            }

            item {
                Button(
                    onClick = {},
                    enabled = false,
                    modifier = Modifier.fillMaxWidth().height(54.dp),
                    shape = RoundedCornerShape(16.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = TukiDeepTeal,
                        disabledContainerColor = TukiDeepTeal.copy(alpha = 0.45f)
                    )
                ) {
                    Text("Submit for Verification", fontWeight = FontWeight.Bold)
                }
                Spacer(Modifier.height(6.dp))
                Text(
                    "Final submission unlocks when automatic GPS capture is connected in the location feature.",
                    modifier = Modifier.fillMaxWidth(),
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodySmall,
                    textAlign = TextAlign.Center
                )
            }
        }

        ContributionBottomBar(
            onHomeClick = onHomeClick,
            onRecentClick = onRecentClick,
            onFavoritesClick = onFavoritesClick,
            onProfileClick = onProfileClick
        )
    }
}

@Composable
private fun PhotoActionButton(
    icon: String,
    label: String,
    modifier: Modifier = Modifier,
    onClick: () -> Unit
) {
    Surface(
        modifier = modifier.clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = TukiTeal.copy(alpha = 0.10f)
    ) {
        Column(
            Modifier.padding(vertical = 16.dp, horizontal = 10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(icon, fontSize = 27.sp)
            Spacer(Modifier.height(6.dp))
            Text(label, color = TukiInk, style = MaterialTheme.typography.labelLarge, textAlign = TextAlign.Center)
        }
    }
}

@Composable
private fun SubmissionStatusCard(
    title: String,
    subtitle: String,
    success: Boolean,
    showProgress: Boolean = false
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = if (success) Color(0xFFE6F5E9) else TukiSky.copy(alpha = 0.28f)
    ) {
        Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
            if (showProgress) {
                CircularProgressIndicator(Modifier.size(22.dp), color = TukiTeal, strokeWidth = 2.dp)
            } else {
                Text(if (success) "✓" else "i", color = if (success) Color(0xFF238B45) else TukiTeal, fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.width(12.dp))
            Column {
                Text(title, color = TukiInk, style = MaterialTheme.typography.titleSmall)
                Text(subtitle, color = TukiMuted, style = MaterialTheme.typography.bodySmall)
            }
        }
    }
}

@Composable
private fun MyContributionsScreen(
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
                    SubmissionStatusCard("Unable to load contributions", error.orEmpty(), success = false)
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
                    ContributionSubmissionCard(submission)
                }
            }
        }

        ContributionBottomBar(
            onHomeClick = onHomeClick,
            onRecentClick = onRecentClick,
            onFavoritesClick = onFavoritesClick,
            onProfileClick = onProfileClick
        )
    }
}

@Composable
private fun ContributionSubmissionCard(submission: TricyclePointSubmissionDto) {
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
            StatusPill(submission.status)
        }
    }
}

@Composable
private fun StatusPill(status: String) {
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
private fun ContributionBottomBar(
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
