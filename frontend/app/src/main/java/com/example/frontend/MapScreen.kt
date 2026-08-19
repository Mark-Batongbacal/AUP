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
import androidx.compose.foundation.layout.widthIn
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
import org.maplibre.geojson.Feature
import org.maplibre.geojson.FeatureCollection
import org.maplibre.geojson.LineString
import org.maplibre.geojson.Point
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.sin
import kotlin.math.sqrt

private val DefaultMapCenter = LatLng(15.1453, 120.5887)
private const val DefaultMapZoom = 14.0
private const val OpenFreeMapStyleUrl = "https://tiles.openfreemap.org/styles/liberty"
private const val RouteSourceId = "tuki-route-source"
private const val RouteLayerId = "tuki-route-layer"
private const val DestinationSourceId = "tuki-destination-source"
private const val DestinationLayerId = "tuki-destination-layer"
private const val FinalDestinationSourceId = "tuki-final-destination-source"
private const val FinalDestinationLayerId = "tuki-final-destination-layer"
private const val TodaSourceId = "tuki-toda-source"
private const val TodaLayerId = "tuki-toda-layer"
private const val FutureLegPrefix = "tuki-future-leg"
private const val TransitRoutePrefix = "tuki-transit-route"

private val TransitRouteColors = listOf(
    "#2563EB",
    "#7C3AED",
    "#0F766E",
    "#DC2626",
    "#C2410C",
    "#4F46E5"
)

data class TransitRouteOverlay(
    val routeId: Long,
    val routeCode: String,
    val routeName: String,
    val points: List<LatLng>
)

data class TodaPointOverlay(
    val id: Long,
    val name: String,
    val pointCode: String,
    val latitude: Double,
    val longitude: Double,
    val radiusMeters: Int,
    val operatorName: String? = null,
    val baseFareText: String? = null
)

private data class MapSelectionInfo(
    val title: String,
    val subtitle: String
)

