package com.example.frontend.screens

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Surface
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.MapScreen
import com.example.frontend.core.location.LocationDetectionFailureMessage
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.location.isLocationSupported
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.places.PlacesRepository
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import org.maplibre.android.geometry.LatLng
import kotlin.math.abs

import androidx.compose.material3.MaterialTheme
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiDeepTeal
import com.example.frontend.ui.theme.TukiSky
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTealSurface
import com.example.frontend.ui.theme.TukiOrangeSurface

private enum class MapPickMode {
    Origin,
    Destination
}

@Composable
fun DestinationSearchScreen(
    origin: String,
    placesRepository: PlacesRepository,
    onBack: () -> Unit = {},
    onFindRoutes: (
        destination: DestinationSearchResultDto,
        originName: String,
        originLatitude: Double,
        originLongitude: Double
    ) -> Unit = { _, _, _, _ -> }
) {
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
    val focusManager = LocalFocusManager.current

    var originText by remember { mutableStateOf(origin) }
    var destinationText by remember { mutableStateOf("") }
    var showMap by remember { mutableStateOf(false) }
    var mapPickMode by remember { mutableStateOf(MapPickMode.Destination) }
    var currentLatitude by remember { mutableStateOf<Double?>(null) }
    var currentLongitude by remember { mutableStateOf<Double?>(null) }
    var currentLocationLabel by remember { mutableStateOf(origin) }
    var originSearchResults by remember { mutableStateOf<List<DestinationSearchResultDto>>(emptyList()) }
    var isSearchingOrigin by remember { mutableStateOf(false) }
    var originSearchError by remember { mutableStateOf<String?>(null) }
    var locationError by remember { mutableStateOf<String?>(null) }
    var selectedDestination by remember { mutableStateOf<DestinationSearchResultDto?>(null) }
    var searchResults by remember { mutableStateOf<List<DestinationSearchResultDto>>(emptyList()) }
    var isSearching by remember { mutableStateOf(false) }
    var isSearchingMore by remember { mutableStateOf(false) }
    var hasExpandedSearch by remember { mutableStateOf(false) }
    var searchError by remember { mutableStateOf<String?>(null) }
    var showUnsupportedLocationDialog by remember { mutableStateOf(false) }

    fun validateSupported(latitude: Double, longitude: Double): Boolean {
        val supported = isLocationSupported(latitude, longitude)
        if (!supported) {
            showUnsupportedLocationDialog = true
        }
        return supported
    }

    suspend fun useCurrentDeviceLocation() {
        locationError = null
        val location = context.currentDeviceLocation()
        if (location == null) {
            locationError = LocationDetectionFailureMessage
            return
        }

        currentLatitude = location.latitude
        currentLongitude = location.longitude
        currentLocationLabel = "Current location"
        originText = "Current location"
        originSearchResults = emptyList()
        validateSupported(location.latitude, location.longitude)
    }

    LaunchedEffect(Unit) {
        useCurrentDeviceLocation()
    }

    LaunchedEffect(currentLatitude, currentLongitude) {
        val lat = currentLatitude ?: return@LaunchedEffect
        val lon = currentLongitude ?: return@LaunchedEffect
        when (val result = placesRepository.reverseGeocode(lat, lon)) {
            is ApiResult.Success -> {
                currentLocationLabel = result.data.name
                originText = result.data.name
            }
            is ApiResult.Failure -> Unit
        }
    }

    LaunchedEffect(originText, currentLatitude, currentLongitude) {
        val query = originText.trim()
        if (query.length < 2 || currentLocationLabel == query) {
            originSearchResults = emptyList()
            originSearchError = null
            return@LaunchedEffect
        }

        delay(350)
        isSearchingOrigin = true
        originSearchError = null

        when (
            val result = placesRepository.searchPlaces(
                query = query,
                focusLatitude = currentLatitude,
                focusLongitude = currentLongitude
            )
        ) {
            is ApiResult.Success -> originSearchResults = result.data.take(5)
            is ApiResult.Failure -> {
                originSearchResults = emptyList()
                originSearchError = result.message
            }
        }

        isSearchingOrigin = false
    }

    LaunchedEffect(selectedDestination?.latitude, selectedDestination?.longitude, selectedDestination?.source) {
        val selected = selectedDestination ?: return@LaunchedEffect
        if (selected.source != "map") return@LaunchedEffect
        when (val result = placesRepository.reverseGeocode(selected.latitude, selected.longitude)) {
            is ApiResult.Success -> {
                val resolved = result.data
                selectedDestination = selected.copy(
                    name = resolved.name,
                    address = resolved.address,
                    category = resolved.category,
                    source = "map-pelias"
                )
                destinationText = resolved.name
            }
            is ApiResult.Failure -> Unit
        }
    }

    LaunchedEffect(destinationText, currentLatitude, currentLongitude) {
        val query = destinationText.trim()
        if (query.length < 2 || selectedDestination?.name == query) {
            searchResults = emptyList()
            searchError = null
            isSearchingMore = false
            hasExpandedSearch = false
            return@LaunchedEffect
        }

        delay(350)
        isSearching = true
        isSearchingMore = false
        hasExpandedSearch = false
        searchError = null

        when (
            val result = placesRepository.searchPlaces(
                query = query,
                focusLatitude = currentLatitude,
                focusLongitude = currentLongitude
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

    if (showUnsupportedLocationDialog) {
        LocationNotSupportedDialog {
            showUnsupportedLocationDialog = false
        }
    }

    if (showMap) {
        BackHandler { showMap = false }
        val isPickingOrigin = mapPickMode == MapPickMode.Origin
        val mapOriginLatitude = currentLatitude
        val mapOriginLongitude = currentLongitude
        val mapOriginPoint = if (isPickingOrigin && mapOriginLatitude != null && mapOriginLongitude != null) {
            LatLng(mapOriginLatitude, mapOriginLongitude)
        } else {
            null
        }

        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(TukiInk.copy(alpha = 0.35f)),
            contentAlignment = Alignment.Center
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth(0.92f)
                    .background(TukiCream, RoundedCornerShape(24.dp))
                    .padding(16.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = if (isPickingOrigin) "Pick origin" else "Pick destination",
                        color = TukiInk,
                        style = MaterialTheme.typography.titleLarge
                    )
                    Text(
                        text = "✕",
                        color = TukiInk,
                        style = MaterialTheme.typography.titleLarge,
                        modifier = Modifier.clickable { showMap = false }
                    )
                }

                Spacer(modifier = Modifier.height(12.dp))

                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(420.dp)
                        .clip(RoundedCornerShape(18.dp))
                ) {
                    MapScreen(
                        routePoints = emptyList(),
                        modifier = Modifier.fillMaxSize(),
                        startPoint = mapOriginPoint,
                        finalDestination = if (!isPickingOrigin) selectedDestination?.let {
                            LatLng(it.latitude, it.longitude)
                        } else null,
                        onMapClick = { point ->
                            if (isPickingOrigin) {
                                currentLatitude = point.latitude
                                currentLongitude = point.longitude
                                currentLocationLabel = "Pinned origin"
                                originText = "Pinned origin"
                                originSearchResults = emptyList()
                                locationError = null
                                validateSupported(point.latitude, point.longitude)
                            } else {
                                selectedDestination = DestinationSearchResultDto(
                                    id = "map-pin-${point.latitude}-${point.longitude}",
                                    name = "Pinned destination",
                                    latitude = point.latitude,
                                    longitude = point.longitude,
                                    category = "map",
                                    source = "map",
                                    address = null
                                )
                                destinationText = "Pinned destination"
                                validateSupported(point.latitude, point.longitude)
                            }
                        }
                    )
                }

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    text = if (isPickingOrigin) {
                        if (mapOriginLatitude != null && mapOriginLongitude != null) {
                            "📍 $currentLocationLabel · %.5f, %.5f".format(mapOriginLatitude, mapOriginLongitude)
                        } else {
                            "Tap the map to choose your origin"
                        }
                    } else {
                        selectedDestination?.let {
                            "📍 ${it.name} · %.5f, %.5f".format(it.latitude, it.longitude)
                        } ?: "Tap the map to choose a destination"
                    },
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodySmall
                )

                if ((isPickingOrigin && mapOriginPoint != null) ||
                    (!isPickingOrigin && selectedDestination != null)
                ) {
                    Spacer(modifier = Modifier.height(12.dp))
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .background(TukiOrange, RoundedCornerShape(14.dp))
                            .clickable { showMap = false }
                            .padding(vertical = 13.dp),
                        horizontalArrangement = Arrangement.Center
                    ) {
                        Text(
                            text = if (isPickingOrigin) "Use This Origin" else "Use This Destination",
                            color = Color.White,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }
        }

        return
    }

    val canSubmit = selectedDestination != null && currentLatitude != null && currentLongitude != null

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .navigationBarsPadding()
    ) {
        Column(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 22.dp, vertical = 18.dp)
        ) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Box(
                    Modifier
                        .size(40.dp)
                        .background(Color.White, RoundedCornerShape(14.dp))
                        .clickable(onClick = onBack),
                    contentAlignment = Alignment.Center
                ) {
                    Text("‹", color = TukiInk, style = MaterialTheme.typography.displaySmall)
                }
                Spacer(Modifier.width(12.dp))
                Text(
                    "Where are you going?",
                    color = TukiInk,
                    style = MaterialTheme.typography.displaySmall
                )
            }

            Spacer(Modifier.height(8.dp))
            Text(
                "Set your pickup and destination in one place, then TUKI will find your best commute options.",
                color = TukiMuted,
                style = MaterialTheme.typography.bodySmall
            )

            Spacer(Modifier.height(20.dp))

            Surface(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(24.dp),
                color = TukiSurfaceRaised,
                shadowElevation = 3.dp
            ) {
                Column(Modifier.padding(16.dp)) {
                    Row(
                        modifier = Modifier.fillMaxWidth().height(IntrinsicSize.Min),
                        verticalAlignment = Alignment.Top
                    ) {
                        Column(
                            modifier = Modifier
                                .width(13.dp)
                                .fillMaxHeight()
                                .padding(top = 42.dp, bottom = 42.dp),
                            horizontalAlignment = Alignment.CenterHorizontally
                        ) {
                            RouteDot(TukiTeal)
                            Box(
                                Modifier
                                    .width(2.dp)
                                    .weight(1f)
                                    .background(TukiTeal.copy(alpha = 0.35f))
                            )
                            RouteDot(TukiOrange)
                        }

                        Spacer(Modifier.width(13.dp))

                        Column(Modifier.weight(1f)) {
                            Text("PICKUP", color = TukiTeal, style = MaterialTheme.typography.labelSmall)
                            Spacer(Modifier.height(6.dp))
                            TextField(
                                value = originText,
                                onValueChange = { value ->
                                    originText = value
                                    if (value != currentLocationLabel) {
                                        currentLatitude = null
                                        currentLongitude = null
                                    }
                                },
                                placeholder = { Text("Current location or pickup", color = TukiMuted, style = MaterialTheme.typography.bodyMedium) },
                                singleLine = true,
                                trailingIcon = {
                                    if (originText.isNotEmpty()) {
                                        Text(
                                            "✕",
                                            color = TukiMuted,
                                            fontSize = 18.sp,
                                            modifier = Modifier
                                                .padding(end = 8.dp)
                                                .clickable { 
                                                    originText = ""
                                                    currentLatitude = null
                                                    currentLongitude = null
                                                }
                                        )
                                    }
                                },
                                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                                keyboardActions = KeyboardActions(onSearch = { focusManager.clearFocus() }),
                                colors = tukiTextFieldColors(),
                                shape = RoundedCornerShape(16.dp),
                                textStyle = MaterialTheme.typography.bodyLarge,
                                modifier = Modifier.fillMaxWidth()
                            )

                            Spacer(Modifier.height(9.dp))
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                SmallActionButton("Use current", TukiTeal) {
                                    coroutineScope.launch { useCurrentDeviceLocation() }
                                }
                                SmallActionButton("Pick on map", TukiInk) {
                                    mapPickMode = MapPickMode.Origin
                                    showMap = true
                                }
                            }

                            if (isSearchingOrigin) InlineSearchStatus("Searching pickup...")
                            
                            val originScrollState = rememberScrollState()
                            Column(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .heightIn(max = if (originSearchResults.isNotEmpty()) 240.dp else 0.dp)
                                    .verticalScroll(originScrollState)
                            ) {
                                originSearchResults.forEach { result ->
                                    SearchResultRow(
                                        result = result,
                                        onClick = {
                                            currentLatitude = result.latitude
                                            currentLongitude = result.longitude
                                            currentLocationLabel = result.name
                                            originText = result.name
                                            originSearchResults = emptyList()
                                            locationError = null
                                            validateSupported(result.latitude, result.longitude)
                                        }
                                    )
                                }
                            }
                            
                            originSearchError?.let { InlineError(it) }
                            locationError?.let { InlineError(it) }

                            Spacer(Modifier.height(18.dp))

                            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                Text("DESTINATION", Modifier.weight(1f), color = TukiOrange, style = MaterialTheme.typography.labelSmall)
                                Text(
                                    "Map",
                                    color = TukiTeal,
                                    style = MaterialTheme.typography.labelLarge,
                                    modifier = Modifier.clickable {
                                        mapPickMode = MapPickMode.Destination
                                        showMap = true
                                    }
                                )
                            }
                            Spacer(Modifier.height(6.dp))
                            TextField(
                                value = destinationText,
                                onValueChange = { value ->
                                    destinationText = value
                                    if (selectedDestination?.name != value) selectedDestination = null
                                },
                                placeholder = { Text("Search or enter a place", color = TukiMuted, style = MaterialTheme.typography.bodyMedium) },
                                singleLine = true,
                                trailingIcon = {
                                    if (destinationText.isNotEmpty()) {
                                        Text(
                                            "✕",
                                            color = TukiMuted,
                                            fontSize = 18.sp,
                                            modifier = Modifier
                                                .padding(end = 8.dp)
                                                .clickable { 
                                                    destinationText = ""
                                                    selectedDestination = null
                                                }
                                        )
                                    }
                                },
                                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                                keyboardActions = KeyboardActions(onSearch = { focusManager.clearFocus() }),
                                colors = tukiTextFieldColors(),
                                shape = RoundedCornerShape(16.dp),
                                textStyle = MaterialTheme.typography.bodyLarge,
                                modifier = Modifier.fillMaxWidth()
                            )

                            if (isSearching) InlineSearchStatus("Searching places...")

                            val destinationScrollState = rememberScrollState()
                            Column(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .heightIn(max = if (searchResults.isNotEmpty()) 280.dp else 0.dp)
                                    .verticalScroll(destinationScrollState)
                            ) {
                                searchResults.forEach { result ->
                                    SearchResultRow(
                                        result = result,
                                        onClick = {
                                            selectedDestination = result
                                            destinationText = result.name
                                            searchResults = emptyList()
                                            validateSupported(result.latitude, result.longitude)
                                        }
                                    )
                                }
                            }

                            if (isSearchingMore) {
                                InlineSearchStatus("Searching more places...")
                            } else if (!isSearching && !hasExpandedSearch && searchResults.isNotEmpty()) {
                                Text(
                                    text = "More places...",
                                    color = TukiTeal,
                                    style = MaterialTheme.typography.labelLarge,
                                    modifier = Modifier
                                        .padding(top = 10.dp)
                                        .clickable {
                                            val query = destinationText.trim()
                                            if (query.length < 2) return@clickable
                                            coroutineScope.launch {
                                                isSearchingMore = true
                                                searchError = null
                                                when (
                                                    val result = placesRepository.searchMorePlaces(
                                                        query = query,
                                                        focusLatitude = currentLatitude,
                                                        focusLongitude = currentLongitude
                                                    )
                                                ) {
                                                    is ApiResult.Success -> {
                                                        if (destinationText.trim() == query) {
                                                            searchResults = mergePlaceResults(
                                                                searchResults,
                                                                result.data
                                                            ).take(12)
                                                            hasExpandedSearch = true
                                                        }
                                                    }
                                                    is ApiResult.Failure -> {
                                                        if (destinationText.trim() == query) {
                                                            searchError = result.message
                                                        }
                                                    }
                                                }
                                                isSearchingMore = false
                                            }
                                        }
                                )
                            }
                            searchError?.let { InlineError(it) }

                            selectedDestination?.address?.takeIf { it.isNotBlank() }?.let { address ->
                                Spacer(Modifier.height(8.dp))
                                Text(
                                    address,
                                    color = TukiMuted,
                                    style = MaterialTheme.typography.bodySmall,
                                    maxLines = 2,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                        }
                    }
                }
            }

            Spacer(Modifier.height(16.dp))
            Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), color = TukiTealSurface) {
                Row(Modifier.padding(15.dp), verticalAlignment = Alignment.Top) {
                    Box(Modifier.size(28.dp).background(TukiTeal, CircleShape), contentAlignment = Alignment.Center) {
                        Text("i", color = Color.White, style = MaterialTheme.typography.labelLarge)
                    }
                    Spacer(Modifier.width(11.dp))
                    Text(
                        "Tip: choose pickup first if you are not starting from your current location.",
                        color = TukiInk,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }

        Box(
            Modifier
                .fillMaxWidth()
                .padding(horizontal = 22.dp, vertical = 18.dp)
                .background(
                    if (canSubmit) TukiOrange else TukiOrange.copy(alpha = 0.45f),
                    RoundedCornerShape(18.dp)
                )
                .clickable(enabled = canSubmit) {
                    val originLat = currentLatitude!!
                    val originLon = currentLongitude!!
                    val destination = selectedDestination!!
                    if (!isLocationSupported(originLat, originLon) ||
                        !isLocationSupported(destination.latitude, destination.longitude)
                    ) {
                        showUnsupportedLocationDialog = true
                        return@clickable
                    }

                    onFindRoutes(
                        destination,
                        originText.ifBlank { currentLocationLabel },
                        originLat,
                        originLon
                    )
                }
                .padding(vertical = 15.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = if (currentLatitude == null || currentLongitude == null) {
                    "Waiting for pickup..."
                } else {
                    "Find Routes"
                },
                color = Color.White,
                style = MaterialTheme.typography.titleMedium
            )
        }
    }
}

