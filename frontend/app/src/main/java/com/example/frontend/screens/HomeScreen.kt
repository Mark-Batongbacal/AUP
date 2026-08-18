package com.example.frontend.screens

import android.Manifest
import android.content.pm.PackageManager
import android.location.Geocoder
import android.location.Location
import android.location.LocationManager
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalInspectionMode
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import com.example.frontend.model.RecentCommute
import com.example.frontend.repository.CommuteRepository
import com.example.frontend.repository.ApiResult
import kotlin.coroutines.resume
import kotlinx.coroutines.suspendCancellableCoroutine
import com.example.frontend.components.BottomBar
import com.example.frontend.components.TukiTab

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

@Composable
fun HomeScreen(
    userName: String = "Juan",
    commuteRepository: CommuteRepository,
    onSearchDestination: (origin: String, destination: String) -> Unit = { _, _ -> },
    onCommuteClick: (RecentCommute) -> Unit = {},
    onRecentClick: () -> Unit = {},
    onFavoritesClick: () -> Unit = {},
    onProfileClick: () -> Unit = {},
    onNewHereClick: () -> Unit = {}
) {
    var destinationQuery by remember { mutableStateOf("") }
    var currentLocationLabel by remember { mutableStateOf("Locating you...") }
    var isLocating by remember { mutableStateOf(true) }
    var recentCommutes by remember { mutableStateOf<List<RecentCommute>>(emptyList()) }
    var isRefreshingRecent by remember { mutableStateOf(false) }
    var recentErrorMessage by remember { mutableStateOf<String?>(null) }

    val context = LocalContext.current
    val inPreview = LocalInspectionMode.current

    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { grantResults ->
        val granted = grantResults[Manifest.permission.ACCESS_FINE_LOCATION] == true ||
                grantResults[Manifest.permission.ACCESS_COARSE_LOCATION] == true
        if (granted) {
            isLocating = true
        } else {
            isLocating = false
            currentLocationLabel = "Location permission denied"
        }
    }

    LaunchedEffect(Unit) {
        isRefreshingRecent = true
        recentErrorMessage = null

        when (val result = commuteRepository.getRecentCommutes()) {
            is ApiResult.Success -> {
                recentCommutes = result.data
            }
            is ApiResult.Error -> {
                recentErrorMessage = result.message
            }
            else -> {}
        }

        isRefreshingRecent = false

        if (inPreview) {
            isLocating = false
            return@LaunchedEffect
        }
        if (context.hasLocationPermission()) {
            val label = getCurrentLocationLabel(context)
            currentLocationLabel = label ?: "Unable to detect location"
            isLocating = false
        } else {
            permissionLauncher.launch(
                arrayOf(
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                )
            )
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
    ) {
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 30.dp),
            contentPadding = androidx.compose.foundation.layout.PaddingValues(top = 30.dp, bottom = 20.dp)
        ) {
            item {
                Text(
                    text = "Hello, $userName \uD83D\uDC4B",
                    color = TukiGray,
                    fontSize = 17.sp,
                    fontWeight = FontWeight.SemiBold
                )

                Spacer(modifier = Modifier.height(6.dp))

                Text(
                    text = "Where are you going?",
                    color = TukiDark,
                    fontSize = 27.sp,
                    fontWeight = FontWeight.ExtraBold
                )

                Spacer(modifier = Modifier.height(22.dp))
            }

            item {
                LocationCard(
                    currentLocationLabel = currentLocationLabel,
                    isLocating = isLocating,
                    destinationQuery = destinationQuery,
                    onDestinationChange = { destinationQuery = it },
                    onSearch = {
                        if (destinationQuery.isNotBlank()) {
                            onSearchDestination(currentLocationLabel, destinationQuery)
                        }
                    }
                )

                Spacer(modifier = Modifier.height(30.dp))
            }

            item {
                Text(
                    text = "RECENT COMMUTES",
                    color = TukiDark,
                    fontSize = 14.sp,
                    fontWeight = FontWeight.ExtraBold
                )
                Spacer(modifier = Modifier.height(12.dp))
            }

            if (isRefreshingRecent) {
                item {
                    Box(modifier = Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(color = TukiTeal, modifier = Modifier.size(24.dp))
                    }
                }
            } else if (recentErrorMessage != null) {
                item {
                    Text(
                        text = "Could not load recent commutes",
                        color = Color.Red,
                        fontSize = 14.sp
                    )
                }
            } else {
                items(recentCommutes, key = { it.id }) { commute ->
                    RecentCommuteCard(commute = commute, onClick = { onCommuteClick(commute) })
                    Spacer(modifier = Modifier.height(14.dp))
                }
            }

            item {
                Spacer(modifier = Modifier.height(4.dp))
                NewHereBanner(onClick = onNewHereClick)
            }
        }

        BottomBar(
            selectedTab = TukiTab.HOME,
            onHomeClick = {},
            onRecentClick = onRecentClick,
            onFavoritesClick = onFavoritesClick,
            onProfileClick = onProfileClick
        )
    }
}

