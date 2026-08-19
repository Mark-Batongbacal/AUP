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
import com.example.frontend.TodaPointOverlay
import com.example.frontend.TransitRouteOverlay
import com.example.frontend.auth.*
import com.example.frontend.core.findActivity
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.auth.RegisterRequest
import com.example.frontend.data.navigation.NavigationLocationUpdate
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.users.UserProfileDto
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.FavoriteRoute
import com.example.frontend.model.HistoryLeg
import com.example.frontend.model.RecentCommute
import com.example.frontend.model.RouteOption
import com.example.frontend.screens.*
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import org.maplibre.android.geometry.LatLng
import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.ZoneOffset

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
    val coroutineScope = rememberCoroutineScope()
    val authRepository = dataProvider.authRepository
    val userRepository = dataProvider.userRepository
    val placesRepository = dataProvider.placesRepository
    val routingRepository = dataProvider.routingRepository
    val navigationRepository = dataProvider.navigationRepository
    val tripRepository = dataProvider.tripRepository
    val transportRouteRepository = dataProvider.transportRouteRepository
    val tricycleRepository = dataProvider.tricycleRepository

    val startDestination = remember {
        if (dataProvider.sessionStore.validSession() != null) {
            AppScreen.HOME.name
        } else {
            AppScreen.ONBOARDING.name
        }
    }

    var currentUserProfile by remember { mutableStateOf<UserProfileDto?>(null) }
    var favorites by remember { mutableStateOf<List<FavoriteRoute>>(emptyList()) }
    var recentCommutes by remember { mutableStateOf<List<RecentCommute>>(emptyList()) }
    var recentTripsLoading by remember { mutableStateOf(false) }
    var recentTripsError by remember { mutableStateOf<String?>(null) }
    var selectedCommute by remember { mutableStateOf<RecentCommute?>(null) }
    var selectedCommuteGeometries by remember { mutableStateOf<List<List<LatLng>>>(emptyList()) }
    var selectedCommuteGeometryLoading by remember { mutableStateOf(false) }
    var selectedRouteOption by remember { mutableStateOf<RouteOption?>(null) }
    var selectedRoutingDestination by remember { mutableStateOf<DestinationSearchResultDto?>(null) }
    var selectedRoutingOriginLatitude by remember { mutableStateOf<Double?>(null) }
    var selectedRoutingOriginLongitude by remember { mutableStateOf<Double?>(null) }
    var activeNavigationSessionId by remember { mutableStateOf<String?>(null) }
    var activeNavigationSnapshot by remember { mutableStateOf<NavigationSnapshotDto?>(null) }
    var navigationTrackingError by remember { mutableStateOf<String?>(null) }
    var transitRouteOverlays by remember { mutableStateOf<List<TransitRouteOverlay>>(emptyList()) }
    var todaPointOverlays by remember { mutableStateOf<List<TodaPointOverlay>>(emptyList()) }
    var resolvedLegGeometries by remember { mutableStateOf<List<List<LatLng>>>(emptyList()) }
    var liveCurrentLegGeometry by remember { mutableStateOf<List<LatLng>>(emptyList()) }
    var showAskAI by remember { mutableStateOf(false) }

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
                        selectedRoutingDestination = null
                        selectedRoutingOriginLatitude = null
                        selectedRoutingOriginLongitude = null
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
                        selectedRoutingDestination = null
                        selectedRoutingOriginLatitude = null
                        selectedRoutingOriginLongitude = null
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
                LaunchedEffect(Unit) {
                    recentTripsLoading = true
                    recentTripsError = null
                    when (val result = tripRepository.getHistory()) {
                        is ApiResult.Success -> {
                            val mapped = mutableListOf<RecentCommute>()
                            for (item in result.data) {
                                var originName = item.originName
                                var destinationName = item.destinationName

                                if (originName.isGenericLocationLabel()) {
                                    when (val place = placesRepository.reverseGeocode(
                                        item.originLatitude,
                                        item.originLongitude
                                    )) {
                                        is ApiResult.Success -> originName = place.data.name
                                        is ApiResult.Failure -> Unit
                                    }
                                }
                                if (destinationName.isGenericLocationLabel()) {
                                    when (val place = placesRepository.reverseGeocode(
                                        item.destinationLatitude,
                                        item.destinationLongitude
                                    )) {
                                        is ApiResult.Success -> destinationName = place.data.name
                                        is ApiResult.Failure -> Unit
                                    }
                                }

                                val recommendation = item.recommendation
                                val orderedLegs = recommendation?.legs?.sortedBy { it.legOrder }.orEmpty()
                                mapped += RecentCommute(
                                    id = item.passengerTripId,
                                    recommendationId = recommendation?.recommendationId,
                                    origin = originName,
                                    destination = destinationName,
                                    originLatitude = item.originLatitude,
                                    originLongitude = item.originLongitude,
                                    destinationLatitude = item.destinationLatitude,
                                    destinationLongitude = item.destinationLongitude,
                                    legs = orderedLegs.size,
                                    minutes = recommendation?.totalMinutes?.toInt() ?: 0,
                                    dateGroup = recentDateGroup(item.startedAt ?: item.createdAt),
                                    steps = orderedLegs.map { leg ->
                                        CommuteStep(
                                            mode = leg.transportMode?.name
                                                ?: leg.route?.routeName
                                                ?: "Transit",
                                            from = leg.fromName
                                                ?: leg.fromStop?.name
                                                ?: originName,
                                            to = leg.toName
                                                ?: leg.toStop?.name
                                                ?: destinationName,
                                            minutes = leg.estimatedMinutes.toInt(),
                                            fare = leg.estimatedFare.toDouble()
                                        )
                                    },
                                    historyLegs = orderedLegs.map { leg ->
                                        HistoryLeg(
                                            mode = leg.transportMode?.code ?: "TRANSIT",
                                            routeId = leg.routeId,
                                            routeName = leg.route?.routeName,
                                            from = leg.fromName ?: leg.fromStop?.name ?: originName,
                                            to = leg.toName ?: leg.toStop?.name ?: destinationName,
                                            startLatitude = leg.startLatitude,
                                            startLongitude = leg.startLongitude,
                                            endLatitude = leg.endLatitude,
                                            endLongitude = leg.endLongitude
                                        )
                                    }
                                )
                            }
                            recentCommutes = mapped
                        }
                        is ApiResult.Failure -> {
                            recentCommutes = emptyList()
                            recentTripsError = result.message
                            if (result.isUnauthorized) {
                                navController.navigate(AppScreen.LOGIN.name) { popUpTo(0) }
                            }
                        }
                    }
                    recentTripsLoading = false
                }

                RecentScreen(
                    commutes = recentCommutes,
                    isLoading = recentTripsLoading,
                    errorMessage = recentTripsError,
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
                        activeNavigationSessionId = null
                        activeNavigationSnapshot = null
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
                        activeNavigationSessionId = null
                        activeNavigationSnapshot = null
                        navController.navigate(AppScreen.LOGIN.name) {
                            popUpTo(0)
                        }
                    }
                )
            }

            composable(route = AppScreen.COMMUTE_DETAIL.name) {
                selectedCommute?.let { commute ->
                    LaunchedEffect(commute.id) {
                        selectedCommuteGeometryLoading = true
                        val geometries = mutableListOf<List<LatLng>>()
                        for (leg in commute.historyLegs) {
                            val startLat = leg.startLatitude
                            val startLon = leg.startLongitude
                            val endLat = leg.endLatitude
                            val endLon = leg.endLongitude
                            if (startLat == null || startLon == null || endLat == null || endLon == null) {
                                geometries.add(emptyList())
                                continue
                            }

                            when (val result = navigationRepository.getGeometry(
                                startLatitude = startLat,
                                startLongitude = startLon,
                                endLatitude = endLat,
                                endLongitude = endLon,
                                mode = leg.mode,
                                routeId = leg.routeId
                            )) {
                                is ApiResult.Success -> geometries += result.data.points.map { point ->
                                    LatLng(point.latitude, point.longitude)
                                }
                                is ApiResult.Failure -> geometries.add(emptyList())
                            }
                        }
                        selectedCommuteGeometries = geometries
                        selectedCommuteGeometryLoading = false
                    }

                    CommuteDetailScreen(
                        commute = commute,
                        legGeometries = selectedCommuteGeometries,
                        isGeometryLoading = selectedCommuteGeometryLoading,
                        onBack = {
                            navController.popBackStack()
                        },
                        onRepeatTrip = {
                            val latitude = commute.destinationLatitude
                            val longitude = commute.destinationLongitude
                            if (latitude != null && longitude != null) {
                                selectedRoutingDestination = DestinationSearchResultDto(
                                    id = "recent-${commute.id}",
                                    name = commute.destination,
                                    latitude = latitude,
                                    longitude = longitude,
                                    category = "recent",
                                    source = "history",
                                    address = null
                                )
                                selectedRoutingOriginLatitude = null
                                selectedRoutingOriginLongitude = null
                                selectedRouteOption = null
                                navController.navigate(routeResults("Current location", commute.destination))
                            }
                        }
                    )
                }
            }

            composable(route = "${AppScreen.DESTINATION_SEARCH.name}/{origin}") { backStackEntry ->
                val origin = backStackEntry.arguments?.getString("origin") ?: ""
                DestinationSearchScreen(
                    origin = origin,
                    placesRepository = placesRepository,
                    onBack = {
                        navController.popBackStack()
                    },
                    onFindRoutes = { destination, originLatitude, originLongitude ->
                        selectedRoutingDestination = destination
                        selectedRoutingOriginLatitude = originLatitude
                        selectedRoutingOriginLongitude = originLongitude
                        navController.navigate(routeResults(origin, destination.name))
                    }
                )
            }

            composable(route = "${AppScreen.ROUTE_RESULTS.name}/{origin}/{destination}") { backStackEntry ->
                val origin = backStackEntry.arguments?.getString("origin") ?: ""
                val destination = backStackEntry.arguments?.getString("destination") ?: ""
                val exactDestination = selectedRoutingDestination
                    ?.takeIf { it.name == destination }

                RouteResultsScreen(
                    origin = origin,
                    destinationQuery = destination,
                    routingRepository = routingRepository,
                    placesRepository = placesRepository,
                    originLatitude = if (exactDestination != null) selectedRoutingOriginLatitude else null,
                    originLongitude = if (exactDestination != null) selectedRoutingOriginLongitude else null,
                    destinationLatitude = exactDestination?.latitude,
                    destinationLongitude = exactDestination?.longitude,
                    onBack = { navController.popBackStack() },
                    onRouteSelect = { option ->
                        selectedRouteOption = option
                        resolvedLegGeometries = option.legRoutePoints.map { segment ->
                            segment.map { point -> LatLng(point.latitude, point.longitude) }
                        }
                        liveCurrentLegGeometry = emptyList()
                        navController.navigate(navigationRoute(origin, destination))
                    },
                    onSuggestToda = {}
                )
            }

            composable(route = "${AppScreen.NAVIGATION.name}/{origin}/{destination}") { backStackEntry ->
                val origin = backStackEntry.arguments?.getString("origin") ?: ""
                val destination = backStackEntry.arguments?.getString("destination") ?: ""
                var isStartingNavigation by remember { mutableStateOf(false) }
                var navigationStartError by remember { mutableStateOf<String?>(null) }
                var hasExistingActiveTrip by remember { mutableStateOf(false) }

                NavigationScreen(
                    origin = origin,
                    destination = destination,
                    steps = selectedRouteOption?.steps.orEmpty(),
                    isStartingNavigation = isStartingNavigation,
                    navigationStartError = navigationStartError,
                    hasActiveTrip = hasExistingActiveTrip,
                    onBack = { navController.popBackStack() },
                    onStartTracking = {
                        val recommendationId = selectedRouteOption?.id
                        if (recommendationId == null) {
                            navigationStartError = "No route is selected. Please go back and choose a route again."
                        } else if (!isStartingNavigation) {
                            coroutineScope.launch {
                                isStartingNavigation = true
                                navigationStartError = null

                                when (val result = navigationRepository.startNavigation(recommendationId)) {
                                    is ApiResult.Success -> {
                                        activeNavigationSessionId = result.data.sessionId
                                        activeNavigationSnapshot = result.data
                                        navigationTrackingError = null
                                        hasExistingActiveTrip = false
                                        navController.navigate(trackingRoute(origin, destination))
                                    }

                                    is ApiResult.Failure -> {
                                        val looksLikeActiveTrip =
                                            result.message.contains("ACTIVE_TRIP_EXISTS", ignoreCase = true) ||
                                                result.message.contains("active trip", ignoreCase = true)
                                        if (looksLikeActiveTrip) {
                                            when (val active = navigationRepository.getActiveNavigation()) {
                                                is ApiResult.Success -> {
                                                    activeNavigationSessionId = active.data.sessionId
                                                    activeNavigationSnapshot = active.data
                                                    navigationTrackingError = null
                                                    hasExistingActiveTrip = true
                                                    navigationStartError =
                                                        "You already have an active trip. Resume it or end it before starting this route."
                                                }
                                                is ApiResult.Failure -> {
                                                    navigationStartError = active.message
                                                }
                                            }
                                        } else {
                                            navigationStartError = result.message
                                        }
                                    }
                                }

                                isStartingNavigation = false
                            }
                        }
                    },
                    onResumeActiveTrip = {
                        selectedRouteOption = null
                        resolvedLegGeometries = emptyList()
                        liveCurrentLegGeometry = emptyList()
                        navController.navigate(trackingRoute("Current location", "Active trip"))
                    },
                    onEndActiveTrip = {
                        val sessionId = activeNavigationSessionId
                        if (sessionId != null && !isStartingNavigation) {
                            coroutineScope.launch {
                                isStartingNavigation = true
                                when (val result = navigationRepository.cancel(sessionId)) {
                                    is ApiResult.Success -> {
                                        activeNavigationSessionId = null
                                        activeNavigationSnapshot = result.data
                                        navigationTrackingError = null
                                        hasExistingActiveTrip = false
                                        navigationStartError = null
                                    }
                                    is ApiResult.Failure -> navigationStartError = result.message
                                }
                                isStartingNavigation = false
                            }
                        }
                    }
                )
            }

            composable(route = "${AppScreen.TRIP_TRACKING.name}/{origin}/{destination}") { backStackEntry ->
                val origin = backStackEntry.arguments?.getString("origin") ?: ""
                val destination = backStackEntry.arguments?.getString("destination") ?: ""
                var isNavigationActionInProgress by remember { mutableStateOf(false) }

                LaunchedEffect(Unit) {
                    if (transitRouteOverlays.isEmpty()) {
                        when (val routes = transportRouteRepository.getActiveRoutes()) {
                            is ApiResult.Success -> {
                                val overlays = mutableListOf<TransitRouteOverlay>()
                                routes.data.take(40).forEach { route ->
                                    when (val points = transportRouteRepository.getRoutePoints(route.routeId)) {
                                        is ApiResult.Success -> {
                                            val geometry = points.data.points
                                                .sortedBy { it.pointOrder }
                                                .map { point ->
                                                    LatLng(point.latitude, point.longitude)
                                                }
                                            if (geometry.size >= 2) {
                                                overlays += TransitRouteOverlay(
                                                    routeId = route.routeId,
                                                    routeCode = route.routeCode,
                                                    routeName = route.routeName,
                                                    points = geometry
                                                )
                                            }
                                        }
                                        is ApiResult.Failure -> Unit
                                    }
                                }
                                transitRouteOverlays = overlays
                            }
                            is ApiResult.Failure -> Unit
                        }
                    }

                    if (todaPointOverlays.isEmpty()) {
                        when (val points = tricycleRepository.getActivePoints()) {
                            is ApiResult.Success -> {
                                todaPointOverlays = points.data.map { point ->
                                    TodaPointOverlay(
                                        id = point.tricyclePointId,
                                        name = point.pointName,
                                        pointCode = point.pointCode,
                                        latitude = point.centerLatitude,
                                        longitude = point.centerLongitude,
                                        radiusMeters = point.radiusMeters,
                                        operatorName = point.operatorName,
                                        baseFareText = point.baseFare?.let { fare ->
                                            "₱${fare.stripTrailingZeros().toPlainString()}"
                                        }
                                    )
                                }
                            }
                            is ApiResult.Failure -> Unit
                        }
                    }
                }

                LaunchedEffect(activeNavigationSessionId) {
                    if (activeNavigationSessionId == null) {
                        when (val active = navigationRepository.getActiveNavigation()) {
                            is ApiResult.Success -> {
                                activeNavigationSessionId = active.data.sessionId
                                activeNavigationSnapshot = active.data
                                selectedRouteOption = null
                                resolvedLegGeometries = emptyList()
                                liveCurrentLegGeometry = emptyList()
                            }
                            is ApiResult.Failure -> navigationTrackingError = active.message
                        }
                    }

                    val sessionId = activeNavigationSessionId ?: return@LaunchedEffect
                    while (true) {
                        val location = context.currentDeviceLocation()
                        if (location == null) {
                            navigationTrackingError = "Current location is unavailable."
                        } else {
                            val timestampMillis = if (location.time > 0L) {
                                location.time
                            } else {
                                System.currentTimeMillis()
                            }

                            val update = NavigationLocationUpdate(
                                latitude = location.latitude,
                                longitude = location.longitude,
                                accuracyMeters = location.accuracy.toDouble(),
                                timestamp = Instant.ofEpochMilli(timestampMillis).toString(),
                                speedMetersPerSecond = if (location.hasSpeed()) {
                                    location.speed.toDouble()
                                } else {
                                    null
                                },
                                bearingDegrees = if (location.hasBearing()) {
                                    location.bearing.toDouble()
                                } else {
                                    null
                                }
                            )

                            when (val result = navigationRepository.updateLocation(sessionId, update)) {
                                is ApiResult.Success -> {
                                    activeNavigationSnapshot = result.data
                                    navigationTrackingError = null
                                    if (
                                        result.data.state.equals("Arrived", ignoreCase = true) ||
                                        result.data.state.equals("Cancelled", ignoreCase = true)
                                    ) {
                                        break
                                    }
                                }
                                is ApiResult.Failure -> navigationTrackingError = result.message
                            }
                        }

                        delay(5_000)
                    }
                }

                val currentLegIndex = activeNavigationSnapshot?.currentLegIndex ?: 0

                LaunchedEffect(selectedRouteOption?.id) {
                    val option = selectedRouteOption ?: return@LaunchedEffect
                    val working = option.legRoutePoints.map { segment ->
                        segment.map { point -> LatLng(point.latitude, point.longitude) }
                    }.toMutableList()

                    option.steps.forEachIndexed { index, step ->
                        if (working.getOrNull(index)?.size ?: 0 >= 2) return@forEachIndexed
                        if (!step.mode.equals("Walk", true) && !step.mode.equals("Tricycle", true)) {
                            return@forEachIndexed
                        }
                        val end = option.legEndPoints.getOrNull(index) ?: return@forEachIndexed
                        val start = if (index == 0) {
                            val lat = selectedRoutingOriginLatitude
                            val lon = selectedRoutingOriginLongitude
                            if (lat != null && lon != null) LatLng(lat, lon) else null
                        } else {
                            option.legEndPoints.getOrNull(index - 1)?.let { LatLng(it.latitude, it.longitude) }
                        } ?: return@forEachIndexed

                        when (val result = navigationRepository.getGeometry(
                            start.latitude,
                            start.longitude,
                            end.latitude,
                            end.longitude,
                            if (step.mode.equals("Tricycle", true)) "TRICYCLE" else "WALK"
                        )) {
                            is ApiResult.Success -> {
                                while (working.size <= index) working.add(emptyList())
                                working[index] = result.data.points.map { point ->
                                    LatLng(point.latitude, point.longitude)
                                }
                            }
                            is ApiResult.Failure -> Unit
                        }
                    }
                    resolvedLegGeometries = working
                }

                LaunchedEffect(
                    currentLegIndex,
                    activeNavigationSnapshot?.currentLeg?.endLatitude,
                    activeNavigationSnapshot?.currentLeg?.endLongitude,
                    activeNavigationSnapshot?.currentLeg?.transportMode
                ) {
                    liveCurrentLegGeometry = emptyList()
                    val leg = activeNavigationSnapshot?.currentLeg ?: return@LaunchedEffect
                    val endLat = leg.endLatitude ?: return@LaunchedEffect
                    val endLon = leg.endLongitude ?: return@LaunchedEffect
                    val mode = leg.transportMode.uppercase()

                    if (mode != "WALK" && mode != "TRIKE" && mode != "TRICYCLE") {
                        return@LaunchedEffect
                    }

                    val currentLocation = context.currentDeviceLocation()
                    val startLat = currentLocation?.latitude ?: leg.startLatitude ?: return@LaunchedEffect
                    val startLon = currentLocation?.longitude ?: leg.startLongitude ?: return@LaunchedEffect

                    when (val geometry = navigationRepository.getGeometry(
                        startLat,
                        startLon,
                        endLat,
                        endLon,
                        if (mode == "WALK") "WALK" else "TRICYCLE"
                    )) {
                        is ApiResult.Success -> liveCurrentLegGeometry = geometry.data.points.map { point ->
                            LatLng(point.latitude, point.longitude)
                        }
                        is ApiResult.Failure -> navigationTrackingError = geometry.message
                    }
                }

                val selectedLegPoints = resolvedLegGeometries.getOrNull(currentLegIndex).orEmpty()
                val routePoints = if (liveCurrentLegGeometry.size >= 2) {
                    liveCurrentLegGeometry
                } else {
                    selectedLegPoints
                }

                val futureRouteSegments = resolvedLegGeometries
                    .drop(currentLegIndex + 1)
                    .filter { it.size >= 2 }

                val legDestination = selectedRouteOption
                    ?.legEndPoints
                    ?.getOrNull(currentLegIndex)
                    ?.let { point ->
                        LatLng(point.latitude, point.longitude)
                    }
                    ?: activeNavigationSnapshot?.currentLeg?.let { leg ->
                        if (leg.endLatitude != null && leg.endLongitude != null) {
                            LatLng(leg.endLatitude, leg.endLongitude)
                        } else {
                            null
                        }
                    }

                val finalDestination = selectedRouteOption
                    ?.legEndPoints
                    ?.lastOrNull()
                    ?.let { point ->
                        LatLng(point.latitude, point.longitude)
                    }
                    ?: selectedRoutingDestination?.let { point ->
                        LatLng(point.latitude, point.longitude)
                    }

                TripTrackingScreen(
                    origin = origin,
                    destination = destination,
                    routePoints = routePoints,
                    futureRouteSegments = futureRouteSegments,
                    legDestination = legDestination,
                    finalDestination = finalDestination,
                    nearbyJeepneyRoutes = transitRouteOverlays,
                    todaPoints = todaPointOverlays,
                    navigationSnapshot = activeNavigationSnapshot,
                    navigationError = navigationTrackingError,
                    isNavigationActionInProgress = isNavigationActionInProgress,
                    onBack = { navController.popBackStack() },
                    onEndTrip = {
                        val sessionId = activeNavigationSessionId
                        if (sessionId != null && !isNavigationActionInProgress) {
                            coroutineScope.launch {
                                isNavigationActionInProgress = true
                                when (val result = navigationRepository.cancel(sessionId)) {
                                    is ApiResult.Success -> {
                                        activeNavigationSessionId = null
                                        activeNavigationSnapshot = result.data
                                        navigationTrackingError = null
                                        selectedRouteOption = null
                                        resolvedLegGeometries = emptyList()
                                        liveCurrentLegGeometry = emptyList()
                                        navController.popBackStack()
                                    }
                                    is ApiResult.Failure -> navigationTrackingError = result.message
                                }
                                isNavigationActionInProgress = false
                            }
                        }
                    },
                    onConfirmBoarding = {
                        val sessionId = activeNavigationSessionId
                        if (sessionId != null && !isNavigationActionInProgress) {
                            coroutineScope.launch {
                                isNavigationActionInProgress = true
                                when (val result = navigationRepository.confirmBoarding(sessionId)) {
                                    is ApiResult.Success -> {
                                        activeNavigationSnapshot = result.data
                                        liveCurrentLegGeometry = emptyList()
                                        navigationTrackingError = null
                                    }
                                    is ApiResult.Failure -> navigationTrackingError = result.message
                                }
                                isNavigationActionInProgress = false
                            }
                        }
                    },
                    onConfirmAlighting = {
                        val sessionId = activeNavigationSessionId
                        if (sessionId != null && !isNavigationActionInProgress) {
                            coroutineScope.launch {
                                isNavigationActionInProgress = true
                                when (val result = navigationRepository.confirmAlighting(sessionId)) {
                                    is ApiResult.Success -> {
                                        activeNavigationSnapshot = result.data
                                        liveCurrentLegGeometry = emptyList()
                                        navigationTrackingError = null
                                    }
                                    is ApiResult.Failure -> navigationTrackingError = result.message
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
                userName = greetingName,
                onBack = {
                    showAskAI = false
                },
                onDestinationConfirmed = { destination ->
                    showAskAI = false
                    selectedRoutingDestination = null
                    selectedRoutingOriginLatitude = null
                    selectedRoutingOriginLongitude = null
                    navController.navigate(routeResults("Current location", destination))
                }
            )
        }
    }
}

private fun recentDateGroup(timestamp: String): String {
    val zone = ZoneId.systemDefault()
    val date = runCatching {
        Instant.parse(timestamp).atZone(zone).toLocalDate()
    }.recoverCatching {
        OffsetDateTime.parse(timestamp).atZoneSameInstant(zone).toLocalDate()
    }.recoverCatching {
        LocalDateTime.parse(timestamp)
            .atZone(ZoneOffset.UTC)
            .withZoneSameInstant(zone)
            .toLocalDate()
    }.getOrNull() ?: return "Earlier"

    val today = LocalDate.now(zone)
    return when (date) {
        today -> "Today"
        today.minusDays(1) -> "Yesterday"
        else -> "Earlier"
    }
}

private fun String.isGenericLocationLabel(): Boolean {
    val value = trim().lowercase()
    return value.isBlank() ||
        value == "current location" ||
        value == "pinned destination" ||
        value == "unknown origin" ||
        value == "unknown destination"
}
