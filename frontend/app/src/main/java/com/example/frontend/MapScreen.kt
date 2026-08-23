package com.example.frontend

import android.Manifest
import android.annotation.SuppressLint
import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.content.pm.PackageManager
import android.view.MotionEvent
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
private const val NavigationMapZoom = 16.5
private const val OpenFreeMapStyleUrl = "https://tiles.openfreemap.org/styles/liberty"
private const val LiveTripLikeMapStyleUrl = "https://tiles.openfreemap.org/styles/positron"
private const val RouteSourceId = "tuki-route-source"
private const val RouteLayerId = "tuki-route-layer"
private const val DestinationSourceId = "tuki-destination-source"
private const val DestinationLayerId = "tuki-destination-layer"
private const val StartSourceId = "tuki-leg-start-source"
private const val StartLayerId = "tuki-leg-start-layer"
private const val FinalDestinationSourceId = "tuki-final-destination-source"
private const val FinalDestinationLayerId = "tuki-final-destination-layer"
private const val TodaSourceId = "tuki-toda-source"
private const val TodaLayerId = "tuki-toda-layer"
private const val FutureLegPrefix = "tuki-future-leg"
private const val TransitRoutePrefix = "tuki-transit-route"

private val TransitRouteColors = listOf(
    "#0D8B97",
    "#F4881F",
    "#0A5B48",
    "#FABE3A",
    "#076773",
    "#112E36"
)

