package com.example.frontend

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import org.maplibre.android.geometry.LatLng
import org.maplibre.android.maps.Style
import org.maplibre.android.style.layers.Property
import org.maplibre.android.style.layers.PropertyFactory
import org.maplibre.android.style.layers.SymbolLayer
import org.maplibre.android.style.sources.GeoJsonSource
import org.maplibre.geojson.Point

private const val MainDestinationPinImageId = "tuki-main-destination-pin-image"

/**
 * Renders the trip's final destination as a conventional red map pin.
 * The icon is bottom-anchored so the tip, not the center of the artwork, marks the coordinate.
 */
internal fun updateMainDestinationPinLayer(
    style: Style,
    destination: LatLng?,
    sourceId: String,
    layerId: String
) {
    if (destination == null) {
        style.removeLayer(layerId)
        style.removeSource(sourceId)
        return
    }

    val point = Point.fromLngLat(destination.longitude, destination.latitude)
    val source = style.getSourceAs<GeoJsonSource>(sourceId)
    if (source != null) {
        source.setGeoJson(point)
    } else {
        style.addSource(GeoJsonSource(sourceId, point))
    }

    // addImage replaces the style image when it already exists, which also makes this safe after
    // a MapLibre style reload.
    style.addImage(MainDestinationPinImageId, mainDestinationPinBitmap)
    style.removeLayer(layerId)
    style.addLayer(
        SymbolLayer(layerId, sourceId).withProperties(
            PropertyFactory.iconImage(MainDestinationPinImageId),
            PropertyFactory.iconAnchor(Property.ICON_ANCHOR_BOTTOM),
            PropertyFactory.iconAllowOverlap(true),
            PropertyFactory.iconIgnorePlacement(true),
            PropertyFactory.iconSize(0.55f)
        )
    )
}

private val mainDestinationPinBitmap: Bitmap by lazy {
    val width = 72
    val height = 92
    val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
    val canvas = Canvas(bitmap)

    val pinPath = Path().apply {
        moveTo(36f, 88f)
        cubicTo(31f, 77f, 9f, 57f, 9f, 34f)
        cubicTo(9f, 18f, 21f, 6f, 36f, 6f)
        cubicTo(51f, 6f, 63f, 18f, 63f, 34f)
        cubicTo(63f, 57f, 41f, 77f, 36f, 88f)
        close()
    }

    val shadowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.argb(48, 0, 0, 0)
        style = Paint.Style.FILL
    }
    canvas.save()
    canvas.translate(0f, 2f)
    canvas.drawPath(pinPath, shadowPaint)
    canvas.restore()

    val pinPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.rgb(220, 53, 69)
        style = Paint.Style.FILL
    }
    canvas.drawPath(pinPath, pinPaint)

    val centerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        style = Paint.Style.FILL
    }
    canvas.drawCircle(36f, 33f, 10.5f, centerPaint)

    bitmap
}
