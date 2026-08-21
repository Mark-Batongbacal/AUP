package com.example.frontend.data.auth

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.apiCall
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSession
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.users.UserProfileDto
import com.example.frontend.data.users.UsersApi
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST

data class LoginRequest(val userName: String, val password: String)
data class GoogleLoginRequest(val idToken: String?)
data class FacebookLoginRequest(val accessToken: String?)
data class FacebookOidcLoginRequest(val idToken: String?, val nonce: String?)

data class RegisterRequest(
    val userName: String,
    val password: String,
    val firstName: String,
    val lastName: String,
    val phoneNumber: String? = null
)

data class ForgotPasswordRequest(val email: String)

data class ResetPasswordRequest(
    val email: String,
    val code: String,
    val newPassword: String
)

data class ChangePasswordOtpRequest(val currentPassword: String)

data class ChangePasswordRequest(
    val currentPassword: String,
    val code: String,
    val newPassword: String
)

data class MessageResponseDto(val message: String?)

data class LoginResponseDto(
    val apiKey: String,
    val expiresAt: String,
    val authenticationScheme: String,
    val headerName: String
)

data class RegisterResponseDto(
    val userId: String,
    val userName: String,
    val firstName: String?,
    val lastName: String?,
    val apiKey: String,
    val expiresAt: String,
    val authenticationScheme: String,
    val headerName: String
)

data class AuthIdentityDto(val userName: String?)
data class AuthenticatedUser(val session: AuthSession, val profile: UserProfileDto)

interface AuthApi {
    @POST("api/auth/login") suspend fun login(@Body request: LoginRequest): Response<LoginResponseDto>
    @POST("api/auth/register") suspend fun register(@Body request: RegisterRequest): Response<RegisterResponseDto>
    @POST("api/auth/google") suspend fun google(@Body request: GoogleLoginRequest): Response<LoginResponseDto>
    @POST("api/auth/facebook") suspend fun facebook(@Body request: FacebookLoginRequest): Response<LoginResponseDto>
    @POST("api/auth/facebook/oidc") suspend fun facebookOidc(@Body request: FacebookOidcLoginRequest): Response<LoginResponseDto>
    @GET("api/auth/me") suspend fun me(): Response<AuthIdentityDto>
    @POST("api/auth/forgot-password") suspend fun forgotPassword(
        @Body request: ForgotPasswordRequest
    ): Response<MessageResponseDto>
    @POST("api/auth/reset-password") suspend fun resetPassword(
        @Body request: ResetPasswordRequest
    ): Response<MessageResponseDto>
    @POST("api/auth/change-password/request-otp") suspend fun requestChangePasswordOtp(
        @Body request: ChangePasswordOtpRequest
    ): Response<MessageResponseDto>
    @POST("api/auth/change-password") suspend fun changePassword(
        @Body request: ChangePasswordRequest
    ): Response<MessageResponseDto>
}

interface AuthRepository {
    suspend fun login(userName: String, password: String): ApiResult<AuthenticatedUser>
    suspend fun register(request: RegisterRequest): ApiResult<AuthenticatedUser>
    suspend fun loginWithGoogle(idToken: String): ApiResult<AuthenticatedUser>
    suspend fun loginWithFacebook(accessToken: String): ApiResult<AuthenticatedUser>
    suspend fun loginWithFacebookOidc(idToken: String, nonce: String): ApiResult<AuthenticatedUser>
    suspend fun getCurrentAuthIdentity(): ApiResult<AuthIdentityDto>
    suspend fun requestPasswordReset(email: String): ApiResult<Unit>
    suspend fun resetPassword(email: String, code: String, newPassword: String): ApiResult<Unit>
    suspend fun requestChangePasswordOtp(currentPassword: String): ApiResult<Unit>
    suspend fun changePassword(currentPassword: String, code: String, newPassword: String): ApiResult<Unit>
    fun logoutLocalSession()
}

class AuthRepositoryImpl(
    private val authApi: AuthApi,
    private val usersApi: UsersApi,
    private val sessionStore: AuthSessionStore,
    private val errors: ApiErrorParser
) : AuthRepository {
    override suspend fun login(userName: String, password: String) =
        authenticate { authApi.login(LoginRequest(userName, password)) }

    override suspend fun register(request: RegisterRequest): ApiResult<AuthenticatedUser> {
        return when (val response = apiCall(errors) { authApi.register(request) }) {
            is ApiResult.Success -> finishAuthentication(response.data.toSession())
            is ApiResult.Failure -> response
        }
    }

    override suspend fun loginWithGoogle(idToken: String) =
        authenticate { authApi.google(GoogleLoginRequest(idToken)) }

    override suspend fun loginWithFacebook(accessToken: String) =
        authenticate { authApi.facebook(FacebookLoginRequest(accessToken)) }

    override suspend fun loginWithFacebookOidc(idToken: String, nonce: String) =
        authenticate { authApi.facebookOidc(FacebookOidcLoginRequest(idToken, nonce)) }

    override suspend fun getCurrentAuthIdentity() =
        authenticatedApiCall(sessionStore, errors) { authApi.me() }

    override suspend fun requestPasswordReset(email: String): ApiResult<Unit> =
        toUnit(apiCall(errors) {
            authApi.forgotPassword(ForgotPasswordRequest(email.trim()))
        })

    override suspend fun resetPassword(
        email: String,
        code: String,
        newPassword: String
    ): ApiResult<Unit> = toUnit(apiCall(errors) {
        authApi.resetPassword(
            ResetPasswordRequest(
                email = email.trim(),
                code = code.trim(),
                newPassword = newPassword
            )
        )
    })

    override suspend fun requestChangePasswordOtp(currentPassword: String): ApiResult<Unit> =
        toUnit(authenticatedApiCall(sessionStore, errors) {
            authApi.requestChangePasswordOtp(ChangePasswordOtpRequest(currentPassword))
        })

    override suspend fun changePassword(
        currentPassword: String,
        code: String,
        newPassword: String
    ): ApiResult<Unit> = toUnit(authenticatedApiCall(sessionStore, errors) {
        authApi.changePassword(ChangePasswordRequest(currentPassword, code.trim(), newPassword))
    })

    override fun logoutLocalSession() = sessionStore.clear()

    private fun toUnit(result: ApiResult<MessageResponseDto>): ApiResult<Unit> = when (result) {
        is ApiResult.Success -> ApiResult.Success(Unit)
        is ApiResult.Failure -> result
    }

    private suspend fun authenticate(call: suspend () -> Response<LoginResponseDto>): ApiResult<AuthenticatedUser> =
        when (val response = apiCall(errors, request = call)) {
            is ApiResult.Success -> finishAuthentication(response.data.toSession())
            is ApiResult.Failure -> response
        }

    private suspend fun finishAuthentication(session: AuthSession): ApiResult<AuthenticatedUser> {
        if (session.apiKey.isBlank() || session.headerName.isBlank() || session.isExpired()) {
            return ApiResult.Failure(null, "The server returned an invalid login response.")
        }
        sessionStore.save(session)
        return when (val profile = authenticatedApiCall(sessionStore, errors) { usersApi.getCurrentUser() }) {
            is ApiResult.Success -> ApiResult.Success(AuthenticatedUser(session, profile.data))
            is ApiResult.Failure -> profile
        }
    }

    private fun LoginResponseDto.toSession() = AuthSession(apiKey, expiresAt, authenticationScheme, headerName)
    private fun RegisterResponseDto.toSession() = AuthSession(apiKey, expiresAt, authenticationScheme, headerName)
}
