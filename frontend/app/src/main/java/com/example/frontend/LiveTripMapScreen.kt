package com.example.frontend

import android.view.MotionEvent
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
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
import androidx.compose.ui.platform.LocalInspectionMode
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import org.maplibre.android.MapLibre
import org.maplibre.android.camera.CameraPosition
import org.maplibre.android.camera.CameraUpdateFactory
import org.maplibre.android.geometry.LatLng
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
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.sin
import kotlin.math.sqrt

private val LiveTripDefaultCenter = LatLng(15.1453, 120.5887)
private const val LiveTripMapStyleUrl = "https://tiles.openfreemap.org/styles/positron"
private const val LiveTripNavigationZoom = 16.7
private const val LiveTripRouteSource = "live-trip-route-source"
private const val LiveTripRouteCasing = "live-trip-route-casing"
private const val LiveTripRouteLayer = "live-trip-route-layer"
private const val LiveTripCurrentSource = "live-trip-current-source"
private const val LiveTripCurrentHalo = "live-trip-current-halo"
private const val LiveTripCurrentLayer = "live-trip-current-layer"
private const val LiveTripDestinationSource = "live-trip-destination-source"
private const val LiveTripDestinationLayer = "live-trip-destination-layer"
private const val LiveTripFinalSource = "live-trip-final-source"
private const val LiveTripFinalLayer = "live-trip-final-layer"
private const val LiveTripFuturePrefix = "live-trip-future"

@Composable
fun LiveTripMapScreen(
    routePoints: List<LatLng>,
    currentPosition: LatLng?,
    legDestination: LatLng?,
    finalDestination: LatLng?,
    futureRouteSegments: List<List<LatLng>> = emptyList(),
    recenterBottomPadding: Dp = 300.dp,
    modifier: Modifier = Modifier
) {
    if (LocalInspectionMode.current) {
        Box(modifier.fillMaxSize())
        return
    }

    val context = androidx.compose.ui.platform.LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    var mapLibreMap by remember { mutableStateOf<MapLibreMap?>(null) }
    var loadedStyle by remember { mutableStateOf<Style?>(null) }
    var followLocation by rememberSaveable { mutableStateOf(true) }

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

    LaunchedEffect(mapView) {
        mapView.getMapAsync { map ->
            mapLibreMap = map
            map.setStyle(LiveTripMapStyleUrl) { style ->
                loadedStyle = style
                updateLiveTripFutureLayers(style, futureRouteSegments)
                updateLiveTripRoute(style, routePoints)
                updateLiveTripCurrentPoint(style, currentPosition)
                updateLiveTripDestination(style, legDestination)
                updateLiveTripFinalDestination(style, finalDestination)

                val target = navigationLookAheadTarget(currentPosition, routePoints)
                    ?: currentPosition
                    ?: routePoints.firstOrNull()
                    ?: legDestination
                    ?: finalDestination
                    ?: LiveTripDefaultCenter
                map.cameraPosition = CameraPosition.Builder()
                    .target(target)
                    .zoom(if (currentPosition != null) LiveTripNavigationZoom else 14.5)
                    .build()
            }
        }
    }

    DisposableEffect(mapView) {
        mapView.setOnTouchListener { _, event ->
            if (event.actionMasked == MotionEvent.ACTION_DOWN) followLocation = false
            false
        }
        onDispose { mapView.setOnTouchListener(null) }
    }

    LaunchedEffect(loadedStyle, routePoints) { loadedStyle?.let { updateLiveTripRoute(it, routePoints) } }
    LaunchedEffect(loadedStyle, currentPosition) { loadedStyle?.let { updateLiveTripCurrentPoint(it, currentPosition) } }
    LaunchedEffect(loadedStyle, legDestination) { loadedStyle?.let { updateLiveTripDestination(it, legDestination) } }
    LaunchedEffect(loadedStyle, finalDestination) { loadedStyle?.let { updateLiveTripFinalDestination(it, finalDestination) } }
    LaunchedEffect(loadedStyle, futureRouteSegments) { loadedStyle?.let { updateLiveTripFutureLayers(it, futureRouteSegments) } }

    LaunchedEffect(mapLibreMap, currentPosition, routePoints, followLocation) {
        if (!followLocation) return@LaunchedEffect
        val map = mapLibreMap ?: return@LaunchedEffect
        val point = currentPosition ?: return@LaunchedEffect
        animateLiveTripCamera(map, point, routePoints)
    }

    Box(modifier.fillMaxSize()) {
        AndroidView(factory = { mapView }, modifier = Modifier.fillMaxSize())

        if (currentPosition != null) {
            Surface(
                modifier = Modifier
                    .align(Alignment.CenterEnd)
                    .padding(end = 16.dp)
                    .size(50.dp)
                    .clickable {
                        followLocation = true
                        mapLibreMap?.let { map -> animateLiveTripCamera(map, currentPosition, routePoints) }
                    },
                shape = CircleShape,
                color = Color(0xFFFFFBF0),
                border = BorderStroke(1.dp, Color(0xFFE2DDD2)),
                shadowElevation = 9.dp,
                tonalElevation = 2.dp
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text("◎", color = Color(0xFF153E4B), fontSize = 27.sp)
                }
            }
        }
    }
}

