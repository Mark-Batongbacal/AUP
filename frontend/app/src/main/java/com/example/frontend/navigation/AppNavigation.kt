package com.example.frontend.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.screens.HomeScreen
import com.example.frontend.screens.ProfileScreen
import com.example.frontend.screens.SettingsScreen
import com.example.frontend.screens.TripTrackingScreen
import com.example.frontend.screens.RouteResultsScreen
import com.example.frontend.repository.MockAuthRepository
import com.example.frontend.repository.MockRouteRepository
import com.example.frontend.repository.MockCommuteRepository
import com.example.frontend.TemporaryMapSamples
import androidx.compose.runtime.remember

@Composable
fun AppNavigation() {
    val navController = rememberNavController()
    val authRepository = remember { MockAuthRepository() }
    val routeRepository = remember { MockRouteRepository() }
    val commuteRepository = remember { MockCommuteRepository() }

    NavHost(
        navController = navController,
        startDestination = AppScreen.LOGIN.name
    ) {
        composable(route = AppScreen.LOGIN.name) {
            LoginScreen(
                authRepository = authRepository,
                onSignUpClick = {
                    navController.navigate(AppScreen.SIGNUP.name)
                },
                onLoginSuccess = {
                    navController.navigate(AppScreen.HOME.name)
                }
            )
        }

        composable(route = AppScreen.SIGNUP.name) {
            SignupScreen(
                authRepository = authRepository,
                onLoginClick = {
                    navController.popBackStack()
                },
                onLoginSuccess = {
                    navController.navigate(AppScreen.HOME.name)
                }
            )
        }

        composable(route = AppScreen.HOME.name) {
            HomeScreen(
                commuteRepository = commuteRepository,
                onSearchDestination = { origin, destination ->
                    navController.navigate("${AppScreen.ROUTE_RESULTS.name}/$origin/$destination")
                },
                onProfileClick = {
                    navController.navigate(AppScreen.PROFILE.name)
                }
            )
        }

        composable(route = AppScreen.PROFILE.name) {
            ProfileScreen(
                onBack = { navController.popBackStack() },
                onEditProfileClick = { navController.navigate(AppScreen.SETTINGS.name) }
            )
        }

        composable(route = AppScreen.SETTINGS.name) {
            SettingsScreen(
                onBack = { navController.popBackStack() },
                onLogoutClick = {
                    navController.navigate(AppScreen.LOGIN.name) {
                        popUpTo(0)
                    }
                }
            )
        }

        composable(route = "${AppScreen.ROUTE_RESULTS.name}/{origin}/{destination}") { backStackEntry ->
            val origin = backStackEntry.arguments?.getString("origin") ?: ""
            val destination = backStackEntry.arguments?.getString("destination") ?: ""
            RouteResultsScreen(
                origin = origin,
                destinationQuery = destination,
                routeRepository = routeRepository,
                onBack = { navController.popBackStack() },
                onRouteSelect = {
                    navController.navigate("${AppScreen.TRIP_TRACKING.name}/$origin/$destination")
                }
            )
        }

        composable(route = "${AppScreen.TRIP_TRACKING.name}/{origin}/{destination}") { backStackEntry ->
            val origin = backStackEntry.arguments?.getString("origin") ?: ""
            val destination = backStackEntry.arguments?.getString("destination") ?: ""
            TripTrackingScreen(
                origin = origin,
                destination = destination,
                routePoints = TemporaryMapSamples.routePoints,
                onBack = { navController.popBackStack() }
            )
        }
    }
}