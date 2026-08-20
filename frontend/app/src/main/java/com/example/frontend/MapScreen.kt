package com.example.frontend

import android.Manifest
import android.annotation.SuppressLint
import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.os.Looper
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalInspectionMode
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import kotlinx.coroutines.delay
import org.maplibre.android.MapLibre
import org.maplibre.android.camera.CameraPosition
import org.maplibre.android.geometry.LatLng
import org.maplibre.android.maps.MapLibreMap
import org.maplibre.android.maps.MapView
import org.maplibre.android.maps.Style
import org.maplibre.android.style.layers.CircleLayer
import org.maplibre.android.style.layers.FillLayer
import org.maplibre.android.style.layers.LineLayer
import org.maplibre.android.style.layers.Property
import org.maplibre.android.style.layers.PropertyFactory
import org.maplibre.android.style.sources.GeoJsonSource
import org.maplibre.geojson.Feature
import org.maplibre.geojson.FeatureCollection
import org.maplibre.geojson.GeoJson
import org.maplibre.geojson.LineString
import org.maplibre.geojson.Point
import org.maplibre.geojson.Polygon
import kotlin.math.*

private const val STYLE = "https://tiles.openfreemap.org/styles/liberty"
private const val ROUTE_SRC = "tuki-route"; private const val ROUTE_LAYER = "tuki-route-layer"
private const val START_SRC = "tuki-start"; private const val START_LAYER = "tuki-start-layer"
private const val DEST_SRC = "tuki-dest"; private const val DEST_LAYER = "tuki-dest-layer"
private const val FINAL_SRC = "tuki-final"; private const val FINAL_LAYER = "tuki-final-layer"
private const val TODA_SRC = "tuki-toda"; private const val TODA_LAYER = "tuki-toda-layer"
private const val USER_SRC = "tuki-user-puck"; private const val USER_LAYER = "tuki-user-puck-layer"
private const val ACC_SRC = "tuki-user-accuracy"; private const val ACC_FILL = "tuki-user-accuracy-fill"; private const val ACC_OUTLINE = "tuki-user-accuracy-outline"
private const val HEADING_SRC = "tuki-user-heading"; private const val HEADING_LAYER = "tuki-user-heading-layer"
private const val FUTURE_PREFIX = "tuki-future"; private const val TRANSIT_PREFIX = "tuki-transit"
private val DEFAULT_CENTER = LatLng(15.1453, 120.5887)
private val TRANSIT_COLORS = listOf("#2563EB", "#7C3AED", "#0F766E", "#DC2626", "#C2410C", "#4F46E5")

data class TransitRouteOverlay(val routeId: Long, val routeCode: String, val routeName: String, val points: List<LatLng>)
data class TodaPointOverlay(val id: Long, val name: String, val pointCode: String, val latitude: Double, val longitude: Double, val radiusMeters: Int, val operatorName: String? = null, val baseFareText: String? = null)

