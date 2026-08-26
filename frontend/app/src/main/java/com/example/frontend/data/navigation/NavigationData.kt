package com.example.frontend.data.navigation

import com.example.frontend.core.location.NavigationSyncSignal
import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.apiCall
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.tripsessions.TripSessionDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query
import java.math.BigDecimal
import java.util.Locale

data class StartNavigationRequest(val recommendationId: String)
data class NavigationRerouteRequest(
    val reason: String = "MANUAL",
    val preference: String? = null,
    val budget: BigDecimal? = null,
    val clearBudget: Boolean = false,
    val destinationName: String? = null,
    val destinationLatitude: Double? = null,
    val destinationLongitude: Double? = null,
    val avoidTransportMode: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val accuracyMeters: Double? = null,
    val timestamp: String? = null,
    val speedMetersPerSecond: Double? = null,
    val bearingDegrees: Double? = null
)
data class NavigationGeometryPointDto(val latitude: Double, val longitude: Double)
data class NavigationGeometryResponseDto(val points: List<NavigationGeometryPointDto>)

data class NavigationLocationUpdate(
    val latitude: Double,
    val longitude: Double,
    val accuracyMeters: Double,
    val timestamp: String,
    val speedMetersPerSecond: Double? = null,
    val bearingDegrees: Double? = null
)

data class NavigationLegDto(
    val legIndex: Int,
    val transportMode: String,
    val routeId: Long? = null,
    val routeName: String?,
    val fromName: String?,
    val toName: String?,
    val startLatitude: Double?,
    val startLongitude: Double?,
    val endLatitude: Double?,
    val endLongitude: Double?,
    val distanceMeters: Double?,
    val fare: BigDecimal
)

data class NavigationInstructionSnapshotDto(
    val type: String,
    val routeName: String?,
    val transportMode: String?,
    val distanceMeters: Double?,
    val requiresConfirmation: Boolean,
    val text: String? = null
)

data class NavigationInstructionDetailDto(
    val sequence: Int,
    val type: String,
    val legIndex: Int,
    val text: String,
    val streetName: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val distanceFromLegStartMeters: Double? = null,
    val triggerDistanceMeters: Double = 30.0,
    val requiresConfirmation: Boolean = false
)

data class NavigationLandmarkDto(
    val name: String,
    val category: String,
    val role: String,
    val relation: String,
    val latitude: Double,
    val longitude: Double,
    val distanceFromTargetMeters: Double,
    val distanceFromRouteStartMeters: Double? = null,
    val triggerBeforeMeters: Double = 0.0,
    val triggerAfterMeters: Double = 0.0
)

data class NavigationStopInfoDto(
    val routeName: String?,
    val latitude: Double?,
    val longitude: Double?,
    val landmark: NavigationLandmarkDto?
)
data class NavigationTriggeredEventDto(val type: String, val landmarkName: String?)
data class NavigationTripSummaryDto(
    val destinationName: String,
    val durationMinutes: Int?,
    val approxFareSpent: BigDecimal,
    val transitLegs: Int,
    val transfers: Int
)

data class NavigationSnapshotDto(
    val sessionId: String,
    val state: String,
    val currentLegIndex: Int,
    val currentLeg: NavigationLegDto?,
    val nextInstruction: NavigationInstructionSnapshotDto?,
    val spokenInstruction: String?,
    val remainingDistanceMeters: Double?,
    val progressMeters: Double,
    val boardInfo: NavigationStopInfoDto?,
    val alightInfo: NavigationStopInfoDto?,
    val landmark: NavigationLandmarkDto?,
    val requiresBoardingConfirmation: Boolean,
    val requiresAlightingConfirmation: Boolean,
    val rerouteRequired: Boolean,
    val status: String,
    val triggeredEvents: List<NavigationTriggeredEventDto>,
    val currentLatitude: Double? = null,
    val currentLongitude: Double? = null,
    val approxFareSpent: BigDecimal = BigDecimal.ZERO,
    val estimatedRemainingFare: BigDecimal = BigDecimal.ZERO,
    val followingInstruction: NavigationInstructionSnapshotDto? = null,
    val tripSummary: NavigationTripSummaryDto? = null,
    val spokenInstructionTemplate: String? = null,
    val currentLegInstructions: List<NavigationInstructionDetailDto> = emptyList(),
    val currentLegLandmarks: List<NavigationLandmarkDto> = emptyList()
) {
    fun displayInstruction(): String? = spokenInstruction?.takeIf { it.isNotBlank() }

    fun withLocalLocation(update: NavigationLocationUpdate): NavigationSnapshotDto = copy(
        currentLatitude = update.latitude,
        currentLongitude = update.longitude
    )

    fun isActiveNavigation(): Boolean =
        !state.equals("Arrived", ignoreCase = true) &&
            !state.equals("Cancelled", ignoreCase = true)
}

interface NavigationApi {
    @POST("api/navigation/start")
    suspend fun start(@Body request: StartNavigationRequest): Response<NavigationSnapshotDto>

