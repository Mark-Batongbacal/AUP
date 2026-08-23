package com.example.frontend.screens

import android.Manifest
import android.content.pm.PackageManager
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalInspectionMode
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import com.example.frontend.MapScreen
import com.example.frontend.MapVisualStyle
import com.example.frontend.R
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.places.PlacesRepository
import com.example.frontend.data.trips.TripRepository
import com.example.frontend.data.trips.toRecentCommute
import com.example.frontend.model.RecentCommute
import kotlinx.coroutines.delay
import org.maplibre.android.geometry.LatLng

private val HomeBg = Color(0xFFF8F5EC)
private val HomeSurface = Color(0xFFFFFBF0)
private val HomeSoft = Color(0xFFEAF1EE)
private val HomeCurrentSky = Color(0xFFDAF1F7)
private val HomeWarm = Color(0xFFFFF0D5)
private val HomeDark = Color(0xFF153E4B)
private val HomeTeal = Color(0xFF2C8E95)
private val HomeOrange = Color(0xFFFF8A1D)
private val HomeMuted = Color(0xFF707A80)
private val HomeDivider = Color(0xFFD4D6D1)
private val HomeAiSurface = HomeDark
private val MapPanel = Color(0xFF173B43)
private val MapYellow = Color(0xFFFFCA19)

private enum class HomeMapPickMode { Origin, Destination }