private fun mergePlaceResults(
    existing: List<DestinationSearchResultDto>,
    expanded: List<DestinationSearchResultDto>
): List<DestinationSearchResultDto> {
    val merged = mutableListOf<DestinationSearchResultDto>()
    (existing + expanded).forEach { candidate ->
        if (merged.none { current -> likelySamePlace(current, candidate) }) {
            merged += candidate
        }
    }
    return merged
}

private fun likelySamePlace(
    first: DestinationSearchResultDto,
    second: DestinationSearchResultDto
): Boolean {
    val firstName = normalizePlaceText(first.name)
    val secondName = normalizePlaceText(second.name)
    if (firstName.isEmpty() || firstName != secondName) return false

    val closeCoordinates = abs(first.latitude - second.latitude) <= 0.002 &&
        abs(first.longitude - second.longitude) <= 0.002
    val firstAddress = normalizePlaceText(first.address.orEmpty())
    val secondAddress = normalizePlaceText(second.address.orEmpty())
    val sameAddress = firstAddress.isNotEmpty() && firstAddress == secondAddress
    return closeCoordinates || sameAddress
}

private fun normalizePlaceText(value: String): String =
    value.lowercase().filter { it.isLetterOrDigit() }

@Composable
private fun RouteDot(color: Color) {
    Box(Modifier.size(13.dp).background(color, CircleShape))
}

