package com.example.frontend.data.ai

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.routing.JeepneyTripPlanDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST
import retrofit2.http.Path

data class AssistantRequest(
    val message: String,
    val originLatitude: Double? = null,
    val originLongitude: Double? = null,
    val tripSessionId: String? = null,
    val destinationId: String? = null,
    val conversationId: String? = null,
    val operationId: String? = null
)

data class ActiveTripAssistantRequest(
    val message: String,
    val destinationId: String? = null,
    val conversationId: String? = null,
    val operationId: String? = null
)

data class AssistantJourneyLegDto(val mode: String, val routeName: String?)
data class AssistantJourneyDto(
    val journeyId: String,
    val recommendationType: String,
    val farePesos: Double,
    val durationSeconds: Double,
    val walkingMeters: Double,
    val legs: List<AssistantJourneyLegDto>,
    val plan: JeepneyTripPlanDto
)

data class AssistantActionDto(
    val type: String,
    val requiresConfirmation: Boolean,
    val tripSessionId: String? = null,
    val budgetPesos: Double? = null,
    val preference: String? = null,
    val maxWalkingMeters: Double? = null,
    val avoidTransportModes: List<String>? = null
)

data class AssistantNavigationStateDto(
    val tripSessionId: String,
    val tripState: String,
    val currentLegIndex: Int,
    val currentMode: String?,
    val currentRouteName: String?,
    val nextInstruction: String?,
    val remainingDistanceMeters: Double?,
    val approxFareSpent: Double,
    val estimatedRemainingFare: Double,
    val status: String?
)

data class AssistantResponseDto(
    val status: String,
    val message: String,
    val journeys: List<AssistantJourneyDto>?,
    val destinations: List<DestinationSearchResultDto>?,
    val navigation: AssistantNavigationStateDto?,
    val destination: DestinationSearchResultDto?,
    val conversationId: String?,
    val surface: String?,
    val action: AssistantActionDto?
)

data class AssistantReplanConfirmationDto(
    val status: String,
    val recommendationId: String?
)

interface AiApi {
    @POST("api/AI/ask")
    suspend fun askPlanning(@Body request: AssistantRequest): Response<AssistantResponseDto>

    @POST("api/AI/trip/{sessionId}/ask")
    suspend fun askTrip(
        @Path("sessionId") sessionId: String,
        @Body request: ActiveTripAssistantRequest
    ): Response<AssistantResponseDto>

    @POST("api/AI/trip/{sessionId}/replan/{recommendationId}/confirm")
    suspend fun confirmTripReplan(
        @Path("sessionId") sessionId: String,
        @Path("recommendationId") recommendationId: String
    ): Response<AssistantReplanConfirmationDto>
}

interface AiRepository {
    suspend fun ask(request: AssistantRequest): ApiResult<AssistantResponseDto>

    suspend fun askTrip(
        sessionId: String,
        request: ActiveTripAssistantRequest
    ): ApiResult<AssistantResponseDto>

    suspend fun confirmTripReplan(
        sessionId: String,
        recommendationId: String
    ): ApiResult<AssistantReplanConfirmationDto>
}

class AiRepositoryImpl(
    private val api: AiApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : AiRepository {
    override suspend fun ask(request: AssistantRequest) =
        authenticatedApiCall(sessions, errors) { api.askPlanning(request) }

    override suspend fun askTrip(
        sessionId: String,
        request: ActiveTripAssistantRequest
    ) = authenticatedApiCall(sessions, errors) {
        api.askTrip(sessionId, request)
    }

    override suspend fun confirmTripReplan(
        sessionId: String,
        recommendationId: String
    ) = authenticatedApiCall(sessions, errors) {
        api.confirmTripReplan(sessionId, recommendationId)
    }
}
