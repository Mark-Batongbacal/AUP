package com.example.frontend.data

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.AuthInterceptor
import com.example.frontend.core.network.apiCall
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSession
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.ridematching.RideMatchDetailsDto
import com.example.frontend.data.routing.JeepneyTripPlanDto
import com.example.frontend.data.routing.TransitMode
import com.example.frontend.data.routing.toDomain
import com.example.frontend.data.tricycle.TricyclePointResponseDto
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response
import java.time.Instant

class NetworkContractTest {
    private val gson = Gson()

    @Test
    fun authInterceptor_usesBackendHeaderWithoutSchemePrefix() {
        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(200))
        server.start()
        try {
            val store = MemorySessionStore(validSession)
            OkHttpClient.Builder().addInterceptor(AuthInterceptor(store)).build()
                .newCall(Request.Builder().url(server.url("/api/users/me")).build())
                .execute().close()

            assertEquals("secret-key", server.takeRequest().getHeader("X-Api-Key"))
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun sessionStore_clearsExpiredCredentials() {
        val store = MemorySessionStore(validSession.copy(expiresAt = "2025-01-01T00:00:00Z"))
        assertNull(store.validSession(Instant.parse("2026-08-19T00:00:00Z")))
        assertTrue(store.cleared)
    }

    @Test
    fun routingPlan_parsesMultipleLegsAndNullableModeDetails() {
        val json = """[{"recommendationType":"fastest","legs":[
          {"mode":0,"routeId":null,"routeName":null,"boardLatitude":14.1,"boardLongitude":121.1,"alightLatitude":14.2,"alightLongitude":121.2,"originLatitude":14.1,"originLongitude":121.1,"destinationLatitude":14.2,"destinationLongitude":121.2,"distanceMeters":250.0,"durationSeconds":180.0,"farePesos":0.0,"generalizedCostPesos":3.0,"walkDistanceMeters":250.0,"walkTimeSeconds":180.0,"trikeDistanceMeters":null,"trikeTimeSeconds":null,"jeepneyDistanceMeters":null,"jeepneyTimeSeconds":null,"trikePointId":null,"trikePointName":null},
          {"mode":2,"routeId":"J-1","routeName":"Bayan Route","boardLatitude":14.2,"boardLongitude":121.2,"alightLatitude":14.5,"alightLongitude":121.5,"originLatitude":14.2,"originLongitude":121.2,"destinationLatitude":14.5,"destinationLongitude":121.5,"distanceMeters":5000.0,"durationSeconds":900.0,"farePesos":15.0,"generalizedCostPesos":30.0,"walkDistanceMeters":null,"walkTimeSeconds":null,"trikeDistanceMeters":null,"trikeTimeSeconds":null,"jeepneyDistanceMeters":5000.0,"jeepneyTimeSeconds":900.0,"trikePointId":null,"trikePointName":null}],
          "originAccess":{"mode":0,"walkDistanceMeters":250.0,"walkTimeSeconds":180.0,"trikePointId":null,"trikePointName":null,"trikePointLatitude":null,"trikePointLongitude":null,"trikeRideDistanceMeters":null,"trikeRideTimeSeconds":null,"totalTimeSeconds":180.0,"totalFarePesos":0.0,"generalizedCostPesos":3.0},
          "destinationAccess":{"mode":1,"walkDistanceMeters":30.0,"walkTimeSeconds":20.0,"trikePointId":"T-1","trikePointName":"Terminal","trikePointLatitude":14.5,"trikePointLongitude":121.5,"trikeRideDistanceMeters":800.0,"trikeRideTimeSeconds":300.0,"totalTimeSeconds":320.0,"totalFarePesos":20.0,"generalizedCostPesos":25.0},
          "transferWalkDistancesMeters":[50.0],"transferWalkTimesSeconds":[40.0],"totalTimeSeconds":1400.0,"totalFarePesos":35.0,"generalizedCostPesos":58.0,"transferCount":0}]"""
        val type = object : TypeToken<List<JeepneyTripPlanDto>>() {}.type
        val plan = gson.fromJson<List<JeepneyTripPlanDto>>(json, type).single()
        val domain = plan.toDomain()

        assertEquals(2, plan.legs.size)
        assertEquals(TransitMode.Walk, domain.legs[0].mode)
        assertEquals(TransitMode.Jeepney, domain.legs[1].mode)
        assertNull(plan.legs[0].routeId)
        assertEquals("T-1", plan.destinationAccess.trikePointId)
    }

    @Test
    fun tricyclePoint_preservesNullableFareAndServiceFields() {
        val dto = gson.fromJson(
            """{"tricyclePointId":7,"stopId":null,"pointCode":"TP7","pointName":"Market","description":null,"address":null,"operatorName":null,"centerLatitude":14.2,"centerLongitude":121.2,"radiusMeters":150,"baseFare":null,"farePerKilometer":null,"averageWaitingTimeSeconds":null,"serviceStartTime":null,"serviceEndTime":null,"isActive":true}""",
            TricyclePointResponseDto::class.java
        )
        assertNull(dto.stopId)
        assertNull(dto.baseFare)
        assertNull(dto.serviceStartTime)
    }

    @Test
    fun rideMatch_preservesNestedBackendDtos() {
        val json = """{"matchId":"10000000-0000-0000-0000-000000000001","requestId":"20000000-0000-0000-0000-000000000001","driverId":"30000000-0000-0000-0000-000000000001","sessionId":9,"vehicleId":null,"pickupDistanceMeters":123.45,"detourDistanceMeters":null,"estimatedPickupMinutes":4.5,"estimatedTripMinutes":12,"estimatedFare":30.00,"matchScore":0.91,"status":"OFFERED","offeredAt":"2026-08-19T01:00:00Z","acceptedAt":null,"completedAt":null,"request":{"requestId":"20000000-0000-0000-0000-000000000001","passengerUserId":"40000000-0000-0000-0000-000000000001","transportModeId":2,"transportMode":{"transportModeId":2,"code":"TRICYCLE","name":"Tricycle","isMotorized":true,"allowsLiveDriver":true,"iconName":null},"pickupName":"Gate","pickupLatitude":14.1,"pickupLongitude":121.1,"dropoffName":"Market","dropoffLatitude":14.2,"dropoffLongitude":121.2,"passengerCount":2,"maxBudget":50,"status":"SEARCHING","requestedAt":"2026-08-19T00:55:00Z","expiresAt":null,"updatedAt":"2026-08-19T00:55:00Z"},"driver":{"driverId":"30000000-0000-0000-0000-000000000001","userId":"50000000-0000-0000-0000-000000000001","licenseNumber":null,"verificationStatus":"VERIFIED","homeTerminalId":null,"averageRating":4.75,"ratingCount":20,"isAvailable":true,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null},"availabilitySession":null,"vehicle":null}"""
        val dto = gson.fromJson(json, RideMatchDetailsDto::class.java)
        assertEquals("TRICYCLE", dto.request?.transportMode?.code)
        assertEquals("VERIFIED", dto.driver?.verificationStatus)
        assertEquals("30.00", dto.estimatedFare?.toPlainString())
    }

    @Test
    fun apiCall_treats204AsSuccess() = runBlocking {
        val result = apiCall(ApiErrorParser(), Unit) { Response.success<Unit>(204, null) }
        assertTrue(result is ApiResult.Success)
    }

    @Test
    fun errorParser_supportsProblemDetailsAndValidationDictionary() {
        val result = ApiErrorParser().parse(400, """{"title":"Validation failed","detail":"Check the request.","errors":{"FirstName":["Required"],"Phone":["Invalid"]}}""")
        assertEquals("Check the request.", result.message)
        assertEquals(listOf("Required", "Invalid"), result.errors)
    }

    @Test
    fun authenticatedCall_doesNotSendRequestWithoutValidSession() = runBlocking {
        var called = false
        val result = authenticatedApiCall(MemorySessionStore(null), ApiErrorParser()) {
            called = true
            Response.success("unexpected")
        }
        assertTrue(result is ApiResult.Failure && result.isUnauthorized)
        assertFalse(called)
    }

    private class MemorySessionStore(initial: AuthSession?) : AuthSessionStore {
        private var value = initial
        var cleared = false
        override fun read() = value
        override fun save(session: AuthSession) { value = session }
        override fun clear() { value = null; cleared = true }
    }

    private companion object {
        val validSession = AuthSession("secret-key", "2099-01-01T00:00:00Z", "ApiKey", "X-Api-Key")
    }
}