@Composable
private fun SmallActionButton(text: String, color: Color, onClick: () -> Unit) {
    Box(
        Modifier
            .background(color, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 13.dp, vertical = 9.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(text, color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.ExtraBold)
    }
}

@Composable
private fun SearchResultRow(result: DestinationSearchResultDto, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp)
            .background(TukiTealSurface, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(Modifier.size(30.dp).background(Color.White, CircleShape), contentAlignment = Alignment.Center) {
            Text("⌖", color = TukiTeal, fontSize = 18.sp)
        }
        Spacer(Modifier.width(10.dp))
        Column(Modifier.weight(1f)) {
            Text(result.name, color = TukiInk, style = MaterialTheme.typography.titleSmall, maxLines = 2, overflow = TextOverflow.Ellipsis)
            result.address?.takeIf { it.isNotBlank() }?.let { address ->
                Text(address, color = TukiMuted, style = MaterialTheme.typography.bodySmall, maxLines = 2, overflow = TextOverflow.Ellipsis)
            }
        }
    }
}

@Composable
private fun InlineSearchStatus(text: String) {
    Row(
        modifier = Modifier.padding(top = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        CircularProgressIndicator(Modifier.size(15.dp), strokeWidth = 2.dp, color = TukiTeal)
        Spacer(Modifier.width(8.dp))
        Text(text, color = TukiMuted, style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun InlineError(message: String) {
    Text(
        text = message,
        color = com.example.frontend.ui.theme.TukiDanger,
        fontSize = 11.sp,
        modifier = Modifier.padding(top = 8.dp)
    )
}

@Composable
private fun tukiTextFieldColors() = TextFieldDefaults.colors(
    focusedContainerColor = Color.White,
    unfocusedContainerColor = Color.White,
    disabledContainerColor = Color.Transparent,
    focusedIndicatorColor = Color.Transparent,
    unfocusedIndicatorColor = Color.Transparent,
    disabledIndicatorColor = Color.Transparent,
    focusedTextColor = TukiInk,
    unfocusedTextColor = TukiInk
)
