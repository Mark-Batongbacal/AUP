package com.example.frontend.data.ai

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.routing.JeepneyTripPlanDto
import com.google.gson.JsonElement
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST

data class AssistantRequest(
    val message: String,
    val originLatitude: Double? = null,
    val originLongitude: Double? = null,
    val tripSessionId: String? = null,
    val destinationId: String? = null
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
data class AssistantResponseDto(
    val status: String,
    val message: String,
    val journeys: List<AssistantJourneyDto>?,
    val destinations: List<DestinationSearchResultDto>?,
    val navigation: JsonElement?,
    val destination: DestinationSearchResultDto?
)

interface AiApi {
    @POST("api/AI/ask")
    suspend fun ask(@Body request: AssistantRequest): Response<AssistantResponseDto>
}

interface AiRepository {
    suspend fun ask(request: AssistantRequest): ApiResult<AssistantResponseDto>
}

class AiRepositoryImpl(
    private val api: AiApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : AiRepository {
    override suspend fun ask(request: AssistantRequest) =
        authenticatedApiCall(sessions, errors) { api.ask(request) }
}
