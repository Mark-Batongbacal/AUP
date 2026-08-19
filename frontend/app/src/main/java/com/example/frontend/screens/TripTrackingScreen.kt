package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.MapScreen
import com.example.frontend.components.ParaPoOverlay
import com.example.frontend.data.navigation.NavigationSnapshotDto
import org.maplibre.android.geometry.LatLng
import kotlin.math.roundToInt

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

@Composable
fun TripTrackingScreen(
    origin: String,
    destination: String,
    routePoints: List<LatLng> = emptyList(),
    navigationSnapshot: NavigationSnapshotDto? = null,
    navigationError: String? = null,
    onBack: () -> Unit = {}
) {
    var showParaPoOverlay by remember { mutableStateOf(false) }

    val instruction = navigationSnapshot?.displayInstruction()
        ?: navigationSnapshot?.nextInstruction?.let { next ->
            val mode = next.transportMode
                ?.lowercase()
                ?.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
            val routeName = next.routeName?.takeIf { it.isNotBlank() }
            listOfNotNull(next.type.takeIf { it.isNotBlank() }, mode, routeName)
                .joinToString(" · ")
        }
        ?: "Waiting for navigation guidance…"

    val remainingDistance = navigationSnapshot?.remainingDistanceMeters
    val distanceText = when {
        remainingDistance == null -> "Waiting for location update"
        remainingDistance >= 1000 -> "%.1f km remaining".format(remainingDistance / 1000.0)
        else -> "${remainingDistance.roundToInt()} m remaining"
    }

    val currentLegDistance = navigationSnapshot?.currentLeg?.distanceMeters
    val progressFraction = if (currentLegDistance != null && currentLegDistance > 0.0) {
        (navigationSnapshot.progressMeters / currentLegDistance)
            .coerceIn(0.0, 1.0)
            .toFloat()
    } else {
        0f
    }

    val canUseParaPo = navigationSnapshot?.requiresAlightingConfirmation == true ||
        navigationSnapshot?.nextInstruction?.type?.contains("alight", ignoreCase = true) == true

    Box(modifier = Modifier.fillMaxSize()) {
        MapScreen(
            routePoints = routePoints,
            modifier = Modifier.fillMaxSize()
        )

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(30.dp)
                .background(Color.White, RoundedCornerShape(20.dp))
                .padding(20.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(32.dp)
                        .background(TukiCream, RoundedCornerShape(8.dp))
                        .clickable(onClick = onBack),
                    contentAlignment = Alignment.Center
                ) {
                    Text(text = "‹", color = TukiDark, fontSize = 20.sp, fontWeight = FontWeight.Bold)
                }
                Spacer(modifier = Modifier.width(16.dp))
                Column {
                    Text(
                        text = "Current Trip",
                        color = TukiGray,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = "$origin → $destination",
                        color = TukiDark,
                        fontSize = 16.sp,
                        fontWeight = FontWeight.ExtraBold
                    )
                    navigationSnapshot?.currentLeg?.routeName
                        ?.takeIf { it.isNotBlank() }
                        ?.let { routeName ->
                            Text(
                                text = routeName,
                                color = TukiTeal,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                }
            }
        }

        Surface(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .padding(20.dp),
            shape = RoundedCornerShape(24.dp),
            color = Color.White,
            tonalElevation = 8.dp,
            shadowElevation = 8.dp
        ) {
            Column(modifier = Modifier.padding(24.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "NEXT STEP",
                            color = TukiTeal,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.ExtraBold
                        )
                        Text(
                            text = instruction,
                            color = TukiDark,
                            fontSize = 19.sp,
                            fontWeight = FontWeight.ExtraBold
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = distanceText,
                            color = TukiGray,
                            fontSize = 14.sp
                        )

                        navigationSnapshot?.landmark?.let { landmark ->
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = "Near ${landmark.name}",
                                color = TukiTeal,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }

                        navigationError?.takeIf { it.isNotBlank() }?.let { error ->
                            Spacer(modifier = Modifier.height(6.dp))
                            Text(
                                text = error,
                                color = MaterialTheme.colorScheme.error,
                                fontSize = 12.sp
                            )
                        }
                    }

                    Spacer(modifier = Modifier.width(12.dp))

                    Surface(
                        modifier = Modifier
                            .size(64.dp)
                            .clickable(enabled = canUseParaPo) {
                                showParaPoOverlay = true
                            },
                        shape = CircleShape,
                        color = if (canUseParaPo) {
                            TukiOrange.copy(alpha = 0.2f)
                        } else {
                            TukiGray.copy(alpha = 0.12f)
                        }
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Text(
                                text = "🔔",
                                fontSize = 28.sp,
                                color = if (canUseParaPo) Color.Unspecified else TukiGray
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(20.dp))

                LinearProgressIndicator(
                    progress = { progressFraction },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(8.dp),
                    color = TukiTeal,
                    trackColor = TukiTeal.copy(alpha = 0.1f),
                    strokeCap = androidx.compose.ui.graphics.StrokeCap.Round
                )

                navigationSnapshot?.status?.takeIf { it.isNotBlank() }?.let { status ->
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = status.replace('_', ' '),
                        color = TukiGray,
                        fontSize = 10.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }
        }

        if (showParaPoOverlay) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(Color.Black.copy(alpha = 0.4f))
                    .clickable { showParaPoOverlay = false },
                contentAlignment = Alignment.Center
            ) {
                ParaPoOverlay(onDismiss = { showParaPoOverlay = false })
            }
        }
    }
}
