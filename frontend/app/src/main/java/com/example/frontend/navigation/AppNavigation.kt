package com.example.frontend.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.SignupScreen

@Composable
fun AppNavigation() {
    val navController = rememberNavController()

    NavHost(
        navController = navController,
        startDestination = AppScreen.LOGIN.name
    ) {
        composable(route = AppScreen.LOGIN.name) {
            LoginScreen(
                onSignUpClick = {
                    navController.navigate(AppScreen.SIGNUP.name)
                }
            )
        }

        composable(route = AppScreen.SIGNUP.name) {
            SignupScreen(
                onLoginClick = {
                    navController.popBackStack()
                }
            )
        }
    }
}