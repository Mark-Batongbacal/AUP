package com.example.frontend.screens

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.data.users.UserProfileDto
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import java.io.ByteArrayOutputStream
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlin.math.max

sealed interface EditProfileResult {
    data class Success(val profile: UserProfileDto) : EditProfileResult
    data class Error(val message: String) : EditProfileResult
}

@Composable
fun EditProfileScreen(
    initialFullName: String,
    initialEmail: String,
    initialPhone: String,
    initialProfileImageUrl: String? = null,
    onBack: () -> Unit = {},
    onSaveChanges: suspend (fullName: String, phoneNumber: String) -> EditProfileResult = {
            _, _ -> EditProfileResult.Error("Saving isn't wired up yet.")
    },
    onUploadPhoto: suspend (ByteArray) -> EditProfileResult = {
        EditProfileResult.Error("Profile photo upload isn't wired up yet.")
    },
    onProfileChanged: (UserProfileDto) -> Unit = {},
    onSaved: (UserProfileDto) -> Unit = {}
) {
    val context = LocalContext.current
    var fullName by remember { mutableStateOf(initialFullName) }
    var phone by remember { mutableStateOf(initialPhone) }
    var profileImageUrl by remember(initialProfileImageUrl) { mutableStateOf(initialProfileImageUrl) }
    var isSaving by remember { mutableStateOf(false) }
    var isUploadingPhoto by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var successMessage by remember { mutableStateOf<String?>(null) }
    val coroutineScope = rememberCoroutineScope()

    val initials = remember(fullName) {
        fullName.split(" ")
            .mapNotNull { it.firstOrNull()?.uppercaseChar() }
            .take(2)
            .joinToString("")
    }

    val photoPicker = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        if (uri == null || isUploadingPhoto) return@rememberLauncherForActivityResult
        errorMessage = null
        successMessage = null
        isUploadingPhoto = true
        coroutineScope.launch {
            val imageBytes = prepareProfileImage(context, uri)
            if (imageBytes == null) {
                errorMessage = "TUKI couldn't read that image. Choose another photo."
                isUploadingPhoto = false
                return@launch
            }

            when (val result = onUploadPhoto(imageBytes)) {
                is EditProfileResult.Success -> {
                    profileImageUrl = result.profile.profileImageUrl
                    successMessage = "Profile photo updated."
                    onProfileChanged(result.profile)
                }
                is EditProfileResult.Error -> errorMessage = result.message
            }
            isUploadingPhoto = false
        }
    }

    val canSave = fullName.isNotBlank() && phone.isNotBlank() && !isSaving && !isUploadingPhoto

    fun save() {
        if (!canSave) return
        errorMessage = null
        successMessage = null
        isSaving = true
        coroutineScope.launch {
            when (val result = onSaveChanges(fullName.trim(), phone.trim())) {
                is EditProfileResult.Success -> {
                    successMessage = "Profile updated."
                    onSaved(result.profile)
                }
                is EditProfileResult.Error -> errorMessage = result.message
            }
            isSaving = false
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .padding(start = 30.dp, end = 30.dp, top = 12.dp, bottom = 30.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiSurfaceRaised, RoundedCornerShape(12.dp))
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text("‹", color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
            Spacer(modifier = Modifier.width(14.dp))
            Text("Edit profile", color = TukiInk, style = MaterialTheme.typography.displaySmall)
        }

        Spacer(modifier = Modifier.height(28.dp))

        Column(
            modifier = Modifier.fillMaxWidth(),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Box(contentAlignment = Alignment.BottomEnd) {
                ProfileAvatar(
                    profileImageUrl = profileImageUrl,
                    initials = initials,
                    size = 100.dp,
                    textStyle = MaterialTheme.typography.displayMedium
                )
                Box(
                    modifier = Modifier
                        .size(32.dp)
                        .background(TukiOrange, CircleShape)
                        .clickable(enabled = !isUploadingPhoto) { photoPicker.launch("image/*") },
                    contentAlignment = Alignment.Center
                ) {
                    if (isUploadingPhoto) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(16.dp),
                            strokeWidth = 2.dp,
                            color = Color.White
                        )
                    } else {
                        Text("📷", fontSize = 14.sp)
                    }
                }
            }

            Spacer(modifier = Modifier.height(10.dp))
            Text(
                if (isUploadingPhoto) "Uploading photo..." else "Change photo",
                color = TukiTeal,
                style = MaterialTheme.typography.labelLarge,
                modifier = Modifier.clickable(enabled = !isUploadingPhoto) { photoPicker.launch("image/*") }
            )
            Spacer(modifier = Modifier.height(28.dp))
        }

        FieldLabel("Full name")
        EditableField(fullName, { fullName = it }, !isSaving && !isUploadingPhoto)
        Spacer(modifier = Modifier.height(18.dp))

        FieldLabel("Email")
        EditableField(initialEmail, {}, false)
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            "Email is tied to your login and can't be changed here yet.",
            color = TukiMuted,
            style = MaterialTheme.typography.bodySmall
        )

        Spacer(modifier = Modifier.height(18.dp))
        FieldLabel("Phone")
        EditableField(phone, { phone = it }, !isSaving && !isUploadingPhoto)
        Spacer(modifier = Modifier.height(20.dp))

        errorMessage?.let { message ->
            Text(message, color = TukiDanger, style = MaterialTheme.typography.labelLarge)
            Spacer(modifier = Modifier.height(10.dp))
        }
        successMessage?.let { message ->
            Text(message, color = TukiTeal, style = MaterialTheme.typography.labelLarge)
            Spacer(modifier = Modifier.height(10.dp))
        }

        Spacer(modifier = Modifier.weight(1f))

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(
                    color = if (canSave) TukiOrange else TukiOrange.copy(alpha = 0.4f),
                    shape = RoundedCornerShape(16.dp)
                )
                .clickable(enabled = canSave) { save() }
                .padding(vertical = 16.dp),
            horizontalArrangement = Arrangement.Center,
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (isSaving) {
                CircularProgressIndicator(
                    modifier = Modifier.size(18.dp),
                    strokeWidth = 2.dp,
                    color = Color.White
                )
            } else {
                Text("Save changes", color = Color.White, style = MaterialTheme.typography.titleLarge)
            }
        }
    }
}

