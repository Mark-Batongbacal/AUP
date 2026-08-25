package com.example.frontend.data

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.storage.AuthSession
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.ai.AiApi
import com.example.frontend.data.ai.AiRepositoryImpl
import com.example.frontend.data.ai.AssistantRequest
import com.example.frontend.data.ai.AssistantResponseDto
import com.example.frontend.data.routing.JeepneyTripPlanDto
import com.example.frontend.data.routing.PlannedJourney
import com.example.frontend.data.routing.toDomain
import com.example.frontend.data.routing.toRouteOption
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class AssistantPlanningContractTest {
    @Test
    fun destinationSelectionSendsOnlyTrustedSelectionFields() = runBlocking {
        val api = RecordingAiApi()
        val repository = AiRepositoryImpl(api, MemorySessionStore(validSession), ApiErrorParser())

        val result = repository.ask(
            AssistantRequest(
                conversationId = "conversation-1",
                destinationSelectionToken = "selection-1",
                selectedDestinationCandidateId = "candidate-2"
            )
        )

        assertTrue(result is ApiResult.Success)
        assertEquals("conversation-1", api.lastRequest?.conversationId)
        assertEquals("selection-1", api.lastRequest?.destinationSelectionToken)
        assertEquals("candidate-2", api.lastRequest?.selectedDestinationCandidateId)
        assertNull(api.lastRequest?.message)
        assertNull(api.lastRequest?.destinationId)
    }

    @Test
    fun exactAssistantJourneyMappingKeepsRecommendationIdAndPlanData() {
        val plan = samplePlan()
        val option = PlannedJourney("recommendation-7", plan)
            .toRouteOption("Home", "SM City Clark")

        assertEquals("recommendation-7", option.id)
        assertEquals(12.0, option.totalFare, 0.0)
        assertEquals(1, option.steps.size)
        assertEquals(1, option.legRoutePoints.size)
        assertEquals(15.1, option.legEndPoints.single().latitude, 0.0)
    }

    private class RecordingAiApi : AiApi {
        var lastRequest: AssistantRequest? = null

        override suspend fun askPlanning(request: AssistantRequest): Response<AssistantResponseDto> {
            lastRequest = request
            return Response.success(
                AssistantResponseDto(
                    status = "DESTINATION_SELECTION_EXPIRED",
                    message = "Expired",
                    journeys = null,
                    destinations = null,
                    navigation = null,
                    destination = null,
                    conversationId = request.conversationId,
                    surface = "Planning",
                    action = null,
                    destinationSelectionToken = null
                )
            )
        }

        override suspend fun askTrip(
            sessionId: String,
            request: com.example.frontend.data.ai.ActiveTripAssistantRequest
        ): Response<AssistantResponseDto> = error("not used")

        override suspend fun confirmTripReplan(
            sessionId: String,
            recommendationId: String
        ): Response<com.example.frontend.data.ai.AssistantReplanConfirmationDto> = error("not used")
    }

    private class MemorySessionStore(private var value: AuthSession?) : AuthSessionStore {
        override fun read(): AuthSession? = value
        override fun save(session: AuthSession) { value = session }
        override fun clear() { value = null }
    }

    private companion object {
        val validSession = AuthSession(
            apiKey = "test-key",
            expiresAt = "2099-01-01T00:00:00Z",
            authenticationScheme = "ApiKey",
            headerName = "X-Api-Key"
        )

        fun samplePlan(): com.example.frontend.data.routing.JourneyPlan {
            val leg = com.example.frontend.data.routing.JeepneyTripLegDto(
                mode = 0,
                routeId = null,
                routeName = null,
                boardLatitude = 15.0,
                boardLongitude = 120.0,
                alightLatitude = 15.1,
                alightLongitude = 120.1,
                originLatitude = 15.0,
                originLongitude = 120.0,
                destinationLatitude = 15.1,
                destinationLongitude = 120.1,
                distanceMeters = 1000.0,
                durationSeconds = 600.0,
                farePesos = 12.0,
                generalizedCostPesos = 20.0,
                walkDistanceMeters = 1000.0,
                walkTimeSeconds = 600.0,
                trikeDistanceMeters = null,
                trikeTimeSeconds = null,
                jeepneyDistanceMeters = null,
                jeepneyTimeSeconds = null,
                trikePointId = null,
                trikePointName = null,
                geometry = listOf(
                    com.example.frontend.data.routing.RouteGeometryPointDto(15.0, 120.0),
                    com.example.frontend.data.routing.RouteGeometryPointDto(15.1, 120.1)
                )
            )
            return com.example.frontend.data.routing.JeepneyTripPlanDto(
                recommendationType = "efficient",
                legs = listOf(leg),
                originAccess = com.example.frontend.data.routing.JeepneyAccessSegmentDto(
                    mode = 0,
                    walkDistanceMeters = 1000.0,
                    walkTimeSeconds = 600.0,
                    trikePointId = null,
                    trikePointName = null,
                    trikePointLatitude = null,
                    trikePointLongitude = null,
                    trikeRideDistanceMeters = null,
                    trikeRideTimeSeconds = null,
                    totalTimeSeconds = 600.0,
                    totalFarePesos = 0.0,
                    generalizedCostPesos = 10.0
                ),
                destinationAccess = com.example.frontend.data.routing.JeepneyAccessSegmentDto(
                    mode = 0,
                    walkDistanceMeters = 0.0,
                    walkTimeSeconds = 0.0,
                    trikePointId = null,
                    trikePointName = null,
                    trikePointLatitude = null,
                    trikePointLongitude = null,
                    trikeRideDistanceMeters = null,
                    trikeRideTimeSeconds = null,
                    totalTimeSeconds = 0.0,
                    totalFarePesos = 0.0,
                    generalizedCostPesos = 0.0
                ),
                transferWalkDistancesMeters = emptyList(),
                transferWalkTimesSeconds = emptyList(),
                totalTimeSeconds = 600.0,
                totalFarePesos = 12.0,
                generalizedCostPesos = 20.0,
                transferCount = 0
            ).toDomain()
        }
    }
}
