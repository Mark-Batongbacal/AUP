package com.example.frontend.screens

import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.ai.AssistantResponseDto
import com.example.frontend.navigation.TripOptionsCoordinator

/**
 * Compatibility shim for the existing TripTrackingScreen call site.
 * Active-trip assistant questions no longer require the UI to fetch or send GPS;
 * the backend builds assistant context from the owned TripSession's reliable state.
 */
@Suppress("UNUSED_PARAMETER")
suspend fun TripOptionsCoordinator.askNavigationAssistant(
    sessionId: String,
    message: String,
    latitude: Double?,
    longitude: Double?
): ApiResult<AssistantResponseDto> =
    askNavigationAssistant(
        sessionId = sessionId,
        message = message
    )
