package com.example.frontend

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.credentials.CredentialManager
import com.example.frontend.auth.FacebookSignInClient
import com.example.frontend.auth.FacebookSignInResult
import com.example.frontend.auth.GoogleSignInClient
import com.example.frontend.auth.GoogleSignInResult
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.favorites.FavoriteTripDto
import com.example.frontend.data.trips.PassengerTripHistoryItemDto
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.FavoriteRoute
import com.example.frontend.model.RouteOption
import com.example.frontend.model.RecentCommute
import com.example.frontend.navigation.AppScreen
import com.example.frontend.screens.AskAiChatScreen
import com.example.frontend.screens.DestinationSearchScreen
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
import com.example.frontend.screens.NavigationScreen
import com.example.frontend.screens.SettingsScreen
import com.example.frontend.screens.TripTrackingScreen
import com.example.frontend.screens.EditProfileScreen
import com.example.frontend.screens.EditProfileResult
import com.example.frontend.screens.PrivacySecurityScreen
import com.example.frontend.screens.ForgotPasswordScreen
import com.example.frontend.screens.ChangePasswordScreen
import com.example.frontend.screens.ChangePasswordResult
import com.example.frontend.screens.LanguageScreen
import com.example.frontend.screens.DeleteAccountResult
import kotlinx.coroutines.launch
import org.maplibre.android.geometry.LatLng



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

private fun PassengerTripHistoryItemDto.toRecentCommute(): RecentCommute {
    val legs = recommendation?.legs.orEmpty()
    return RecentCommute(
        id = passengerTripId,
        recommendationId = recommendation?.recommendationId,
        origin = originName,
        destination = destinationName,
        originLatitude = originLatitude,
        originLongitude = originLongitude,
        destinationLatitude = destinationLatitude,
        destinationLongitude = destinationLongitude,
        legs = legs.size.takeIf { it > 0 } ?: 1,
        minutes = recommendation?.totalMinutes?.toInt() ?: 0,
        dateGroup = (startedAt ?: createdAt).take(10),
        steps = legs.map { leg ->
            CommuteStep(
                mode = leg.transportMode?.name ?: "Commute",
                from = leg.fromName ?: leg.fromStop?.name.orEmpty(),
                to = leg.toName ?: leg.toStop?.name.orEmpty(),
                minutes = leg.estimatedMinutes.toInt(),
                fare = leg.estimatedFare.toDouble()
            )
        }
    )
}

