package com.example.frontend.data.users

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.PUT

data class UpdateUserProfileRequest(
    val firstName: String? = null,
    val lastName: String? = null,
    val phoneNumber: String? = null,
    val profileImageUrl: String? = null,
    val preferredLanguage: String? = null
)

data class UserProfileDto(
    val userId: String,
    val firstName: String?,
    val lastName: String?,
    val phoneNumber: String?,
    val role: String,
    val profileImageUrl: String?,
    val createdAt: String,
    val updatedAt: String?,
    val email: String? = null,
    val tripsTaken: Int = 0,
    val favoritesCount: Int = 0,
    val preferredLanguage: String = "English"
)

interface UsersApi {
    @GET("api/users/me")
    suspend fun getCurrentUser(): Response<UserProfileDto>

    @PUT("api/users/me")
    suspend fun updateCurrentUser(@Body request: UpdateUserProfileRequest): Response<UserProfileDto>

    @DELETE("api/users/me")
    suspend fun deleteCurrentUser(): Response<Unit>
}

interface UserRepository {
    suspend fun getCurrentUser(): ApiResult<UserProfileDto>
    suspend fun updateCurrentUser(request: UpdateUserProfileRequest): ApiResult<UserProfileDto>

    suspend fun deleteCurrentUser(): ApiResult<Unit>
}

class UserRepositoryImpl(
    private val api: UsersApi,
    private val sessionStore: AuthSessionStore,
    private val errors: ApiErrorParser,
    private val onPreferredLanguageChanged: (String) -> Unit = {}
) : UserRepository {
    override suspend fun getCurrentUser(): ApiResult<UserProfileDto> {
        val result = authenticatedApiCall(sessionStore, errors) { api.getCurrentUser() }
        if (result is ApiResult.Success) {
            onPreferredLanguageChanged(result.data.preferredLanguage)
        }
        return result
    }

    override suspend fun updateCurrentUser(request: UpdateUserProfileRequest): ApiResult<UserProfileDto> {
        val result = authenticatedApiCall(sessionStore, errors) { api.updateCurrentUser(request) }
        if (result is ApiResult.Success) {
            onPreferredLanguageChanged(result.data.preferredLanguage)
        }
        return result
    }

    override suspend fun deleteCurrentUser(): ApiResult<Unit> =
        authenticatedApiCall(sessionStore, errors, noContentValue = Unit) { api.deleteCurrentUser() }
}
