package com.example.frontend

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.*
import com.example.frontend.model.RecentCommute
import com.example.frontend.navigation.AppScreen
import com.example.frontend.screens.CommuteDetailScreen
import com.example.frontend.screens.HomeScreen
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.OnboardingScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.ui.theme.FrontendTheme
import com.example.frontend.screens.RouteResultsScreen

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

    var selectedCommute by remember {
        mutableStateOf<RecentCommute?>(null)
    }

    var searchOrigin by remember {
        mutableStateOf("")
    }

    var searchDestination by remember {
        mutableStateOf("")
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
                },
                onLoginSuccess = {
                    currentScreen = AppScreen.HOME
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
                },
                onLoginSuccess = {
                    currentScreen = AppScreen.HOME
                }
            )
        }

        AppScreen.HOME -> {
            HomeScreen(
                onSearchDestination = { origin, destination ->
                    searchOrigin = origin
                    searchDestination = destination
                    currentScreen = AppScreen.ROUTE_RESULTS
                },
                onCommuteClick = { commute ->
                    selectedCommute = commute
                    currentScreen = AppScreen.COMMUTE_DETAIL
                },
                onRecentClick = {},
                onFavoritesClick = {},
                onProfileClick = {},
                onNewHereClick = {}
            )
        }

        AppScreen.COMMUTE_DETAIL -> {
            selectedCommute?.let { commute ->
                CommuteDetailScreen(
                    commute = commute,
                    onBack = {
                        currentScreen = AppScreen.HOME
                    }
                )
            }
        }

        AppScreen.ROUTE_RESULTS -> {
            RouteResultsScreen(
                origin = searchOrigin,
                destinationQuery = searchDestination,
                onBack = {
                    currentScreen = AppScreen.HOME
                },
                onRouteSelect = { route ->
                    // TODO: once there's a "commute in progress" / tracking screen
                }
            )
        }
    }
}