@Composable
fun MapScreen(
    routePoints: List<LatLng>,
    modifier: Modifier = Modifier,
    selectedDestination: LatLng? = null,
    finalDestination: LatLng? = null,
    futureRouteSegments: List<List<LatLng>> = emptyList(),
    transitRoutes: List<TransitRouteOverlay> = emptyList(),
    todaPoints: List<TodaPointOverlay> = emptyList(),
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
    var selectedTransitRouteId by remember { mutableStateOf<Long?>(null) }
    var selectionInfo by remember { mutableStateOf<MapSelectionInfo?>(null) }

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

                val cameraTarget = routePoints.firstOrNull()
                    ?: selectedDestination
                    ?: finalDestination
                    ?: DefaultMapCenter
                map.cameraPosition = CameraPosition.Builder()
                    .target(cameraTarget)
                    .zoom(DefaultMapZoom)
                    .build()

                updateTransitRouteLayers(style, transitRoutes, selectedTransitRouteId)
                updateFutureLegLayers(style, futureRouteSegments)
                updateRouteLayer(style, routePoints)
                updateTodaLayer(style, todaPoints)
                updateDestinationLayer(style, selectedDestination)
                updateFinalDestinationLayer(style, finalDestination)
                configureLocationComponent(context, map, style, hasLocationPermission)
            }
        }
    }

    DisposableEffect(mapLibreMap, onMapClick, transitRoutes, todaPoints) {
        val map = mapLibreMap
        if (map == null || (onMapClick == null && transitRoutes.isEmpty() && todaPoints.isEmpty())) {
            onDispose { }
        } else {
            val listener = MapLibreMap.OnMapClickListener { point ->
                val toda = todaPoints
                    .map { item -> item to distanceMeters(point, LatLng(item.latitude, item.longitude)) }
                    .minByOrNull { it.second }
                    ?.takeIf { (_, distance) -> distance <= 100.0 }

                if (toda != null) {
                    selectedTransitRouteId = null
                    selectionInfo = MapSelectionInfo(
                        title = toda.first.name,
                        subtitle = listOfNotNull(
                            "TODA · ${toda.first.pointCode}",
                            toda.first.operatorName?.takeIf { it.isNotBlank() },
                            toda.first.baseFareText?.let { "Base fare $it" }
                        ).joinToString(" · ")
                    )
                    true
                } else {
                    val route = transitRoutes
                        .map { overlay -> overlay to nearestRoutePointDistanceMeters(point, overlay.points) }
                        .minByOrNull { it.second }
                        ?.takeIf { (_, distance) -> distance <= 85.0 }

                    if (route != null) {
                        selectedTransitRouteId = route.first.routeId
                        selectionInfo = MapSelectionInfo(
                            title = route.first.routeName,
                            subtitle = "Jeepney route · ${route.first.routeCode}"
                        )
                        true
                    } else {
                        selectedTransitRouteId = null
                        selectionInfo = null
                        onMapClick?.invoke(point)
                        true
                    }
                }
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

    LaunchedEffect(loadedStyle, finalDestination) {
        loadedStyle?.let { updateFinalDestinationLayer(it, finalDestination) }
    }

    LaunchedEffect(loadedStyle, futureRouteSegments) {
        loadedStyle?.let { updateFutureLegLayers(it, futureRouteSegments) }
    }

    LaunchedEffect(loadedStyle, transitRoutes, selectedTransitRouteId) {
        loadedStyle?.let { updateTransitRouteLayers(it, transitRoutes, selectedTransitRouteId) }
    }

    LaunchedEffect(loadedStyle, todaPoints) {
        loadedStyle?.let { updateTodaLayer(it, todaPoints) }
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

        selectionInfo?.let { info ->
            Surface(
                modifier = Modifier
                    .align(Alignment.CenterEnd)
                    .padding(16.dp)
                    .widthIn(max = 230.dp),
                color = MaterialTheme.colorScheme.surface,
                tonalElevation = 8.dp,
                shadowElevation = 8.dp,
                shape = MaterialTheme.shapes.medium
            ) {
                Column(Modifier.padding(14.dp)) {
                    Text(info.title, style = MaterialTheme.typography.titleSmall)
                    Text(
                        info.subtitle,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 4.dp)
                    )
                }
            }
        }

        if (!hasLocationPermission) {
            LocationPermissionBanner(
                canRequestAgain = activity?.shouldShowLocationPermissionRationale() != false || !hasRequestedLocationPermission,
                onRequestPermission = requestLocationPermission,
                modifier = Modifier.align(Alignment.BottomCenter).padding(16.dp)
            )
        }
    }
}

private fun updateTransitRouteLayers(
    style: Style,
    routes: List<TransitRouteOverlay>,
    selectedRouteId: Long?
) {
    routes.forEachIndexed { index, route ->
        if (route.points.size < 2) return@forEachIndexed

        val sourceId = "$TransitRoutePrefix-source-${route.routeId}"
        val layerId = "$TransitRoutePrefix-layer-${route.routeId}"
        val geometry = LineString.fromLngLats(
            route.points.map { Point.fromLngLat(it.longitude, it.latitude) }
        )
        val source = style.getSourceAs<GeoJsonSource>(sourceId)
        if (source != null) {
            source.setGeoJson(geometry)
        } else {
            style.addSource(GeoJsonSource(sourceId, geometry))
        }

        style.removeLayer(layerId)
        val selected = route.routeId == selectedRouteId
        style.addLayer(
            LineLayer(layerId, sourceId).withProperties(
                PropertyFactory.lineColor(TransitRouteColors[index % TransitRouteColors.size]),
                PropertyFactory.lineWidth(if (selected) 5f else 2.5f),
                PropertyFactory.lineOpacity(if (selected) 0.9f else 0.24f),
                PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
                PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
            )
        )
    }
}

private fun updateFutureLegLayers(style: Style, segments: List<List<LatLng>>) {
    repeat(24) { index ->
        style.removeLayer("$FutureLegPrefix-layer-$index")
        style.removeSource("$FutureLegPrefix-source-$index")
    }

    segments.take(24).forEachIndexed { index, points ->
        if (points.size < 2) return@forEachIndexed
        val sourceId = "$FutureLegPrefix-source-$index"
        val layerId = "$FutureLegPrefix-layer-$index"
        val geometry = LineString.fromLngLats(
            points.map { Point.fromLngLat(it.longitude, it.latitude) }
        )
        style.addSource(GeoJsonSource(sourceId, geometry))
        style.addLayer(
            LineLayer(layerId, sourceId).withProperties(
                PropertyFactory.lineColor("#64748B"),
                PropertyFactory.lineWidth(if (index == 0) 4f else 3f),
                PropertyFactory.lineOpacity(if (index == 0) 0.5f else 0.28f),
                PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
                PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
            )
        )
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
            PropertyFactory.lineOpacity(1f),
            PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
            PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
        )
    )
}

