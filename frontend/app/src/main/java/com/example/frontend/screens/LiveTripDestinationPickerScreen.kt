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
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
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
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.MapScreen
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.core.location.isLocationSupported
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.places.PlacesRepository
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiTealSurface
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/**
 * Destination-only variant of the Home destination picker used while a trip is active.
 * The search, expanded results, map pinning, reverse geocoding and service-area validation
 * intentionally mirror DestinationSearchScreen. Confirming a place is owned by TripTrackingScreen,
 * which keeps the existing active-trip reroute behavior unchanged.
 */
@Composable
fun LiveTripDestinationPickerScreen(
    placesRepository: PlacesRepository,
    focusLatitude: Double?,
    focusLongitude: Double?,
    onBack: () -> Unit,
    onDestinationSelected: (DestinationSearchResultDto) -> Unit
) {
    val focusManager = LocalFocusManager.current
    val coroutineScope = rememberCoroutineScope()

    var destinationText by remember { mutableStateOf("") }
    var selectedDestination by remember { mutableStateOf<DestinationSearchResultDto?>(null) }
    var searchResults by remember { mutableStateOf<List<DestinationSearchResultDto>>(emptyList()) }
    var isSearching by remember { mutableStateOf(false) }
    var isSearchingMore by remember { mutableStateOf(false) }
    var hasExpandedSearch by remember { mutableStateOf(false) }
    var searchError by remember { mutableStateOf<String?>(null) }
    var showMap by remember { mutableStateOf(false) }
    var showUnsupportedLocationDialog by remember { mutableStateOf(false) }

    fun validateSupported(latitude: Double, longitude: Double): Boolean {
        val supported = isLocationSupported(latitude, longitude)
        if (!supported) showUnsupportedLocationDialog = true
        return supported
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

    LaunchedEffect(destinationText, focusLatitude, focusLongitude) {
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
                focusLatitude = focusLatitude,
                focusLongitude = focusLongitude
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
        LocationNotSupportedDialog { showUnsupportedLocationDialog = false }
    }

    if (showMap) {
        BackHandler { showMap = false }
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
                        TukiInterfaceText.pickDestination,
                        color = TukiInk,
                        style = MaterialTheme.typography.titleLarge
                    )
                    Text(
                        "✕",
                        color = TukiInk,
                        style = MaterialTheme.typography.titleLarge,
                        modifier = Modifier.clickable { showMap = false }
                    )
                }

                Spacer(Modifier.height(12.dp))

                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(420.dp)
                        .clip(RoundedCornerShape(18.dp))
                ) {
                    MapScreen(
                        routePoints = emptyList(),
                        modifier = Modifier.fillMaxSize(),
                        finalDestination = selectedDestination?.let {
                            org.maplibre.android.geometry.LatLng(it.latitude, it.longitude)
                        },
                        onMapClick = { point ->
                            selectedDestination = DestinationSearchResultDto(
                                id = "map-pin-${point.latitude}-${point.longitude}",
                                name = TukiInterfaceText.pinnedDestination,
                                latitude = point.latitude,
                                longitude = point.longitude,
                                category = "map",
                                source = "map",
                                address = null
                            )
                            destinationText = TukiInterfaceText.pinnedDestination
                            validateSupported(point.latitude, point.longitude)
                        }
                    )
                }

                Spacer(Modifier.height(12.dp))
                Text(
                    selectedDestination?.let {
                        "📍 ${it.name} · %.5f, %.5f".format(it.latitude, it.longitude)
                    } ?: TukiInterfaceText.tapMapChooseDestination,
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodySmall
                )

                if (selectedDestination != null) {
                    Spacer(Modifier.height(12.dp))
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .background(TukiOrange, RoundedCornerShape(14.dp))
                            .clickable { showMap = false }
                            .padding(vertical = 13.dp),
                        horizontalArrangement = Arrangement.Center
                    ) {
                        Text(
                            TukiInterfaceText.useThisDestination,
                            color = Color.White,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }
        }
        return
    }

    val canSubmit = selectedDestination != null

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
                    modifier = Modifier
                        .size(40.dp)
                        .background(TukiSurfaceRaised, RoundedCornerShape(14.dp))
                        .clickable(onClick = onBack),
                    contentAlignment = Alignment.Center
                ) {
                    Text("‹", color = TukiInk, style = MaterialTheme.typography.displaySmall)
                }
                Spacer(Modifier.size(12.dp))
                Text(
                    TukiInterfaceText.whereAreYouGoing,
                    color = TukiInk,
                    style = MaterialTheme.typography.displaySmall
                )
            }

            Spacer(Modifier.height(8.dp))
            Text(
                if (TukiInterfaceText.isFilipino) {
                    "Pumili ng bagong destinasyon para sa kasalukuyang biyahe."
                } else {
                    "Choose a new destination for your active trip."
                },
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
                    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            TukiInterfaceText.destinationUpper,
                            modifier = Modifier.weight(1f),
                            color = TukiOrange,
                            style = MaterialTheme.typography.labelSmall
                        )
                        Text(
                            TukiInterfaceText.map,
                            color = TukiTeal,
                            style = MaterialTheme.typography.labelLarge,
                            modifier = Modifier.clickable { showMap = true }
                        )
                    }
                    Spacer(Modifier.height(6.dp))

                    TextField(
                        value = destinationText,
                        onValueChange = { value ->
                            destinationText = value
                            if (selectedDestination?.name != value) selectedDestination = null
                        },
                        placeholder = {
                            Text(
                                TukiInterfaceText.searchOrEnterPlace,
                                color = TukiMuted,
                                style = MaterialTheme.typography.bodyMedium
                            )
                        },
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
                        colors = liveTripDestinationTextFieldColors(),
                        shape = RoundedCornerShape(16.dp),
                        textStyle = MaterialTheme.typography.bodyLarge,
                        modifier = Modifier.fillMaxWidth()
                    )

                    if (isSearching) LiveTripDestinationSearchStatus(TukiInterfaceText.searchingPlaces)

                    val destinationScrollState = rememberScrollState()
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(max = if (searchResults.isNotEmpty()) 280.dp else 0.dp)
                            .verticalScroll(destinationScrollState)
                    ) {
                        searchResults.forEach { result ->
                            LiveTripDestinationResultRow(result) {
                                selectedDestination = result
                                destinationText = result.name
                                searchResults = emptyList()
                                validateSupported(result.latitude, result.longitude)
                            }
                        }
                    }

                    if (isSearchingMore) {
                        LiveTripDestinationSearchStatus(TukiInterfaceText.searchingMorePlaces)
                    } else if (!isSearching && !hasExpandedSearch && searchResults.isNotEmpty()) {
                        Text(
                            TukiInterfaceText.morePlaces,
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
                                                focusLatitude = focusLatitude,
                                                focusLongitude = focusLongitude
                                            )
                                        ) {
                                            is ApiResult.Success -> {
                                                if (destinationText.trim() == query) {
                                                    searchResults = mergeLiveTripPlaceResults(searchResults, result.data).take(12)
                                                    hasExpandedSearch = true
                                                }
                                            }
                                            is ApiResult.Failure -> {
                                                if (destinationText.trim() == query) searchError = result.message
                                            }
                                        }
                                        isSearchingMore = false
                                    }
                                }
                        )
                    }

                    searchError?.let {
                        Text(
                            it,
                            color = com.example.frontend.ui.theme.TukiDanger,
                            fontSize = 11.sp,
                            modifier = Modifier.padding(top = 8.dp)
                        )
                    }

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

            Spacer(Modifier.height(16.dp))
            Surface(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(18.dp),
                color = TukiTealSurface
            ) {
                Row(Modifier.padding(15.dp), verticalAlignment = Alignment.Top) {
                    Box(
                        Modifier.size(28.dp).background(TukiTeal, CircleShape),
                        contentAlignment = Alignment.Center
                    ) {
                        Text("i", color = Color.White, style = MaterialTheme.typography.labelLarge)
                    }
                    Spacer(Modifier.size(11.dp))
                    Text(
                        if (TukiInterfaceText.isFilipino) {
                            "Gagamitin ng TUKI ang kasalukuyang lokasyon mo at muling kakalkulahin ang aktibong biyahe papunta sa bagong destinasyon."
                        } else {
                            "TUKI will use your current location and recalculate the active trip to the new destination."
                        },
                        color = TukiInk,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }

        Box(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 22.dp, vertical = 18.dp)
                .background(
                    if (canSubmit) TukiOrange else TukiOrange.copy(alpha = 0.45f),
                    RoundedCornerShape(18.dp)
                )
                .clickable(enabled = canSubmit) {
                    val destination = selectedDestination ?: return@clickable
                    if (!isLocationSupported(destination.latitude, destination.longitude)) {
                        showUnsupportedLocationDialog = true
                        return@clickable
                    }
                    onDestinationSelected(destination)
                }
                .padding(vertical = 15.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                if (TukiInterfaceText.isFilipino) "Palitan ang Destinasyon" else "Change Destination",
                color = Color.White,
                style = MaterialTheme.typography.titleMedium
            )
        }
    }
}

