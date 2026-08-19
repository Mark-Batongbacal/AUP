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
import com.example.frontend.data.auth.AuthRepository
import kotlinx.coroutines.launch

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiError = Color(0xFFB00020)

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
    var fullName by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var passwordVisible by remember { mutableStateOf(false) }
    var confirmPasswordVisible by remember { mutableStateOf(false) }
    var isSigningUp by remember { mutableStateOf(false) }
    var signUpError by remember { mutableStateOf<String?>(null) }

    val scrollState = rememberScrollState()

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.White)
            .systemBarsPadding()
            .verticalScroll(scrollState),
        contentAlignment = Alignment.Center
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 28.dp, vertical = 20.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Image(
                painter = painterResource(R.drawable.tuki_logo),
                contentDescription = "TUKI logo",
                modifier = Modifier.size(75.dp),
                contentScale = ContentScale.Fit
            )

            Spacer(modifier = Modifier.height(4.dp))

            Text(
                text = "TUKI.",
                color = TukiTeal,
                fontSize = 32.sp,
                fontWeight = FontWeight.ExtraBold
            )

            Spacer(modifier = Modifier.height(16.dp))

            Text(
                text = "Create an account",
                color = Color.Black,
                fontSize = 26.sp,
                fontWeight = FontWeight.ExtraBold
            )

            Spacer(modifier = Modifier.height(4.dp))

            Text(
                text = "Start your seamless commute today",
                color = TukiGray,
                fontSize = 16.sp,
                fontWeight = FontWeight.SemiBold
            )

            Spacer(modifier = Modifier.height(20.dp))

            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                SignUpTextField(
                    label = "Full Name",
                    value = fullName,
                    enabled = !isSigningUp,
                    onValueChange = {
                        fullName = it
                        signUpError = null
                    }
                )

                SignUpTextField(
                    label = "Email",
                    value = email,
                    enabled = !isSigningUp,
                    onValueChange = {
                        email = it
                        signUpError = null
                    }
                )

                PasswordField(
                    label = "Password",
                    value = password,
                    visible = passwordVisible,
                    enabled = !isSigningUp,
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
                    enabled = !isSigningUp,
                    onValueChange = {
                        confirmPassword = it
                        signUpError = null
                    },
                    onVisibilityToggle = { confirmPasswordVisible = !confirmPasswordVisible }
                )
            }

            signUpError?.let { message ->
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
                    if (isSigningUp) return@Button

                    val normalizedName = fullName.trim()
                    val normalizedEmail = email.trim()
                    when {
                        normalizedName.isBlank() -> signUpError = "Enter your full name."
                        normalizedName.split(Regex("\\s+")).size < 2 ->
                            signUpError = "Enter both your first and last name."
                        normalizedEmail.isBlank() -> signUpError = "Enter your email address."
                        !normalizedEmail.contains("@") -> signUpError = "Enter a valid email address."
                        password.length < 8 -> signUpError = "Password must be at least 8 characters."
                        password != confirmPassword -> signUpError = "Passwords do not match."
                        else -> coroutineScope.launch {
                            signUpError = null
                            isSigningUp = true
                            try {
                                when (val result = onSignUpClick(normalizedName, normalizedEmail, password)) {
                                    LoginActionResult.Success -> onLoginSuccess()
                                    LoginActionResult.Canceled -> Unit
                                    is LoginActionResult.Error -> signUpError = result.message
                                }
                            } finally {
                                isSigningUp = false
                            }
                        }
                    }
                },
                modifier = Modifier.fillMaxWidth().height(60.dp),
                enabled = !isSigningUp,
                shape = RoundedCornerShape(16.dp),
                colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
            ) {
                if (isSigningUp) {
                    CircularProgressIndicator(color = Color.White, modifier = Modifier.size(24.dp))
                } else {
                    Text(
                        text = "Sign up",
                        fontSize = 25.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = "Already have an account? ",
                    color = TukiGray,
                    fontSize = 17.sp,
                    fontWeight = FontWeight.Medium
                )

                Text(
                    text = "Log in",
                    color = TukiOrange,
                    fontSize = 17.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.clickable(enabled = !isSigningUp) { onLoginClick() }
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
        Text(
            text = label,
            color = Color.Black,
            fontSize = 14.sp,
            fontWeight = FontWeight.Medium
        )
        Spacer(modifier = Modifier.height(4.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(50.dp),
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
        Text(
            text = label,
            color = Color.Black,
            fontSize = 14.sp,
            fontWeight = FontWeight.Medium
        )
        Spacer(modifier = Modifier.height(4.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(50.dp),
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