private fun animateLiveTripCamera(map: MapLibreMap, current: LatLng, route: List<LatLng>) {
    val target = navigationLookAheadTarget(current, route) ?: current
    val bearing = navigationBearing(current, route)
    val builder = CameraPosition.Builder()
        .target(target)
        .zoom(map.cameraPosition.zoom.coerceAtLeast(LiveTripNavigationZoom))
    if (bearing != null) builder.bearing(bearing)
    map.animateCamera(CameraUpdateFactory.newCameraPosition(builder.build()), 650)
}

private fun updateLiveTripRoute(style: Style, points: List<LatLng>) {
    if (points.size < 2) {
        style.removeLayer(LiveTripRouteLayer)
        style.removeLayer(LiveTripRouteCasing)
        style.removeSource(LiveTripRouteSource)
        return
    }
    val geometry = LineString.fromLngLats(points.map { Point.fromLngLat(it.longitude, it.latitude) })
    val source = style.getSourceAs<GeoJsonSource>(LiveTripRouteSource)
    if (source != null) {
        source.setGeoJson(geometry)
        return
    }
    style.addSource(GeoJsonSource(LiveTripRouteSource, geometry))
    style.addLayer(
        LineLayer(LiveTripRouteCasing, LiveTripRouteSource).withProperties(
            PropertyFactory.lineColor("#FFF9EB"),
            PropertyFactory.lineWidth(9.5f),
            PropertyFactory.lineOpacity(0.96f),
            PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
            PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
        )
    )
    style.addLayer(
        LineLayer(LiveTripRouteLayer, LiveTripRouteSource).withProperties(
            PropertyFactory.lineColor("#153E4B"),
            PropertyFactory.lineWidth(5.8f),
            PropertyFactory.lineOpacity(1f),
            PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
            PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
        )
    )
}

private fun updateLiveTripCurrentPoint(style: Style, point: LatLng?) {
    if (point == null) {
        style.removeLayer(LiveTripCurrentLayer)
        style.removeLayer(LiveTripCurrentHalo)
        style.removeSource(LiveTripCurrentSource)
        return
    }
    val geometry = Point.fromLngLat(point.longitude, point.latitude)
    val source = style.getSourceAs<GeoJsonSource>(LiveTripCurrentSource)
    if (source != null) {
        source.setGeoJson(geometry)
        return
    }
    style.addSource(GeoJsonSource(LiveTripCurrentSource, geometry))
    style.addLayer(
        CircleLayer(LiveTripCurrentHalo, LiveTripCurrentSource).withProperties(
            PropertyFactory.circleColor("#4D8DFF"),
            PropertyFactory.circleRadius(17f),
            PropertyFactory.circleOpacity(0.20f)
        )
    )
    style.addLayer(
        CircleLayer(LiveTripCurrentLayer, LiveTripCurrentSource).withProperties(
            PropertyFactory.circleColor("#3478F6"),
            PropertyFactory.circleRadius(8.5f),
            PropertyFactory.circleStrokeColor("#FFFFFF"),
            PropertyFactory.circleStrokeWidth(3f)
        )
    )
}

private fun updateLiveTripDestination(style: Style, point: LatLng?) {
    updateLiveTripPoint(style, point, LiveTripDestinationSource, LiveTripDestinationLayer, "#EE5B57", 10f)
}

