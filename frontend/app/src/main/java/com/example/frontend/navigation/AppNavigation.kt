package com.example.frontend.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.repository.MockAuthRepository
import androidx.compose.runtime.remember

@Composable
fun AppNavigation() {
    val navController = rememberNavController()
    val authRepository = remember { MockAuthRepository() }

    NavHost(
        navController = navController,
        startDestination = AppScreen.LOGIN.name
    ) {
        composable(route = AppScreen.LOGIN.name) {
            LoginScreen(
                authRepository = authRepository,
                onSignUpClick = {
                    navController.navigate(AppScreen.SIGNUP.name)
                }
            )
        }

        composable(route = AppScreen.SIGNUP.name) {
            SignupScreen(
                authRepository = authRepository,
                onLoginClick = {
                    navController.popBackStack()
                }
            )
        }
    }
}