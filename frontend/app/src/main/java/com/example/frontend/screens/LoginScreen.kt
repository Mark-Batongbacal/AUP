package com.example.frontend.screens

import androidx.compose.foundation.BorderStroke
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
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
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.auth.AuthRepository
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiOutline
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.launch

@Composable
fun LoginScreen(
    authRepository: AuthRepository,
    onBack: () -> Unit = {},
    onSignUpClick: () -> Unit = {},
    onLoginSuccess: () -> Unit = {},
    onForgotPasswordClick: () -> Unit = {},
    onGuestLoginClick: () -> Unit = {},
    onPasswordLoginClick: suspend (String, String) -> LoginActionResult = { _, _ ->
        LoginActionResult.Error("Password login is not configured.")
    },
    onGoogleLoginClick: suspend () -> LoginActionResult = {
        LoginActionResult.Error("Google login is not configured.")
    },
    onFacebookLoginClick: suspend () -> LoginActionResult = {
        LoginActionResult.Error("Facebook login is not configured.")
    },
) {
    val coroutineScope = rememberCoroutineScope()

    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var passwordVisible by remember { mutableStateOf(false) }
    var isPasswordLoggingIn by remember { mutableStateOf(false) }
    var isGoogleLoggingIn by remember { mutableStateOf(false) }
    var isFacebookLoggingIn by remember { mutableStateOf(false) }
    var isGuestLoggingIn by remember { mutableStateOf(false) }
    var showGuestAccessNotice by remember { mutableStateOf(false) }
    var loginError by remember { mutableStateOf<String?>(null) }

    val isLoginInProgress =
        isPasswordLoggingIn || isGoogleLoggingIn || isFacebookLoggingIn || isGuestLoggingIn

    fun handleResult(result: LoginActionResult) {
        when (result) {
            LoginActionResult.Success -> onLoginSuccess()
            LoginActionResult.Canceled -> Unit
            is LoginActionResult.Error -> loginError = result.message
        }
    }

    if (showGuestAccessNotice) {
        AlertDialog(
            onDismissRequest = {},
            title = { Text("Guest access is active") },
            text = {
                Text(
                    "You can use TUKI for 24 hours, including navigation, history, and favorites. " +
                        "Create an account if you want access without the guest time limit."
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        showGuestAccessNotice = false
                        onGuestLoginClick()
                    }
                ) { Text("Continue") }
            }
        )
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .background(TukiCream)
            .statusBarsPadding()
            .padding(start = 34.dp, end = 34.dp, top = 12.dp, bottom = 15.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.Center
        ) {
            Image(
                painter = painterResource(R.drawable.tuki_logo),
                contentDescription = "TUKI logo",
                modifier = Modifier.size(50.dp),
                contentScale = ContentScale.Fit
            )
            Spacer(modifier = Modifier.width(10.dp))
            Text(text = "TUKI.", color = TukiTeal, style = MaterialTheme.typography.displaySmall)
        }

        Spacer(modifier = Modifier.height(20.dp))

        Text(text = "Welcome back", color = TukiInk, style = MaterialTheme.typography.displaySmall)
        Spacer(modifier = Modifier.height(4.dp))
        Text(text = "Log in to continue your commute", color = TukiMuted, style = MaterialTheme.typography.bodyLarge)
        Spacer(modifier = Modifier.height(25.dp))

        Column(modifier = Modifier.fillMaxWidth()) {
            Text(text = "Email", color = TukiInk, style = MaterialTheme.typography.titleMedium)
            Spacer(modifier = Modifier.height(8.dp))
            TextField(
                value = email,
                onValueChange = { email = it; loginError = null },
                modifier = Modifier.fillMaxWidth().height(60.dp),
                enabled = !isLoginInProgress,
                singleLine = true,
                shape = RoundedCornerShape(15.dp),
                colors = loginFieldColors(),
                textStyle = MaterialTheme.typography.bodyLarge
            )

            Spacer(modifier = Modifier.height(10.dp))
            Text(text = "Password", color = TukiInk, style = MaterialTheme.typography.titleMedium)
            Spacer(modifier = Modifier.height(8.dp))
            TextField(
                value = password,
                onValueChange = { password = it; loginError = null },
                modifier = Modifier.fillMaxWidth().height(60.dp),
                enabled = !isLoginInProgress,
                singleLine = true,
                shape = RoundedCornerShape(15.dp),
                visualTransformation = if (passwordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                trailingIcon = {
                    Text(
                        text = if (passwordVisible) "HIDE" else "SHOW",
                        color = TukiTeal,
                        style = MaterialTheme.typography.labelLarge,
                        modifier = Modifier.padding(end = 14.dp).clickable(enabled = !isLoginInProgress) {
                            passwordVisible = !passwordVisible
                        }
                    )
                },
                colors = loginFieldColors(),
                textStyle = MaterialTheme.typography.bodyLarge
            )

            Spacer(modifier = Modifier.height(6.dp))
            Text(
                text = "Forgot password?",
                modifier = Modifier.align(Alignment.End).clickable(enabled = !isLoginInProgress) { onForgotPasswordClick() },
                color = TukiTeal,
                style = MaterialTheme.typography.labelLarge
            )
        }

        loginError?.let { message ->
            Spacer(modifier = Modifier.height(12.dp))
            Text(text = message, color = TukiDanger, style = MaterialTheme.typography.labelLarge)
        }

        Spacer(modifier = Modifier.height(20.dp))
        Button(
            onClick = {
                if (isLoginInProgress) return@Button
                val normalizedEmail = email.trim()
                when {
                    normalizedEmail.isBlank() -> loginError = "Enter your email address."
                    password.isBlank() -> loginError = "Enter your password."
                    password.length < 8 -> loginError = "Password must be at least 8 characters."
                    else -> coroutineScope.launch {
                        loginError = null
                        isPasswordLoggingIn = true
                        try { handleResult(onPasswordLoginClick(normalizedEmail, password)) }
                        finally { isPasswordLoggingIn = false }
                    }
                }
            },
            modifier = Modifier.fillMaxWidth().height(60.dp),
            enabled = !isLoginInProgress,
            shape = RoundedCornerShape(22.dp),
            colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
        ) {
            if (isPasswordLoggingIn) CircularProgressIndicator(color = Color.White, modifier = Modifier.size(24.dp))
            else Text(text = "Log in", style = MaterialTheme.typography.titleLarge)
        }

        Spacer(modifier = Modifier.height(15.dp))
        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Box(modifier = Modifier.weight(1f).height(1.dp).background(TukiOutline))
            Text(text = "OR", modifier = Modifier.padding(horizontal = 14.dp), color = TukiMuted, fontSize = 15.sp, fontWeight = FontWeight.Bold)
            Box(modifier = Modifier.weight(1f).height(1.dp).background(TukiOutline))
        }

        Spacer(modifier = Modifier.height(15.dp))
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            OutlinedButton(
                onClick = {
                    if (!isLoginInProgress) coroutineScope.launch {
                        loginError = null
                        isGoogleLoggingIn = true
                        try { handleResult(onGoogleLoginClick()) }
                        finally { isGoogleLoggingIn = false }
                    }
                },
                modifier = Modifier.weight(1f).height(56.dp),
                enabled = !isLoginInProgress,
                shape = RoundedCornerShape(16.dp),
                border = BorderStroke(1.dp, TukiOutline),
                colors = ButtonDefaults.outlinedButtonColors(containerColor = TukiSurfaceRaised, contentColor = TukiInk),
                contentPadding = PaddingValues(0.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Image(painter = painterResource(R.drawable.google_logo), contentDescription = "Google", modifier = Modifier.size(20.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(text = if (isGoogleLoggingIn) "..." else "Google", color = TukiInk, style = MaterialTheme.typography.labelLarge)
                }
            }

            OutlinedButton(
                onClick = {
                    if (!isLoginInProgress) coroutineScope.launch {
                        loginError = null
                        isFacebookLoggingIn = true
                        try { handleResult(onFacebookLoginClick()) }
                        finally { isFacebookLoggingIn = false }
                    }
                },
                modifier = Modifier.weight(1f).height(56.dp),
                enabled = !isLoginInProgress,
                shape = RoundedCornerShape(16.dp),
                border = BorderStroke(1.dp, TukiOutline),
                colors = ButtonDefaults.outlinedButtonColors(containerColor = TukiSurfaceRaised, contentColor = TukiInk),
                contentPadding = PaddingValues(0.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Image(painter = painterResource(R.drawable.facebook_logo), contentDescription = "Facebook", modifier = Modifier.size(20.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(text = if (isFacebookLoggingIn) "..." else "Facebook", color = TukiInk, style = MaterialTheme.typography.labelLarge)
                }
            }
        }

        Spacer(modifier = Modifier.height(12.dp))
        OutlinedButton(
            onClick = {
                if (isLoginInProgress) return@OutlinedButton
                coroutineScope.launch {
                    loginError = null
                    isGuestLoggingIn = true
                    try {
                        when (val result = authRepository.loginAsGuest()) {
                            is ApiResult.Success -> showGuestAccessNotice = true
                            is ApiResult.Failure -> loginError = result.message
                        }
                    } finally { isGuestLoggingIn = false }
                }
            },
            modifier = Modifier.fillMaxWidth().height(56.dp),
            enabled = !isLoginInProgress,
            shape = RoundedCornerShape(16.dp),
            border = BorderStroke(1.dp, TukiOutline),
            colors = ButtonDefaults.outlinedButtonColors(containerColor = TukiSurfaceRaised, contentColor = TukiInk)
        ) {
            if (isGuestLoggingIn) CircularProgressIndicator(modifier = Modifier.size(22.dp), color = TukiTeal)
            else Text(text = "Continue as Guest", color = TukiInk, style = MaterialTheme.typography.labelLarge)
        }

        Spacer(modifier = Modifier.height(8.dp))
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(text = "New to Tuki? ", color = TukiMuted, style = MaterialTheme.typography.bodyLarge)
            Text(text = "Sign up", color = TukiOrange, style = MaterialTheme.typography.labelLarge, modifier = Modifier.clickable(enabled = !isLoginInProgress) { onSignUpClick() })
        }
    }
}

@Composable
private fun loginFieldColors() = TextFieldDefaults.colors(
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

sealed interface LoginActionResult {
    data object Success : LoginActionResult
    data object Canceled : LoginActionResult
    data class Error(val message: String) : LoginActionResult
}
