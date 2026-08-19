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
import androidx.compose.ui.res.stringResource
import androidx.credentials.CredentialManager
import com.example.frontend.auth.FacebookSignInClient
import com.example.frontend.auth.FacebookSignInResult
import com.example.frontend.auth.GoogleSignInClient
import com.example.frontend.auth.GoogleSignInResult
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
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
import com.example.frontend.ui.theme.FrontendTheme
import com.example.frontend.screens.DestinationSearchScreen
import com.example.frontend.screens.AskAiChatScreen
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier


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
    val activity = context.findActivity()
    val googleServerClientId = stringResource(R.string.google_server_client_id)
    val facebookAppId = stringResource(R.string.facebook_app_id)
    val facebookClientToken = stringResource(R.string.facebook_client_token)
    val dataProvider = remember { TukiDataProvider(context.applicationContext) }
    val authRepository = dataProvider.authRepository
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

    var showAskAI by remember {
        mutableStateOf(false)
    }

    Box(
        modifier = Modifier.fillMaxSize()
    ) {

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
                                    when (val authResult =
                                        authRepository.loginWithGoogle(googleResult.idToken)) {
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
                    onNewHereClick = {},

                    //Card 1
                    onPinDestinationClick = { origin ->
                        searchOrigin = origin
                        currentScreen = AppScreen.DESTINATION_SEARCH
                    },
                    // Card 2
                    onAskAiClick = {
                        showAskAI = true
                    }
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

            AppScreen.DESTINATION_SEARCH -> {
                DestinationSearchScreen(
                    origin = searchOrigin,
                    onBack = {
                        // Lets the user bail out to Home if they tapped the
                        // wrong card.
                        currentScreen = AppScreen.HOME
                    },
                    onFindRoutes = { destination ->
                        searchDestination = destination
                        currentScreen = AppScreen.ROUTE_RESULTS
                    }
                )
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
                    },
                    onSuggestToda = {
                        // TODO: wire to a "suggest a TODA" form/flow.
                    }
                )
            }
        }

            if (showAskAI) {
                AskAiChatScreen(
                    onBack = {
                        showAskAI = false
                    },
                    onDestinationConfirmed = { destination ->
                        searchOrigin = "Current location"
                        searchDestination = destination
                        showAskAI = false
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