    @GET("api/navigation/active")
    suspend fun active(): Response<NavigationSnapshotDto>

    @GET("api/navigation/geometry")
    suspend fun geometry(
        @Query("startLat") startLatitude: Double,
        @Query("startLon") startLongitude: Double,
        @Query("endLat") endLatitude: Double,
        @Query("endLon") endLongitude: Double,
        @Query("mode") mode: String,
        @Query("routeId") routeId: Long? = null
    ): Response<NavigationGeometryResponseDto>

    @POST("api/navigation/{sessionId}/location")
    suspend fun location(
        @Path("sessionId") sessionId: String,
        @Body update: NavigationLocationUpdate
    ): Response<NavigationSnapshotDto>

    @POST("api/navigation/{sessionId}/boarding")
    suspend fun boarding(@Path("sessionId") sessionId: String): Response<NavigationSnapshotDto>

    @POST("api/navigation/{sessionId}/alighting")
    suspend fun alighting(@Path("sessionId") sessionId: String): Response<NavigationSnapshotDto>

    @POST("api/tripsessions/{sessionId}/cancel")
    suspend fun cancel(@Path("sessionId") sessionId: String): Response<TripSessionDto>

    @POST("api/navigation/{sessionId}/reroute")
    suspend fun reroute(
        @Path("sessionId") sessionId: String,
        @Body request: NavigationRerouteRequest
    ): Response<NavigationSnapshotDto>
}

interface NavigationRepository {
    suspend fun startNavigation(recommendationId: String): ApiResult<NavigationSnapshotDto>
    suspend fun getActiveNavigation(): ApiResult<NavigationSnapshotDto>
    fun restoreActiveNavigation(): NavigationSnapshotDto?
    suspend fun getGeometry(
        startLatitude: Double,
        startLongitude: Double,
        endLatitude: Double,
        endLongitude: Double,
        mode: String,
        routeId: Long? = null
    ): ApiResult<NavigationGeometryResponseDto>
    suspend fun updateLocation(sessionId: String, update: NavigationLocationUpdate): ApiResult<NavigationSnapshotDto>
    suspend fun confirmBoarding(sessionId: String): ApiResult<NavigationSnapshotDto>
    suspend fun confirmAlighting(sessionId: String): ApiResult<NavigationSnapshotDto>
    suspend fun cancel(sessionId: String): ApiResult<TripSessionDto>
    suspend fun reroute(
        sessionId: String,
        request: NavigationRerouteRequest = NavigationRerouteRequest()
    ): ApiResult<NavigationSnapshotDto>
    fun saveLocalActiveNavigation(snapshot: NavigationSnapshotDto)
    fun clearLocalActiveNavigation(sessionId: String)
    fun clearLocalNavigation()
}