private fun updateTodaLayer(style: Style, points: List<TodaPointOverlay>) {
    if (points.isEmpty()) {
        style.removeLayer(TodaLayerId)
        style.removeSource(TodaSourceId)
        return
    }

    val features = points.map { item ->
        Feature.fromGeometry(Point.fromLngLat(item.longitude, item.latitude))
    }
    val collection = FeatureCollection.fromFeatures(features)
    val source = style.getSourceAs<GeoJsonSource>(TodaSourceId)
    if (source != null) {
        source.setGeoJson(collection)
        return
    }

    style.addSource(GeoJsonSource(TodaSourceId, collection))
    style.addLayer(
        CircleLayer(TodaLayerId, TodaSourceId).withProperties(
            PropertyFactory.circleColor("#7C3AED"),
            PropertyFactory.circleRadius(6f),
            PropertyFactory.circleOpacity(0.78f),
            PropertyFactory.circleStrokeColor("#FFFFFF"),
            PropertyFactory.circleStrokeWidth(2f)
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

private fun updateFinalDestinationLayer(style: Style, destination: LatLng?) {
    if (destination == null) {
        style.removeLayer(FinalDestinationLayerId)
        style.removeSource(FinalDestinationSourceId)
        return
    }

    val point = Point.fromLngLat(destination.longitude, destination.latitude)
    val source = style.getSourceAs<GeoJsonSource>(FinalDestinationSourceId)
    if (source != null) {
        source.setGeoJson(point)
        return
    }

    style.addSource(GeoJsonSource(FinalDestinationSourceId, point))
    style.addLayer(
        CircleLayer(FinalDestinationLayerId, FinalDestinationSourceId).withProperties(
            PropertyFactory.circleColor("#E11D48"),
            PropertyFactory.circleRadius(11f),
            PropertyFactory.circleStrokeColor("#FFFFFF"),
            PropertyFactory.circleStrokeWidth(4f)
        )
    )
}

private fun nearestRoutePointDistanceMeters(point: LatLng, route: List<LatLng>): Double =
    route.minOfOrNull { routePoint -> distanceMeters(point, routePoint) } ?: Double.POSITIVE_INFINITY

private fun distanceMeters(a: LatLng, b: LatLng): Double {
    val earthRadius = 6_371_000.0
    val lat1 = Math.toRadians(a.latitude)
    val lat2 = Math.toRadians(b.latitude)
    val deltaLat = Math.toRadians(b.latitude - a.latitude)
    val deltaLon = Math.toRadians(b.longitude - a.longitude)
    val sinLat = sin(deltaLat / 2.0)
    val sinLon = sin(deltaLon / 2.0)
    val h = sinLat * sinLat + cos(lat1) * cos(lat2) * sinLon * sinLon
    val c = 2.0 * atan2(sqrt(h), sqrt(1.0 - h))
    return earthRadius * c
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