private fun updateLiveTripFinalDestination(style: Style, point: LatLng?) {
    updateLiveTripPoint(style, point, LiveTripFinalSource, LiveTripFinalLayer, "#F59A3A", 7f)
}

private fun updateLiveTripPoint(style: Style, point: LatLng?, sourceId: String, layerId: String, color: String, radius: Float) {
    if (point == null) {
        style.removeLayer(layerId)
        style.removeSource(sourceId)
        return
    }
    val geometry = Point.fromLngLat(point.longitude, point.latitude)
    val source = style.getSourceAs<GeoJsonSource>(sourceId)
    if (source != null) {
        source.setGeoJson(geometry)
        return
    }
    style.addSource(GeoJsonSource(sourceId, geometry))
    style.addLayer(
        CircleLayer(layerId, sourceId).withProperties(
            PropertyFactory.circleColor(color),
            PropertyFactory.circleRadius(radius),
            PropertyFactory.circleStrokeColor("#FFF9EB"),
            PropertyFactory.circleStrokeWidth(3.5f)
        )
    )
}

private fun updateLiveTripFutureLayers(style: Style, segments: List<List<LatLng>>) {
    repeat(12) { index ->
        style.removeLayer("$LiveTripFuturePrefix-layer-$index")
        style.removeSource("$LiveTripFuturePrefix-source-$index")
    }
    segments.take(12).forEachIndexed { index, points ->
        if (points.size < 2) return@forEachIndexed
        val sourceId = "$LiveTripFuturePrefix-source-$index"
        val layerId = "$LiveTripFuturePrefix-layer-$index"
        val geometry = LineString.fromLngLats(points.map { Point.fromLngLat(it.longitude, it.latitude) })
        style.addSource(GeoJsonSource(sourceId, geometry))
        style.addLayer(
            LineLayer(layerId, sourceId).withProperties(
                PropertyFactory.lineColor(if (index == 0) "#F59A3A" else "#829093"),
                PropertyFactory.lineWidth(if (index == 0) 4f else 3f),
                PropertyFactory.lineOpacity(if (index == 0) 0.72f else 0.28f),
                PropertyFactory.lineCap(Property.LINE_CAP_ROUND),
                PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND)
            )
        )
    }
}

private fun navigationLookAheadTarget(current: LatLng?, route: List<LatLng>): LatLng? {
    current ?: return null
    if (route.size < 2) return current
    val nearest = route.indices.minByOrNull { index -> distanceMeters(current, route[index]) } ?: return current
    var accumulated = 0.0
    var previous = current
    for (index in nearest until route.size) {
        val candidate = route[index]
        accumulated += distanceMeters(previous, candidate)
        if (accumulated >= 105.0) return candidate
        previous = candidate
    }
    return route.lastOrNull() ?: current
}

private fun navigationBearing(current: LatLng, route: List<LatLng>): Double? {
    if (route.size < 2) return null
    val nearest = route.indices.minByOrNull { index -> distanceMeters(current, route[index]) } ?: return null
    val target = route.getOrNull((nearest + 2).coerceAtMost(route.lastIndex)) ?: return null
    val lat1 = Math.toRadians(current.latitude)
    val lat2 = Math.toRadians(target.latitude)
    val dLon = Math.toRadians(target.longitude - current.longitude)
    val y = sin(dLon) * cos(lat2)
    val x = cos(lat1) * sin(lat2) - sin(lat1) * cos(lat2) * cos(dLon)
    return (Math.toDegrees(atan2(y, x)) + 360.0) % 360.0
}

private fun distanceMeters(a: LatLng, b: LatLng): Double {
    val earthRadius = 6_371_000.0
    val lat1 = Math.toRadians(a.latitude)
    val lat2 = Math.toRadians(b.latitude)
    val dLat = Math.toRadians(b.latitude - a.latitude)
    val dLon = Math.toRadians(b.longitude - a.longitude)
    val sinLat = sin(dLat / 2.0)
    val sinLon = sin(dLon / 2.0)
    val h = sinLat * sinLat + cos(lat1) * cos(lat2) * sinLon * sinLon
    return earthRadius * 2.0 * atan2(sqrt(h), sqrt(1.0 - h))
}