enum class MapVisualStyle { General, LiveTrip }

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
    startPoint: LatLng? = null,
    selectedDestination: LatLng? = null,
    finalDestination: LatLng? = null,
    futureRouteSegments: List<List<LatLng>> = emptyList(),
    transitRoutes: List<TransitRouteOverlay> = if (routePoints.isNotEmpty()) TukiMapOverlayState.selectedJourneyJeepneyRoutes else emptyList(),
    todaPoints: List<TodaPointOverlay> = TukiMapOverlayState.todaPoints,
    onMapClick: ((LatLng) -> Unit)? = null,
    navigationTrackingEnabled: Boolean = false,
    navigationTrackingPoint: LatLng? = null,
    visualStyle: MapVisualStyle = MapVisualStyle.General,
    showDeviceLocation: Boolean = true,
    fitRouteBounds: Boolean = false,
    routeBoundsPoints: List<LatLng> = routePoints,
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
    var followNavigationLocation by rememberSaveable(navigationTrackingEnabled) { mutableStateOf(navigationTrackingEnabled) }

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

    LaunchedEffect(showDeviceLocation) {
        if (showDeviceLocation && !hasLocationPermission && !hasRequestedLocationPermission) {
            requestLocationPermission()
        }
    }

    val mapView = remember(context) {
        MapLibre.getInstance(context)
        MapView(context).apply { onCreate(null) }
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

    LaunchedEffect(mapView, visualStyle, showDeviceLocation) {
        mapView.getMapAsync { map ->
            mapLibreMap = map
            map.uiSettings.isCompassEnabled = visualStyle != MapVisualStyle.LiveTrip
            val mapStyleUrl = if (visualStyle == MapVisualStyle.LiveTrip) LiveTripLikeMapStyleUrl else OpenFreeMapStyleUrl
            map.setStyle(mapStyleUrl) { style ->
                loadedStyle = style

                val cameraTarget = if (navigationTrackingEnabled) navigationTrackingPoint else null
                    ?: routePoints.firstOrNull()
                    ?: startPoint
                    ?: selectedDestination
                    ?: finalDestination
                    ?: DefaultMapCenter
                map.cameraPosition = CameraPosition.Builder()
                    .target(cameraTarget)
                    .zoom(if (navigationTrackingEnabled && navigationTrackingPoint != null) NavigationMapZoom else DefaultMapZoom)
                    .build()

                updateTransitRouteLayers(style, transitRoutes, selectedTransitRouteId)
                updateFutureLegLayers(style, futureRouteSegments, visualStyle)
                updateRouteLayer(style, routePoints, visualStyle)
                updateTodaLayer(style, todaPoints, visualStyle)
                updateStartLayer(style, startPoint, visualStyle)
                updateDestinationLayer(style, selectedDestination, visualStyle)
                updateFinalDestinationLayer(style, finalDestination)
                configureLocationComponent(context, map, style, hasLocationPermission && showDeviceLocation)
            }
        }
    }

    DisposableEffect(mapView, navigationTrackingEnabled) {
        if (!navigationTrackingEnabled) {
            mapView.setOnTouchListener(null)
            onDispose { }
        } else {
            mapView.setOnTouchListener { _, event ->
                if (event.actionMasked == MotionEvent.ACTION_DOWN && followNavigationLocation) followNavigationLocation = false
                false
            }
            onDispose { mapView.setOnTouchListener(null) }
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

    LaunchedEffect(mapLibreMap, navigationTrackingPoint, followNavigationLocation, navigationTrackingEnabled) {
        if (!navigationTrackingEnabled || !followNavigationLocation) return@LaunchedEffect
        val map = mapLibreMap ?: return@LaunchedEffect
        val point = navigationTrackingPoint ?: return@LaunchedEffect
        val currentZoom = map.cameraPosition.zoom.takeIf { it >= NavigationMapZoom } ?: NavigationMapZoom
        map.animateCamera(
            CameraUpdateFactory.newCameraPosition(
                CameraPosition.Builder().target(point).zoom(currentZoom).build()
            ),
            650
        )
    }

    LaunchedEffect(loadedStyle, fitRouteBounds, routeBoundsPoints, startPoint, selectedDestination, navigationTrackingEnabled) {
        if (!fitRouteBounds || navigationTrackingEnabled) return@LaunchedEffect
        val map = mapLibreMap ?: return@LaunchedEffect
        if (loadedStyle == null || routeBoundsPoints.isEmpty()) return@LaunchedEffect
        val density = context.resources.displayMetrics.density
        val sidePadding = (26f * density).toInt()
        val verticalPadding = (34f * density).toInt()
        fitMapCameraToRoute(
            map = map,
            mapView = mapView,
            routePoints = routeBoundsPoints,
            anchors = listOfNotNull(startPoint, selectedDestination, finalDestination),
            insets = MapCameraInsets(
                left = sidePadding,
                top = verticalPadding,
                right = sidePadding,
                bottom = verticalPadding
            )
        )
    }

    LaunchedEffect(loadedStyle, routePoints, visualStyle) {
        loadedStyle?.let { updateRouteLayer(it, routePoints, visualStyle) }
    }
    LaunchedEffect(loadedStyle, startPoint, visualStyle) {
        loadedStyle?.let { updateStartLayer(it, startPoint, visualStyle) }
    }
    LaunchedEffect(loadedStyle, selectedDestination, visualStyle) {
        loadedStyle?.let { updateDestinationLayer(it, selectedDestination, visualStyle) }
    }
    LaunchedEffect(loadedStyle, finalDestination) {
        loadedStyle?.let { updateFinalDestinationLayer(it, finalDestination) }
    }
    LaunchedEffect(loadedStyle, futureRouteSegments, visualStyle) {
        loadedStyle?.let { updateFutureLegLayers(it, futureRouteSegments, visualStyle) }
    }
    LaunchedEffect(loadedStyle, transitRoutes, selectedTransitRouteId, todaPoints, visualStyle, finalDestination) {
        loadedStyle?.let { style ->
            updateTransitRouteLayers(style, transitRoutes, selectedTransitRouteId)
            updateTodaLayer(style, todaPoints, visualStyle)
            updateFinalDestinationLayer(style, finalDestination)
        }
    }
    LaunchedEffect(loadedStyle, todaPoints, visualStyle, finalDestination) {
        loadedStyle?.let {
            updateTodaLayer(it, todaPoints, visualStyle)
            updateFinalDestinationLayer(it, finalDestination)
        }
    }
    LaunchedEffect(loadedStyle, hasLocationPermission, showDeviceLocation) {
        val style = loadedStyle ?: return@LaunchedEffect
        val map = mapLibreMap ?: return@LaunchedEffect
        configureLocationComponent(context, map, style, hasLocationPermission && showDeviceLocation)
    }

    Box(modifier = modifier.fillMaxSize()) {
        AndroidView(factory = { mapView }, modifier = Modifier.fillMaxSize())

        selectionInfo?.let { info ->
            Surface(
                modifier = Modifier.align(Alignment.CenterEnd).padding(16.dp).widthIn(max = 230.dp),
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

        if (navigationTrackingEnabled && navigationTrackingPoint != null && !followNavigationLocation) {
            Button(
                onClick = { followNavigationLocation = true },
                modifier = Modifier.align(Alignment.CenterEnd).padding(end = 16.dp)
            ) { Text("◎ Recenter") }
        }

        if (showDeviceLocation && !hasLocationPermission) {
            LocationPermissionBanner(
                canRequestAgain = activity?.shouldShowLocationPermissionRationale() != false || !hasRequestedLocationPermission,
                onRequestPermission = requestLocationPermission,
                modifier = Modifier.align(Alignment.BottomCenter).padding(16.dp)
            )
        }
    }
}

private fun updateTransitRouteLayers(style: Style, routes: List<TransitRouteOverlay>, selectedRouteId: Long?) {
    routes.forEachIndexed { index, route ->
        if (route.points.size < 2) return@forEachIndexed
        val sourceId = "$TransitRoutePrefix-source-${route.routeId}"
        val layerId = "$TransitRoutePrefix-layer-${route.routeId}"
        val geometry = LineString.fromLngLats(route.points.map { Point.fromLngLat(it.longitude, it.latitude) })
        val source = style.getSourceAs<GeoJsonSource>(sourceId)
        if (source != null) source.setGeoJson(geometry) else style.addSource(GeoJsonSource(sourceId, geometry))
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

private fun updateFutureLegLayers(style: Style, segments: List<List<LatLng>>, visualStyle: MapVisualStyle) {
    repeat(24) { index ->
        style.removeLayer("$FutureLegPrefix-layer-$index")
        style.removeSource("$FutureLegPrefix-source-$index")
    }
    segments.take(24).forEachIndexed { index, points ->
        if (points.size < 2) return@forEachIndexed
        val sourceId = "$FutureLegPrefix-source-$index"
        val layerId = "$FutureLegPrefix-layer-$index"
        val geometry = LineString.fromLngLats(points.map { Point.fromLngLat(it.longitude, it.latitude) })
        style.addSource(GeoJsonSource(sourceId, geometry))
        style.addLayer(
            LineLayer(layerId, sourceId).withProperties(
                PropertyFactory.lineColor(
                    if (visualStyle == MapVisualStyle.LiveTrip) {
                        if (index == 0) "#F59A3A" else "#829093"
                    } else {
                        if (index == 0) "#F4881F" else "#112E36"
                    }
                ),
                PropertyFactory.lineWidth(if (index == 0) 4.5f else 3f),
                PropertyFactory.lineOpacity(if (index == 0) 0.72f else 0.28f),
                PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
                PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
            )
        )
    }
}

private fun updateRouteLayer(style: Style, routePoints: List<LatLng>, visualStyle: MapVisualStyle) {
    if (routePoints.size < 2) {
        style.removeLayer(RouteLayerId)
        style.removeSource(RouteSourceId)
        return
    }
    val routeGeometry = LineString.fromLngLats(routePoints.map { Point.fromLngLat(it.longitude, it.latitude) })
    val source = style.getSourceAs<GeoJsonSource>(RouteSourceId)
    if (source != null) {
        source.setGeoJson(routeGeometry)
        return
    }
    style.addSource(GeoJsonSource(RouteSourceId, routeGeometry))
    style.addLayer(
        LineLayer(RouteLayerId, RouteSourceId).withProperties(
            PropertyFactory.lineColor(if (visualStyle == MapVisualStyle.LiveTrip) "#153E4B" else "#0D8B97"),
            PropertyFactory.lineWidth(6f),
            PropertyFactory.lineOpacity(1f),
            PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
            PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
        )
    )
}

private fun updateTodaLayer(style: Style, points: List<TodaPointOverlay>, visualStyle: MapVisualStyle) {
    if (points.isEmpty()) {
        style.removeLayer(TodaLayerId)
        style.removeSource(TodaSourceId)
        return
    }
    val collection = FeatureCollection.fromFeatures(points.map { Feature.fromGeometry(Point.fromLngLat(it.longitude, it.latitude)) })
    val source = style.getSourceAs<GeoJsonSource>(TodaSourceId)
    if (source != null) {
        source.setGeoJson(collection)
        style.removeLayer(TodaLayerId)
    } else {
        style.addSource(GeoJsonSource(TodaSourceId, collection))
    }
    style.addLayer(
        CircleLayer(TodaLayerId, TodaSourceId).withProperties(
            PropertyFactory.circleColor(if (visualStyle == MapVisualStyle.LiveTrip) "#3478F6" else "#076773"),
            PropertyFactory.circleRadius(7f),
            PropertyFactory.circleOpacity(0.92f),
            PropertyFactory.circleStrokeColor(if (visualStyle == MapVisualStyle.LiveTrip) "#FFFFFF" else "#FFF9E9"),
            PropertyFactory.circleStrokeWidth(2.5f)
        )
    )
}

private fun updateStartLayer(style: Style, start: LatLng?, visualStyle: MapVisualStyle) {
    if (start == null) {
        style.removeLayer(StartLayerId)
        style.removeSource(StartSourceId)
        return
    }
    val point = Point.fromLngLat(start.longitude, start.latitude)
    val source = style.getSourceAs<GeoJsonSource>(StartSourceId)
    if (source != null) {
        source.setGeoJson(point)
        return
    }
    style.addSource(GeoJsonSource(StartSourceId, point))
    style.addLayer(
        CircleLayer(StartLayerId, StartSourceId).withProperties(
            PropertyFactory.circleColor(if (visualStyle == MapVisualStyle.LiveTrip) "#3478F6" else "#0D8B97"),
            PropertyFactory.circleRadius(8f),
            PropertyFactory.circleStrokeColor(if (visualStyle == MapVisualStyle.LiveTrip) "#FFFFFF" else "#FFF9E9"),
            PropertyFactory.circleStrokeWidth(3f)
        )
    )
}

private fun updateDestinationLayer(style: Style, destination: LatLng?, visualStyle: MapVisualStyle) {
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
            PropertyFactory.circleColor(if (visualStyle == MapVisualStyle.LiveTrip) "#F59A3A" else "#F4881F"),
            PropertyFactory.circleRadius(8f),
            PropertyFactory.circleStrokeColor(if (visualStyle == MapVisualStyle.LiveTrip) "#FFFFFF" else "#FFF9E9"),
            PropertyFactory.circleStrokeWidth(3f)
        )
    )
}

private fun updateFinalDestinationLayer(style: Style, destination: LatLng?) {
    updateMainDestinationPinLayer(style, destination, FinalDestinationSourceId, FinalDestinationLayerId)
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
private fun configureLocationComponent(context: Context, map: MapLibreMap, style: Style, enabled: Boolean) {
    val locationComponent = map.locationComponent
    if (!enabled) {
        if (locationComponent.isLocationComponentActivated) locationComponent.isLocationComponentEnabled = false
        return
    }
    if (!locationComponent.isLocationComponentActivated) {
        val options = LocationComponentActivationOptions.builder(context, style).useDefaultLocationEngine(true).build()
        locationComponent.activateLocationComponent(options)
    }
    locationComponent.isLocationComponentEnabled = true
}

@Composable
private fun LocationPermissionBanner(canRequestAgain: Boolean, onRequestPermission: () -> Unit, modifier: Modifier = Modifier) {
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
