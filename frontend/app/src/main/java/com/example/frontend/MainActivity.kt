package com.example.frontend

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalContext
import androidx.credentials.CredentialManager
import com.example.frontend.auth.FacebookSignInClient
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.auth.RegisterRequest
import com.example.frontend.ui.theme.FrontendTheme

class MainActivity : ComponentActivity() {
    private val facebookSignInClient = FacebookSignInClient()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            FrontendTheme {
                TukiApp(facebookSignInClient)
            }
        }
    }

    @Deprecated("Deprecated in Java")
    @Suppress("DEPRECATION")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        facebookSignInClient.onActivityResult(requestCode, resultCode, data)
    }
}

@Composable
fun TukiApp(
    facebookSignInClient: FacebookSignInClient = FacebookSignInClient()
) {
    val context = LocalContext.current
    val dataProvider = remember { TukiDataProvider(context.applicationContext) }
    val authRepository = dataProvider.authRepository
    val googleSignInClient = remember {
        GoogleSignInClient(CredentialManager.create(context))
    }

    val hasStoredSession = remember {
        dataProvider.sessionStore.validSession() != null
    }

    var currentScreen by remember {
        mutableStateOf(if (hasStoredSession) AppScreen.HOME else AppScreen.ONBOARDING)
    }

    LaunchedEffect(hasStoredSession) {
        if (hasStoredSession) {
            when (authRepository.getCurrentAuthIdentity()) {
                is ApiResult.Success -> Unit
                is ApiResult.Failure -> currentScreen = AppScreen.LOGIN
            }
        }
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
                },
                onSignUpClick = { fullName, email, password ->
                    val nameParts = fullName
                        .trim()
                        .split(Regex("\\s+"), limit = 2)

                    if (nameParts.size < 2) {
                        LoginActionResult.Error("Enter both your first and last name.")
                    } else {
                        when (
                            val authResult = authRepository.register(
                                RegisterRequest(
                                    userName = email,
                                    password = password,
                                    firstName = nameParts[0],
                                    lastName = nameParts[1]
                                )
                            )
                        ) {
                            is ApiResult.Success -> LoginActionResult.Success
                            is ApiResult.Failure -> LoginActionResult.Error(authResult.message)
                        }
                    }
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
                onRecentClick = { currentScreen = AppScreen.RECENT },
                onFavoritesClick = { currentScreen = AppScreen.FAVORITES },
                onProfileClick = { currentScreen = AppScreen.PROFILE },
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
                onLogoutClick = {
                    authRepository.logoutLocalSession()
                    currentScreen = AppScreen.LOGIN
                },
                onHomeClick = {
                    currentScreen = AppScreen.HOME
                },
                onRecentClick = {
                    currentScreen = AppScreen.RECENT
                },
                onFavoritesClick = {
                    currentScreen = AppScreen.FAVORITES
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

private tailrec fun Context.findActivity(): Activity? =
    when (this) {
        is Activity -> this
        is ContextWrapper -> baseContext.findActivity()
        else -> null
    }
