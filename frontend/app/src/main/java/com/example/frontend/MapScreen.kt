package com.example.frontend

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.content.pm.PackageManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalInspectionMode
import androidx.compose.ui.unit.dp
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import com.google.android.gms.maps.CameraUpdateFactory
import com.google.android.gms.maps.model.CameraPosition
import com.google.android.gms.maps.model.LatLng
import com.google.maps.android.compose.CameraPositionState
import com.google.maps.android.compose.GoogleMap
import com.google.maps.android.compose.GoogleMapComposable
import com.google.maps.android.compose.MapProperties
import com.google.maps.android.compose.MapUiSettings
import com.google.maps.android.compose.Marker
import com.google.maps.android.compose.Polyline
import com.google.maps.android.compose.rememberCameraPositionState
import com.google.maps.android.compose.rememberUpdatedMarkerState

private val DefaultMapCenter = LatLng(15.1453, 120.5887)
private const val DefaultMapZoom = 14f

@Composable
fun MapScreen(
    routePoints: List<LatLng>,
    modifier: Modifier = Modifier,
    mapContent: @Composable @GoogleMapComposable () -> Unit = {},
) {
    if (LocalInspectionMode.current) {
        MapPreviewPlaceholder(modifier)
        return
    }

    val context = LocalContext.current
    val activity = context.findActivity()
    var hasLocationPermission by remember { mutableStateOf(context.hasLocationPermission()) }
    var hasRequestedLocationPermission by rememberSaveable { mutableStateOf(false) }

    val locationPermissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { grantResults ->
        hasRequestedLocationPermission = true
        hasLocationPermission = grantResults[Manifest.permission.ACCESS_FINE_LOCATION] == true ||
            grantResults[Manifest.permission.ACCESS_COARSE_LOCATION] == true
    }

    val requestLocationPermission = {
        locationPermissionLauncher.launch(
            arrayOf(
                Manifest.permission.ACCESS_FINE_LOCATION,
                Manifest.permission.ACCESS_COARSE_LOCATION
            )
        )
    }

    LaunchedEffect(Unit) {
        if (!hasLocationPermission && !hasRequestedLocationPermission) {
            requestLocationPermission()
        }
    }

    val cameraTarget = routePoints.firstOrNull() ?: DefaultMapCenter
    val cameraPositionState = rememberCameraPositionState {
        position = CameraPosition.fromLatLngZoom(cameraTarget, DefaultMapZoom)
    }
    var mapLoaded by remember { mutableStateOf(false) }

    LaunchedEffect(mapLoaded, cameraTarget) {
        if (mapLoaded) {
            cameraPositionState.moveCamera(
                latitude = cameraTarget.latitude,
                longitude = cameraTarget.longitude,
                zoom = DefaultMapZoom
            )
        }
    }

    Box(modifier = modifier.fillMaxSize()) {
        GoogleMap(
            modifier = Modifier.fillMaxSize(),
            cameraPositionState = cameraPositionState,
            properties = MapProperties(isMyLocationEnabled = hasLocationPermission),
            uiSettings = MapUiSettings(myLocationButtonEnabled = hasLocationPermission),
            onMapLoaded = { mapLoaded = true }
        ) {
            mapContent()

            // Later, backend route coordinates can be passed directly to this function.
            DrawRoute(routePoints)
        }

        if (!hasLocationPermission) {
            LocationPermissionBanner(
                canRequestAgain = activity?.shouldShowLocationPermissionRationale() != false ||
                    !hasRequestedLocationPermission,
                onRequestPermission = requestLocationPermission,
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(16.dp)
            )
        }
    }
}

/**
 * Adds a marker to the current GoogleMap content using only coordinates and display text.
 *
 * Required: latitude, longitude, title.
 * Optional: snippet, shown by Google Maps in the marker info window.
 */
@Composable
@GoogleMapComposable
fun MapMarker(
    latitude: Double,
    longitude: Double,
    title: String,
    snippet: String? = null,
) {
    Marker(
        state = rememberUpdatedMarkerState(position = LatLng(latitude, longitude)),
        title = title,
        snippet = snippet
    )
}

/**
 * Draws an ordered coordinate list as a polyline. This intentionally contains no
 * jeepney, tricycle, fare, station, or recommendation logic.
 */
@Composable
@GoogleMapComposable
fun DrawRoute(routePoints: List<LatLng>) {
    if (routePoints.size < 2) return

    Polyline(
        points = routePoints,
        color = Color(0xFF15919B), // Match TukiTeal
        width = 15f
    )
}

/**
 * Moves the map camera to a coordinate and zoom level.
 * Backend-provided route coordinates can later choose these values before drawing.
 */
fun CameraPositionState.moveCamera(
    latitude: Double,
    longitude: Double,
    zoom: Float,
) {
    move(CameraUpdateFactory.newLatLngZoom(LatLng(latitude, longitude), zoom))
}

@Composable
private fun LocationPermissionBanner(
    canRequestAgain: Boolean,
    onRequestPermission: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 6.dp,
        shadowElevation = 6.dp,
        shape = MaterialTheme.shapes.medium
    ) {
        Column(Modifier.padding(16.dp)) {
            Text(
                text = "Location permission is off",
                style = MaterialTheme.typography.titleMedium
            )
            Text(
                text = if (canRequestAgain) {
                    "Allow location access to show the standard Google Maps current-location indicator."
                } else {
                    "Enable location access in Android settings to show the standard Google Maps current-location indicator."
                },
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp)
            )
            if (canRequestAgain) {
                Button(
                    onClick = onRequestPermission,
                    modifier = Modifier.padding(top = 12.dp)
                ) {
                    Text("Allow location")
                }
            }
        }
    }
}

@Composable
private fun MapPreviewPlaceholder(modifier: Modifier = Modifier) {
    Box(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.surfaceContainerHigh),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = "Google Map",
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

private fun Context.hasLocationPermission(): Boolean {
    return ContextCompat.checkSelfPermission(
        this,
        Manifest.permission.ACCESS_FINE_LOCATION
    ) == PackageManager.PERMISSION_GRANTED ||
        ContextCompat.checkSelfPermission(
            this,
            Manifest.permission.ACCESS_COARSE_LOCATION
        ) == PackageManager.PERMISSION_GRANTED
}

private fun Activity.shouldShowLocationPermissionRationale(): Boolean {
    return ActivityCompat.shouldShowRequestPermissionRationale(
        this,
        Manifest.permission.ACCESS_FINE_LOCATION
    ) || ActivityCompat.shouldShowRequestPermissionRationale(
        this,
        Manifest.permission.ACCESS_COARSE_LOCATION
    )
}

private tailrec fun Context.findActivity(): Activity? {
    return when (this) {
        is Activity -> this
        is ContextWrapper -> baseContext.findActivity()
        else -> null
    }
}