@Composable
fun HomeScreen(
    userName: String = "Juan",
    tripRepository: TripRepository,
    placesRepository: PlacesRepository,
    isGuest: Boolean = false,
    onSearchDestination: (origin: String, destination: String) -> Unit = { _, _ -> },
    onFindRoutes: (
        destination: DestinationSearchResultDto,
        originName: String,
        originLatitude: Double,
        originLongitude: Double
    ) -> Unit = { _, _, _, _ -> },
    onCommuteClick: (RecentCommute) -> Unit = {},
    onRecentClick: () -> Unit = {},
    onFavoritesClick: () -> Unit = {},
    onProfileClick: () -> Unit = {},
    onNewHereClick: () -> Unit = {},
    onPinDestinationClick: (origin: String) -> Unit = {},
    onAskAiClick: () -> Unit = {},
    activeTripDescription: String? = null,
    onResumeActiveTrip: () -> Unit = {}
) {
    var currentLocationLabel by remember { mutableStateOf("Locating you...") }
    var originLatitude by remember { mutableStateOf<Double?>(null) }
    var originLongitude by remember { mutableStateOf<Double?>(null) }
    var isLocating by remember { mutableStateOf(true) }
    var locateRequest by remember { mutableIntStateOf(0) }
    var selectedDestination by remember { mutableStateOf<DestinationSearchResultDto?>(null) }
    var recentPlaces by remember { mutableStateOf<List<RecentCommute>>(emptyList()) }
    var recentPlacesLoading by remember { mutableStateOf(false) }
    var mapMode by remember { mutableStateOf(HomeMapPickMode.Destination) }
    var showMapPicker by remember { mutableStateOf(false) }
    var mapSelection by remember { mutableStateOf<DestinationSearchResultDto?>(null) }
    var mapSearchText by remember { mutableStateOf("") }
    var mapSearchResults by remember { mutableStateOf<List<DestinationSearchResultDto>>(emptyList()) }
    var mapSearchLoading by remember { mutableStateOf(false) }
    var mapSearchError by remember { mutableStateOf<String?>(null) }

    val context = LocalContext.current
    val inPreview = LocalInspectionMode.current

    fun openMapPicker(mode: HomeMapPickMode) {
        mapMode = mode
        mapSearchText = ""
        mapSearchResults = emptyList()
        mapSearchError = null
        mapSelection = when (mode) {
            HomeMapPickMode.Origin -> {
                val lat = originLatitude
                val lon = originLongitude
                if (lat != null && lon != null) {
                    DestinationSearchResultDto(
                        id = "origin-$lat-$lon",
                        name = currentLocationLabel.routeOriginLabel(),
                        latitude = lat,
                        longitude = lon,
                        category = "origin",
                        source = "current",
                        address = "Pickup point"
                    )
                } else {
                    null
                }
            }
            HomeMapPickMode.Destination -> selectedDestination
        }
        showMapPicker = true
    }

    fun submitRoute() {
        val destination = selectedDestination ?: return
        val lat = originLatitude ?: return
        val lon = originLongitude ?: return
        onFindRoutes(destination, currentLocationLabel.routeOriginLabel(), lat, lon)
    }

    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { grantResults ->
        val granted = grantResults[Manifest.permission.ACCESS_FINE_LOCATION] == true ||
                grantResults[Manifest.permission.ACCESS_COARSE_LOCATION] == true
        if (granted) {
            isLocating = true
            locateRequest += 1
        } else {
            isLocating = false
            currentLocationLabel = "Location permission denied"
        }
    }

    LaunchedEffect(locateRequest) {
        if (inPreview) {
            currentLocationLabel = "Sun Street"
            originLatitude = 15.2193
            originLongitude = 120.5816
            isLocating = false
            return@LaunchedEffect
        }

        if (!context.hasLocationPermission()) {
            permissionLauncher.launch(
                arrayOf(
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                )
            )
            return@LaunchedEffect
        }

        isLocating = true
        val location = context.currentDeviceLocation()
        if (location == null) {
            currentLocationLabel = "Unable to detect location"
            isLocating = false
            return@LaunchedEffect
        }

        originLatitude = location.latitude
        originLongitude = location.longitude
        currentLocationLabel = "Current location"
        when (val place = placesRepository.reverseGeocode(location.latitude, location.longitude)) {
            is ApiResult.Success -> currentLocationLabel = place.data.name
            is ApiResult.Failure -> Unit
        }
        isLocating = false
    }

    LaunchedEffect(isGuest) {
        if (isGuest || inPreview) {
            recentPlaces = emptyList()
            recentPlacesLoading = false
            return@LaunchedEffect
        }

        recentPlacesLoading = true
        recentPlaces = when (val result = tripRepository.getRecentJourneys()) {
            is ApiResult.Success -> result.data
                .distinctBy { it.destinationName to it.destinationLatitude to it.destinationLongitude }
                .take(3)
                .map { it.toRecentCommute() }
            is ApiResult.Failure -> emptyList()
        }
        recentPlacesLoading = false
    }

    LaunchedEffect(showMapPicker, mapSearchText, originLatitude, originLongitude) {
        if (!showMapPicker) return@LaunchedEffect
        val query = mapSearchText.trim()
        if (query.length < 2) {
            mapSearchResults = emptyList()
            mapSearchError = null
            mapSearchLoading = false
            return@LaunchedEffect
        }

        delay(300)
        mapSearchLoading = true
        mapSearchError = null
        when (
            val result = placesRepository.searchPlaces(
                query = query,
                focusLatitude = originLatitude,
                focusLongitude = originLongitude
            )
        ) {
            is ApiResult.Success -> mapSearchResults = result.data.take(5)
            is ApiResult.Failure -> {
                mapSearchResults = emptyList()
                mapSearchError = result.message
            }
        }
        mapSearchLoading = false
    }

    LaunchedEffect(showMapPicker, mapSelection?.latitude, mapSelection?.longitude, mapSelection?.source) {
        val selection = mapSelection ?: return@LaunchedEffect
        if (!showMapPicker || selection.source != "map") return@LaunchedEffect
        when (val place = placesRepository.reverseGeocode(selection.latitude, selection.longitude)) {
            is ApiResult.Success -> {
                val resolved = place.data
                mapSelection = selection.copy(
                    name = resolved.name,
                    address = resolved.address,
                    category = resolved.category,
                    source = "map-resolved"
                )
            }
            is ApiResult.Failure -> Unit
        }
    }

    Box(Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(HomeBg)
        ) {
            Column(
                modifier = Modifier
                    .weight(1f)
                    .fillMaxWidth()
                    .statusBarsPadding()
                    .padding(horizontal = 18.dp)
            ) {
                Spacer(Modifier.height(10.dp))

                HomeHeader()

                Spacer(Modifier.height(12.dp))

                Text(
                    text = "Hello, ${userName.ifBlank { "TUKI rider" }} 👋",
                    color = HomeDark,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.ExtraBold
                )
                Spacer(Modifier.height(4.dp))
                Text(
                    text = "Where to today?",
                    color = HomeDark,
                    fontSize = 34.sp,
                    lineHeight = 37.sp,
                    fontWeight = FontWeight.ExtraBold,
                    fontFamily = com.example.frontend.ui.theme.TukiDisplayFontFamily
                )
                Spacer(Modifier.height(5.dp))
                Text(
                    text = "Plan your trip or ask our AI for the best way to go.",
                    color = HomeMuted,
                    fontSize = 13.sp,
                    lineHeight = 18.sp,
                    fontWeight = FontWeight.Medium
                )

                Spacer(Modifier.height(14.dp))

                CurrentLocationCard(
                    currentLocationLabel = currentLocationLabel,
                    isLocating = isLocating,
                    onChangeClick = { openMapPicker(HomeMapPickMode.Origin) }
                )

                activeTripDescription?.takeIf { it.isNotBlank() }?.let { description ->
                    Spacer(Modifier.height(12.dp))
                    ActiveTripCard(
                        description = description,
                        onResumeClick = onResumeActiveTrip
                    )
                }

                Spacer(Modifier.height(12.dp))

                DestinationCard(
                    selectedDestination = selectedDestination,
                    canFindRoutes = selectedDestination != null && originLatitude != null && originLongitude != null,
                    onClick = { openMapPicker(HomeMapPickMode.Destination) },
                    onFindRoutesClick = ::submitRoute
                )

                Spacer(Modifier.height(14.dp))

                RecentPlacesSection(
                    recentPlaces = recentPlaces,
                    isLoading = recentPlacesLoading,
                    onViewAllClick = onRecentClick,
                    onPlaceClick = onCommuteClick,
                    onAddShortcutClick = { openMapPicker(HomeMapPickMode.Destination) }
                )

                Spacer(Modifier.height(12.dp))

                AskTukiAiCard(onClick = onAskAiClick)
            }

            BottomBar(
                selectedTab = TukiTab.HOME,
                onHomeClick = {},
                onRecentClick = onRecentClick,
                onFavoritesClick = onFavoritesClick,
                onProfileClick = onProfileClick
            )
        }

        if (showMapPicker) {
            BackHandler { showMapPicker = false }
            HomeMapPickerOverlay(
                mode = mapMode,
                selection = mapSelection,
                searchText = mapSearchText,
                searchResults = mapSearchResults,
                isSearching = mapSearchLoading,
                searchError = mapSearchError,
                originPoint = originLatitude?.let { lat -> originLongitude?.let { lon -> LatLng(lat, lon) } },
                onSearchTextChange = { mapSearchText = it },
                onSearchResultClick = { result ->
                    mapSelection = result
                    mapSearchText = result.name
                    mapSearchResults = emptyList()
                    mapSearchError = null
                },
                onMapClick = { point ->
                    mapSelection = DestinationSearchResultDto(
                        id = "map-${point.latitude}-${point.longitude}",
                        name = if (mapMode == HomeMapPickMode.Origin) "Pinned pickup" else "Pinned destination",
                        latitude = point.latitude,
                        longitude = point.longitude,
                        category = "map",
                        source = "map",
                        address = null
                    )
                },
                onBack = { showMapPicker = false },
                onDone = {
                    val selection = mapSelection
                    if (selection != null) {
                        if (mapMode == HomeMapPickMode.Origin) {
                            originLatitude = selection.latitude
                            originLongitude = selection.longitude
                            currentLocationLabel = selection.name
                        } else {
                            selectedDestination = selection
                        }
                        showMapPicker = false
                    }
                }
            )
        }
    }
}

