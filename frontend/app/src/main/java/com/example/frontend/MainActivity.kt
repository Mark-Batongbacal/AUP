package com.example.frontend

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.credentials.CredentialManager
import com.example.frontend.auth.AuthRepository
import com.example.frontend.auth.AuthResult
import com.example.frontend.auth.GoogleSignInClient
import com.example.frontend.auth.GoogleSignInResult
import com.example.frontend.auth.SharedPreferencesTukiCredentialStore
import com.example.frontend.auth.TukiApiClient
import com.example.frontend.model.RecentCommute
import com.example.frontend.navigation.AppScreen
import com.example.frontend.screens.CommuteDetailScreen
import com.example.frontend.screens.FavoritesScreen
import com.example.frontend.screens.HomeScreen
import com.example.frontend.screens.LoginActionResult
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.OnboardingScreen
import com.example.frontend.screens.ProfileScreen
import com.example.frontend.screens.RecentScreen
import com.example.frontend.screens.RouteResultsScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.screens.SettingsScreen
import com.example.frontend.screens.TripTrackingScreen
import com.example.frontend.ui.theme.FrontendTheme
import com.example.frontend.repository.MockRouteRepository
import com.example.frontend.repository.MockCommuteRepository
import com.example.frontend.repository.MockAuthRepository
import com.example.frontend.repository.MockAIRepository

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
    val routeRepository = remember { MockRouteRepository() }
    val commuteRepository = remember { MockCommuteRepository() }
    val authRepositoryImpl = remember { MockAuthRepository() }
    val aiRepository = remember { MockAIRepository() }

    val context = LocalContext.current
    val activity = context.findActivity()
    val googleServerClientId = stringResource(R.string.google_server_client_id)
    val credentialStore = remember {
        SharedPreferencesTukiCredentialStore(context.applicationContext)
    }
    val authRepository = remember {
        AuthRepository(
            authApi = TukiApiClient.createAuthApi(),
            credentialStore = credentialStore
        )
    }
    val googleSignInClient = remember {
        GoogleSignInClient(CredentialManager.create(context))
    }

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
                authRepository = authRepositoryImpl,
                onBack = {
                    currentScreen = AppScreen.ONBOARDING
                },
                onSignUpClick = {
                    currentScreen = AppScreen.SIGNUP
                },
                onLoginSuccess = {
                    currentScreen = AppScreen.HOME
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
                                    AuthResult.Success -> LoginActionResult.Success
                                    is AuthResult.Failure -> LoginActionResult.Error(authResult.message)
                                }
                            }

                            is GoogleSignInResult.Failure -> {
                                LoginActionResult.Error(googleResult.message)
                            }
                        }
                    }
                }
            )
        }

        AppScreen.SIGNUP -> {
            SignupScreen(
                authRepository = authRepositoryImpl,
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
                commuteRepository = commuteRepository,
                onSearchDestination = { origin, destination ->
                    searchOrigin = origin
                    searchDestination = destination
                    currentScreen = AppScreen.ROUTE_RESULTS
                },
                onCommuteClick = { commute ->
                    selectedCommute = commute
                    currentScreen = AppScreen.COMMUTE_DETAIL
                },
                onRecentClick = {currentScreen = AppScreen.RECENT},
                onFavoritesClick = {currentScreen = AppScreen.FAVORITES},
                onProfileClick = {currentScreen = AppScreen.PROFILE},
                onNewHereClick = {}
            )
        }

        AppScreen.RECENT -> {
            RecentScreen(
                onHomeClick = {
                    currentScreen = AppScreen.HOME
                },
                onFavoritesClick = {
                    currentScreen = AppScreen.FAVORITES
                },
                onProfileClick = {
                    currentScreen = AppScreen.PROFILE
                }
            )
        }

        AppScreen.FAVORITES -> {
            FavoritesScreen(
                onHomeClick = {
                    currentScreen = AppScreen.HOME
                },
                onRecentClick = {
                    currentScreen = AppScreen.RECENT
                },
                onProfileClick = {
                    currentScreen = AppScreen.PROFILE
                }
            )
        }

        AppScreen.PROFILE -> {
            ProfileScreen(
                onBack = {
                    currentScreen = AppScreen.HOME
                },
                onHomeClick = {
                    currentScreen = AppScreen.HOME
                },
                onRecentClick = {
                    currentScreen = AppScreen.RECENT
                },
                onFavoritesClick = {
                    currentScreen = AppScreen.FAVORITES
                },
                onEditProfileClick = {
                    currentScreen = AppScreen.SETTINGS
                }
            )
        }

        AppScreen.SETTINGS -> {
            SettingsScreen(
                onBack = {
                    currentScreen = AppScreen.PROFILE
                },
                onLogoutClick = {
                    currentScreen = AppScreen.LOGIN
                }
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
                routeRepository = routeRepository,
                onBack = {
                    currentScreen = AppScreen.HOME
                },
                onRouteSelect = { route ->
                    currentScreen = AppScreen.TRIP_TRACKING
                }
            )
        }

        AppScreen.TRIP_TRACKING -> {
            TripTrackingScreen(
                origin = searchOrigin,
                destination = searchDestination,
                routePoints = TemporaryMapSamples.routePoints,
                onBack = {
                    currentScreen = AppScreen.ROUTE_RESULTS
                }
            )
        }
    }
}

private tailrec fun Context.findActivity(): Activity? =
    when (this) {
        is Activity -> this
        is ContextWrapper -> baseContext.findActivity()
        else -> null
    }