private suspend fun prepareProfileImage(context: Context, uri: Uri): ByteArray? = withContext(Dispatchers.IO) {
    runCatching {
        val bitmap = context.contentResolver.openInputStream(uri)?.use(BitmapFactory::decodeStream)
            ?: return@runCatching null
        val largestSide = max(bitmap.width, bitmap.height)
        val targetLargestSide = 1_024
        val scaled = if (largestSide > targetLargestSide) {
            val scale = targetLargestSide.toFloat() / largestSide.toFloat()
            Bitmap.createScaledBitmap(
                bitmap,
                (bitmap.width * scale).toInt().coerceAtLeast(1),
                (bitmap.height * scale).toInt().coerceAtLeast(1),
                true
            )
        } else {
            bitmap
        }

        val output = ByteArrayOutputStream()
        scaled.compress(Bitmap.CompressFormat.JPEG, 85, output)
        if (scaled !== bitmap) scaled.recycle()
        bitmap.recycle()
        output.toByteArray().takeIf { it.isNotEmpty() }
    }.getOrNull()
}

@Composable
private fun FieldLabel(text: String) {
    Text(text, color = TukiInk, style = MaterialTheme.typography.titleSmall)
    Spacer(modifier = Modifier.height(8.dp))
}

@Composable
private fun EditableField(
    value: String,
    onValueChange: (String) -> Unit,
    enabled: Boolean
) {
    TextField(
        value = value,
        onValueChange = onValueChange,
        enabled = enabled,
        singleLine = true,
        colors = TextFieldDefaults.colors(
            focusedContainerColor = TukiSurfaceRaised,
            unfocusedContainerColor = TukiSurfaceRaised,
            disabledContainerColor = TukiSurfaceRaised,
            focusedIndicatorColor = Color.Transparent,
            unfocusedIndicatorColor = Color.Transparent,
            disabledIndicatorColor = Color.Transparent,
            focusedTextColor = TukiInk,
            unfocusedTextColor = TukiInk,
            disabledTextColor = TukiMuted
        ),
        shape = RoundedCornerShape(14.dp),
        textStyle = MaterialTheme.typography.bodyLarge,
        modifier = Modifier.fillMaxWidth()
    )
}
