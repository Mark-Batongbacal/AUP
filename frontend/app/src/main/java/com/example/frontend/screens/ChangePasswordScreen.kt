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
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiError = Color(0xFFB00020)

sealed interface ChangePasswordResult {
    data object Success : ChangePasswordResult
    data class Error(val message: String) : ChangePasswordResult
}

@Composable
fun ChangePasswordScreen(
    onBack: () -> Unit = {},
    onRequestOtp: suspend (currentPassword: String) -> ChangePasswordResult = {
        ChangePasswordResult.Error("Password change isn't wired up yet.")
    },
    onChangePassword: suspend (currentPassword: String, code: String, newPassword: String) -> ChangePasswordResult = { _, _, _ ->
        ChangePasswordResult.Error("Password change isn't wired up yet.")
    },
    onPasswordChanged: () -> Unit = {}
) {
    var currentPassword by remember { mutableStateOf("") }
    var otpCode by remember { mutableStateOf("") }
    var newPassword by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }

    var currentPasswordVisible by remember { mutableStateOf(false) }
    var newPasswordVisible by remember { mutableStateOf(false) }
    var confirmPasswordVisible by remember { mutableStateOf(false) }

    var otpSent by remember { mutableStateOf(false) }
    var isWorking by remember { mutableStateOf(false) }
    var isSuccess by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var infoMessage by remember { mutableStateOf<String?>(null) }

    val coroutineScope = rememberCoroutineScope()

    fun requestOtp() {
        if (isWorking || isSuccess) return
        if (currentPassword.isBlank()) {
            errorMessage = "Enter your current password."
            return
        }

        coroutineScope.launch {
            isWorking = true
            errorMessage = null
            infoMessage = null
            when (val result = onRequestOtp(currentPassword)) {
                ChangePasswordResult.Success -> {
                    otpSent = true
                    infoMessage = "OTP sent to your account email."
                }
                is ChangePasswordResult.Error -> errorMessage = result.message
            }
            isWorking = false
        }
    }

    fun submitChange() {
        if (isWorking || isSuccess) return

        when {
            currentPassword.isBlank() -> errorMessage = "Enter your current password."
            otpCode.isBlank() -> errorMessage = "Enter the OTP from your email."
            newPassword.length < 8 -> errorMessage = "New password must be at least 8 characters."
            newPassword == currentPassword -> errorMessage = "New password must be different from your current password."
            newPassword != confirmPassword -> errorMessage = "New password and confirmation do not match."
            else -> {
                coroutineScope.launch {
                    isWorking = true
                    errorMessage = null
                    infoMessage = null
                    when (val result = onChangePassword(currentPassword, otpCode.trim(), newPassword)) {
                        ChangePasswordResult.Success -> {
                            isSuccess = true
                            infoMessage = "Password changed successfully."
                            currentPassword = ""
                            otpCode = ""
                            newPassword = ""
                            confirmPassword = ""
                            delay(1000)
                            onPasswordChanged()
                        }
                        is ChangePasswordResult.Error -> errorMessage = result.message
                    }
                    isWorking = false
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
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp, vertical = 20.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiCream2, RoundedCornerShape(12.dp))
                    .clickable(enabled = !isWorking, onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(text = "\u2039", color = TukiDark, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            }
            Spacer(modifier = Modifier.width(14.dp))
            Text(text = "Change password", color = TukiDark, fontSize = 22.sp, fontWeight = FontWeight.ExtraBold)
        }

        Spacer(modifier = Modifier.height(10.dp))

        Text(
            text = if (otpSent) {
                "Enter the OTP sent to your email, then choose your new password."
            } else {
                "Confirm your current password first. We'll email you an OTP before changing it."
            },
            color = TukiGray,
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold
        )

        Spacer(modifier = Modifier.height(28.dp))

        PasswordField(
            label = "Current password",
            value = currentPassword,
            visible = currentPasswordVisible,
            enabled = !isWorking && !isSuccess && !otpSent,
            onValueChange = {
                currentPassword = it
                errorMessage = null
            },
            onVisibilityToggle = { currentPasswordVisible = !currentPasswordVisible }
        )

        if (otpSent) {
            Spacer(modifier = Modifier.height(18.dp))

            Column {
                Text(text = "OTP code", color = TukiDark, fontSize = 14.sp, fontWeight = FontWeight.SemiBold)
                Spacer(modifier = Modifier.height(8.dp))
                TextField(
                    value = otpCode,
                    onValueChange = {
                        otpCode = it.filter(Char::isDigit).take(10)
                        errorMessage = null
                    },
                    modifier = Modifier.fillMaxWidth().height(56.dp),
                    enabled = !isWorking && !isSuccess,
                    singleLine = true,
                    shape = RoundedCornerShape(14.dp),
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = TukiCream2,
                        unfocusedContainerColor = TukiCream2,
                        disabledContainerColor = TukiCream2.copy(alpha = 0.6f),
                        focusedIndicatorColor = Color.Transparent,
                        unfocusedIndicatorColor = Color.Transparent,
                        disabledIndicatorColor = Color.Transparent,
                        focusedTextColor = TukiDark,
                        unfocusedTextColor = TukiDark,
                        disabledTextColor = TukiGray
                    )
                )
            }

            Spacer(modifier = Modifier.height(18.dp))

            PasswordField(
                label = "New password",
                value = newPassword,
                visible = newPasswordVisible,
                enabled = !isWorking && !isSuccess,
                onValueChange = {
                    newPassword = it
                    errorMessage = null
                },
                onVisibilityToggle = { newPasswordVisible = !newPasswordVisible }
            )

            Spacer(modifier = Modifier.height(4.dp))

            Text(
                text = "Must be at least 8 characters.",
                color = TukiGray,
                fontSize = 11.sp
            )

            Spacer(modifier = Modifier.height(18.dp))

            PasswordField(
                label = "Confirm new password",
                value = confirmPassword,
                visible = confirmPasswordVisible,
                enabled = !isWorking && !isSuccess,
                onValueChange = {
                    confirmPassword = it
                    errorMessage = null
                },
                onVisibilityToggle = { confirmPasswordVisible = !confirmPasswordVisible }
            )
        }

        errorMessage?.let { message ->
            Spacer(modifier = Modifier.height(16.dp))
            Text(text = message, color = TukiError, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
        }

        infoMessage?.let { message ->
            Spacer(modifier = Modifier.height(16.dp))
            Text(text = message, color = TukiTeal, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
        }

        Spacer(modifier = Modifier.height(28.dp))

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(
                    color = if (isWorking || isSuccess) TukiOrange.copy(alpha = 0.4f) else TukiOrange,
                    shape = RoundedCornerShape(16.dp)
                )
                .clickable(enabled = !isWorking && !isSuccess) {
                    if (otpSent) submitChange() else requestOtp()
                }
                .padding(vertical = 16.dp),
            horizontalArrangement = Arrangement.Center,
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (isWorking) {
                CircularProgressIndicator(
                    modifier = Modifier.size(18.dp),
                    strokeWidth = 2.dp,
                    color = Color.White
                )
            } else {
                Text(
                    text = when {
                        isSuccess -> "Saved"
                        otpSent -> "Change password"
                        else -> "Send OTP"
                    },
                    color = Color.White,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }

        if (otpSent && !isSuccess) {
            Spacer(modifier = Modifier.height(8.dp))
            TextButton(
                onClick = { requestOtp() },
                enabled = !isWorking
            ) {
                Text("Resend OTP", color = TukiTeal, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun PasswordField(
    label: String,
    value: String,
    visible: Boolean,
    enabled: Boolean,
    onValueChange: (String) -> Unit,
    onVisibilityToggle: () -> Unit
) {
    Column {
        Text(text = label, color = TukiDark, fontSize = 14.sp, fontWeight = FontWeight.SemiBold)
        Spacer(modifier = Modifier.height(8.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(56.dp),
            enabled = enabled,
            singleLine = true,
            shape = RoundedCornerShape(14.dp),
            visualTransformation = if (visible) VisualTransformation.None else PasswordVisualTransformation(),
            trailingIcon = {
                Text(
                    text = if (visible) "HIDE" else "SHOW",
                    color = TukiTeal,
                    fontSize = 11.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier
                        .padding(end = 12.dp)
                        .clickable(enabled = enabled, onClick = onVisibilityToggle)
                )
            },
            colors = TextFieldDefaults.colors(
                focusedContainerColor = TukiCream2,
                unfocusedContainerColor = TukiCream2,
                disabledContainerColor = TukiCream2.copy(alpha = 0.6f),
                focusedIndicatorColor = Color.Transparent,
                unfocusedIndicatorColor = Color.Transparent,
                disabledIndicatorColor = Color.Transparent,
                focusedTextColor = TukiDark,
                unfocusedTextColor = TukiDark,
                disabledTextColor = TukiGray
            )
        )
    }
}
