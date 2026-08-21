package com.example.frontend.data.favorites

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path

data class AddFavoriteTripRequest(val recommendationId: String, val note: String? = null)

data class FavoriteTripDto(
    val favoriteTripId: String,
    val userId: String,
    val recommendationId: String,
    val origin: String?,
    val destination: String?,
    val recommendationType: String,
    val totalMinutes: Double,
    val totalFare: Double,
    val walkingDistanceMeters: Double,
    val transferCount: Int,
    val timesUsed: Int,
    val note: String?,
    val createdAt: String
)

interface FavoritesApi {
    @GET("api/favorite-trips") suspend fun list(): Response<List<FavoriteTripDto>>
    @POST("api/favorite-trips") suspend fun add(@Body request: AddFavoriteTripRequest): Response<FavoriteTripDto>
    @DELETE("api/favorite-trips/{favoriteTripId}") suspend fun remove(@Path("favoriteTripId") favoriteTripId: String): Response<Unit>
}

interface FavoritesRepository {
    suspend fun getFavorites(): ApiResult<List<FavoriteTripDto>>
    suspend fun addFavorite(recommendationId: String, note: String? = null): ApiResult<FavoriteTripDto>
    suspend fun removeFavorite(favoriteTripId: String): ApiResult<Unit>
}

class FavoritesRepositoryImpl(
    private val api: FavoritesApi,
    private val sessions: AuthSessionStore,
    private val errors: ApiErrorParser
) : FavoritesRepository {
    override suspend fun getFavorites() = authenticatedApiCall(sessions, errors) { api.list() }
    override suspend fun addFavorite(recommendationId: String, note: String?) =
        authenticatedApiCall(sessions, errors) { api.add(AddFavoriteTripRequest(recommendationId, note)) }
    override suspend fun removeFavorite(favoriteTripId: String) =
        authenticatedApiCall(sessions, errors, noContentValue = Unit) { api.remove(favoriteTripId) }
}