@Composable
fun MapScreen(
    routePoints: List<LatLng>, modifier: Modifier = Modifier, startPoint: LatLng? = null,
    selectedDestination: LatLng? = null, finalDestination: LatLng? = null,
    futureRouteSegments: List<List<LatLng>> = emptyList(), transitRoutes: List<TransitRouteOverlay> = emptyList(),
    todaPoints: List<TodaPointOverlay> = emptyList(), onMapClick: ((LatLng) -> Unit)? = null
) {
    if (LocalInspectionMode.current) return MapPreviewPlaceholder(modifier)
    val context = LocalContext.current; val lifecycle = LocalLifecycleOwner.current
    val activity = context.findActivity()
    var permission by remember { mutableStateOf(context.hasLocationPermission()) }
    var requested by rememberSaveable { mutableStateOf(false) }
    var map by remember { mutableStateOf<MapLibreMap?>(null) }; var style by remember { mutableStateOf<Style?>(null) }
    var location by remember { mutableStateOf<Location?>(null) }; var rendered by remember { mutableStateOf<Location?>(null) }

    val launcher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { r ->
        requested = true; permission = r[Manifest.permission.ACCESS_FINE_LOCATION] == true || r[Manifest.permission.ACCESS_COARSE_LOCATION] == true
    }
    val request = { launcher.launch(arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION)) }
    LaunchedEffect(Unit) { if (!permission && !requested) request() }

    val mapView = remember(context) { MapLibre.getInstance(context); MapView(context).apply { onCreate(null) } }
    DisposableEffect(lifecycle, mapView) {
        val observer = LifecycleEventObserver { _, e -> when (e) {
            Lifecycle.Event.ON_START -> mapView.onStart(); Lifecycle.Event.ON_RESUME -> mapView.onResume()
            Lifecycle.Event.ON_PAUSE -> mapView.onPause(); Lifecycle.Event.ON_STOP -> mapView.onStop(); else -> Unit
        } }
        lifecycle.lifecycle.addObserver(observer)
        onDispose { lifecycle.lifecycle.removeObserver(observer); mapView.onDestroy() }
    }

    DisposableEffect(permission) {
        if (!permission) return@DisposableEffect onDispose { }
        val lm = context.getSystemService(Context.LOCATION_SERVICE) as LocationManager
        val listener = object : LocationListener { override fun onLocationChanged(l: Location) {
            if (l.accuracy <= 0f) return
            val old = location
            if (old != null && l.time < old.time) return
            if (old != null && old.distanceTo(l) < 1f && l.accuracy >= old.accuracy) return
            location = l
        } }
        @SuppressLint("MissingPermission") fun start() {
            listOf(LocationManager.GPS_PROVIDER, LocationManager.NETWORK_PROVIDER).forEach { p ->
                if (runCatching { lm.isProviderEnabled(p) }.getOrDefault(false)) runCatching { lm.requestLocationUpdates(p, 1000L, 2f, listener, Looper.getMainLooper()) }
            }
            val cached = listOf(LocationManager.GPS_PROVIDER, LocationManager.NETWORK_PROVIDER).mapNotNull { runCatching { lm.getLastKnownLocation(it) }.getOrNull() }.maxByOrNull { it.time }
            if (cached != null) location = cached
        }
        start(); onDispose { lm.removeUpdates(listener) }
    }

    LaunchedEffect(mapView) { mapView.getMapAsync { m ->
        map = m; m.setStyle(STYLE) { s -> style = s
            m.cameraPosition = CameraPosition.Builder().target(routePoints.firstOrNull() ?: startPoint ?: selectedDestination ?: finalDestination ?: DEFAULT_CENTER).zoom(14.0).build()
            updateAllLayers(s, routePoints, startPoint, selectedDestination, finalDestination, futureRouteSegments, transitRoutes, todaPoints, location)
        }
    } }

    DisposableEffect(map, onMapClick) {
        val m = map ?: return@DisposableEffect onDispose { }
        val listener = MapLibreMap.OnMapClickListener { p -> onMapClick?.invoke(p); true }
        m.addOnMapClickListener(listener); onDispose { m.removeOnMapClickListener(listener) }
    }

    LaunchedEffect(style, routePoints, startPoint, selectedDestination, finalDestination, futureRouteSegments, transitRoutes, todaPoints) {
        style?.let { updateAllLayers(it, routePoints, startPoint, selectedDestination, finalDestination, futureRouteSegments, transitRoutes, todaPoints, null) }
    }
    LaunchedEffect(style, location) {
        val s = style ?: return@LaunchedEffect; val target = location ?: return@LaunchedEffect
        val from = rendered
        if (from == null) { updateUserGeometry(s, target); rendered = target; return@LaunchedEffect }
        repeat(10) { i ->
            val t = (i + 1) / 10.0; val smooth = interpolate(from, target, t)
            updateUserGeometry(s, smooth); rendered = smooth; delay(50)
        }
    }

    Box(modifier.fillMaxSize()) {
        AndroidView(factory = { mapView }, modifier = Modifier.fillMaxSize())
        if (!permission) PermissionBanner(activity, requested, request, Modifier.align(Alignment.BottomCenter).padding(16.dp))
    }
}

