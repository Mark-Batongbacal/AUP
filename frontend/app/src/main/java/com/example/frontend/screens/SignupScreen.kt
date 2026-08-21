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
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
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
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R
import com.example.frontend.components.OtpCodeField
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.auth.AuthRepository
import com.example.frontend.data.auth.RegisterRequest
import kotlinx.coroutines.launch

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiError = Color(0xFFB00020)

private enum class SignupStage {
    DETAILS,
    OTP,
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
    var stage by remember { mutableStateOf(SignupStage.DETAILS) }
    var fullName by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var otpCode by remember { mutableStateOf("") }
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
                    stage = SignupStage.OTP
                    otpCode = ""
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
        if (otpCode.length != 8) {
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
            .background(Color.White)
            .systemBarsPadding()
            .verticalScroll(rememberScrollState()),
        contentAlignment = Alignment.Center
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 28.dp, vertical = 20.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Row(modifier = Modifier.fillMaxWidth()) {
                Text(
                    text = "‹",
                    color = TukiTeal,
                    fontSize = 28.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.clickable(enabled = !isWorking) {
                        when (stage) {
                            SignupStage.DETAILS -> onBack()
                            SignupStage.OTP -> {
                                stage = SignupStage.DETAILS
                                signUpError = null
                                infoMessage = null
                            }
                            SignupStage.PASSWORD -> {
                                stage = SignupStage.OTP
                                signUpError = null
                                infoMessage = null
                            }
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
            Text("TUKI.", color = TukiTeal, fontSize = 32.sp, fontWeight = FontWeight.ExtraBold)

            Spacer(modifier = Modifier.height(16.dp))
            Text(
                text = when (stage) {
                    SignupStage.DETAILS -> "Create an account"
                    SignupStage.OTP -> "Verify your email"
                    SignupStage.PASSWORD -> "Create your password"
                },
                color = Color.Black,
                fontSize = 25.sp,
                fontWeight = FontWeight.ExtraBold
            )
            Spacer(modifier = Modifier.height(5.dp))
            Text(
                text = when (stage) {
                    SignupStage.DETAILS -> "We'll verify your email before asking for a password."
                    SignupStage.OTP -> "Enter the 8-digit code we sent to ${email.trim()}."
                    SignupStage.PASSWORD -> "Email verified. Finish setting up your TUKI account."
                },
                color = TukiGray,
                fontSize = 14.sp,
                fontWeight = FontWeight.SemiBold
            )

            Spacer(modifier = Modifier.height(24.dp))

            when (stage) {
                SignupStage.DETAILS -> {
                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        SignUpTextField(
                            label = "Full Name",
                            value = fullName,
                            enabled = !isWorking,
                            onValueChange = {
                                fullName = it
                                signUpError = null
                            }
                        )
                        SignUpTextField(
                            label = "Email",
                            value = email,
                            enabled = !isWorking,
                            onValueChange = {
                                email = it
                                signUpError = null
                            }
                        )
                        Text(
                            text = "We'll send a one-time code to confirm this email.",
                            color = TukiGray,
                            fontSize = 12.sp
                        )
                    }
                }

                SignupStage.OTP -> {
                    OtpCodeField(
                        code = otpCode,
                        onCodeChange = {
                            otpCode = it
                            signUpError = null
                        },
                        enabled = !isWorking
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    TextButton(onClick = { requestOtp() }, enabled = !isWorking) {
                        Text("Resend OTP", color = TukiTeal, fontWeight = FontWeight.Bold)
                    }
                }

                SignupStage.PASSWORD -> {
                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        PasswordField(
                            label = "Password",
                            value = password,
                            visible = passwordVisible,
                            enabled = !isWorking,
                            onValueChange = {
                                password = it
                                signUpError = null
                            },
                            onVisibilityToggle = { passwordVisible = !passwordVisible }
                        )
                        PasswordField(
                            label = "Confirm Password",
                            value = confirmPassword,
                            visible = confirmPasswordVisible,
                            enabled = !isWorking,
                            onValueChange = {
                                confirmPassword = it
                                signUpError = null
                            },
                            onVisibilityToggle = { confirmPasswordVisible = !confirmPasswordVisible }
                        )
                    }
                }
            }

            signUpError?.let { message ->
                Spacer(modifier = Modifier.height(12.dp))
                Text(message, color = TukiError, fontSize = 14.sp, fontWeight = FontWeight.SemiBold)
            }
            infoMessage?.let { message ->
                Spacer(modifier = Modifier.height(12.dp))
                Text(message, color = TukiTeal, fontSize = 14.sp, fontWeight = FontWeight.SemiBold)
            }

            Spacer(modifier = Modifier.height(20.dp))
            Button(
                onClick = {
                    when (stage) {
                        SignupStage.DETAILS -> requestOtp()
                        SignupStage.OTP -> verifyOtp()
                        SignupStage.PASSWORD -> completeRegistration()
                    }
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
                        text = when (stage) {
                            SignupStage.DETAILS -> "Send OTP"
                            SignupStage.OTP -> "Verify OTP"
                            SignupStage.PASSWORD -> "Create account"
                        },
                        fontSize = 19.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }

            Spacer(modifier = Modifier.height(16.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Already have an account? ", color = TukiGray, fontSize = 16.sp)
                Text(
                    text = "Log in",
                    color = TukiOrange,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold,
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
        Text(label, color = Color.Black, fontSize = 14.sp, fontWeight = FontWeight.Medium)
        Spacer(modifier = Modifier.height(4.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(52.dp),
            enabled = enabled,
            singleLine = true,
            shape = RoundedCornerShape(14.dp),
            colors = signUpFieldColors()
        )
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
        Text(label, color = Color.Black, fontSize = 14.sp, fontWeight = FontWeight.Medium)
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
                    fontSize = 11.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.padding(end = 12.dp).clickable(enabled = enabled, onClick = onVisibilityToggle)
                )
            },
            colors = signUpFieldColors()
        )
    }
}

@Composable
private fun signUpFieldColors() = TextFieldDefaults.colors(
    focusedContainerColor = TukiCream,
    unfocusedContainerColor = TukiCream,
    disabledContainerColor = TukiCream,
    focusedIndicatorColor = Color.Transparent,
    unfocusedIndicatorColor = Color.Transparent,
    disabledIndicatorColor = Color.Transparent
)
