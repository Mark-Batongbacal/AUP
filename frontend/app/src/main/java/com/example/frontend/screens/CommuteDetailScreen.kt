package com.example.frontend.screens

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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.MapScreen
import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RecentCommute
import org.maplibre.android.geometry.LatLng

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)

@Composable
fun CommuteDetailScreen(
    commute: RecentCommute,
    legGeometries: List<List<LatLng>> = emptyList(),
    isGeometryLoading: Boolean = false,
    onBack: () -> Unit = {},
    onRepeatTrip: () -> Unit = {}
) {
    var selectedLegIndex by remember(commute.id) { mutableIntStateOf(0) }
    val selectedGeometry = legGeometries.getOrNull(selectedLegIndex).orEmpty()
    val selectedHistoryLeg = commute.historyLegs.getOrNull(selectedLegIndex)
    val selectedLegStart = selectedHistoryLeg?.let { leg ->
        if (leg.startLatitude != null && leg.startLongitude != null) {
            LatLng(leg.startLatitude, leg.startLongitude)
        } else null
    }
    val selectedLegEnd = selectedHistoryLeg?.let { leg ->
        if (leg.endLatitude != null && leg.endLongitude != null) {
            LatLng(leg.endLatitude, leg.endLongitude)
        } else null
    }
    val finalDestination = if (commute.destinationLatitude != null && commute.destinationLongitude != null) {
        LatLng(commute.destinationLatitude, commute.destinationLongitude)
    } else null

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .padding(horizontal = 24.dp, vertical = 24.dp)
    ) {
        Text(
            text = "← Back",
            color = TukiTeal,
            fontSize = 16.sp,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.clickable(onClick = onBack)
        )

        Spacer(modifier = Modifier.height(16.dp))

        Text(
            text = "${commute.origin} → ${commute.destination}",
            color = TukiDark,
            fontSize = 22.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = "${commute.legs} legs · ${commute.minutes} min total",
            color = TukiTeal,
            fontSize = 15.sp,
            fontWeight = FontWeight.SemiBold
        )

        Spacer(modifier = Modifier.height(14.dp))

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(TukiOrange, RoundedCornerShape(14.dp))
                .clickable(onClick = onRepeatTrip)
                .padding(vertical = 13.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(modifier = Modifier.weight(1f), contentAlignment = Alignment.Center) {
                Text("Repeat Trip", color = Color.White, fontWeight = FontWeight.Bold)
            }
        }

        Spacer(modifier = Modifier.height(14.dp))

        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(260.dp)
                .clip(RoundedCornerShape(18.dp))
        ) {
            MapScreen(
                routePoints = selectedGeometry,
                startPoint = selectedLegStart,
                selectedDestination = selectedLegEnd,
                finalDestination = finalDestination,
                futureRouteSegments = legGeometries.drop(selectedLegIndex + 1),
                modifier = Modifier.fillMaxSize()
            )
            if (isGeometryLoading) {
                CircularProgressIndicator(
                    modifier = Modifier.align(Alignment.Center),
                    color = TukiTeal
                )
            }
        }

        Spacer(modifier = Modifier.height(14.dp))
        Text(
            text = "Teal = leg start · Orange = leg end · Red = final destination",
            color = TukiGray,
            fontSize = 12.sp
        )
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = "Tap a leg to inspect its path",
            color = TukiGray,
            fontSize = 12.sp
        )
        Spacer(modifier = Modifier.height(10.dp))

        if (commute.steps.isEmpty()) {
            Text(
                text = "No step-by-step breakdown saved for this trip yet.",
                color = TukiGray,
                fontSize = 15.sp
            )
        } else {
            LazyColumn {
                itemsIndexed(commute.steps) { index, step ->
                    StepRow(
                        step = step,
                        selected = index == selectedLegIndex,
                        onClick = { selectedLegIndex = index }
                    )
                }
            }
        }
    }
}

@Composable
private fun StepRow(step: CommuteStep, selected: Boolean, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                if (selected) TukiCream2 else Color.White.copy(alpha = 0.65f),
                RoundedCornerShape(14.dp)
            )
            .clickable(onClick = onClick)
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .width(6.dp)
                .height(36.dp)
                .background(if (selected) TukiTeal else TukiOrange, RoundedCornerShape(3.dp))
        )
        Spacer(modifier = Modifier.width(12.dp))
        Column {
            Text(
                text = "${step.mode}: ${step.from} → ${step.to}",
                color = TukiDark,
                fontWeight = FontWeight.Bold,
                fontSize = 15.sp
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = "${step.minutes} min" + (step.fare?.let { " · ₱$it" } ?: ""),
                color = TukiGray,
                fontSize = 13.sp
            )
        }
    }
    Spacer(modifier = Modifier.height(10.dp))
}
