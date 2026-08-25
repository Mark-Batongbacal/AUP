package com.example.frontend.screens

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.MapScreen
import com.example.frontend.MapVisualStyle
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.location.isLocationSupported
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.places.PlacesRepository
import com.example.frontend.ui.theme.TukiThemeRuntime
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import org.maplibre.android.geometry.LatLng
import kotlin.math.abs

private val LiveTripMapPanel = Color(0xFF0C303A)
private val LiveTripMapSelector = Color(0xFFF8F5EC)
private val LiveTripMapAction = Color(0xFFFF8A1D)
private val LiveTripMapTeal: Color
    get() = if (TukiThemeRuntime.darkMode) Color(0xFF43B5BD) else Color(0xFF2C8E95)
private val LiveTripMapSoft: Color
    get() = if (TukiThemeRuntime.darkMode) Color(0xFF17333D) else Color(0xFFEAF1EE)

/**
 * Live Trip destination selector using the same full-screen map/search UX as HomeMapPickerOverlay.
 * Only confirmation behavior differs: Done keeps the current trip session and calls the existing
 * DESTINATION_CHANGED reroute flow through TripTrackingScreen.
 */
@Composable
fun LiveTripDestinationPickerScreen(
    placesRepository: PlacesRepository,
    focusLatitude: Double?,
    focusLongitude: Double?,
    onBack: () -> Unit,
    onDestinationSelected: (DestinationSearchResultDto) -> Unit
) {
    val context = LocalContext.current
    val focusManager = LocalFocusManager.current
    val scope = rememberCoroutineScope()

    var currentFocusLatitude by remember { mutableStateOf(focusLatitude) }
    var currentFocusLongitude by remember { mutableStateOf(focusLongitude) }
    var areaLabel by remember { mutableStateOf(TukiInterfaceText.currentArea) }
    var selection by remember { mutableStateOf<DestinationSearchResultDto?>(null) }
    var searchText by remember { mutableStateOf("") }
    var searchResults by remember { mutableStateOf<List<DestinationSearchResultDto>>(emptyList()) }
    var isSearching by remember { mutableStateOf(false) }
    var isSearchingMore by remember { mutableStateOf(false) }
    var searchExpanded by remember { mutableStateOf(false) }
    var searchError by remember { mutableStateOf<String?>(null) }
    var showUnsupportedLocationDialog by remember { mutableStateOf(false) }

    BackHandler(onBack = onBack)

    LaunchedEffect(focusLatitude, focusLongitude) {
        var lat = focusLatitude
        var lon = focusLongitude
        if (lat == null || lon == null) {
            context.currentDeviceLocation()?.let { location ->
                lat = location.latitude
                lon = location.longitude
            }
        }
        currentFocusLatitude = lat
        currentFocusLongitude = lon

        if (lat != null && lon != null) {
            when (val result = placesRepository.reverseGeocode(lat!!, lon!!)) {
                is ApiResult.Success -> {
                    areaLabel = result.data.locality?.takeIf { it.isNotBlank() }
                        ?: TukiInterfaceText.currentArea
                }
                is ApiResult.Failure -> Unit
            }
        }
    }

    LaunchedEffect(searchText, currentFocusLatitude, currentFocusLongitude) {
        val query = searchText.trim()
        searchExpanded = false
        isSearchingMore = false
        if (query.length < 2) {
            searchResults = emptyList()
            searchError = null
            isSearching = false
            return@LaunchedEffect
        }

        delay(300)
        isSearching = true
        searchError = null
        when (
            val result = placesRepository.searchPlaces(
                query = query,
                focusLatitude = currentFocusLatitude,
                focusLongitude = currentFocusLongitude
            )
        ) {
            is ApiResult.Success -> searchResults = result.data.take(5)
            is ApiResult.Failure -> {
                searchResults = emptyList()
                searchError = result.message
            }
        }
        isSearching = false
    }

    LaunchedEffect(selection?.latitude, selection?.longitude, selection?.source) {
        val selected = selection ?: return@LaunchedEffect
        if (selected.source != "map") return@LaunchedEffect
        when (val result = placesRepository.reverseGeocode(selected.latitude, selected.longitude)) {
            is ApiResult.Success -> {
                val resolved = result.data
                selection = selected.copy(
                    name = resolved.name,
                    address = resolved.address,
                    category = resolved.category,
                    source = "map-resolved",
                    locality = resolved.locality
                )
            }
            is ApiResult.Failure -> Unit
        }
    }

    if (showUnsupportedLocationDialog) {
        LocationNotSupportedDialog { showUnsupportedLocationDialog = false }
    }

    val selectedPoint = selection?.let { LatLng(it.latitude, it.longitude) }
    val originPoint = currentFocusLatitude?.let { lat ->
        currentFocusLongitude?.let { lon -> LatLng(lat, lon) }
    }
    val canSearchMore = searchText.trim().length >= 2 && !isSearching && !searchExpanded

    Box(Modifier.fillMaxSize().background(LiveTripMapPanel)) {
        MapScreen(
            routePoints = emptyList(),
            modifier = Modifier.fillMaxSize(),
            startPoint = originPoint,
            selectedDestination = null,
            finalDestination = selectedPoint,
            cameraFocusPoint = selectedPoint,
            onMapClick = { point ->
                selection = DestinationSearchResultDto(
                    id = "map-${point.latitude}-${point.longitude}",
                    name = TukiInterfaceText.pinnedDestination,
                    latitude = point.latitude,
                    longitude = point.longitude,
                    category = "map",
                    source = "map",
                    address = null
                )
                searchText = ""
                searchResults = emptyList()
                searchExpanded = false
                searchError = null
            },
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
                    .background(LiveTripMapPanel.copy(alpha = 0.95f), RoundedCornerShape(24.dp))
                    .padding(horizontal = 10.dp, vertical = 8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    Modifier.size(38.dp).clickable(onClick = onBack),
                    contentAlignment = Alignment.Center
                ) {
                    Text("‹", color = Color(0xFF75C7E8), fontSize = 35.sp, fontWeight = FontWeight.Bold)
                }
                Box(
                    Modifier
                        .widthIn(max = 120.dp)
                        .background(LiveTripMapSelector, RoundedCornerShape(10.dp))
                        .padding(horizontal = 10.dp, vertical = 8.dp)
                ) {
                    Text(
                        areaLabel.ifBlank { TukiInterfaceText.currentArea },
                        color = Color(0xFF153E4B),
                        fontSize = 14.sp,
                        fontWeight = FontWeight.SemiBold,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
                Spacer(Modifier.width(8.dp))
                TextField(
                    value = searchText,
                    onValueChange = {
                        searchText = it
                        if (selection?.name != it) selection = null
                    },
                    placeholder = {
                        Text(
                            TukiInterfaceText.searchLocation,
                            color = Color.White.copy(alpha = 0.55f),
                            fontSize = 16.sp
                        )
                    },
                    singleLine = true,
                    trailingIcon = {
                        if (searchText.isNotEmpty()) {
                            Text(
                                "✕",
                                color = Color.White.copy(alpha = 0.7f),
                                fontSize = 18.sp,
                                modifier = Modifier
                                    .padding(end = 8.dp)
                                    .clickable {
                                        searchText = ""
                                        selection = null
                                    }
                            )
                        }
                    },
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                    keyboardActions = KeyboardActions(onSearch = { focusManager.clearFocus() }),
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

            if (isSearching || isSearchingMore || canSearchMore || searchError != null || searchResults.isNotEmpty()) {
                val scrollState = rememberScrollState()
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 300.dp)
                        .padding(top = 8.dp)
                        .background(LiveTripMapPanel.copy(alpha = 0.95f), RoundedCornerShape(18.dp))
                        .verticalScroll(scrollState)
                        .padding(vertical = 7.dp)
                ) {
                    if (isSearching) {
                        Row(
                            Modifier.padding(horizontal = 14.dp, vertical = 9.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            CircularProgressIndicator(
                                Modifier.size(16.dp),
                                color = LiveTripMapTeal,
                                strokeWidth = 2.dp
                            )
                            Spacer(Modifier.width(9.dp))
                            Text(
                                TukiInterfaceText.searchingNearbyPlaces,
                                color = Color.White.copy(alpha = 0.75f),
                                fontSize = 13.sp
                            )
                        }
                    }

                    searchError?.let {
                        Text(
                            it,
                            Modifier.padding(horizontal = 14.dp, vertical = 8.dp),
                            color = com.example.frontend.ui.theme.TukiDanger,
                            fontSize = 12.sp
                        )
                    }

                    searchResults.forEach { result ->
                        Row(
                            Modifier
                                .fillMaxWidth()
                                .clickable {
                                    selection = result
                                    searchText = result.name
                                    searchResults = emptyList()
                                    searchExpanded = false
                                    searchError = null
                                }
                                .padding(horizontal = 14.dp, vertical = 9.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Box(
                                Modifier.size(30.dp).background(Color.White.copy(alpha = 0.12f), CircleShape),
                                contentAlignment = Alignment.Center
                            ) {
                                Text("⌖", color = LiveTripMapTeal, fontSize = 17.sp)
                            }
                            Spacer(Modifier.width(10.dp))
                            Column(Modifier.weight(1f)) {
                                Text(
                                    result.name,
                                    color = Color.White,
                                    fontSize = 13.sp,
                                    fontWeight = FontWeight.ExtraBold,
                                    maxLines = 2,
                                    overflow = TextOverflow.Ellipsis
                                )
                                result.address?.takeIf { it.isNotBlank() }?.let { address ->
                                    Text(
                                        address,
                                        color = Color.White.copy(alpha = 0.62f),
                                        fontSize = 11.sp,
                                        maxLines = 2,
                                        overflow = TextOverflow.Ellipsis
                                    )
                                }
                            }
                        }
                    }

                    when {
                        isSearchingMore -> {
                            Row(
                                Modifier.padding(horizontal = 14.dp, vertical = 10.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                CircularProgressIndicator(
                                    Modifier.size(16.dp),
                                    color = LiveTripMapTeal,
                                    strokeWidth = 2.dp
                                )
                                Spacer(Modifier.width(9.dp))
                                Text(
                                    TukiInterfaceText.searchingMorePlaces,
                                    color = Color.White.copy(alpha = 0.75f),
                                    fontSize = 13.sp
                                )
                            }
                        }

                        canSearchMore -> {
                            Box(
                                Modifier
                                    .fillMaxWidth()
                                    .clickable {
                                        val query = searchText.trim()
                                        if (query.length < 2 || isSearching || isSearchingMore || searchExpanded) {
                                            return@clickable
                                        }
                                        scope.launch {
                                            isSearchingMore = true
                                            searchError = null
                                            when (
                                                val result = placesRepository.searchMorePlaces(
                                                    query = query,
                                                    focusLatitude = currentFocusLatitude,
                                                    focusLongitude = currentFocusLongitude
                                                )
                                            ) {
                                                is ApiResult.Success -> {
                                                    if (searchText.trim() == query) {
                                                        searchResults = mergeLiveTripMapResults(
                                                            searchResults,
                                                            result.data
                                                        ).take(12)
                                                        searchExpanded = true
                                                    }
                                                }

                                                is ApiResult.Failure -> {
                                                    if (searchText.trim() == query) {
                                                        searchError = result.message
                                                    }
                                                }
                                            }
                                            isSearchingMore = false
                                        }
                                    }
                                    .padding(horizontal = 14.dp, vertical = 12.dp),
                                contentAlignment = Alignment.Center
                            ) {
                                Text(
                                    TukiInterfaceText.morePlaces,
                                    color = LiveTripMapTeal,
                                    fontSize = 13.sp,
                                    fontWeight = FontWeight.ExtraBold
                                )
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
                .background(
                    LiveTripMapPanel,
                    RoundedCornerShape(topStart = 28.dp, topEnd = 28.dp)
                )
                .navigationBarsPadding()
                .padding(horizontal = 22.dp, vertical = 22.dp)
        ) {
            Text(
                TukiInterfaceText.destination,
                color = Color.White,
                fontSize = 25.sp,
                fontWeight = FontWeight.ExtraBold
            )
            Spacer(Modifier.width(1.dp))
            Spacer(Modifier.padding(top = 18.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    Modifier.size(34.dp).background(LiveTripMapSoft, CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Box(Modifier.size(25.dp).background(LiveTripMapTeal, CircleShape))
                    Box(Modifier.size(15.dp).background(LiveTripMapPanel, CircleShape))
                }
                Spacer(Modifier.width(16.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        selection?.name ?: TukiInterfaceText.tapMapOrSearchPlace,
                        color = Color.White,
                        fontSize = 18.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        selection?.address ?: TukiInterfaceText.moveMapThenDone,
                        color = Color.White.copy(alpha = 0.55f),
                        fontSize = 13.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }
            Spacer(Modifier.padding(top = 20.dp))
            Box(
                Modifier
                    .fillMaxWidth()
                    .background(
                        if (selection != null) LiveTripMapAction else LiveTripMapAction.copy(alpha = 0.45f),
                        RoundedCornerShape(28.dp)
                    )
                    .clickable(enabled = selection != null) {
                        val selected = selection ?: return@clickable
                        if (!isLocationSupported(selected.latitude, selected.longitude)) {
                            showUnsupportedLocationDialog = true
                            return@clickable
                        }
                        onDestinationSelected(selected)
                    }
                    .padding(vertical = 17.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    TukiInterfaceText.done,
                    color = Color.White,
                    fontSize = 20.sp,
                    fontWeight = FontWeight.ExtraBold
                )
            }
        }
    }
}

private fun mergeLiveTripMapResults(
    existing: List<DestinationSearchResultDto>,
    expanded: List<DestinationSearchResultDto>
): List<DestinationSearchResultDto> {
    val merged = mutableListOf<DestinationSearchResultDto>()
    (existing + expanded).forEach { candidate ->
        if (merged.none { current -> liveTripMapPlacesLikelySame(current, candidate) }) {
            merged += candidate
        }
    }
    return merged
}

private fun liveTripMapPlacesLikelySame(
    first: DestinationSearchResultDto,
    second: DestinationSearchResultDto
): Boolean {
    val firstName = normalizeLiveTripMapPlaceText(first.name)
    val secondName = normalizeLiveTripMapPlaceText(second.name)
    if (firstName.isEmpty() || firstName != secondName) return false

    val closeCoordinates = abs(first.latitude - second.latitude) <= 0.002 &&
        abs(first.longitude - second.longitude) <= 0.002
    val firstAddress = normalizeLiveTripMapPlaceText(first.address.orEmpty())
    val secondAddress = normalizeLiveTripMapPlaceText(second.address.orEmpty())
    val sameAddress = firstAddress.isNotEmpty() && firstAddress == secondAddress
    return closeCoordinates || sameAddress
}

private fun normalizeLiveTripMapPlaceText(value: String): String =
    value.lowercase().filter { it.isLetterOrDigit() }