private fun updateAllLayers(s: Style, route: List<LatLng>, start: LatLng?, dest: LatLng?, finalDest: LatLng?, future: List<List<LatLng>>, transit: List<TransitRouteOverlay>, toda: List<TodaPointOverlay>, user: Location?) {
    line(s, ROUTE_SRC, ROUTE_LAYER, route, "#15919B", 6f, 1f)
    point(s, START_SRC, START_LAYER, start, "#15919B", 8f)
    point(s, DEST_SRC, DEST_LAYER, dest, "#FF9318", 9f)
    point(s, FINAL_SRC, FINAL_LAYER, finalDest, "#E11D48", 11f)
    val features = toda.map { Feature.fromGeometry(Point.fromLngLat(it.longitude, it.latitude)) }
    if (features.isEmpty()) { s.removeLayer(TODA_LAYER); s.removeSource(TODA_SRC) } else {
        geo(s, TODA_SRC, FeatureCollection.fromFeatures(features)); if (s.getLayer(TODA_LAYER) == null) s.addLayer(CircleLayer(TODA_LAYER, TODA_SRC).withProperties(PropertyFactory.circleColor("#7C3AED"), PropertyFactory.circleRadius(6f), PropertyFactory.circleStrokeColor("#FFF"), PropertyFactory.circleStrokeWidth(2f)))
    }
    repeat(24) { i -> s.removeLayer("$FUTURE_PREFIX-layer-$i"); s.removeSource("$FUTURE_PREFIX-source-$i") }
    future.take(24).forEachIndexed { i, pts -> if (pts.size > 1) line(s, "$FUTURE_PREFIX-source-$i", "$FUTURE_PREFIX-layer-$i", pts, if (i == 0) "#FF9318" else "#64748B", if (i == 0) 4.5f else 3f, if (i == 0) .72f else .28f) }
    transit.forEachIndexed { i, r -> if (r.points.size > 1) line(s, "$TRANSIT_PREFIX-source-${r.routeId}", "$TRANSIT_PREFIX-layer-${r.routeId}", r.points, TRANSIT_COLORS[i % TRANSIT_COLORS.size], 2.5f, .24f) }
    if (user != null) updateUserGeometry(s, user)
}

private fun line(s: Style, src: String, layer: String, pts: List<LatLng>, color: String, width: Float, opacity: Float) {
    if (pts.size < 2) { s.removeLayer(layer); s.removeSource(src); return }
    val g = LineString.fromLngLats(pts.map { Point.fromLngLat(it.longitude, it.latitude) }); val old = s.getSourceAs<GeoJsonSource>(src)
    if (old != null) old.setGeoJson(g) else { s.addSource(GeoJsonSource(src, g)); s.addLayer(LineLayer(layer, src).withProperties(PropertyFactory.lineColor(color), PropertyFactory.lineWidth(width), PropertyFactory.lineOpacity(opacity), PropertyFactory.lineCap(Property.LINE_CAP_ROUND), PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND))) }
}
private fun point(s: Style, src: String, layer: String, p: LatLng?, color: String, radius: Float) {
    if (p == null) { s.removeLayer(layer); s.removeSource(src); return }; val g = Point.fromLngLat(p.longitude, p.latitude); val old = s.getSourceAs<GeoJsonSource>(src)
    if (old != null) old.setGeoJson(g) else { s.addSource(GeoJsonSource(src, g)); s.addLayer(CircleLayer(layer, src).withProperties(PropertyFactory.circleColor(color), PropertyFactory.circleRadius(radius), PropertyFactory.circleStrokeColor("#FFF"), PropertyFactory.circleStrokeWidth(3f))) }
}
private fun geo(s: Style, src: String, data: GeoJson) { val old = s.getSourceAs<GeoJsonSource>(src); if (old != null) old.setGeoJson(data) else s.addSource(GeoJsonSource(src, data)) }

