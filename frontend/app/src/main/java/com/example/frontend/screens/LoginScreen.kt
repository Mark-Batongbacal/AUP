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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.OutlinedButton
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
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.auth.AuthRepository
import kotlinx.coroutines.launch
import androidx.compose.material3.CircularProgressIndicator

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiError = Color(0xFFB00020)

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

    var isLoggingIn by remember {
        mutableStateOf(false)
    }
    var isGoogleLoggingIn by remember { mutableStateOf(false) }
    var isFacebookLoggingIn by remember { mutableStateOf(false) }
    var loginError by remember { mutableStateOf<String?>(null) }

    val isLoginInProgress = isPasswordLoggingIn || isGoogleLoggingIn || isFacebookLoggingIn

    fun handleResult(result: LoginActionResult) {
        when (result) {
            LoginActionResult.Success -> onLoginSuccess()
            LoginActionResult.Canceled -> Unit
            is LoginActionResult.Error -> loginError = result.message
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .background(Color.White)
            .padding(start = 34.dp, end = 34.dp, top = 25.dp, bottom = 15.dp),
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
            Text(
                text = "TUKI.",
                color = TukiTeal,
                fontSize = 30.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        Spacer(modifier = Modifier.height(20.dp))

        Text(
            text = "Welcome back",
            color = Color.Black,
            fontSize = 24.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(4.dp))

        Text(
            text = "Log in to continue your commute",
            color = TukiGray,
            fontSize = 16.sp,
            fontWeight = FontWeight.SemiBold
        )

        Spacer(modifier = Modifier.height(25.dp))

        Column(modifier = Modifier.fillMaxWidth()) {
            Text(text = "Email", color = Color.Black, fontSize = 18.sp)
            Spacer(modifier = Modifier.height(8.dp))

            TextField(
                value = email,
                onValueChange = {
                    email = it
                    loginError = null
                },
                modifier = Modifier.fillMaxWidth().height(60.dp),
                enabled = !isLoginInProgress,
                singleLine = true,
                shape = RoundedCornerShape(15.dp),
                colors = loginFieldColors()
            )

            Spacer(modifier = Modifier.height(10.dp))

            Text(text = "Password", color = Color.Black, fontSize = 18.sp)
            Spacer(modifier = Modifier.height(8.dp))

            TextField(
                value = password,
                onValueChange = {
                    password = it
                    loginError = null
                },
                modifier = Modifier.fillMaxWidth().height(60.dp),
                enabled = !isLoginInProgress,
                singleLine = true,
                shape = RoundedCornerShape(15.dp),
                visualTransformation = if (passwordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                trailingIcon = {
                    Text(
                        text = if (passwordVisible) "HIDE" else "SHOW",
                        color = TukiTeal,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier
                            .padding(end = 14.dp)
                            .clickable(enabled = !isLoginInProgress) {
                                passwordVisible = !passwordVisible
                            }
                    )
                },
                colors = loginFieldColors()
            )

            Spacer(modifier = Modifier.height(6.dp))

            Text(
                text = "Forgot password?",
                modifier = Modifier
                    .align(Alignment.End)
                    .clickable(enabled = !isLoginInProgress) { onForgotPasswordClick() },
                color = TukiTeal,
                fontSize = 17.sp,
                fontWeight = FontWeight.Bold
            )
        }

        loginError?.let { message ->
            Spacer(modifier = Modifier.height(12.dp))
            Text(
                text = message,
                color = TukiError,
                fontSize = 14.sp,
                fontWeight = FontWeight.SemiBold
            )
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
                        try {
                            handleResult(onPasswordLoginClick(normalizedEmail, password))
                        } finally {
                            isPasswordLoggingIn = false
                        }
                    }
                }
            },
            modifier = Modifier.fillMaxWidth().height(60.dp),
            enabled = !isLoginInProgress,
            shape = RoundedCornerShape(22.dp),
            colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
        ) {
            if (isLoggingIn) {
                CircularProgressIndicator(color = Color.White, modifier = Modifier.size(24.dp))
            } else {
                Text(
                    text = if (isPasswordLoggingIn) "Logging in..." else "Log in",
                    fontSize = 25.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }

        Spacer(modifier = Modifier.height(15.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(modifier = Modifier.weight(1f).height(1.dp).background(Color.LightGray))
            Text(
                text = "OR",
                modifier = Modifier.padding(horizontal = 14.dp),
                color = TukiGray,
                fontSize = 15.sp,
                fontWeight = FontWeight.Bold
            )
            Box(modifier = Modifier.weight(1f).height(1.dp).background(Color.LightGray))
        }

        Spacer(modifier = Modifier.height(15.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            OutlinedButton(
                onClick = {
                    if (!isLoginInProgress) {
                        coroutineScope.launch {
                            loginError = null
                            isGoogleLoggingIn = true
                            try {
                                handleResult(onGoogleLoginClick())
                            } finally {
                                isGoogleLoggingIn = false
                            }
                        }
                    }
                },
                modifier = Modifier.weight(1f).height(56.dp),
                enabled = !isLoginInProgress,
                shape = RoundedCornerShape(16.dp),
                border = BorderStroke(2.dp, Color(0xFFE8E8E8)),
                contentPadding = PaddingValues(0.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Image(
                        painter = painterResource(R.drawable.google_logo),
                        contentDescription = "Google",
                        modifier = Modifier.size(20.dp)
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = if (isGoogleLoggingIn) "..." else "Google",
                        color = TukiDark,
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }

            OutlinedButton(
                onClick = {
                    if (!isLoginInProgress) {
                        coroutineScope.launch {
                            loginError = null
                            isFacebookLoggingIn = true
                            try {
                                handleResult(onFacebookLoginClick())
                            } finally {
                                isFacebookLoggingIn = false
                            }
                        }
                    }
                },
                modifier = Modifier.weight(1f).height(56.dp),
                enabled = !isLoginInProgress,
                shape = RoundedCornerShape(16.dp),
                border = BorderStroke(2.dp, Color(0xFFE8E8E8)),
                contentPadding = PaddingValues(0.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Image(
                        painter = painterResource(R.drawable.facebook_logo),
                        contentDescription = "Facebook",
                        modifier = Modifier.size(20.dp)
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = if (isFacebookLoggingIn) "..." else "Facebook",
                        color = TukiDark,
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        OutlinedButton(
            onClick = onGuestLoginClick,
            modifier = Modifier
                .fillMaxWidth()
                .height(56.dp),
            enabled = !isLoginInProgress,
            shape = RoundedCornerShape(16.dp),
            border = BorderStroke(2.dp, Color(0xFFE8E8E8))
        ) {
            Text(
                text = "Continue as Guest",
                color = TukiDark,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
        }

        Spacer(modifier = Modifier.height(8.dp))

        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                text = "New to Tuki? ",
                color = TukiGray,
                fontSize = 17.sp,
                fontWeight = FontWeight.SemiBold
            )
            Text(
                text = "Sign up",
                color = TukiOrange,
                fontSize = 17.sp,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.clickable(enabled = !isLoginInProgress) { onSignUpClick() }
            )
        }
    }
}

@Composable
private fun loginFieldColors() = TextFieldDefaults.colors(
    focusedContainerColor = TukiCream,
    unfocusedContainerColor = TukiCream,
    disabledContainerColor = TukiCream,
    focusedIndicatorColor = Color.Transparent,
    unfocusedIndicatorColor = Color.Transparent,
    disabledIndicatorColor = Color.Transparent
)

sealed interface LoginActionResult {
    data object Success : LoginActionResult
    data object Canceled : LoginActionResult
    data class Error(val message: String) : LoginActionResult
}