class NavigationRepositoryImpl(
    private val api: NavigationApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser,
    private val localStore: NavigationLocalStore = NoOpNavigationLocalStore
) : NavigationRepository {
    private val cacheLock = Any()
    private val snapshotsBySession = mutableMapOf<String, NavigationSnapshotDto>()

    init {
        localStore.readActiveSnapshot()
            ?.takeIf { it.isActiveNavigation() }
            ?.let { snapshotsBySession[it.sessionId] = it }
    }

    override suspend fun startNavigation(recommendationId: String): ApiResult<NavigationSnapshotDto> =
        cacheSnapshot(call { api.start(StartNavigationRequest(recommendationId)) }, resetSyncSignal = true)

    override suspend fun getActiveNavigation(): ApiResult<NavigationSnapshotDto> {
        val remote = call { api.active() }
        if (remote is ApiResult.Success) return cacheSnapshot(remote)

        val failure = remote as ApiResult.Failure
        if (failure.statusCode == 404) {
            restoreActiveNavigation()?.let { clearSessionCache(it.sessionId) }
        }
        val restored = if (failure.isTransientForLocalRecovery()) restoreActiveNavigation() else null
        return restored?.let { ApiResult.Success(it) } ?: failure
    }

    override fun restoreActiveNavigation(): NavigationSnapshotDto? = synchronized(cacheLock) {
        snapshotsBySession.values.firstOrNull { it.isActiveNavigation() }
            ?: localStore.readActiveSnapshot()
                ?.takeIf { it.isActiveNavigation() }
                ?.also { snapshotsBySession[it.sessionId] = it }
    }

    override suspend fun getGeometry(
        startLatitude: Double,
        startLongitude: Double,
        endLatitude: Double,
        endLongitude: Double,
        mode: String,
        routeId: Long?
    ): ApiResult<NavigationGeometryResponseDto> {
        val cacheKey = geometryCacheKey(
            startLatitude,
            startLongitude,
            endLatitude,
            endLongitude,
            mode,
            routeId
        )
        return when (
            val remote = apiCall(errors) {
                api.geometry(startLatitude, startLongitude, endLatitude, endLongitude, mode, routeId)
            }
        ) {
            is ApiResult.Success -> remote.also { localStore.saveGeometry(cacheKey, it.data) }
            is ApiResult.Failure -> {
                if (remote.isTransientForLocalRecovery()) {
                    localStore.readGeometry(cacheKey)?.let { ApiResult.Success(it) } ?: remote
                } else {
                    remote
                }
            }
        }
    }

    override suspend fun updateLocation(
        sessionId: String,
        update: NavigationLocationUpdate
    ): ApiResult<NavigationSnapshotDto> {
        val forceSync = NavigationSyncSignal.consumeImmediateSync()
        if (!forceSync) {
            val local = cachedSnapshot(sessionId)?.withLocalLocation(update)
            if (local != null) {
                saveLocalSnapshot(local)
                return ApiResult.Success(local)
            }
        }

        // Recovery without a cache, or a meaningful local navigation event, is allowed to
        // contact the backend. Routine on-route fixes are persisted locally instead.
        val result = call { api.location(sessionId, update) }
        if (result is ApiResult.Success) {
            return cacheSnapshot(result)
        }

        // Do not lose an off-route/leg-end confirmation merely because connectivity dropped
        // for one attempt. The next tracking tick will retry while local guidance keeps running.
        if (forceSync) NavigationSyncSignal.requestImmediateSync(samples = 1)
        return result
    }

    override suspend fun confirmBoarding(sessionId: String): ApiResult<NavigationSnapshotDto> =
        cacheSnapshot(call { api.boarding(sessionId) }, resetSyncSignal = true)

    override suspend fun confirmAlighting(sessionId: String): ApiResult<NavigationSnapshotDto> =
        cacheSnapshot(call { api.alighting(sessionId) }, resetSyncSignal = true)

    override suspend fun cancel(sessionId: String): ApiResult<TripSessionDto> {
        val result = call { api.cancel(sessionId) }
        if (result is ApiResult.Success) {
            clearSessionCache(sessionId)
            NavigationSyncSignal.reset()
        }
        return result
    }

    override suspend fun reroute(
        sessionId: String,
        request: NavigationRerouteRequest
    ): ApiResult<NavigationSnapshotDto> =
        cacheSnapshot(call { api.reroute(sessionId, request) }, resetSyncSignal = true)

    override fun saveLocalActiveNavigation(snapshot: NavigationSnapshotDto) {
        if (snapshot.isActiveNavigation()) {
            saveLocalSnapshot(snapshot)
        } else {
            clearSessionCache(snapshot.sessionId)
        }
    }

    override fun clearLocalActiveNavigation(sessionId: String) {
        clearSessionCache(sessionId)
        NavigationSyncSignal.reset()
    }

    override fun clearLocalNavigation() {
        synchronized(cacheLock) { snapshotsBySession.clear() }
        localStore.clearAll()
        NavigationSyncSignal.reset()
    }

    private fun cachedSnapshot(sessionId: String): NavigationSnapshotDto? = synchronized(cacheLock) {
        snapshotsBySession[sessionId]
            ?: localStore.readActiveSnapshot()
                ?.takeIf { it.sessionId == sessionId && it.isActiveNavigation() }
                ?.also { snapshotsBySession[sessionId] = it }
    }

    private fun saveLocalSnapshot(snapshot: NavigationSnapshotDto) {
        synchronized(cacheLock) { snapshotsBySession[snapshot.sessionId] = snapshot }
        localStore.saveActiveSnapshot(snapshot)
    }

    private fun cacheSnapshot(
        result: ApiResult<NavigationSnapshotDto>,
        resetSyncSignal: Boolean = false
    ): ApiResult<NavigationSnapshotDto> {
        if (result is ApiResult.Success) {
            val snapshot = result.data
            if (snapshot.isActiveNavigation()) {
                synchronized(cacheLock) {
                    snapshotsBySession.clear()
                    snapshotsBySession[snapshot.sessionId] = snapshot
                }
                localStore.saveActiveSnapshot(snapshot)
            } else {
                clearSessionCache(snapshot.sessionId)
            }
            if (resetSyncSignal) NavigationSyncSignal.reset()
        }
        return result
    }

    private fun clearSessionCache(sessionId: String) {
        synchronized(cacheLock) { snapshotsBySession.remove(sessionId) }
        localStore.clearActiveSnapshot(sessionId)
    }

    private fun geometryCacheKey(
        startLatitude: Double,
        startLongitude: Double,
        endLatitude: Double,
        endLongitude: Double,
        mode: String,
        routeId: Long?
    ): String = listOf(
        normalizedCoordinate(startLatitude),
        normalizedCoordinate(startLongitude),
        normalizedCoordinate(endLatitude),
        normalizedCoordinate(endLongitude),
        mode.trim().uppercase(Locale.ROOT),
        routeId?.toString().orEmpty()
    ).joinToString("|")

    private fun normalizedCoordinate(value: Double): String =
        String.format(Locale.US, "%.6f", value)

    private fun ApiResult.Failure.isTransientForLocalRecovery(): Boolean =
        statusCode == null || statusCode >= 500

    private suspend fun <T : Any> call(block: suspend () -> Response<T>) =
        authenticatedApiCall(sessions, errors, request = block)
}