@Composable
private fun LiveTripDestinationResultRow(
    result: DestinationSearchResultDto,
    onClick: () -> Unit
) {
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
        Spacer(Modifier.size(10.dp))
        Column(Modifier.weight(1f)) {
            Text(
                result.name,
                color = TukiInk,
                style = MaterialTheme.typography.titleSmall,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
            result.address?.takeIf { it.isNotBlank() }?.let { address ->
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

@Composable
private fun LiveTripDestinationSearchStatus(text: String) {
    Row(
        modifier = Modifier.padding(top = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        CircularProgressIndicator(Modifier.size(15.dp), strokeWidth = 2.dp, color = TukiTeal)
        Spacer(Modifier.size(8.dp))
        Text(text, color = TukiMuted, style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun liveTripDestinationTextFieldColors() = TextFieldDefaults.colors(
    focusedContainerColor = Color.White,
    unfocusedContainerColor = Color.White,
    disabledContainerColor = Color.Transparent,
    focusedIndicatorColor = Color.Transparent,
    unfocusedIndicatorColor = Color.Transparent,
    disabledIndicatorColor = Color.Transparent,
    focusedTextColor = TukiInk,
    unfocusedTextColor = TukiInk
)

private fun mergeLiveTripPlaceResults(
    existing: List<DestinationSearchResultDto>,
    expanded: List<DestinationSearchResultDto>
): List<DestinationSearchResultDto> {
    val merged = mutableListOf<DestinationSearchResultDto>()
    (existing + expanded).forEach { candidate ->
        val duplicate = merged.any { current ->
            current.name.equals(candidate.name, ignoreCase = true) &&
                kotlin.math.abs(current.latitude - candidate.latitude) <= 0.002 &&
                kotlin.math.abs(current.longitude - candidate.longitude) <= 0.002
        }
        if (!duplicate) merged += candidate
    }
    return merged
}
