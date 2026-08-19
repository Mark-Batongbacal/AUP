package com.example.frontend

import android.Manifest
import android.annotation.SuppressLint
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
import androidx.compose.runtime.DisposableEffect
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
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import org.maplibre.android.MapLibre
import org.maplibre.android.camera.CameraPosition
import org.maplibre.android.camera.CameraUpdateFactory
import org.maplibre.android.geometry.LatLng
import org.maplibre.android.location.LocationComponentActivationOptions
import org.maplibre.android.maps.MapLibreMap
import org.maplibre.android.maps.MapView
import org.maplibre.android.maps.Style
import org.maplibre.android.style.layers.CircleLayer
import org.maplibre.android.style.layers.LineLayer
import org.maplibre.android.style.layers.Property
import org.maplibre.android.style.layers.PropertyFactory
import org.maplibre.android.style.sources.GeoJsonSource
import org.maplibre.geojson.LineString
import org.maplibre.geojson.Point

private val DefaultMapCenter = LatLng(15.1453, 120.5887)
private const val DefaultMapZoom = 14.0
private const val OpenFreeMapStyleUrl = "https://tiles.openfreemap.org/styles/liberty"
private const val RouteSourceId = "tuki-route-source"
private const val RouteLayerId = "tuki-route-layer"
private const val DestinationSourceId = "tuki-destination-source"
private const val DestinationLayerId = "tuki-destination-layer"

@Composable
fun MapScreen(
    routePoints: List<LatLng>,
    modifier: Modifier = Modifier,
    selectedDestination: LatLng? = null,
    onMapClick: ((LatLng) -> Unit)? = null,
) {
    if (LocalInspectionMode.current) {
        MapPreviewPlaceholder(modifier)
        return
    }

    val context = LocalContext.current
    val activity = context.findActivity()
    val lifecycleOwner = LocalLifecycleOwner.current

    var hasLocationPermission by remember { mutableStateOf(context.hasLocationPermission()) }
    var hasRequestedLocationPermission by rememberSaveable { mutableStateOf(false) }
    var mapLibreMap by remember { mutableStateOf<MapLibreMap?>(null) }
    var loadedStyle by remember { mutableStateOf<Style?>(null) }

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

    val mapView = remember(context) {
        MapLibre.getInstance(context)
        MapView(context).apply {
            onCreate(null)
        }
    }

    DisposableEffect(lifecycleOwner, mapView) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_START -> mapView.onStart()
                Lifecycle.Event.ON_RESUME -> mapView.onResume()
                Lifecycle.Event.ON_PAUSE -> mapView.onPause()
                Lifecycle.Event.ON_STOP -> mapView.onStop()
                else -> Unit
            }
        }

        lifecycleOwner.lifecycle.addObserver(observer)

        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
            if (lifecycleOwner.lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED)) mapView.onPause()
            if (lifecycleOwner.lifecycle.currentState.isAtLeast(Lifecycle.State.STARTED)) mapView.onStop()
            mapView.onDestroy()
        }
    }

    LaunchedEffect(mapView) {
        mapView.getMapAsync { map ->
            mapLibreMap = map
            map.setStyle(OpenFreeMapStyleUrl) { style ->
                loadedStyle = style

                val cameraTarget = routePoints.firstOrNull() ?: selectedDestination ?: DefaultMapCenter
                map.cameraPosition = CameraPosition.Builder()
                    .target(cameraTarget)
                    .zoom(DefaultMapZoom)
                    .build()

                updateRouteLayer(style, routePoints)
                updateDestinationLayer(style, selectedDestination)
                configureLocationComponent(context, map, style, hasLocationPermission)
            }
        }
    }

    DisposableEffect(mapLibreMap, onMapClick) {
        val map = mapLibreMap
        if (map == null || onMapClick == null) {
            onDispose { }
        } else {
            val listener = MapLibreMap.OnMapClickListener { point ->
                onMapClick(point)
                true
            }
            map.addOnMapClickListener(listener)
            onDispose { map.removeOnMapClickListener(listener) }
        }
    }

    LaunchedEffect(loadedStyle, routePoints) {
        loadedStyle?.let { updateRouteLayer(it, routePoints) }
    }

    LaunchedEffect(loadedStyle, selectedDestination) {
        loadedStyle?.let { updateDestinationLayer(it, selectedDestination) }
    }

    LaunchedEffect(loadedStyle, hasLocationPermission) {
        val style = loadedStyle ?: return@LaunchedEffect
        val map = mapLibreMap ?: return@LaunchedEffect
        configureLocationComponent(context, map, style, hasLocationPermission)
    }

    Box(modifier = modifier.fillMaxSize()) {
        AndroidView(
            factory = { mapView },
            modifier = Modifier.fillMaxSize()
        )

        if (!hasLocationPermission) {
            LocationPermissionBanner(
                canRequestAgain = activity?.shouldShowLocationPermissionRationale() != false || !hasRequestedLocationPermission,
                onRequestPermission = requestLocationPermission,
                modifier = Modifier.align(Alignment.BottomCenter).padding(16.dp)
            )
        }
    }
}

