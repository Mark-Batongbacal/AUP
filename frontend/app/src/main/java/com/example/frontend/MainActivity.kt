package com.example.frontend

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.*
import com.example.frontend.navigation.AppScreen
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.OnboardingScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.ui.theme.FrontendTheme

class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            FrontendTheme {
                TukiApp()
            }
        }
    }
}

@Composable
fun TukiApp() {
    var currentScreen by remember {
        mutableStateOf(AppScreen.ONBOARDING)
    }

    when (currentScreen) {
        AppScreen.ONBOARDING -> {
            OnboardingScreen(
                onLetsRideClick = {
                    currentScreen = AppScreen.LOGIN
                }
            )
        }

        AppScreen.LOGIN -> {
            LoginScreen(
                onBack = {
                    currentScreen = AppScreen.ONBOARDING
                },
                onSignUpClick = {
                    currentScreen = AppScreen.SIGNUP
                }
            )
        }

        AppScreen.SIGNUP -> {
            SignupScreen(
                onBack = {
                    currentScreen = AppScreen.LOGIN
                },
                onLoginClick = {
                    currentScreen = AppScreen.LOGIN
                }
            )
        }
    }
}