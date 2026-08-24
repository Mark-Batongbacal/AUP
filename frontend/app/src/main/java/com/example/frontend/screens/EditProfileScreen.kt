package com.example.frontend.screens

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
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.data.users.UserProfileDto
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

sealed interface EditProfileResult {
    data class Success(val profile: UserProfileDto) : EditProfileResult
    data class Error(val message: String) : EditProfileResult
}

/**
 * Reached by tapping "Edit Profile" in the Profile screen's account list.
 * Full name + phone are sent to the backend on Save; see the note at the
 * top of this file re: why Email is disabled.
 */
@Composable
fun EditProfileScreen(
    initialFullName: String,
    initialEmail: String,
    initialPhone: String,
    onBack: () -> Unit = {},
    onChangePhotoClick: () -> Unit = {},
    onSaveChanges: suspend (fullName: String, phoneNumber: String) -> EditProfileResult = {
            _, _ -> EditProfileResult.Error("Saving isn't wired up yet.")
    },
    onSaved: (UserProfileDto) -> Unit = {}
) {
    var fullName by remember { mutableStateOf(initialFullName) }
    var phone by remember { mutableStateOf(initialPhone) }
    var isSaving by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var successMessage by remember { mutableStateOf<String?>(null) }

    val coroutineScope = rememberCoroutineScope()

    val initials = remember(fullName) {
        fullName.split(" ")
            .mapNotNull { it.firstOrNull()?.uppercaseChar() }
            .take(2)
            .joinToString("")
    }

    val canSave = fullName.isNotBlank() && phone.isNotBlank() && !isSaving

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
                is EditProfileResult.Error -> {
                    errorMessage = result.message
                }
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
        // Header
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiSky.copy(alpha = 0.35f), RoundedCornerShape(12.dp))
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(text = "\u2039", color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
            Spacer(modifier = Modifier.width(14.dp))
            Text(
                text = "Edit profile",
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
        }

        Spacer(modifier = Modifier.height(28.dp))

        // Avatar + change photo
        Column(
            modifier = Modifier.fillMaxWidth(),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Box(contentAlignment = Alignment.BottomEnd) {
                Box(
                    modifier = Modifier
                        .size(100.dp)
                        .background(TukiTeal, CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = initials.ifBlank { "?" },
                        color = Color.White,
                        style = MaterialTheme.typography.displayMedium
                    )
                }
                Box(
                    modifier = Modifier
                        .size(32.dp)
                        .background(TukiOrange, CircleShape)
                        .clickable(onClick = onChangePhotoClick),
                    contentAlignment = Alignment.Center
                ) {
                    Text(text = "\uD83D\uDCF7", fontSize = 14.sp) // 📷
                }
            }

            Spacer(modifier = Modifier.height(10.dp))

            Text(
                text = "Change photo",
                color = TukiTeal,
                style = MaterialTheme.typography.labelLarge,
                modifier = Modifier.clickable(onClick = onChangePhotoClick)
            )

            Spacer(modifier = Modifier.height(28.dp))
        }

        // Full name
        FieldLabel(text = "Full name")
        EditableField(
            value = fullName,
            onValueChange = { fullName = it },
            enabled = !isSaving
        )

        Spacer(modifier = Modifier.height(18.dp))

        // Email (disabled — see note at top of file)
        FieldLabel(text = "Email")
        EditableField(
            value = initialEmail,
            onValueChange = {},
            enabled = false
        )
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = "Email is tied to your login and can't be changed here yet.",
            color = TukiMuted,
            style = MaterialTheme.typography.bodySmall
        )

        Spacer(modifier = Modifier.height(18.dp))

        // Phone
        FieldLabel(text = "Phone")
        EditableField(
            value = phone,
            onValueChange = { phone = it },
            enabled = !isSaving
        )

        Spacer(modifier = Modifier.height(20.dp))

        errorMessage?.let { message ->
            Text(text = message, color = TukiDanger, style = MaterialTheme.typography.labelLarge)
            Spacer(modifier = Modifier.height(10.dp))
        }
        successMessage?.let { message ->
            Text(text = message, color = TukiTeal, style = MaterialTheme.typography.labelLarge)
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
                Text(text = "Save changes", color = Color.White, style = MaterialTheme.typography.titleLarge)
            }
        }
    }
}

@Composable
private fun FieldLabel(text: String) {
    Text(text = text, color = TukiInk, style = MaterialTheme.typography.titleSmall)
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
            focusedContainerColor = TukiSky.copy(alpha = 0.2f),
            unfocusedContainerColor = TukiSky.copy(alpha = 0.2f),
            disabledContainerColor = TukiSky.copy(alpha = 0.1f),
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