@Composable
private fun HomeHeader() {
    Row(
        Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Image(
            painter = painterResource(R.drawable.tuki_logo),
            contentDescription = "TUKI",
            modifier = Modifier.size(50.dp)
        )
        Spacer(Modifier.width(8.dp))
        Text(
            text = "TUKI.",
            color = HomeTeal,
            fontSize = 32.sp,
            fontWeight = FontWeight.ExtraBold,
            fontFamily = com.example.frontend.ui.theme.TukiDisplayFontFamily
        )
    }
}

@Composable
private fun ActiveTripCard(
    description: String,
    onResumeClick: () -> Unit
) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onResumeClick),
        shape = RoundedCornerShape(22.dp),
        color = HomeDark,
        shadowElevation = 4.dp
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 18.dp, vertical = 15.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(42.dp)
                    .background(HomeOrange.copy(alpha = 0.18f), CircleShape),
                contentAlignment = Alignment.Center
            ) {
                Text("▶", color = HomeOrange, fontSize = 18.sp, fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    "TRIP IN PROGRESS",
                    color = HomeOrange,
                    fontSize = 10.sp,
                    fontWeight = FontWeight.ExtraBold
                )
                Spacer(Modifier.height(2.dp))
                Text(
                    description,
                    color = Color.White,
                    fontSize = 14.sp,
                    fontWeight = FontWeight.Bold,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
            Spacer(Modifier.width(8.dp))
            Text("Resume  →", color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.ExtraBold)
        }
    }
}

