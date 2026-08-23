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
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.components.OtpCodeField
import com.example.frontend.components.OtpResendButton
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private val TukiCream: Color get() = com.example.frontend.ui.theme.TukiCream
private val TukiCream2: Color get() = com.example.frontend.ui.theme.TukiSky
private val TukiDark: Color get() = com.example.frontend.ui.theme.TukiInk
private val TukiGray: Color get() = com.example.frontend.ui.theme.TukiMuted
private val TukiTeal: Color get() = com.example.frontend.ui.theme.TukiTeal
private val TukiOrange: Color get() = com.example.frontend.ui.theme.TukiOrange
private val TukiError: Color get() = com.example.frontend.ui.theme.TukiDanger

private enum class ChangePasswordStage {
    CURRENT_PASSWORD,
    OTP,
    NEW_PASSWORD
}

@Composable
fun ChangePasswordScreen(
    onBack: () -> Unit = {},
    onPasswordChanged: () -> Unit = {}
) {
    val context = LocalContext.current.applicationContext
    val authRepository = remember(context) { TukiDataProvider(context).authRepository }
    val coroutineScope = rememberCoroutineScope()

    var stage by remember { mutableStateOf(ChangePasswordStage.CURRENT_PASSWORD) }
    var currentPassword by remember { mutableStateOf("") }
    var otpCode by remember { mutableStateOf("") }
    var newPassword by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var otpSendGeneration by remember { mutableStateOf(0) }

    var currentPasswordVisible by remember { mutableStateOf(false) }
    var newPasswordVisible by remember { mutableStateOf(false) }
    var confirmPasswordVisible by remember { mutableStateOf(false) }

    var isWorking by remember { mutableStateOf(false) }
    var isSuccess by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var infoMessage by remember { mutableStateOf<String?>(null) }

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
            when (val result = authRepository.requestChangePasswordOtp(currentPassword)) {
                is ApiResult.Success -> {
                    stage = ChangePasswordStage.OTP
                    otpCode = ""
                    otpSendGeneration += 1
                    infoMessage = "We've sent an 8-digit OTP to your account email."
                }
                is ApiResult.Failure -> errorMessage = result.message
            }
            isWorking = false
        }
    }

    fun verifyOtp() {
        if (isWorking || isSuccess) return
        if (otpCode.length != 8) {
            errorMessage = "Enter the complete 8-digit code."
            return
        }

        coroutineScope.launch {
            isWorking = true
            errorMessage = null
            infoMessage = null
            when (val result = authRepository.verifyChangePasswordOtp(currentPassword, otpCode)) {
                is ApiResult.Success -> stage = ChangePasswordStage.NEW_PASSWORD
                is ApiResult.Failure -> errorMessage = result.message
            }
            isWorking = false
        }
    }

    fun submitChange() {
        if (isWorking || isSuccess) return
        when {
            newPassword.length < 8 -> errorMessage = "New password must be at least 8 characters."
            newPassword == currentPassword -> errorMessage = "New password must be different from your current password."
            newPassword != confirmPassword -> errorMessage = "New password and confirmation do not match."
            else -> coroutineScope.launch {
                isWorking = true
                errorMessage = null
                infoMessage = null
                when (val result = authRepository.changePassword(currentPassword, otpCode, newPassword)) {
                    is ApiResult.Success -> {
                        isSuccess = true
                        infoMessage = "Password changed successfully."
                        currentPassword = ""
                        otpCode = ""
                        newPassword = ""
                        confirmPassword = ""
                        delay(800)
                        onPasswordChanged()
                    }
                    is ApiResult.Failure -> errorMessage = result.message
                }
                isWorking = false
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
                    .clickable(enabled = !isWorking) {
                        when (stage) {
                            ChangePasswordStage.CURRENT_PASSWORD -> onBack()
                            ChangePasswordStage.OTP -> {
                                stage = ChangePasswordStage.CURRENT_PASSWORD
                                errorMessage = null
                                infoMessage = null
                            }
                            ChangePasswordStage.NEW_PASSWORD -> {
                                stage = ChangePasswordStage.OTP
                                errorMessage = null
                                infoMessage = null
                            }
                        }
                    },
                contentAlignment = Alignment.Center
            ) {
                Text(text = "‹", color = TukiDark, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            }
            Spacer(modifier = Modifier.width(14.dp))
            Text(
                text = when (stage) {
                    ChangePasswordStage.CURRENT_PASSWORD -> "Change password"
                    ChangePasswordStage.OTP -> "Check your email"
                    ChangePasswordStage.NEW_PASSWORD -> "New password"
                },
                color = TukiDark,
                fontSize = 22.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        Spacer(modifier = Modifier.height(12.dp))
        Text(
            text = when (stage) {
                ChangePasswordStage.CURRENT_PASSWORD -> "Confirm your current password first. We'll send an OTP to your account email."
                ChangePasswordStage.OTP -> "We've sent an 8-digit OTP to the email on your TUKI account."
                ChangePasswordStage.NEW_PASSWORD -> "OTP verified. You can now choose your new password."
            },
            color = TukiGray,
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold
        )

        Spacer(modifier = Modifier.height(28.dp))

        when (stage) {
            ChangePasswordStage.CURRENT_PASSWORD -> PasswordField(
                label = "Current password",
                value = currentPassword,
                visible = currentPasswordVisible,
                enabled = !isWorking && !isSuccess,
                onValueChange = { currentPassword = it; errorMessage = null },
                onVisibilityToggle = { currentPasswordVisible = !currentPasswordVisible }
            )

            ChangePasswordStage.OTP -> {
                OtpCodeField(
                    code = otpCode,
                    onCodeChange = { otpCode = it; errorMessage = null },
                    enabled = !isWorking && !isSuccess
                )
                Spacer(modifier = Modifier.height(8.dp))
                OtpResendButton(sendGeneration = otpSendGeneration, enabled = !isWorking && !isSuccess, onResend = { requestOtp() })
            }

            ChangePasswordStage.NEW_PASSWORD -> {
                PasswordField(
                    label = "New password",
                    value = newPassword,
                    visible = newPasswordVisible,
                    enabled = !isWorking && !isSuccess,
                    onValueChange = { newPassword = it; errorMessage = null },
                    onVisibilityToggle = { newPasswordVisible = !newPasswordVisible }
                )
                Spacer(modifier = Modifier.height(18.dp))
                PasswordField(
                    label = "Confirm new password",
                    value = confirmPassword,
                    visible = confirmPasswordVisible,
                    enabled = !isWorking && !isSuccess,
                    onValueChange = { confirmPassword = it; errorMessage = null },
                    onVisibilityToggle = { confirmPasswordVisible = !confirmPasswordVisible }
                )
            }
        }

        errorMessage?.let { message ->
            Spacer(modifier = Modifier.height(16.dp))
            Text(message, color = TukiError, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
        }
        infoMessage?.let { message ->
            Spacer(modifier = Modifier.height(16.dp))
            Text(message, color = TukiTeal, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
        }

        Spacer(modifier = Modifier.height(28.dp))
        Button(
            onClick = {
                when (stage) {
                    ChangePasswordStage.CURRENT_PASSWORD -> requestOtp()
                    ChangePasswordStage.OTP -> verifyOtp()
                    ChangePasswordStage.NEW_PASSWORD -> submitChange()
                }
            },
            modifier = Modifier.fillMaxWidth().height(56.dp),
            enabled = !isWorking && !isSuccess,
            shape = RoundedCornerShape(16.dp),
            colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
        ) {
            if (isWorking) {
                CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp, color = Color.White)
            } else {
                Text(
                    text = when (stage) {
                        ChangePasswordStage.CURRENT_PASSWORD -> "Send OTP"
                        ChangePasswordStage.OTP -> "Verify OTP"
                        ChangePasswordStage.NEW_PASSWORD -> "Change password"
                    },
                    color = Color.White,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold
                )
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
                    modifier = Modifier.padding(end = 12.dp).clickable(enabled = enabled, onClick = onVisibilityToggle)
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