private fun updateRouteLayer(style: Style, routePoints: List<LatLng>) {
    if (routePoints.size < 2) {
        style.removeLayer(RouteLayerId)
        style.removeSource(RouteSourceId)
        return
    }

    val routeGeometry = LineString.fromLngLats(
        routePoints.map { Point.fromLngLat(it.longitude, it.latitude) }
    )

    val source = style.getSourceAs<GeoJsonSource>(RouteSourceId)
    if (source != null) {
        source.setGeoJson(routeGeometry)
        return
    }

    style.addSource(GeoJsonSource(RouteSourceId, routeGeometry))
    style.addLayer(
        LineLayer(RouteLayerId, RouteSourceId).withProperties(
            PropertyFactory.lineColor("#15919B"),
            PropertyFactory.lineWidth(6f),
            PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
            PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
        )
    )
}

private fun updateDestinationLayer(style: Style, destination: LatLng?) {
    if (destination == null) {
        style.removeLayer(DestinationLayerId)
        style.removeSource(DestinationSourceId)
        return
    }

    val point = Point.fromLngLat(destination.longitude, destination.latitude)
    val source = style.getSourceAs<GeoJsonSource>(DestinationSourceId)

    if (source != null) {
        source.setGeoJson(point)
        return
    }

    style.addSource(GeoJsonSource(DestinationSourceId, point))
    style.addLayer(
        CircleLayer(DestinationLayerId, DestinationSourceId).withProperties(
            PropertyFactory.circleColor("#FF9318"),
            PropertyFactory.circleRadius(9f),
            PropertyFactory.circleStrokeColor("#FFFFFF"),
            PropertyFactory.circleStrokeWidth(3f)
        )
    )
}

@SuppressLint("MissingPermission")
private fun configureLocationComponent(
    context: Context,
    map: MapLibreMap,
    style: Style,
    enabled: Boolean
) {
    val locationComponent = map.locationComponent

    if (!enabled) {
        if (locationComponent.isLocationComponentActivated) {
            locationComponent.isLocationComponentEnabled = false
        }
        return
    }

    if (!locationComponent.isLocationComponentActivated) {
        val options = LocationComponentActivationOptions.builder(context, style)
            .useDefaultLocationEngine(true)
            .build()
        locationComponent.activateLocationComponent(options)
    }

    locationComponent.isLocationComponentEnabled = true
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
            Text("Location permission is off", style = MaterialTheme.typography.titleMedium)
            Text(
                text = if (canRequestAgain) {
                    "Allow location access to show your current position on the map."
                } else {
                    "Enable location access in Android settings to show your current position on the map."
                },
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp)
            )
            if (canRequestAgain) {
                Button(onClick = onRequestPermission, modifier = Modifier.padding(top = 12.dp)) {
                    Text("Allow location")
                }
            }
        }
    }
}

@Composable
private fun MapPreviewPlaceholder(modifier: Modifier = Modifier) {
    Box(
        modifier = modifier.fillMaxSize().background(MaterialTheme.colorScheme.surfaceContainerHigh),
        contentAlignment = Alignment.Center
    ) {
        Text("MapLibre map", color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

private fun Context.hasLocationPermission(): Boolean =
    ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED ||
        ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED

private fun Activity.shouldShowLocationPermissionRationale(): Boolean =
    ActivityCompat.shouldShowRequestPermissionRationale(this, Manifest.permission.ACCESS_FINE_LOCATION) ||
        ActivityCompat.shouldShowRequestPermissionRationale(this, Manifest.permission.ACCESS_COARSE_LOCATION)

private tailrec fun Context.findActivity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.findActivity()
    else -> null
}
