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
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
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
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R
import com.example.frontend.components.OtpCodeField
import com.example.frontend.components.OtpResendButton
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.auth.AuthRepository
import com.example.frontend.data.auth.RegisterRequest
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiSurfaceRaised
import kotlinx.coroutines.launch

private enum class SignupStage {
    VERIFY_EMAIL,
    PASSWORD
}

@Composable
fun SignupScreen(
    authRepository: AuthRepository,
    onBack: () -> Unit = {},
    onLoginClick: () -> Unit = {},
    onLoginSuccess: () -> Unit = {},
    onSignUpClick: suspend (String, String, String) -> LoginActionResult = { _, _, _ ->
        LoginActionResult.Error("Sign up is not configured.")
    }
) {
    val coroutineScope = rememberCoroutineScope()
    var stage by remember { mutableStateOf(SignupStage.VERIFY_EMAIL) }
    var fullName by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var otpCode by remember { mutableStateOf("") }
    var otpSent by remember { mutableStateOf(false) }
    var otpSendGeneration by remember { mutableStateOf(0) }
    var password by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var passwordVisible by remember { mutableStateOf(false) }
    var confirmPasswordVisible by remember { mutableStateOf(false) }
    var isWorking by remember { mutableStateOf(false) }
    var signUpError by remember { mutableStateOf<String?>(null) }
    var infoMessage by remember { mutableStateOf<String?>(null) }

    @Suppress("UNUSED_VARIABLE")
    val compatibilityCallback = onSignUpClick

    fun normalizedDetails(): Pair<String, String>? {
        val normalizedName = fullName.trim()
        val normalizedEmail = email.trim()
        when {
            normalizedName.isBlank() -> signUpError = "Enter your full name."
            normalizedName.split(Regex("\\s+")).size < 2 -> signUpError = "Enter both your first and last name."
            normalizedEmail.isBlank() || !normalizedEmail.contains("@") -> signUpError = "Enter a valid email address."
            else -> return normalizedName to normalizedEmail
        }
        return null
    }

    fun requestOtp() {
        if (isWorking) return
        val details = normalizedDetails() ?: return
        coroutineScope.launch {
            isWorking = true
            signUpError = null
            infoMessage = null
            when (val result = authRepository.requestRegistrationOtp(details.second)) {
                is ApiResult.Success -> {
                    otpSent = true
                    otpCode = ""
                    otpSendGeneration += 1
                    infoMessage = "We've sent an 8-digit OTP to ${details.second}."
                }
                is ApiResult.Failure -> signUpError = result.message
            }
            isWorking = false
        }
    }

    fun verifyOtp() {
        if (isWorking) return
        val details = normalizedDetails() ?: return
        if (!otpSent || otpCode.length != 8) {
            signUpError = "Enter the complete 8-digit OTP."
            return
        }
        coroutineScope.launch {
            isWorking = true
            signUpError = null
            infoMessage = null
            when (val result = authRepository.verifyRegistrationOtp(details.second, otpCode)) {
                is ApiResult.Success -> stage = SignupStage.PASSWORD
                is ApiResult.Failure -> signUpError = result.message
            }
            isWorking = false
        }
    }

    fun completeRegistration() {
        if (isWorking) return
        val details = normalizedDetails() ?: return
        when {
            password.length < 8 -> signUpError = "Password must be at least 8 characters."
            password != confirmPassword -> signUpError = "Passwords do not match."
            else -> {
                val parts = details.first.split(Regex("\\s+"), limit = 2)
                coroutineScope.launch {
                    isWorking = true
                    signUpError = null
                    infoMessage = null
                    val request = RegisterRequest(
                        userName = details.second,
                        password = password,
                        firstName = parts[0],
                        lastName = parts[1],
                        verificationCode = otpCode
                    )
                    when (val result = authRepository.register(request)) {
                        is ApiResult.Success -> onLoginSuccess()
                        is ApiResult.Failure -> signUpError = result.message
                    }
                    isWorking = false
                }
            }
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .navigationBarsPadding()
            .verticalScroll(rememberScrollState()),
        contentAlignment = Alignment.Center
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(start = 28.dp, end = 28.dp, top = 12.dp, bottom = 20.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Row(modifier = Modifier.fillMaxWidth()) {
                Text(
                    text = "‹",
                    color = TukiTeal,
                    fontSize = 28.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.clickable(enabled = !isWorking) {
                        if (stage == SignupStage.PASSWORD) {
                            stage = SignupStage.VERIFY_EMAIL
                            signUpError = null
                            infoMessage = "Email already verified."
                        } else {
                            onBack()
                        }
                    }
                )
            }

            Image(
                painter = painterResource(R.drawable.tuki_logo),
                contentDescription = "TUKI logo",
                modifier = Modifier.size(72.dp),
                contentScale = ContentScale.Fit
            )
            Text(text = "TUKI.", color = TukiTeal, style = MaterialTheme.typography.displaySmall)

            Spacer(modifier = Modifier.height(16.dp))
            Text(
                text = if (stage == SignupStage.VERIFY_EMAIL) "Create an account" else "Create your password",
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
            Spacer(modifier = Modifier.height(5.dp))
            Text(
                text = if (stage == SignupStage.VERIFY_EMAIL) {
                    "Verify your email first, then we'll ask you to create a password."
                } else {
                    "Email verified. Finish setting up your TUKI account."
                },
                color = TukiMuted,
                style = MaterialTheme.typography.bodyLarge
            )

            Spacer(modifier = Modifier.height(24.dp))

            if (stage == SignupStage.VERIFY_EMAIL) {
                Column(modifier = Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    SignUpTextField(
                        label = "Full Name",
                        value = fullName,
                        enabled = !isWorking && !otpSent,
                        onValueChange = { fullName = it; signUpError = null }
                    )
                    SignUpTextField(
                        label = "Email",
                        value = email,
                        enabled = !isWorking && !otpSent,
                        onValueChange = { email = it; signUpError = null }
                    )
                    Text(
                        text = if (otpSent) "OTP sent. Enter the 8-digit code below." else "We'll send a one-time code to confirm this email.",
                        color = TukiMuted,
                        style = MaterialTheme.typography.bodySmall
                    )

                    if (otpSent) {
                        Spacer(modifier = Modifier.height(4.dp))
                        OtpCodeField(code = otpCode, onCodeChange = { otpCode = it; signUpError = null }, enabled = !isWorking)
                        OtpResendButton(sendGeneration = otpSendGeneration, enabled = !isWorking, onResend = { requestOtp() })
                    }
                }
            } else {
                Column(modifier = Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    SignupPasswordField(
                        label = "Password",
                        value = password,
                        visible = passwordVisible,
                        enabled = !isWorking,
                        onValueChange = { password = it; signUpError = null },
                        onVisibilityToggle = { passwordVisible = !passwordVisible }
                    )
                    SignupPasswordField(
                        label = "Confirm Password",
                        value = confirmPassword,
                        visible = confirmPasswordVisible,
                        enabled = !isWorking,
                        onValueChange = { confirmPassword = it; signUpError = null },
                        onVisibilityToggle = { confirmPasswordVisible = !confirmPasswordVisible }
                    )
                }
            }

            signUpError?.let { message ->
                Spacer(modifier = Modifier.height(12.dp))
                Text(message, color = TukiDanger, style = MaterialTheme.typography.labelLarge)
            }
            infoMessage?.let { message ->
                Spacer(modifier = Modifier.height(12.dp))
                Text(message, color = TukiTeal, style = MaterialTheme.typography.labelLarge)
            }

            Spacer(modifier = Modifier.height(20.dp))
            Button(
                onClick = {
                    if (stage == SignupStage.PASSWORD) completeRegistration()
                    else if (otpSent) verifyOtp()
                    else requestOtp()
                },
                modifier = Modifier.fillMaxWidth().height(58.dp),
                enabled = !isWorking,
                shape = RoundedCornerShape(16.dp),
                colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
            ) {
                if (isWorking) {
                    CircularProgressIndicator(color = Color.White, modifier = Modifier.size(22.dp))
                } else {
                    Text(
                        text = when {
                            stage == SignupStage.PASSWORD -> "Create account"
                            otpSent -> "Verify OTP"
                            else -> "Send OTP"
                        },
                        fontSize = 19.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }

            Spacer(modifier = Modifier.height(16.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Already have an account? ", color = TukiMuted, style = MaterialTheme.typography.bodyLarge)
                Text(
                    text = "Log in",
                    color = TukiOrange,
                    style = MaterialTheme.typography.labelLarge,
                    modifier = Modifier.clickable(enabled = !isWorking) { onLoginClick() }
                )
            }
        }
    }
}

@Composable
private fun SignUpTextField(
    label: String,
    value: String,
    enabled: Boolean,
    onValueChange: (String) -> Unit
) {
    Column {
        Text(label, color = TukiInk, style = MaterialTheme.typography.titleMedium)
        Spacer(modifier = Modifier.height(4.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(52.dp),
            enabled = enabled,
            singleLine = true,
            shape = RoundedCornerShape(14.dp),
            colors = signUpFieldColors(),
            textStyle = MaterialTheme.typography.bodyLarge
        )
    }
}

@Composable
private fun SignupPasswordField(
    label: String,
    value: String,
    visible: Boolean,
    enabled: Boolean,
    onValueChange: (String) -> Unit,
    onVisibilityToggle: () -> Unit
) {
    Column {
        Text(label, color = TukiInk, style = MaterialTheme.typography.titleMedium)
        Spacer(modifier = Modifier.height(4.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(52.dp),
            enabled = enabled,
            singleLine = true,
            shape = RoundedCornerShape(14.dp),
            visualTransformation = if (visible) VisualTransformation.None else PasswordVisualTransformation(),
            trailingIcon = {
                Text(
                    text = if (visible) "HIDE" else "SHOW",
                    color = TukiTeal,
                    style = MaterialTheme.typography.labelLarge,
                    modifier = Modifier.padding(end = 12.dp).clickable(enabled = enabled, onClick = onVisibilityToggle)
                )
            },
            colors = signUpFieldColors(),
            textStyle = MaterialTheme.typography.bodyLarge
        )
    }
}

@Composable
private fun signUpFieldColors() = TextFieldDefaults.colors(
    focusedContainerColor = TukiSurfaceRaised,
    unfocusedContainerColor = TukiSurfaceRaised,
    disabledContainerColor = TukiSurfaceRaised.copy(alpha = 0.7f),
    focusedIndicatorColor = Color.Transparent,
    unfocusedIndicatorColor = Color.Transparent,
    disabledIndicatorColor = Color.Transparent,
    focusedTextColor = TukiInk,
    unfocusedTextColor = TukiInk,
    disabledTextColor = TukiMuted,
    cursorColor = TukiTeal
)