@Composable
private fun CurrentLocationCard(
    currentLocationLabel: String,
    isLocating: Boolean,
    onChangeClick: () -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        color = HomeCurrentSky,
        shadowElevation = 2.dp
    ) {
        Row(
            Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Surface(Modifier.size(52.dp), shape = RoundedCornerShape(18.dp), color = Color.White.copy(alpha = 0.42f)) {
                Box(contentAlignment = Alignment.Center) {
                    Text("⊙", color = HomeTeal, fontSize = 35.sp, fontWeight = FontWeight.Bold)
                }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text("CURRENT LOCATION", color = HomeTeal, fontSize = 11.sp, fontWeight = FontWeight.ExtraBold)
                Spacer(Modifier.height(3.dp))
                if (isLocating) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        CircularProgressIndicator(Modifier.size(15.dp), color = HomeTeal, strokeWidth = 2.dp)
                        Spacer(Modifier.width(7.dp))
                        Text("Locating you...", color = HomeDark, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
                    }
                } else {
                    Text(
                        text = currentLocationLabel,
                        color = HomeDark,
                        fontSize = 21.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        fontWeight = FontWeight.ExtraBold
                    )
                }
                Text("Mabalacat City", color = HomeMuted, fontSize = 14.sp)
            }
            Box(Modifier.width(1.dp).height(55.dp).background(HomeTeal.copy(alpha = 0.18f)))
            Column(
                Modifier
                    .width(86.dp)
                    .clickable(onClick = onChangeClick)
                    .padding(start = 10.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text("✎", color = HomeTeal, fontSize = 25.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(4.dp))
                Text("Tap to\nchange", color = HomeTeal, fontSize = 12.sp, lineHeight = 15.sp, fontWeight = FontWeight.ExtraBold)
            }
        }
    }
}

