package com.example.frontend.screens

import android.Manifest
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
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
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
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.location.hasDeviceLocationPermission
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.contributions.CapturedTricycleSubmissionLocation
import com.example.frontend.data.contributions.CreateTricyclePointSubmissionRequest
import com.example.frontend.data.contributions.toCapturedTricycleSubmissionLocation
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.launch
import java.io.ByteArrayOutputStream

private data class LocationSubmissionPhoto(
    val bytes: ByteArray,
    val contentType: String,
    val fileName: String
)

@Composable
fun LocationAwareTricycleSubmissionScreen(
    dataProvider: TukiDataProvider,
    onBack: () -> Unit,
    onHomeClick: () -> Unit,
    onRecentClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onProfileClick: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()

    var selectedPhoto by remember { mutableStateOf<LocationSubmissionPhoto?>(null) }
    var proofImageUrl by remember { mutableStateOf<String?>(null) }
    var isUploading by remember { mutableStateOf(false) }
    var uploadError by remember { mutableStateOf<String?>(null) }

    var capturedLocation by remember { mutableStateOf<CapturedTricycleSubmissionLocation?>(null) }
    var isLocating by remember { mutableStateOf(false) }
    var locationError by remember { mutableStateOf<String?>(null) }

    var todaName by remember { mutableStateOf("") }
    var landmark by remember { mutableStateOf("") }

    var isSubmitting by remember { mutableStateOf(false) }
    var submissionError by remember { mutableStateOf<String?>(null) }
    var submissionSucceeded by remember { mutableStateOf(false) }

    fun captureLocation() {
        if (isLocating || submissionSucceeded) return
        scope.launch {
            isLocating = true
            locationError = null
            val detected = context.currentDeviceLocation()
                ?.toCapturedTricycleSubmissionLocation()
            capturedLocation = detected
            if (detected == null) {
                locationError = "We couldn't detect a usable current location. Make sure Location is enabled, then try again."
            }
            isLocating = false
        }
    }

    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { result ->
        val granted = result[Manifest.permission.ACCESS_FINE_LOCATION] == true ||
            result[Manifest.permission.ACCESS_COARSE_LOCATION] == true
        if (granted) {
            captureLocation()
        } else {
            capturedLocation = null
            locationError = "Location permission is required so TUKI can verify where this tricycle point was reported."
        }
    }

    fun ensureLocationCapture() {
        if (context.hasDeviceLocationPermission()) {
            captureLocation()
        } else {
            permissionLauncher.launch(
                arrayOf(
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                )
            )
        }
    }

    fun upload(photo: LocationSubmissionPhoto) {
        selectedPhoto = photo
        proofImageUrl = null
        uploadError = null
        submissionError = null
        submissionSucceeded = false
        capturedLocation = null
        locationError = null

        scope.launch {
            isUploading = true
            when (
                val result = dataProvider.tricycleSubmissionRepository.uploadProof(
                    imageBytes = photo.bytes,
                    contentType = photo.contentType,
                    fileName = photo.fileName
                )
            ) {
                is ApiResult.Success -> {
                    proofImageUrl = result.data.proofImageUrl
                    ensureLocationCapture()
                }
                is ApiResult.Failure -> uploadError = result.message
            }
            isUploading = false
        }
    }

    fun submit() {
        val proof = proofImageUrl ?: return
        if (isSubmitting || isUploading || submissionSucceeded) return
        if (!context.hasDeviceLocationPermission()) {
            locationError = "Location permission is required before this report can be submitted."
            ensureLocationCapture()
            return
        }

        scope.launch {
            isSubmitting = true
            submissionError = null
            locationError = null

            // Always refresh immediately before submission so the stored coordinates represent
            // where the passenger is at verification time rather than a stale earlier fix.
            val freshLocation = context.currentDeviceLocation()
                ?.toCapturedTricycleSubmissionLocation()

            if (freshLocation == null) {
                capturedLocation = null
                locationError = "We couldn't detect a usable current location. Make sure Location is enabled, then try again."
                isSubmitting = false
                return@launch
            }

            capturedLocation = freshLocation
            val request = CreateTricyclePointSubmissionRequest(
                proofImageUrl = proof,
                latitude = freshLocation.latitude,
                longitude = freshLocation.longitude,
                accuracyMeters = freshLocation.accuracyMeters,
                locationCapturedAt = freshLocation.capturedAt.toString(),
                suggestedTodaName = todaName.trim().takeIf { it.isNotEmpty() },
                suggestedLandmark = landmark.trim().takeIf { it.isNotEmpty() }
            )

            when (val result = dataProvider.tricycleSubmissionRepository.createSubmission(request)) {
                is ApiResult.Success -> {
                    submissionSucceeded = true
                    submissionError = null
                }
                is ApiResult.Failure -> {
                    submissionError = result.message.ifBlank {
                        "Your contribution could not be submitted. Please try again."
                    }
                }
            }
            isSubmitting = false
        }
    }

    val cameraLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.TakePicturePreview()
    ) { bitmap: Bitmap? ->
        if (bitmap != null) {
            val output = ByteArrayOutputStream()
            bitmap.compress(Bitmap.CompressFormat.JPEG, 92, output)
            upload(
                LocationSubmissionPhoto(
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
                upload(LocationSubmissionPhoto(bytes, mime, "tricycle-proof-gallery.$extension"))
            }.onFailure {
                uploadError = it.message ?: "Unable to read the selected image."
            }
        }
    }

    val canSubmit = proofImageUrl != null &&
        capturedLocation != null &&
        !isUploading &&
        !isLocating &&
        !isSubmitting &&
        !submissionSucceeded

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
                    LocationPhotoActionButton("📷", "Take Photo", Modifier.weight(1f)) {
                        cameraLauncher.launch(null)
                    }
                    LocationPhotoActionButton("▧", "Choose from Gallery", Modifier.weight(1f)) {
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
                            enabled = !isUploading && !isSubmitting,
                            onClick = { cameraLauncher.launch(null) }
                        ) { Text("↻ Retake Photo", color = TukiTeal) }
                        TextButton(
                            modifier = Modifier.weight(1f),
                            enabled = !isUploading && !isSubmitting,
                            onClick = { galleryLauncher.launch("image/*") }
                        ) { Text("✎ Change Photo", color = TukiTeal) }
                    }
                }
            }

            item {
                when {
                    selectedPhoto == null -> LocationSubmissionStatusCard(
                        title = "Photo required",
                        subtitle = "Take a photo or choose one from your gallery before continuing.",
                        success = false
                    )
                    isUploading -> LocationSubmissionStatusCard(
                        title = "Uploading proof photo…",
                        subtitle = "Keep this screen open while TUKI prepares your submission.",
                        success = false,
                        showProgress = true
                    )
                    proofImageUrl != null -> LocationSubmissionStatusCard(
                        title = "Proof photo ready",
                        subtitle = "Your photo was securely uploaded to TUKI.",
                        success = true
                    )
                    else -> LocationSubmissionStatusCard(
                        title = "Photo upload failed",
                        subtitle = uploadError ?: "Please try another photo.",
                        success = false
                    )
                }
            }

            item {
                OutlinedTextField(
                    value = todaName,
                    onValueChange = { if (it.length <= 200 && !submissionSucceeded) todaName = it },
                    enabled = !submissionSucceeded,
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
                    onValueChange = { if (it.length <= 300 && !submissionSucceeded) landmark = it },
                    enabled = !submissionSucceeded,
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Landmark (optional)") },
                    placeholder = { Text("e.g., Near public market") },
                    minLines = 2,
                    maxLines = 3,
                    shape = RoundedCornerShape(14.dp)
                )
            }

            item {
                when {
                    isLocating -> LocationSubmissionStatusCard(
                        title = "Detecting location…",
                        subtitle = "TUKI is securely detecting your current location for verification.",
                        success = false,
                        showProgress = true
                    )
                    capturedLocation != null -> LocationSubmissionStatusCard(
                        title = "Location detected",
                        subtitle = "Your location is ready for verification. Coordinates are hidden from this screen.",
                        success = true
                    )
                    else -> {
                        Column {
                            LocationSubmissionStatusCard(
                                title = "Location required",
                                subtitle = locationError ?: "TUKI needs your current location to verify where this tricycle point was reported.",
                                success = false
                            )
                            if (selectedPhoto != null && proofImageUrl != null) {
                                TextButton(onClick = ::ensureLocationCapture) {
                                    Text("Try location again", color = TukiTeal)
                                }
                            }
                        }
                    }
                }
            }

            if (!submissionError.isNullOrBlank()) {
                item {
                    LocationSubmissionStatusCard(
                        title = "Submission failed",
                        subtitle = submissionError.orEmpty(),
                        success = false
                    )
                }
            }

            if (submissionSucceeded) {
                item {
                    LocationSubmissionStatusCard(
                        title = "Submitted for verification",
                        subtitle = "Thank you. TUKI administrators will verify the photo and detected location before it becomes official transport data.",
                        success = true
                    )
                }
            }

            item {
                Button(
                    onClick = ::submit,
                    enabled = canSubmit,
                    modifier = Modifier.fillMaxWidth().height(54.dp),
                    shape = RoundedCornerShape(16.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = TukiDeepTeal,
                        disabledContainerColor = TukiDeepTeal.copy(alpha = 0.45f)
                    )
                ) {
                    if (isSubmitting) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(20.dp),
                            color = Color.White,
                            strokeWidth = 2.dp
                        )
                        Spacer(Modifier.width(9.dp))
                        Text("Submitting…", fontWeight = FontWeight.Bold)
                    } else {
                        Text(
                            if (submissionSucceeded) "Submitted" else "Submit for Verification",
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
                Spacer(Modifier.height(6.dp))
                Text(
                    when {
                        submissionSucceeded -> "You can track this report from My Contributions."
                        proofImageUrl == null -> "Add a proof photo to continue."
                        capturedLocation == null -> "Location must be detected before submission."
                        else -> "Your detected coordinates are sent only for administrator verification."
                    },
                    modifier = Modifier.fillMaxWidth(),
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodySmall,
                    textAlign = TextAlign.Center
                )
            }
        }

        ContributionBottomBarForLocationSubmission(
            onHomeClick = onHomeClick,
            onRecentClick = onRecentClick,
            onFavoritesClick = onFavoritesClick,
            onProfileClick = onProfileClick
        )
    }
}

@Composable
private fun LocationPhotoActionButton(
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
private fun LocationSubmissionStatusCard(
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
                Text(
                    if (success) "✓" else "i",
                    color = if (success) Color(0xFF238B45) else TukiTeal,
                    fontWeight = FontWeight.Bold
                )
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
private fun ContributionBottomBarForLocationSubmission(
    onHomeClick: () -> Unit,
    onRecentClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onProfileClick: () -> Unit
) {
    com.example.frontend.components.BottomBar(
        selectedTab = com.example.frontend.components.TukiTab.CONTRIBUTIONS,
        onHomeClick = onHomeClick,
        onRecentClick = onRecentClick,
        onContributionsClick = {},
        onFavoritesClick = onFavoritesClick,
        onProfileClick = onProfileClick
    )
}
