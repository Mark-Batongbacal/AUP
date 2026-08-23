package com.example.frontend

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider

/**
 * Shared process-scoped map overlay state.
 *
 * TODA points are transportation infrastructure, not route-specific guidance, so every map can
 * display the same active set without manually threading the list through every screen.
 */
val LocalTukiDataProvider = staticCompositionLocalOf<TukiDataProvider?> { null }

object TukiMapOverlayState {
    var todaPoints by mutableStateOf<List<TodaPointOverlay>>(emptyList())
        private set

    private var todaLoadInProgress = false
    private var todaLoaded = false

    suspend fun ensureTodaPoints(dataProvider: TukiDataProvider?) {
        if (dataProvider == null || todaLoaded || todaLoadInProgress) return

        todaLoadInProgress = true
        when (val result = dataProvider.tricycleRepository.getActivePoints()) {
            is ApiResult.Success -> {
                todaPoints = result.data.map { point ->
                    TodaPointOverlay(
                        id = point.tricyclePointId,
                        name = point.pointName,
                        pointCode = point.pointCode,
                        latitude = point.centerLatitude,
                        longitude = point.centerLongitude,
                        radiusMeters = point.radiusMeters,
                        operatorName = point.operatorName,
                        baseFareText = point.baseFare?.let { fare ->
                            "₱${fare.stripTrailingZeros().toPlainString()}"
                        }
                    )
                }
                todaLoaded = true
            }

            is ApiResult.Failure -> Unit
        }
        todaLoadInProgress = false
    }
}