private fun updateUserGeometry(s: Style, l: Location) {
    val center = Point.fromLngLat(l.longitude, l.latitude); val accuracy = l.accuracy.coerceIn(5f, 150f).toDouble()
    geo(s, ACC_SRC, accuracyCircle(l.latitude, l.longitude, accuracy))
    if (s.getLayer(ACC_FILL) == null) s.addLayer(FillLayer(ACC_FILL, ACC_SRC).withProperties(PropertyFactory.fillColor("#2563EB"), PropertyFactory.fillOpacity(.10f)))
    if (s.getLayer(ACC_OUTLINE) == null) s.addLayer(LineLayer(ACC_OUTLINE, ACC_SRC).withProperties(PropertyFactory.lineColor("#2563EB"), PropertyFactory.lineWidth(1.5f), PropertyFactory.lineOpacity(.35f)))
    geo(s, USER_SRC, center); if (s.getLayer(USER_LAYER) == null) s.addLayer(CircleLayer(USER_LAYER, USER_SRC).withProperties(PropertyFactory.circleColor("#2563EB"), PropertyFactory.circleRadius(8f), PropertyFactory.circleStrokeColor("#FFF"), PropertyFactory.circleStrokeWidth(3f)))
    if (l.hasBearing() && l.speed >= .5f) { geo(s, HEADING_SRC, headingLine(l)); if (s.getLayer(HEADING_LAYER) == null) s.addLayer(LineLayer(HEADING_LAYER, HEADING_SRC).withProperties(PropertyFactory.lineColor("#2563EB"), PropertyFactory.lineWidth(3f), PropertyFactory.lineCap(Property.LINE_CAP_ROUND))) }
    else { s.removeLayer(HEADING_LAYER); s.removeSource(HEADING_SRC) }
}
private fun accuracyCircle(lat: Double, lon: Double, r: Double): Polygon { val lr = r / 111320.0; val or = r / (111320.0 * cos(Math.toRadians(lat)).coerceAtLeast(.1)); val pts = (0..48).map { i -> val a = 2 * PI * i / 48; Point.fromLngLat(lon + or * cos(a), lat + lr * sin(a)) }; return Polygon.fromLngLats(listOf(pts)) }
private fun headingLine(l: Location): LineString { val d = 18.0; val b = Math.toRadians(l.bearing.toDouble()); val lr = d / 111320.0; val or = d / (111320.0 * cos(Math.toRadians(l.latitude)).coerceAtLeast(.1)); val end = Point.fromLngLat(l.longitude + or * sin(b), l.latitude + lr * cos(b)); return LineString.fromLngLats(listOf(Point.fromLngLat(l.longitude, l.latitude), end)) }
private fun interpolate(a: Location, b: Location, t: Double) = Location("tuki").apply { latitude = a.latitude + (b.latitude-a.latitude)*t; longitude = a.longitude + (b.longitude-a.longitude)*t; accuracy = a.accuracy + (b.accuracy-a.accuracy)*t.toFloat(); bearing = if (b.hasBearing()) b.bearing else 0f; speed = if (b.hasSpeed()) b.speed else 0f; time = b.time }

@Composable private fun PermissionBanner(activity: Activity?, requested: Boolean, request: () -> Unit, modifier: Modifier) { val can = activity?.let { ActivityCompat.shouldShowRequestPermissionRationale(it, Manifest.permission.ACCESS_FINE_LOCATION) || ActivityCompat.shouldShowRequestPermissionRationale(it, Manifest.permission.ACCESS_COARSE_LOCATION) } != false || !requested; Surface(modifier.fillMaxWidth(), color=MaterialTheme.colorScheme.surface, tonalElevation=6.dp, shadowElevation=6.dp) { androidx.compose.foundation.layout.Column(Modifier.padding(16.dp)) { Text("Location permission is off", style=MaterialTheme.typography.titleMedium); Text(if(can) "Allow location access to show your live position." else "Enable location access in Android settings.", color=MaterialTheme.colorScheme.onSurfaceVariant, modifier=Modifier.padding(top=4.dp)); if(can) Button(onClick=request, modifier=Modifier.padding(top=12.dp)) { Text("Allow location") } } } }
@Composable private fun MapPreviewPlaceholder(modifier: Modifier) { Box(modifier.fillMaxSize().background(MaterialTheme.colorScheme.surfaceContainerHigh), contentAlignment=Alignment.Center) { Text("MapLibre map") } }
private fun Context.hasLocationPermission() = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)==PackageManager.PERMISSION_GRANTED || ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION)==PackageManager.PERMISSION_GRANTED
private fun Context.findActivity(): Activity? = when(this) { is Activity -> this; is ContextWrapper -> baseContext.findActivity(); else -> null }
