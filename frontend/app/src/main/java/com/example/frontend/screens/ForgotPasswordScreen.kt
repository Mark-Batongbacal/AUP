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
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R
import com.example.frontend.components.OtpCodeField
import com.example.frontend.components.OtpResendButton
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

import androidx.compose.material3.MaterialTheme
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiSurfaceRaised

private enum class ForgotPasswordStage {
    EMAIL,
    OTP,
    PASSWORD
}

@Composable
fun ForgotPasswordScreen(
    onBack: () -> Unit = {},
    onResetSent: () -> Unit = {}
) {
    val context = LocalContext.current.applicationContext
    val authRepository = remember(context) { TukiDataProvider(context).authRepository }
    val coroutineScope = rememberCoroutineScope()

    var stage by remember { mutableStateOf(ForgotPasswordStage.EMAIL) }
    var email by remember { mutableStateOf("") }
    var code by remember { mutableStateOf("") }
    var newPassword by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var otpSendGeneration by remember { mutableStateOf(0) }
    var isWorking by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var info by remember { mutableStateOf<String?>(null) }

    fun requestCode() {
        if (isWorking) return
        val normalizedEmail = email.trim()
        if (normalizedEmail.isBlank() || !normalizedEmail.contains("@")) {
            error = "Enter a valid email address."
            return
        }

        coroutineScope.launch {
            isWorking = true
            error = null
            info = null
            when (val result = authRepository.requestPasswordReset(normalizedEmail)) {
                is ApiResult.Success -> {
                    stage = ForgotPasswordStage.OTP
                    code = ""
                    otpSendGeneration += 1
                    info = "We sent an 8-digit code to $normalizedEmail."
                }
                is ApiResult.Failure -> error = result.message
            }
            isWorking = false
        }
    }

    fun verifyCode() {
        if (isWorking) return
        if (code.length != 8) {
            error = "Enter the complete 8-digit code."
            return
        }

        coroutineScope.launch {
            isWorking = true
            error = null
            info = null
            when (val result = authRepository.verifyPasswordResetOtp(email.trim(), code)) {
                is ApiResult.Success -> stage = ForgotPasswordStage.PASSWORD
                is ApiResult.Failure -> error = result.message
            }
            isWorking = false
        }
    }

    fun resetPassword() {
        if (isWorking) return
        when {
            newPassword.length < 8 -> error = "New password must be at least 8 characters."
            newPassword != confirmPassword -> error = "New password and confirmation do not match."
            else -> coroutineScope.launch {
                isWorking = true
                error = null
                info = null
                when (val result = authRepository.resetPassword(email.trim(), code, newPassword)) {
                    is ApiResult.Success -> {
                        info = "Password reset successfully."
                        delay(800)
                        onResetSent()
                    }
                    is ApiResult.Failure -> error = result.message
                }
                isWorking = false
            }
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .background(TukiCream)
            .statusBarsPadding()
            .padding(start = 34.dp, end = 34.dp, top = 12.dp, bottom = 28.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.Start
        ) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiSurfaceRaised, RoundedCornerShape(12.dp))
                    .clickable(enabled = !isWorking) {
                        when (stage) {
                            ForgotPasswordStage.EMAIL -> onBack()
                            ForgotPasswordStage.OTP -> {
                                stage = ForgotPasswordStage.EMAIL
                                error = null
                                info = null
                            }
                            ForgotPasswordStage.PASSWORD -> {
                                stage = ForgotPasswordStage.OTP
                                error = null
                                info = null
                            }
                        }
                    },
                contentAlignment = Alignment.Center
            ) {
                Text(text = "‹", color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
        }

        Spacer(modifier = Modifier.height(18.dp))
        Image(
            painter = painterResource(R.drawable.tuki_logo),
            contentDescription = "TUKI logo",
            modifier = Modifier.size(72.dp),
            contentScale = ContentScale.Fit
        )
        Text("TUKI.", color = TukiDeepTeal, style = MaterialTheme.typography.displaySmall)

        Spacer(modifier = Modifier.height(28.dp))
        Text(
            text = when (stage) {
                ForgotPasswordStage.EMAIL -> "Reset Password"
                ForgotPasswordStage.OTP -> "Check your email"
                ForgotPasswordStage.PASSWORD -> "Create new password"
            },
            color = TukiInk,
            style = MaterialTheme.typography.displaySmall
        )

        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = when (stage) {
                ForgotPasswordStage.EMAIL -> "Enter the email linked to your TUKI account."
                ForgotPasswordStage.OTP -> "We've sent an 8-digit OTP to ${email.trim()}."
                ForgotPasswordStage.PASSWORD -> "Your code is verified. Choose a new password."
            },
            color = TukiMuted,
            style = MaterialTheme.typography.bodyLarge,
            modifier = Modifier.padding(horizontal = 8.dp)
        )

        Spacer(modifier = Modifier.height(30.dp))

        when (stage) {
            ForgotPasswordStage.EMAIL -> ResetTextField(
                label = "Email",
                value = email,
                enabled = !isWorking,
                onValueChange = {
                    email = it
                    error = null
                }
            )

            ForgotPasswordStage.OTP -> {
                OtpCodeField(
                    code = code,
                    onCodeChange = {
                        code = it
                        error = null
                    },
                    enabled = !isWorking
                )
                Spacer(modifier = Modifier.height(10.dp))
                OtpResendButton(
                    sendGeneration = otpSendGeneration,
                    enabled = !isWorking,
                    onResend = { requestCode() }
                )
            }

            ForgotPasswordStage.PASSWORD -> {
                ResetTextField(
                    label = "New password",
                    value = newPassword,
                    enabled = !isWorking,
                    isPassword = true,
                    onValueChange = {
                        newPassword = it
                        error = null
                    }
                )
                Spacer(modifier = Modifier.height(18.dp))
                ResetTextField(
                    label = "Confirm new password",
                    value = confirmPassword,
                    enabled = !isWorking,
                    isPassword = true,
                    onValueChange = {
                        confirmPassword = it
                        error = null
                    }
                )
            }
        }

        error?.let { message ->
            Spacer(modifier = Modifier.height(14.dp))
            Text(message, color = TukiDanger, style = MaterialTheme.typography.labelLarge)
        }
        info?.let { message ->
            Spacer(modifier = Modifier.height(14.dp))
            Text(message, color = TukiTeal, style = MaterialTheme.typography.labelLarge)
        }

        Spacer(modifier = Modifier.height(26.dp))
        Button(
            onClick = {
                when (stage) {
                    ForgotPasswordStage.EMAIL -> requestCode()
                    ForgotPasswordStage.OTP -> verifyCode()
                    ForgotPasswordStage.PASSWORD -> resetPassword()
                }
            },
            modifier = Modifier.fillMaxWidth().height(58.dp),
            enabled = !isWorking,
            shape = RoundedCornerShape(18.dp),
            colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
        ) {
            if (isWorking) {
                CircularProgressIndicator(color = Color.White, modifier = Modifier.size(22.dp))
            } else {
                Text(
                    text = when (stage) {
                        ForgotPasswordStage.EMAIL -> "Send OTP"
                        ForgotPasswordStage.OTP -> "Verify OTP"
                        ForgotPasswordStage.PASSWORD -> "Reset Password"
                    },
                    style = MaterialTheme.typography.titleLarge
                )
            }
        }
    }
}

@Composable
private fun ResetTextField(
    label: String,
    value: String,
    enabled: Boolean,
    isPassword: Boolean = false,
    onValueChange: (String) -> Unit
) {
    Column(modifier = Modifier.fillMaxWidth()) {
        Text(text = label, color = TukiInk, style = MaterialTheme.typography.titleMedium)
        Spacer(modifier = Modifier.height(8.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(58.dp),
            enabled = enabled,
            singleLine = true,
            shape = RoundedCornerShape(15.dp),
            visualTransformation = if (isPassword) PasswordVisualTransformation() else androidx.compose.ui.text.input.VisualTransformation.None,
            colors = TextFieldDefaults.colors(
                focusedContainerColor = TukiSurfaceRaised,
                unfocusedContainerColor = TukiSurfaceRaised,
                disabledContainerColor = TukiSurfaceRaised.copy(alpha = 0.65f),
                focusedIndicatorColor = Color.Transparent,
                unfocusedIndicatorColor = Color.Transparent,
                disabledIndicatorColor = Color.Transparent,
                focusedTextColor = TukiInk,
                unfocusedTextColor = TukiInk
            ),
            textStyle = MaterialTheme.typography.bodyLarge
        )
    }
}
