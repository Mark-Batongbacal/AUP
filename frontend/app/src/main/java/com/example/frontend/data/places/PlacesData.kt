package com.example.frontend.data.places

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Query

data class DestinationSearchResultDto(
    val id: String,
    val name: String,
    val latitude: Double,
    val longitude: Double,
    val category: String,
    val source: String,
    val address: String?
)

interface PlacesApi {
    @GET("api/places/search")
    suspend fun search(
        @Query("q") query: String,
        @Query("focusLat") focusLatitude: Double? = null,
        @Query("focusLon") focusLongitude: Double? = null
    ): Response<List<DestinationSearchResultDto>>
}

interface PlacesRepository {
    suspend fun searchPlaces(query: String, focusLatitude: Double? = null, focusLongitude: Double? = null): ApiResult<List<DestinationSearchResultDto>>
}

class PlacesRepositoryImpl(
    private val api: PlacesApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : PlacesRepository {
    override suspend fun searchPlaces(query: String, focusLatitude: Double?, focusLongitude: Double?) =
        authenticatedApiCall(sessions, errors) { api.search(query, focusLatitude, focusLongitude) }
}