@Composable
private fun LocationCard(
    currentLocationLabel: String,
    isLocating: Boolean,
    destinationQuery: String,
    onDestinationChange: (String) -> Unit,
    onSearch: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                color = TukiCream2,
                shape = RoundedCornerShape(18.dp)
            )
            .padding(18.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(11.dp)
                    .background(color = TukiTeal, shape = CircleShape)
            )

            Spacer(modifier = Modifier.width(12.dp))

            if (isLocating) {
                CircularProgressIndicator(
                    modifier = Modifier.size(14.dp),
                    strokeWidth = 2.dp,
                    color = TukiTeal
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = "Locating you...",
                    color = TukiDark,
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold
                )
            } else {
                Text(
                    text = "$currentLocationLabel (current location)",
                    color = TukiDark,
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }

        Spacer(modifier = Modifier.height(14.dp))

        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(11.dp)
                    .background(color = TukiOrange, shape = RoundedCornerShape(2.dp))
            )

            Spacer(modifier = Modifier.width(12.dp))

            TextField(
                value = destinationQuery,
                onValueChange = onDestinationChange,
                placeholder = {
                    Text(text = "Type your destination...", color = TukiGray, fontSize = 15.sp)
                },
                singleLine = true,
                colors = TextFieldDefaults.colors(
                    focusedContainerColor = Color.Transparent,
                    unfocusedContainerColor = Color.Transparent,
                    disabledContainerColor = Color.Transparent,
                    focusedIndicatorColor = Color.Transparent,
                    unfocusedIndicatorColor = Color.Transparent,
                    disabledIndicatorColor = Color.Transparent,
                    focusedTextColor = TukiDark,
                    unfocusedTextColor = TukiDark
                ),
                keyboardActions = androidx.compose.foundation.text.KeyboardActions(
                    onSearch = { onSearch() }
                ),
                modifier = Modifier.fillMaxWidth()
            )
        }
    }
}

@Composable
private fun RecentCommuteCard(commute: RecentCommute, onClick: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(color = TukiCream2, shape = RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(16.dp)
    ) {
        Text(
            text = "${commute.origin} to ${commute.destination}",
            color = TukiDark,
            fontSize = 17.sp,
            fontWeight = FontWeight.Bold
        )
        Spacer(modifier = Modifier.height(6.dp))
        Text(
            text = "${commute.legs} legs \u00B7 ${commute.minutes} min",
            color = TukiTeal,
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold
        )
    }
}

@Composable
private fun NewHereBanner(onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(color = TukiTeal, shape = RoundedCornerShape(18.dp))
            .clickable(onClick = onClick)
            .padding(20.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(text = "New here?", color = Color.White, fontSize = 18.sp, fontWeight = FontWeight.Bold)
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = "Learn how \u201cpara po\u201d works",
                color = Color.White.copy(alpha = 0.85f),
                fontSize = 14.sp
            )
        }
        Text(text = "\u2192", color = Color.White, fontSize = 22.sp, fontWeight = FontWeight.Bold)
    }
}

private val TukiCream2 = Color(0xFFFAEBC7)

private fun android.content.Context.hasLocationPermission(): Boolean {
    return ContextCompat.checkSelfPermission(
        this, Manifest.permission.ACCESS_FINE_LOCATION
    ) == PackageManager.PERMISSION_GRANTED ||
            ContextCompat.checkSelfPermission(
                this, Manifest.permission.ACCESS_COARSE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED
}

private suspend fun getCurrentLocationLabel(context: android.content.Context): String? {
    if (!context.hasLocationPermission()) return null

    val locationManager = context.getSystemService(android.content.Context.LOCATION_SERVICE) as? LocationManager
        ?: return null

    val location: Location? = try {
        val providers = locationManager.getProviders(true)
        providers.mapNotNull { provider ->
            @Suppress("MissingPermission")
            locationManager.getLastKnownLocation(provider)
        }.maxByOrNull { it.time }
    } catch (e: SecurityException) {
        null
    }

    location ?: return null
    return reverseGeocode(context, location.latitude, location.longitude)
}

private suspend fun reverseGeocode(context: android.content.Context, lat: Double, lng: Double): String? {
    val geocoder = Geocoder(context)
    return try {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            suspendCancellableCoroutine { cont ->
                geocoder.getFromLocation(lat, lng, 1) { addresses ->
                    val address = addresses.firstOrNull()
                    cont.resume(address?.subLocality ?: address?.locality)
                }
            }
        } else {
            @Suppress("DEPRECATION")
            val addresses = geocoder.getFromLocation(lat, lng, 1)
            val address = addresses?.firstOrNull()
            address?.subLocality ?: address?.locality
        }
    } catch (e: Exception) {
        null
    }
}