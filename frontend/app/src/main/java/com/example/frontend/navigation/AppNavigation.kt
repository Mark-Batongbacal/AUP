package com.example.frontend.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.credentials.CredentialManager
import com.example.frontend.R
import com.example.frontend.auth.*
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.findActivity
import com.example.frontend.screens.*
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.TemporaryMapSamples
import androidx.compose.runtime.remember

import com.example.frontend.data.auth.RegisterRequest

@Composable
fun AppNavigation(
    dataProvider: TukiDataProvider,
    facebookSignInClient: FacebookSignInClient
) {
    val context = LocalContext.current
    val activity = context.findActivity()
    val googleServerClientId = stringResource(R.string.google_server_client_id)
    val facebookAppId = stringResource(R.string.facebook_app_id)
    val facebookClientToken = stringResource(R.string.facebook_client_token)

    val googleSignInClient = remember {
        GoogleSignInClient(CredentialManager.create(context))
    }

    val navController = rememberNavController()
    val authRepository = dataProvider.authRepository
    val routingRepository = dataProvider.routingRepository
    val tripRepository = dataProvider.tripRepository

    NavHost(
        navController = navController,
        startDestination = AppScreen.ONBOARDING.name
    ) {
        composable(route = AppScreen.ONBOARDING.name) {
            OnboardingScreen(
                onLetsRideClick = {
                    navController.navigate(AppScreen.LOGIN.name)
                }
            )
        }

        composable(route = AppScreen.LOGIN.name) {
            LoginScreen(
                authRepository = authRepository,
                onBack = {
                    navController.popBackStack()
                },
                onSignUpClick = {
                    navController.navigate(AppScreen.SIGNUP.name)
                },
                onLoginSuccess = {
                    navController.navigate(AppScreen.HOME.name) {
                        popUpTo(AppScreen.LOGIN.name) { inclusive = true }
                    }
                },
                onForgotPasswordClick = {
                    navController.navigate(AppScreen.FORGOT_PASSWORD.name)
                },
                onGuestLoginClick = {
                    navController.navigate(AppScreen.HOME.name) {
                        popUpTo(AppScreen.LOGIN.name) { inclusive = true }
                    }
                },
                onPasswordLoginClick = { email, password ->
                    when (val authResult = authRepository.login(email, password)) {
                        is ApiResult.Success -> LoginActionResult.Success
                        is ApiResult.Failure -> LoginActionResult.Error(authResult.message)
                    }
                },
                onGoogleLoginClick = {
                    if (activity == null) {
                        LoginActionResult.Error("Google sign-in is unavailable right now.")
                    } else {
                        when (
                            val googleResult = googleSignInClient.getIdToken(
                                activity = activity,
                                serverClientId = googleServerClientId
                            )
                        ) {
                            is GoogleSignInResult.Success -> {
                                when (val authResult = authRepository.loginWithGoogle(googleResult.idToken)) {
                                    is ApiResult.Success -> LoginActionResult.Success
                                    is ApiResult.Failure -> LoginActionResult.Error(authResult.message)
                                }
                            }

                            is GoogleSignInResult.Failure -> {
                                LoginActionResult.Error(googleResult.message)
                            }
                        }
                    }
                },
                onFacebookLoginClick = {
                    if (activity == null) {
                        LoginActionResult.Error("Facebook sign-in is unavailable right now.")
                    } else {
                        when (
                            val facebookResult = facebookSignInClient.getAccessToken(
                                activity = activity,
                                appId = facebookAppId,
                                clientToken = facebookClientToken
                            )
                        ) {
                            is FacebookSignInResult.Success -> {
                                when (
                                    val authResult = authRepository.loginWithFacebook(
                                        facebookResult.accessToken
                                    )
                                ) {
                                    is ApiResult.Success -> LoginActionResult.Success
                                    is ApiResult.Failure -> LoginActionResult.Error(authResult.message)
                                }
                            }

                            FacebookSignInResult.Canceled -> {
                                LoginActionResult.Canceled
                            }

                            is FacebookSignInResult.Failure -> {
                                LoginActionResult.Error(facebookResult.message)
                            }
                        }
                    }
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
                    navController.navigate(AppScreen.HOME.name) {
                        popUpTo(AppScreen.SIGNUP.name) { inclusive = true }
                    }
                },
                onSignUpClick = { fullName, email, password ->
                    val nameParts = fullName.trim().split(Regex("\\s+"), limit = 2)
                    if (nameParts.size < 2) {
                        LoginActionResult.Error("Enter both your first and last name.")
                    } else {
                        val result = authRepository.register(
                            RegisterRequest(
                                userName = email,
                                password = password,
                                firstName = nameParts[0],
                                lastName = nameParts[1]
                            )
                        )
                        when (result) {
                            is ApiResult.Success -> LoginActionResult.Success
                            is ApiResult.Failure -> LoginActionResult.Error(result.message)
                        }
                    }
                }
            )
        }

        composable(route = AppScreen.FORGOT_PASSWORD.name) {
            ForgotPasswordScreen(
                onBack = { navController.popBackStack() },
                onResetSent = { navController.popBackStack() }
            )
        }

        composable(route = AppScreen.HOME.name) {
            HomeScreen(
                tripRepository = tripRepository,
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
                    authRepository.logoutLocalSession()
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
                routingRepository = routingRepository,
                onBack = { navController.popBackStack() },
                onRouteSelect = {
                    navController.navigate("${AppScreen.NAVIGATION.name}/$origin/$destination")
                }
            )
        }

        composable(route = "${AppScreen.NAVIGATION.name}/{origin}/{destination}") { backStackEntry ->
            val origin = backStackEntry.arguments?.getString("origin") ?: ""
            val destination = backStackEntry.arguments?.getString("destination") ?: ""
            // Mocking steps for now
            val mockSteps = listOf(
                com.example.frontend.model.CommuteStep("Jeepney", origin, "Terminal", 15, 13.0),
                com.example.frontend.model.CommuteStep("Walk", "Terminal", destination, 5, 0.0)
            )
            NavigationScreen(
                origin = origin,
                destination = destination,
                steps = mockSteps,
                onBack = { navController.popBackStack() },
                onStartTracking = {
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