private fun FavoriteTripDto.toFavoriteRoute(): FavoriteRoute = FavoriteRoute(
    id = favoriteTripId,
    origin = origin.orEmpty(),
    destination = destination.orEmpty(),
    timesUsed = timesUsed,
    note = note.orEmpty()
)

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
    val coroutineScope = rememberCoroutineScope()

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

    var destinationLat by remember {
        mutableStateOf<Double?>(null)
    }

    var destinationLng by remember {
        mutableStateOf<Double?>(null)
    }

    var originLat by remember {
        mutableStateOf<Double?>(null)
    }

    var originLng by remember {
        mutableStateOf<Double?>(null)
    }

    var showAskAI by remember {
        mutableStateOf(false)
    }

    // User Profile State
    var userFullName by remember { mutableStateOf("Juan Dela Cruz") }
    var userEmail by remember { mutableStateOf("juan.delacruz@example.com") }
    var userPhone by remember { mutableStateOf("09123456789") }
    var recentCommutes by remember { mutableStateOf<List<RecentCommute>>(emptyList()) }
    var isLoadingRecent by remember { mutableStateOf(false) }
    var favoriteRoutes by remember { mutableStateOf<List<FavoriteRoute>>(emptyList()) }
    var isLoadingFavorites by remember { mutableStateOf(false) }
    var selectedRoute by remember { mutableStateOf<RouteOption?>(null) }
    var navigationSnapshot by remember { mutableStateOf<com.example.frontend.data.navigation.NavigationSnapshotDto?>(null) }
    var isStartingNavigation by remember { mutableStateOf(false) }
    var navigationStartError by remember { mutableStateOf<String?>(null) }
    var isNavigationActionInProgress by remember { mutableStateOf(false) }
    var navigationTrackingError by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(currentScreen) {
        if (currentScreen == AppScreen.RECENT) {
            isLoadingRecent = true
            when (val result = dataProvider.tripRepository.getHistory()) {
                is ApiResult.Success -> recentCommutes = result.data.map { it.toRecentCommute() }
                is ApiResult.Failure -> Unit // TODO: surface result.message once RecentScreen has an error slot
            }
            isLoadingRecent = false
        }

        if (currentScreen == AppScreen.FAVORITES) {
            isLoadingFavorites = true
            when (val result = dataProvider.favoritesRepository.getFavorites()) {
                is ApiResult.Success -> favoriteRoutes = result.data.map { it.toFavoriteRoute() }
                is ApiResult.Failure -> Unit // TODO: surface result.message once FavoritesScreen has an error slot
            }
            isLoadingFavorites = false
        }
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
                    authRepository = authRepository,
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
                    authRepository = authRepository,
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
                    tripRepository = dataProvider.tripRepository,
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
                    commutes = recentCommutes,
                    isLoading = isLoadingRecent,
                    onCommuteClick = { commute ->
                        selectedCommute = commute
                        currentScreen = AppScreen.COMMUTE_DETAIL
                    },
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
                    favorites = favoriteRoutes,
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
                    userName = userFullName,
                    userEmail = userEmail,
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
                        currentScreen = AppScreen.EDIT_PROFILE
                    },
                    onPrivacySecurityClick = {
                        currentScreen = AppScreen.PRIVACY_SECURITY
                    },
                    onLanguageClick = {
                        currentScreen = AppScreen.LANGUAGE
                    }
                )
            }

            AppScreen.EDIT_PROFILE -> {
                EditProfileScreen(
                    initialFullName = userFullName,
                    initialEmail = userEmail,
                    initialPhone = userPhone,
                    onBack = { currentScreen = AppScreen.PROFILE },
                    onSaveChanges = { fullName, phone ->
                        val parts = fullName.trim().split(" ", limit = 2)
                        when (val result = dataProvider.userRepository.updateCurrentUser(
                            com.example.frontend.data.users.UpdateUserProfileRequest(
                                firstName = parts.getOrNull(0).orEmpty(),
                                lastName = parts.getOrNull(1).orEmpty(),
                                phoneNumber = phone
                            )
                        )) {
                            is ApiResult.Success -> EditProfileResult.Success(result.data)
                            is ApiResult.Failure -> EditProfileResult.Error(result.message)
                        }
                    },
                    onSaved = { profile ->
                        userFullName = listOfNotNull(profile.firstName, profile.lastName).joinToString(" ")
                        userPhone = profile.phoneNumber.orEmpty()
                        currentScreen = AppScreen.PROFILE
                    }
                )
            }

            AppScreen.PRIVACY_SECURITY -> {
                PrivacySecurityScreen(
                    onBack = { currentScreen = AppScreen.PROFILE },
                    onChangePasswordClick = { currentScreen = AppScreen.CHANGE_PASSWORD },
                    on2FAToggle = { isEnabled ->
                        // TODO: Update 2FA setting on backend/preferences
                    },
                    onConfirmDeleteAccount = {
                        when (val result = dataProvider.userRepository.deleteCurrentUser()) {
                            is ApiResult.Success -> {
                                authRepository.logoutLocalSession()
                                DeleteAccountResult.Success
                            }
                            is ApiResult.Failure -> DeleteAccountResult.Error(
                                result.message.ifBlank { "Couldn't delete your account. Please try again." }
                            )
                        }
                    },
                    onAccountDeleted = {
                        selectedCommute = null
                        recentCommutes = emptyList()
                        favoriteRoutes = emptyList()
                        selectedRoute = null
                        navigationSnapshot = null
                        currentScreen = AppScreen.LOGIN
                    }
                )
            }

            AppScreen.CHANGE_PASSWORD -> {
                ChangePasswordScreen(
                    onBack = { currentScreen = AppScreen.PRIVACY_SECURITY },
                    onChangePassword = { currentPassword, newPassword ->
                        when (val result = authRepository.changePassword(currentPassword, newPassword)) {
                            is ApiResult.Success -> ChangePasswordResult.Success
                            is ApiResult.Failure -> ChangePasswordResult.Error(
                                result.message.ifBlank { "Current password is incorrect." }
                            )
                        }
                    },
                    onPasswordChanged = { currentScreen = AppScreen.PRIVACY_SECURITY }
                )
            }

            AppScreen.LANGUAGE -> {
                LanguageScreen(
                    onBack = { currentScreen = AppScreen.PROFILE },
                    onSaveLanguage = { selectedLanguage ->
                        // TODO: Persist language selection in App Preferences / DataStore
                        currentScreen = AppScreen.PROFILE
                    }
                )
            }

            AppScreen.SETTINGS -> {
                SettingsScreen(
                    onBack = { currentScreen = AppScreen.PROFILE },
                    onLogoutClick = {
                        authRepository.logoutLocalSession()
                        currentScreen = AppScreen.ONBOARDING
                    }
                )
            }

            AppScreen.FORGOT_PASSWORD -> {
                ForgotPasswordScreen(
                    onBack = { currentScreen = AppScreen.LOGIN },
                    onResetSent = { currentScreen = AppScreen.LOGIN }
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
                    placesRepository = dataProvider.placesRepository,
                    origin = searchOrigin,
                    onBack = {
                        // Lets the user bail out to Home if they tapped the
                        // wrong card.
                        currentScreen = AppScreen.HOME
                    },
                    onFindRoutes = { destination, originLatitude, originLongitude ->
                        searchDestination = destination.name
                        destinationLat = destination.latitude
                        destinationLng = destination.longitude
                        originLat = originLatitude
                        originLng = originLongitude
                        currentScreen = AppScreen.ROUTE_RESULTS
                    }
                )
            }

            AppScreen.ROUTE_RESULTS -> {
                RouteResultsScreen(
                    origin = searchOrigin,
                    destinationQuery = searchDestination,
                    routingRepository = dataProvider.routingRepository,
                    placesRepository = dataProvider.placesRepository,
                    originLatitude = originLat,
                    originLongitude = originLng,
                    destinationLatitude = destinationLat,
                    destinationLongitude = destinationLng,
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

        AppScreen.NAVIGATION -> {
        NavigationScreen(
            origin = searchOrigin,
            destination = searchDestination,
            steps = selectedRoute?.steps ?: emptyList(),
            isStartingNavigation = isStartingNavigation,
            navigationStartError = navigationStartError,
            hasActiveTrip = navigationSnapshot != null,
            onBack = {
                currentScreen = AppScreen.ROUTE_RESULTS
            },
            onStartTracking = {
                val route = selectedRoute
                if (route == null) {
                    navigationStartError = "Select a route first."
                } else {
                    coroutineScope.launch {
                        isStartingNavigation = true
                        navigationStartError = null
                        when (val result = dataProvider.navigationRepository.startNavigation(route.id)) {
                            is ApiResult.Success -> {
                                navigationSnapshot = result.data
                                currentScreen = AppScreen.TRIP_TRACKING
                            }
                            is ApiResult.Failure -> {
                                navigationStartError = result.message
                            }
                        }
                        isStartingNavigation = false
                    }
                }
            },
            onResumeActiveTrip = {
                currentScreen = AppScreen.TRIP_TRACKING
            },
            onEndActiveTrip = {
                val snapshot = navigationSnapshot
                if (snapshot != null) {
                    coroutineScope.launch {
                        dataProvider.navigationRepository.cancel(snapshot.sessionId)
                        navigationSnapshot = null
                    }
                }
            }
        )
    }

        AppScreen.TRIP_TRACKING -> {
        val snapshot = navigationSnapshot
        TripTrackingScreen(
            origin = searchOrigin,
            destination = searchDestination,
            routePoints = selectedRoute?.routePoints?.map { LatLng(it.latitude, it.longitude) }
                ?: emptyList(),
            futureRouteSegments = selectedRoute?.legRoutePoints?.map { segment ->
                segment.map { LatLng(it.latitude, it.longitude) }
            } ?: emptyList(),
            finalDestination = destinationLat?.let { lat ->
                destinationLng?.let { lng -> LatLng(lat, lng) }
            },
            navigationSnapshot = snapshot,
            navigationError = navigationTrackingError,
            isNavigationActionInProgress = isNavigationActionInProgress,
            onBack = {
                currentScreen = AppScreen.HOME
            },
            onEndTrip = {
                val current = navigationSnapshot
                if (current != null) {
                    coroutineScope.launch {
                        isNavigationActionInProgress = true
                        dataProvider.navigationRepository.cancel(current.sessionId)
                        navigationSnapshot = null
                        isNavigationActionInProgress = false
                        currentScreen = AppScreen.HOME
                    }
                } else {
                    currentScreen = AppScreen.HOME
                }
            },
            onConfirmBoarding = {
                val current = navigationSnapshot
                if (current != null) {
                    coroutineScope.launch {
                        isNavigationActionInProgress = true
                        when (val result = dataProvider.navigationRepository.confirmBoarding(current.sessionId)) {
                            is ApiResult.Success -> {
                                navigationSnapshot = result.data
                                navigationTrackingError = null
                            }
                            is ApiResult.Failure -> {
                                navigationTrackingError = result.message
                            }
                        }
                        isNavigationActionInProgress = false
                    }
                }
            },
            onConfirmAlighting = {
                val current = navigationSnapshot
                if (current != null) {
                    coroutineScope.launch {
                        isNavigationActionInProgress = true
                        when (val result = dataProvider.navigationRepository.confirmAlighting(current.sessionId)) {
                            is ApiResult.Success -> {
                                navigationSnapshot = result.data
                                navigationTrackingError = null
                            }
                            is ApiResult.Failure -> {
                                navigationTrackingError = result.message
                            }
                        }
                        isNavigationActionInProgress = false
                    }
                }
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
                        destinationLat = null
                        destinationLng = null
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
