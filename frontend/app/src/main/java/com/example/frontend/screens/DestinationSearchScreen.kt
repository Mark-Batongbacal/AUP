package com.example.frontend.screens

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
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

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

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
            return@LaunchedEffect
        }

        delay(350)
        isSearching = true
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
                .background(Color.Black.copy(alpha = 0.35f)),
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
                        color = TukiDark,
                        fontSize = 18.sp,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = "✕",
                        color = TukiDark,
                        fontSize = 18.sp,
                        fontWeight = FontWeight.Bold,
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
                        selectedDestination = if (!isPickingOrigin) selectedDestination?.let {
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
                    color = TukiGray,
                    fontSize = 13.sp
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

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.4f))
            .statusBarsPadding()
            .navigationBarsPadding()
            .padding(vertical = 16.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth(0.9f)
                .background(TukiCream, RoundedCornerShape(24.dp))
                .verticalScroll(rememberScrollState())
                .padding(20.dp)
        ) {
            Text(
                text = "← Back",
                color = TukiTeal,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.clickable(onClick = onBack)
            )

            Spacer(modifier = Modifier.height(12.dp))

            Text(
                text = "Where are you going?",
                color = TukiDark,
                fontSize = 24.sp,
                fontWeight = FontWeight.ExtraBold
            )

            Spacer(modifier = Modifier.height(8.dp))

            Text(
                text = "Type your destination and we'll pull up your best commute options.",
                color = TukiGray,
                fontSize = 13.sp,
                fontWeight = FontWeight.Medium
            )

            Spacer(modifier = Modifier.height(16.dp))

            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(TukiCream2, RoundedCornerShape(14.dp))
                    .padding(horizontal = 14.dp, vertical = 12.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(modifier = Modifier.size(10.dp).background(TukiTeal, CircleShape))
                    Spacer(modifier = Modifier.width(10.dp))
                    Text(
                        text = "Current Location / Origin",
                        color = TukiDark,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Bold
                    )
                }

                Spacer(modifier = Modifier.height(10.dp))

                TextField(
                    value = originText,
                    onValueChange = { value ->
                        originText = value
                        if (value != currentLocationLabel) {
                            currentLatitude = null
                            currentLongitude = null
                        }
                    },
                    placeholder = {
                        Text(
                            text = "Search or edit origin",
                            color = TukiGray,
                            fontSize = 14.sp
                        )
                    },
                    singleLine = true,
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = Color.White.copy(alpha = 0.65f),
                        unfocusedContainerColor = Color.White.copy(alpha = 0.65f),
                        disabledContainerColor = Color.Transparent,
                        focusedIndicatorColor = Color.Transparent,
                        unfocusedIndicatorColor = Color.Transparent,
                        disabledIndicatorColor = Color.Transparent,
                        focusedTextColor = TukiDark,
                        unfocusedTextColor = TukiDark
                    ),
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier.fillMaxWidth()
                )

                if (isSearchingOrigin) {
                    Row(
                        modifier = Modifier.padding(top = 10.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(16.dp),
                            strokeWidth = 2.dp,
                            color = TukiTeal
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Searching origins...", color = TukiGray, fontSize = 12.sp)
                    }
                }

                originSearchResults.forEach { result ->
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(top = 8.dp)
                            .background(Color.White.copy(alpha = 0.65f), RoundedCornerShape(12.dp))
                            .clickable {
                                currentLatitude = result.latitude
                                currentLongitude = result.longitude
                                currentLocationLabel = result.name
                                originText = result.name
                                originSearchResults = emptyList()
                                locationError = null
                                validateSupported(result.latitude, result.longitude)
                            }
                            .padding(horizontal = 12.dp, vertical = 10.dp)
                    ) {
                        Column {
                            Text(result.name, color = TukiDark, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                            result.address?.takeIf { it.isNotBlank() }?.let { address ->
                                Text(address, color = TukiGray, fontSize = 11.sp)
                            }
                        }
                    }
                }

                originSearchError?.let { message ->
                    Text(
                        text = message,
                        color = Color.Red,
                        fontSize = 11.sp,
                        modifier = Modifier.padding(top = 8.dp)
                    )
                }

                locationError?.let { message ->
                    Text(
                        text = message,
                        color = Color.Red,
                        fontSize = 11.sp,
                        modifier = Modifier.padding(top = 8.dp)
                    )
                }

                Spacer(modifier = Modifier.height(10.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Row(
                        modifier = Modifier
                            .weight(1f)
                            .background(TukiTeal, RoundedCornerShape(12.dp))
                            .clickable {
                                coroutineScope.launch {
                                    useCurrentDeviceLocation()
                                }
                            }
                            .padding(vertical = 11.dp),
                        horizontalArrangement = Arrangement.Center
                    ) {
                        Text("Use Current Location", color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                    }

                    Row(
                        modifier = Modifier
                            .weight(1f)
                            .background(TukiDark, RoundedCornerShape(12.dp))
                            .clickable {
                                mapPickMode = MapPickMode.Origin
                                showMap = true
                            }
                            .padding(vertical = 11.dp),
                        horizontalArrangement = Arrangement.Center
                    ) {
                        Text("Pick Origin on Map", color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(TukiDark, RoundedCornerShape(18.dp))
                    .padding(16.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(32.dp)
                            .background(Color.White.copy(alpha = 0.12f), RoundedCornerShape(10.dp)),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(text = "📍", fontSize = 15.sp)
                    }
                    Spacer(modifier = Modifier.width(10.dp))
                    Text(
                        text = "Pin your destination",
                        color = Color.White,
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold
                    )
                }

                Spacer(modifier = Modifier.height(12.dp))

                TextField(
                    value = destinationText,
                    onValueChange = { value ->
                        destinationText = value
                        if (selectedDestination?.name != value) selectedDestination = null
                    },
                    placeholder = {
                        Text(
                            text = "Type or search a place",
                            color = Color.White.copy(alpha = 0.5f),
                            fontSize = 14.sp
                        )
                    },
                    singleLine = true,
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = Color.White.copy(alpha = 0.08f),
                        unfocusedContainerColor = Color.White.copy(alpha = 0.08f),
                        disabledContainerColor = Color.Transparent,
                        focusedIndicatorColor = Color.Transparent,
                        unfocusedIndicatorColor = Color.Transparent,
                        disabledIndicatorColor = Color.Transparent,
                        focusedTextColor = Color.White,
                        unfocusedTextColor = Color.White
                    ),
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier.fillMaxWidth()
                )

                if (isSearching) {
                    Row(
                        modifier = Modifier.padding(top = 10.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(16.dp),
                            strokeWidth = 2.dp,
                            color = TukiTeal
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Searching places...", color = Color.White.copy(alpha = 0.7f), fontSize = 12.sp)
                    }
                }

                searchResults.forEach { result ->
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(top = 8.dp)
                            .background(Color.White.copy(alpha = 0.08f), RoundedCornerShape(12.dp))
                            .clickable {
                                selectedDestination = result
                                destinationText = result.name
                                searchResults = emptyList()
                                validateSupported(result.latitude, result.longitude)
                            }
                            .padding(horizontal = 12.dp, vertical = 10.dp)
                    ) {
                        Column {
                            Text(result.name, color = Color.White, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                            result.address?.takeIf { it.isNotBlank() }?.let { address ->
                                Text(address, color = Color.White.copy(alpha = 0.6f), fontSize = 11.sp)
                            }
                        }
                    }
                }

                searchError?.let { message ->
                    Text(
                        text = message,
                        color = Color(0xFFFFB4AB),
                        fontSize = 11.sp,
                        modifier = Modifier.padding(top = 8.dp)
                    )
                }

                Spacer(modifier = Modifier.height(10.dp))

                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(Color.White.copy(alpha = 0.08f), RoundedCornerShape(14.dp))
                        .clickable {
                            mapPickMode = MapPickMode.Destination
                            showMap = true
                        }
                        .padding(vertical = 12.dp),
                    horizontalArrangement = Arrangement.Center
                ) {
                    Text("🗺️ Open map", color = Color.White.copy(alpha = 0.85f), fontSize = 14.sp)
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            val canSubmit = selectedDestination != null && currentLatitude != null && currentLongitude != null

            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(
                        if (canSubmit) TukiOrange else TukiOrange.copy(alpha = 0.4f),
                        RoundedCornerShape(14.dp)
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
                    .padding(vertical = 14.dp),
                horizontalArrangement = Arrangement.Center
            ) {
                Text(
                    text = if (currentLatitude == null || currentLongitude == null) {
                        "Waiting for location..."
                    } else {
                        "Find Routes"
                    },
                    color = Color.White,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }
    }
}
