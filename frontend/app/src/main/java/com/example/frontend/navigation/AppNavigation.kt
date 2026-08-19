package com.example.frontend.navigation

import android.net.Uri
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.credentials.CredentialManager
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.example.frontend.R
import com.example.frontend.TemporaryMapSamples
import com.example.frontend.auth.*
import com.example.frontend.core.findActivity
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.auth.RegisterRequest
import com.example.frontend.data.users.UserProfileDto
import com.example.frontend.model.FavoriteRoute
import com.example.frontend.model.RecentCommute
import com.example.frontend.screens.*

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
    val userRepository = dataProvider.userRepository
    val routingRepository = dataProvider.routingRepository
    val tripRepository = dataProvider.tripRepository

    val startDestination = remember {
        if (dataProvider.sessionStore.validSession() != null) {
            AppScreen.HOME.name
        } else {
            AppScreen.ONBOARDING.name
        }
    }

    var currentUserProfile by remember {
        mutableStateOf<UserProfileDto?>(null)
    }

    var favorites by remember {
        mutableStateOf<List<FavoriteRoute>>(emptyList())
    }

    var selectedCommute by remember {
        mutableStateOf<RecentCommute?>(null)
    }

    var showAskAI by remember {
        mutableStateOf(false)
    }

    val profileDisplayName = currentUserProfile?.let { profile ->
        listOfNotNull(
            profile.firstName?.trim()?.takeIf { it.isNotEmpty() },
            profile.lastName?.trim()?.takeIf { it.isNotEmpty() }
        ).joinToString(" ")
    }?.takeIf { it.isNotBlank() } ?: "User"

    val greetingName = currentUserProfile?.firstName
        ?.trim()
        ?.takeIf { it.isNotEmpty() }
        ?: profileDisplayName.substringBefore(' ')

    fun routeResults(origin: String, destination: String): String =
        "${AppScreen.ROUTE_RESULTS.name}/${Uri.encode(origin)}/${Uri.encode(destination)}"

    fun navigationRoute(origin: String, destination: String): String =
        "${AppScreen.NAVIGATION.name}/${Uri.encode(origin)}/${Uri.encode(destination)}"

    fun trackingRoute(origin: String, destination: String): String =
        "${AppScreen.TRIP_TRACKING.name}/${Uri.encode(origin)}/${Uri.encode(destination)}"

    Box(modifier = Modifier.fillMaxSize()) {
        NavHost(
            navController = navController,
            startDestination = startDestination
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
                LaunchedEffect(Unit) {
                    if (dataProvider.sessionStore.validSession() != null) {
                        when (val result = userRepository.getCurrentUser()) {
                            is ApiResult.Success -> currentUserProfile = result.data
                            is ApiResult.Failure -> {
                                if (result.isUnauthorized) {
                                    currentUserProfile = null
                                    navController.navigate(AppScreen.LOGIN.name) {
                                        popUpTo(0)
                                    }
                                }
                            }
                        }
                    }
                }

                HomeScreen(
                    userName = greetingName,
                    tripRepository = tripRepository,
                    onSearchDestination = { origin, destination ->
                        navController.navigate(routeResults(origin, destination))
                    },
                    onCommuteClick = { commute ->
                        selectedCommute = commute
                        navController.navigate(AppScreen.COMMUTE_DETAIL.name)
                    },
                    onRecentClick = {
                        navController.navigate(AppScreen.RECENT.name)
                    },
                    onFavoritesClick = {
                        navController.navigate(AppScreen.FAVORITES.name)
                    },
                    onProfileClick = {
                        navController.navigate(AppScreen.PROFILE.name)
                    },
                    onNewHereClick = {},
                    onPinDestinationClick = { origin ->
                        navController.navigate(
                            "${AppScreen.DESTINATION_SEARCH.name}/${Uri.encode(origin)}"
                        )
                    },
                    onAskAiClick = {
                        showAskAI = true
                    }
                )
            }

            composable(route = AppScreen.RECENT.name) {
                RecentScreen(
                    onCommuteClick = { commute ->
                        selectedCommute = commute
                        navController.navigate(AppScreen.COMMUTE_DETAIL.name)
                    },
                    onHomeClick = {
                        navController.navigate(AppScreen.HOME.name)
                    },
                    onFavoritesClick = {
                        navController.navigate(AppScreen.FAVORITES.name)
                    },
                    onProfileClick = {
                        navController.navigate(AppScreen.PROFILE.name)
                    }
                )
            }

            composable(route = AppScreen.FAVORITES.name) {
                LaunchedEffect(Unit) {
                    if (dataProvider.sessionStore.validSession() != null) {
                        when (val result = dataProvider.favoritesRepository.getFavorites()) {
                            is ApiResult.Success -> {
                                favorites = result.data.map { dto ->
                                    FavoriteRoute(
                                        id = dto.favoriteTripId,
                                        origin = dto.origin ?: "Unknown origin",
                                        destination = dto.destination ?: "Unknown destination",
                                        timesUsed = dto.timesUsed,
                                        note = dto.note.orEmpty()
                                    )
                                }
                            }

                            is ApiResult.Failure -> Unit
                        }
                    }
                }

                FavoritesScreen(
                    favorites = favorites,
                    onHomeClick = {
                        navController.navigate(AppScreen.HOME.name)
                    },
                    onRecentClick = {
                        navController.navigate(AppScreen.RECENT.name)
                    },
                    onProfileClick = {
                        navController.navigate(AppScreen.PROFILE.name)
                    }
                )
            }

            composable(route = AppScreen.PROFILE.name) {
                LaunchedEffect(Unit) {
                    if (dataProvider.sessionStore.validSession() != null) {
                        when (val result = userRepository.getCurrentUser()) {
                            is ApiResult.Success -> currentUserProfile = result.data
                            is ApiResult.Failure -> {
                                if (result.isUnauthorized) {
                                    currentUserProfile = null
                                    navController.navigate(AppScreen.LOGIN.name) {
                                        popUpTo(0)
                                    }
                                }
                            }
                        }
                    }
                }

                ProfileScreen(
                    userName = profileDisplayName,
                    userEmail = currentUserProfile?.email.orEmpty(),
                    tripsTaken = currentUserProfile?.tripsTaken ?: 0,
                    favoritesCount = currentUserProfile?.favoritesCount ?: 0,
                    onBack = { navController.popBackStack() },
                    onEditProfileClick = {
                        navController.navigate(AppScreen.SETTINGS.name)
                    },
                    onLogoutClick = {
                        authRepository.logoutLocalSession()
                        currentUserProfile = null
                        navController.navigate(AppScreen.LOGIN.name) {
                            popUpTo(0)
                        }
                    },
                    onHomeClick = {
                        navController.navigate(AppScreen.HOME.name)
                    },
                    onRecentClick = {
                        navController.navigate(AppScreen.RECENT.name)
                    },
                    onFavoritesClick = {
                        navController.navigate(AppScreen.FAVORITES.name)
                    }
                )
            }

            composable(route = AppScreen.SETTINGS.name) {
                SettingsScreen(
                    onBack = { navController.popBackStack() },
                    onLogoutClick = {
                        authRepository.logoutLocalSession()
                        currentUserProfile = null
                        navController.navigate(AppScreen.LOGIN.name) {
                            popUpTo(0)
                        }
                    }
                )
            }

            composable(route = AppScreen.COMMUTE_DETAIL.name) {
                selectedCommute?.let { commute ->
                    CommuteDetailScreen(
                        commute = commute,
                        onBack = {
                            navController.popBackStack()
                        }
                    )
                }
            }

            composable(route = "${AppScreen.DESTINATION_SEARCH.name}/{origin}") { backStackEntry ->
                val origin = backStackEntry.arguments?.getString("origin") ?: ""
                DestinationSearchScreen(
                    origin = origin,
                    onBack = {
                        navController.popBackStack()
                    },
                    onFindRoutes = { destination ->
                        navController.navigate(routeResults(origin, destination))
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
                        navController.navigate(navigationRoute(origin, destination))
                    },
                    onSuggestToda = {}
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
                        navController.navigate(trackingRoute(origin, destination))
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

        if (showAskAI) {
            AskAiChatScreen(
                userName = greetingName,
                onBack = {
                    showAskAI = false
                },
                onDestinationConfirmed = { destination ->
                    showAskAI = false
                    navController.navigate(routeResults("Current location", destination))
                }
            )
        }
    }
}