@Composable
private fun DestinationCard(
    selectedDestination: DestinationSearchResultDto?,
    canFindRoutes: Boolean,
    onClick: () -> Unit,
    onFindRoutesClick: () -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        color = HomeSurface,
        shadowElevation = 3.dp
    ) {
        Row(
            Modifier
                .clickable(onClick = onClick)
                .padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Surface(Modifier.size(52.dp), shape = RoundedCornerShape(18.dp), color = HomeWarm) {
                Box(contentAlignment = Alignment.Center) {
                    Text("●", color = HomeOrange, fontSize = 31.sp, fontWeight = FontWeight.Bold)
                    Text("⌄", color = Color.White, fontSize = 22.sp, fontWeight = FontWeight.ExtraBold)
                }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text("DESTINATION", color = HomeOrange, fontSize = 11.sp, fontWeight = FontWeight.ExtraBold)
                Spacer(Modifier.height(4.dp))
                Text(
                    selectedDestination?.name ?: "Where are you going?",
                    color = HomeDark,
                    fontSize = 20.sp,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    fontWeight = FontWeight.ExtraBold,
                    fontFamily = com.example.frontend.ui.theme.TukiDisplayFontFamily
                )
                Spacer(Modifier.height(8.dp))
                Row(
                    Modifier
                        .fillMaxWidth(0.82f)
                        .height(40.dp)
                        .widthIn(min = 190.dp, max = 250.dp)
                        .background(Color.White.copy(alpha = 0.92f), RoundedCornerShape(16.dp))
                        .padding(horizontal = 13.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text("⌕", color = HomeDark, fontSize = 20.sp)
                    Spacer(Modifier.width(8.dp))
                    Text(
                        if (selectedDestination == null) "Search or enter a place" else "Tap to change destination",
                        color = HomeMuted,
                        fontSize = 13.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
                if (selectedDestination != null) {
                    Spacer(Modifier.height(9.dp))
                    Box(
                        Modifier
                            .fillMaxWidth()
                            .background(if (canFindRoutes) HomeOrange else HomeOrange.copy(alpha = 0.45f), RoundedCornerShape(16.dp))
                            .clickable(enabled = canFindRoutes, onClick = onFindRoutesClick)
                            .padding(vertical = 11.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Text("Find Routes", color = Color.White, fontSize = 15.sp, fontWeight = FontWeight.ExtraBold)
                    }
                }
            }
            Spacer(Modifier.width(4.dp))
            Text("›", color = HomeDark, fontSize = 32.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun RecentPlacesSection(
    recentPlaces: List<RecentCommute>,
    isLoading: Boolean,
    onViewAllClick: () -> Unit,
    onPlaceClick: (RecentCommute) -> Unit,
    onAddShortcutClick: () -> Unit
) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Text("Recent places", Modifier.weight(1f), color = HomeDark, fontSize = 16.sp, fontWeight = FontWeight.ExtraBold)
        Text(
            "View all",
            color = HomeTeal,
            fontSize = 13.sp,
            fontWeight = FontWeight.ExtraBold,
            modifier = Modifier.clickable(onClick = onViewAllClick)
        )
    }
    Spacer(Modifier.height(8.dp))

    when {
        isLoading -> {
            Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(19.dp), color = HomeSurface, shadowElevation = 1.dp) {
                Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                    CircularProgressIndicator(Modifier.size(17.dp), color = HomeTeal, strokeWidth = 2.dp)
                    Spacer(Modifier.width(10.dp))
                    Text("Finding your recent places...", color = HomeMuted, fontSize = 12.sp)
                }
            }
        }
        recentPlaces.isEmpty() -> {
            EmptyRecentPlacesCard(onClick = onAddShortcutClick)
        }
        else -> {
            Row(
                Modifier
                    .fillMaxWidth()
                    .horizontalScroll(rememberScrollState()),
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                recentPlaces.forEach { commute ->
                    RecentPlaceCard(commute = commute, onClick = { onPlaceClick(commute) })
                }
                AddShortcutCard(onClick = onAddShortcutClick)
            }
        }
    }
}

@Composable
private fun RecentPlaceCard(commute: RecentCommute, onClick: () -> Unit) {
    Surface(
        modifier = Modifier
            .width(108.dp)
            .height(104.dp)
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = HomeSurface,
        shadowElevation = 1.dp
    ) {
        Column(
            Modifier.padding(horizontal = 12.dp, vertical = 12.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Surface(Modifier.size(40.dp), shape = CircleShape, color = HomeWarm) {
                Box(contentAlignment = Alignment.Center) { Text(recentIcon(commute), color = HomeTeal, fontSize = 22.sp) }
            }
            Spacer(Modifier.height(7.dp))
            Text(
                commute.destination,
                color = HomeDark,
                fontSize = 12.sp,
                lineHeight = 14.sp,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
                fontWeight = FontWeight.ExtraBold
            )
            Text(
                "${commute.minutes.coerceAtLeast(0)} min",
                color = HomeMuted,
                fontSize = 11.sp,
                fontFamily = com.example.frontend.ui.theme.TukiUtilityFontFamily
            )
        }
    }
}

@Composable
private fun AddShortcutCard(onClick: () -> Unit) {
    Surface(
        modifier = Modifier
            .width(108.dp)
            .height(104.dp)
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = HomeSurface,
        shadowElevation = 1.dp
    ) {
        Column(
            Modifier.padding(horizontal = 12.dp, vertical = 12.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Surface(Modifier.size(40.dp), shape = CircleShape, color = HomeWarm) {
                Box(contentAlignment = Alignment.Center) { Text("+", color = HomeTeal, fontSize = 29.sp, fontWeight = FontWeight.Bold) }
            }
            Spacer(Modifier.height(10.dp))
            Text("Add\nshortcut", color = HomeDark, fontSize = 13.sp, lineHeight = 15.sp, fontWeight = FontWeight.ExtraBold)
        }
    }
}

@Composable
private fun EmptyRecentPlacesCard(onClick: () -> Unit) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .height(86.dp)
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = HomeSurface,
        shadowElevation = 1.dp
    ) {
        Row(Modifier.padding(horizontal = 14.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(Modifier.size(42.dp), shape = CircleShape, color = HomeWarm) {
                Box(contentAlignment = Alignment.Center) { Text("+", color = HomeTeal, fontSize = 28.sp, fontWeight = FontWeight.Bold) }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text("Start your journey with TUKI", color = HomeDark, fontSize = 15.sp, fontWeight = FontWeight.ExtraBold)
                Spacer(Modifier.height(3.dp))
                Text("Pick a destination and your recent places will appear here.", color = HomeMuted, fontSize = 11.sp, lineHeight = 15.sp)
            }
            Text("›", color = HomeTeal, fontSize = 29.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun AskTukiAiCard(onClick: () -> Unit) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .height(88.dp)
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(22.dp),
        color = HomeAiSurface,
        shadowElevation = 4.dp
    ) {
        Row(Modifier.padding(horizontal = 15.dp, vertical = 13.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(Modifier.size(48.dp), shape = CircleShape, color = HomeTeal.copy(alpha = 0.35f)) {
                Box(contentAlignment = Alignment.Center) { Text("✨", fontSize = 24.sp) }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("Ask TUKI AI", color = Color.White, fontSize = 19.sp, fontWeight = FontWeight.ExtraBold)
                    Spacer(Modifier.width(8.dp))
                    Box(
                        Modifier
                            .background(HomeOrange, RoundedCornerShape(9.dp))
                            .padding(horizontal = 8.dp, vertical = 4.dp)
                    ) {
                        Text("NEW", color = Color.White, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                    }
                }
                Spacer(Modifier.height(4.dp))
                Text("Let AI find the best way to go.", color = Color.White.copy(alpha = 0.82f), fontSize = 12.sp)
            }
            Text("›", color = Color.White, fontSize = 31.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun HomeMapPickerOverlay(
    mode: HomeMapPickMode,
    selection: DestinationSearchResultDto?,
    searchText: String,
    searchResults: List<DestinationSearchResultDto>,
    isSearching: Boolean,
    searchError: String?,
    originPoint: LatLng?,
    onSearchTextChange: (String) -> Unit,
    onSearchResultClick: (DestinationSearchResultDto) -> Unit,
    onMapClick: (LatLng) -> Unit,
    onBack: () -> Unit,
    onDone: () -> Unit
) {
    val selectedPoint = selection?.let { LatLng(it.latitude, it.longitude) }

    val markerColor = HomeTeal
    val markerSurface = HomeSoft

    Box(Modifier.fillMaxSize().background(MapPanel)) {
        MapScreen(
            routePoints = emptyList(),
            modifier = Modifier.fillMaxSize(),
            startPoint = if (mode == HomeMapPickMode.Origin) selectedPoint ?: originPoint else originPoint,
            selectedDestination = null,
            onMapClick = onMapClick,
            visualStyle = MapVisualStyle.LiveTrip,
            showDeviceLocation = false
        )

        Column(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(horizontal = 14.dp, vertical = 10.dp)
        ) {
            Row(
                Modifier
                    .fillMaxWidth()
                    .background(MapPanel.copy(alpha = 0.95f), RoundedCornerShape(24.dp))
                    .padding(horizontal = 10.dp, vertical = 8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(Modifier.size(38.dp).clickable(onClick = onBack), contentAlignment = Alignment.Center) {
                    Text("‹", color = Color(0xFF75C7E8), fontSize = 35.sp, fontWeight = FontWeight.Bold)
                }
                Box(
                    Modifier
                        .background(MapYellow, RoundedCornerShape(10.dp))
                        .padding(horizontal = 10.dp, vertical = 8.dp)
                ) {
                    Text("Mabalacat", color = HomeDark, fontSize = 14.sp, fontWeight = FontWeight.SemiBold)
                }
                Spacer(Modifier.width(8.dp))
                TextField(
                    value = searchText,
                    onValueChange = onSearchTextChange,
                    placeholder = { Text("Enter address to search", color = Color.White.copy(alpha = 0.55f), fontSize = 16.sp) },
                    singleLine = true,
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = Color.Transparent,
                        unfocusedContainerColor = Color.Transparent,
                        disabledContainerColor = Color.Transparent,
                        focusedIndicatorColor = Color.Transparent,
                        unfocusedIndicatorColor = Color.Transparent,
                        disabledIndicatorColor = Color.Transparent,
                        focusedTextColor = Color.White,
                        unfocusedTextColor = Color.White
                    ),
                    modifier = Modifier.weight(1f)
                )
            }

            if (isSearching || searchError != null || searchResults.isNotEmpty()) {
                Column(
                    Modifier
                        .fillMaxWidth()
                        .padding(top = 8.dp)
                        .background(MapPanel.copy(alpha = 0.95f), RoundedCornerShape(18.dp))
                        .padding(vertical = 7.dp)
                ) {
                    if (isSearching) {
                        Row(Modifier.padding(horizontal = 14.dp, vertical = 9.dp), verticalAlignment = Alignment.CenterVertically) {
                            CircularProgressIndicator(Modifier.size(16.dp), color = MapYellow, strokeWidth = 2.dp)
                            Spacer(Modifier.width(9.dp))
                            Text("Searching nearby places...", color = Color.White.copy(alpha = 0.75f), fontSize = 13.sp)
                        }
                    }
                    searchError?.let { Text(it, Modifier.padding(horizontal = 14.dp, vertical = 8.dp), color = com.example.frontend.ui.theme.TukiDanger, fontSize = 12.sp) }
                    searchResults.forEach { result ->
                        Row(
                            Modifier
                                .fillMaxWidth()
                                .clickable { onSearchResultClick(result) }
                                .padding(horizontal = 14.dp, vertical = 9.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Box(Modifier.size(30.dp).background(Color.White.copy(alpha = 0.12f), CircleShape), contentAlignment = Alignment.Center) {
                                Text("⌖", color = MapYellow, fontSize = 17.sp)
                            }
                            Spacer(Modifier.width(10.dp))
                            Column(Modifier.weight(1f)) {
                                Text(result.name, color = Color.White, fontSize = 13.sp, fontWeight = FontWeight.ExtraBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                result.address?.takeIf { it.isNotBlank() }?.let { address ->
                                    Text(address, color = Color.White.copy(alpha = 0.62f), fontSize = 11.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                }
                            }
                        }
                    }
                }
            }
        }

        Column(
            Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .background(MapPanel, RoundedCornerShape(topStart = 28.dp, topEnd = 28.dp))
                .navigationBarsPadding()
                .padding(horizontal = 22.dp, vertical = 22.dp)
        ) {
            Text(
                if (mode == HomeMapPickMode.Origin) "Pick-up point" else "Destination",
                color = Color.White,
                fontSize = 25.sp,
                fontWeight = FontWeight.ExtraBold
            )
            Spacer(Modifier.height(18.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(34.dp).background(markerSurface, CircleShape), contentAlignment = Alignment.Center) {
                    Box(Modifier.size(25.dp).background(markerColor, CircleShape))
                    Box(Modifier.size(15.dp).background(MapPanel, CircleShape))
                }
                Spacer(Modifier.width(16.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        selection?.name ?: "Tap the map or search for a place",
                        color = Color.White,
                        fontSize = 18.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        selection?.address ?: "Move around the map, then press Done.",
                        color = Color.White.copy(alpha = 0.55f),
                        fontSize = 13.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }
            Spacer(Modifier.height(20.dp))
            Box(
                Modifier
                    .fillMaxWidth()
                    .background(if (selection != null) MapYellow else MapYellow.copy(alpha = 0.45f), RoundedCornerShape(28.dp))
                    .clickable(enabled = selection != null, onClick = onDone)
                    .padding(vertical = 17.dp),
                contentAlignment = Alignment.Center
            ) {
                Text("Done", color = HomeDark, fontSize = 20.sp, fontWeight = FontWeight.ExtraBold)
            }
        }
    }
}

private fun recentIcon(commute: RecentCommute): String = when {
    commute.destination.contains("library", true) -> "▦"
    commute.destination.contains("mall", true) || commute.destination.contains("city", true) -> "▣"
    commute.destination.contains("terminal", true) || commute.destination.contains("station", true) -> "▤"
    else -> "⌖"
}

private fun String.routeOriginLabel(): String = when {
    isBlank() -> "Current location"
    contains("locating", true) -> "Current location"
    contains("permission", true) -> "Current location"
    contains("unable", true) -> "Current location"
    else -> this
}

private fun android.content.Context.hasLocationPermission(): Boolean {
    return ContextCompat.checkSelfPermission(
        this,
        Manifest.permission.ACCESS_FINE_LOCATION
    ) == PackageManager.PERMISSION_GRANTED ||
            ContextCompat.checkSelfPermission(
                this,
                Manifest.permission.ACCESS_COARSE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED
}
