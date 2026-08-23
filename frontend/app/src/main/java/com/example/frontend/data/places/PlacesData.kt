package com.example.frontend.data.places

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.apiCall
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

    @GET("api/places/search/more")
    suspend fun searchMore(
        @Query("q") query: String,
        @Query("focusLat") focusLatitude: Double? = null,
        @Query("focusLon") focusLongitude: Double? = null
    ): Response<List<DestinationSearchResultDto>>

    @GET("api/places/reverse")
    suspend fun reverse(
        @Query("lat") latitude: Double,
        @Query("lon") longitude: Double
    ): Response<DestinationSearchResultDto>
}

interface PlacesRepository {
    suspend fun searchPlaces(
        query: String,
        focusLatitude: Double? = null,
        focusLongitude: Double? = null
    ): ApiResult<List<DestinationSearchResultDto>>

    suspend fun searchMorePlaces(
        query: String,
        focusLatitude: Double? = null,
        focusLongitude: Double? = null
    ): ApiResult<List<DestinationSearchResultDto>>

    suspend fun reverseGeocode(latitude: Double, longitude: Double): ApiResult<DestinationSearchResultDto>
}

class PlacesRepositoryImpl(
    private val api: PlacesApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : PlacesRepository {
    override suspend fun searchPlaces(query: String, focusLatitude: Double?, focusLongitude: Double?) =
        apiCall(errors) { api.search(query, focusLatitude, focusLongitude) }

    override suspend fun searchMorePlaces(query: String, focusLatitude: Double?, focusLongitude: Double?) =
        apiCall(errors) { api.searchMore(query, focusLatitude, focusLongitude) }

    override suspend fun reverseGeocode(latitude: Double, longitude: Double) =
        apiCall(errors) { api.reverse(latitude, longitude) }
}
