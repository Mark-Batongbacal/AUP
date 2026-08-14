package com.example.frontend.previews

import androidx.compose.runtime.Composable
import androidx.compose.ui.tooling.preview.Preview
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.OnboardingScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.ui.theme.FrontendTheme

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Onboarding"
)
@Composable
fun OnboardingPreview() {
    FrontendTheme {
        OnboardingScreen(
            onLetsRideClick = {}
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Login"
)
@Composable
fun LoginPreview() {
    FrontendTheme {
        LoginScreen(
            onBack = {}
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "SignUp"
)
@Composable
fun SignUpPreview() {
    FrontendTheme {
        SignupScreen(
            onBack = {}
        )
    }
}