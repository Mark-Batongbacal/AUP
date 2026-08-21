package com.example.frontend.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
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
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiError = Color(0xFFB00020)

@Composable
fun ForgotPasswordScreen(
    onBack: () -> Unit = {},
    onResetSent: () -> Unit = {}
) {
    val context = LocalContext.current.applicationContext
    val authRepository = remember(context) { TukiDataProvider(context).authRepository }

    var email by remember { mutableStateOf("") }
    var code by remember { mutableStateOf("") }
    var newPassword by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var codeSent by remember { mutableStateOf(false) }
    var isWorking by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var successMessage by remember { mutableStateOf<String?>(null) }

    val coroutineScope = rememberCoroutineScope()

    fun requestCode() {
        if (isWorking) return
        if (email.isBlank() || !email.contains("@")) {
            error = "Enter a valid email address."
            return
        }

        coroutineScope.launch {
            isWorking = true
            error = null
            successMessage = null
            when (val result = authRepository.requestPasswordReset(email.trim())) {
                is ApiResult.Success -> {
                    codeSent = true
                    successMessage = "OTP sent. Check your email."
                }
                is ApiResult.Failure -> error = result.message
            }
            isWorking = false
        }
    }

    fun resetPassword() {
        if (isWorking) return
        when {
            code.isBlank() -> error = "Enter the OTP from your email."
            newPassword.length < 8 -> error = "New password must be at least 8 characters."
            newPassword != confirmPassword -> error = "New password and confirmation do not match."
            else -> {
                coroutineScope.launch {
                    isWorking = true
                    error = null
                    successMessage = null
                    when (val result = authRepository.resetPassword(email.trim(), code.trim(), newPassword)) {
                        is ApiResult.Success -> {
                            successMessage = "Password reset successfully."
                            delay(900)
                            onResetSent()
                        }
                        is ApiResult.Failure -> error = result.message
                    }
                    isWorking = false
                }
            }
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .background(Color.White)
            .padding(start = 34.dp, end = 34.dp, top = 35.dp, bottom = 28.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.Start
        ) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiCream, RoundedCornerShape(12.dp))
                    .clickable(enabled = !isWorking, onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(text = "\u2039", color = TukiDark, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            }
        }

        Spacer(modifier = Modifier.height(20.dp))

        Image(
            painter = painterResource(R.drawable.tuki_logo),
            contentDescription = "TUKI logo",
            modifier = Modifier.size(75.dp),
            contentScale = ContentScale.Fit
        )

        Text(
            text = "TUKI.",
            color = TukiTeal,
            fontSize = 34.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(30.dp))

        Text(
            text = if (codeSent) "Enter your OTP" else "Reset Password",
            color = Color.Black,
            fontSize = 26.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(8.dp))

        Text(
            text = if (codeSent) {
                "We sent a reset code to $email"
            } else {
                "Enter your email to receive a password reset code"
            },
            color = TukiGray,
            fontSize = 16.sp,
            fontWeight = FontWeight.SemiBold,
            modifier = Modifier.padding(horizontal = 10.dp),
            textAlign = androidx.compose.ui.text.style.TextAlign.Center
        )

        Spacer(modifier = Modifier.height(34.dp))

        if (!codeSent) {
            ResetTextField(
                label = "Email",
                value = email,
                enabled = !isWorking,
                onValueChange = {
                    email = it
                    error = null
                }
            )
        } else {
            ResetTextField(
                label = "OTP code",
                value = code,
                enabled = !isWorking,
                onValueChange = {
                    code = it.filter(Char::isDigit).take(10)
                    error = null
                }
            )

            Spacer(modifier = Modifier.height(18.dp))

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

        error?.let { message ->
            Spacer(modifier = Modifier.height(14.dp))
            Text(
                text = message,
                color = TukiError,
                fontSize = 14.sp,
                fontWeight = FontWeight.SemiBold
            )
        }

        successMessage?.let { message ->
            Spacer(modifier = Modifier.height(14.dp))
            Text(
                text = message,
                color = TukiTeal,
                fontSize = 14.sp,
                fontWeight = FontWeight.SemiBold
            )
        }

        Spacer(modifier = Modifier.height(30.dp))

        Button(
            onClick = { if (codeSent) resetPassword() else requestCode() },
            modifier = Modifier.fillMaxWidth().height(60.dp),
            enabled = !isWorking,
            shape = RoundedCornerShape(22.dp),
            colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
        ) {
            if (isWorking) {
                CircularProgressIndicator(color = Color.White, modifier = Modifier.size(24.dp))
            } else {
                Text(
                    text = if (codeSent) "Reset Password" else "Send OTP",
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }

        if (codeSent) {
            Spacer(modifier = Modifier.height(14.dp))
            TextButton(
                onClick = { requestCode() },
                enabled = !isWorking
            ) {
                Text("Resend OTP", color = TukiTeal, fontWeight = FontWeight.Bold)
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
        Text(text = label, color = Color.Black, fontSize = 16.sp, fontWeight = FontWeight.SemiBold)
        Spacer(modifier = Modifier.height(8.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(60.dp),
            enabled = enabled,
            singleLine = true,
            shape = RoundedCornerShape(15.dp),
            visualTransformation = if (isPassword) PasswordVisualTransformation() else androidx.compose.ui.text.input.VisualTransformation.None,
            colors = TextFieldDefaults.colors(
                focusedContainerColor = TukiCream,
                unfocusedContainerColor = TukiCream,
                disabledContainerColor = TukiCream,
                focusedIndicatorColor = Color.Transparent,
                unfocusedIndicatorColor = Color.Transparent,
                disabledIndicatorColor = Color.Transparent,
                focusedTextColor = TukiDark,
                unfocusedTextColor = TukiDark
            )
        )
    }
}
