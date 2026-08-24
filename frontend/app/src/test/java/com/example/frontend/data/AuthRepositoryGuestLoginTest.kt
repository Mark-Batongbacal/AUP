package com.example.frontend.data

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.storage.AuthSession
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.data.auth.AuthApi
import com.example.frontend.data.auth.AuthIdentityDto
import com.example.frontend.data.auth.AuthRepositoryImpl
import com.example.frontend.data.auth.ChangePasswordOtpRequest
import com.example.frontend.data.auth.ChangePasswordOtpVerifyRequest
import com.example.frontend.data.auth.ChangePasswordRequest
import com.example.frontend.data.auth.FacebookLoginRequest
import com.example.frontend.data.auth.FacebookOidcLoginRequest
import com.example.frontend.data.auth.ForgotPasswordRequest
import com.example.frontend.data.auth.GoogleLoginRequest
import com.example.frontend.data.auth.LoginRequest
import com.example.frontend.data.auth.LoginResponseDto
import com.example.frontend.data.auth.MessageResponseDto
import com.example.frontend.data.auth.PasswordOtpVerifyRequest
import com.example.frontend.data.auth.RegisterRequest
import com.example.frontend.data.auth.RegisterResponseDto
import com.example.frontend.data.auth.RegistrationOtpRequest
import com.example.frontend.data.auth.RegistrationOtpVerifyRequest
import com.example.frontend.data.auth.ResetPasswordRequest
import com.example.frontend.data.users.UpdateUserProfileRequest
import com.example.frontend.data.users.UserProfileDto
import com.example.frontend.data.users.UsersApi
import kotlinx.coroutines.runBlocking
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class AuthRepositoryGuestLoginTest {
    @Test
    fun loginAsGuest_whenEndpointMissingReportsServerVersion() = runBlocking {
        val repository = AuthRepositoryImpl(
            FakeAuthApi(guestResponse = errorResponse(404)),
            FakeUsersApi(),
            MemorySessionStore(),
            ApiErrorParser()
        )

        val result = repository.loginAsGuest()

        assertTrue(result is ApiResult.Failure)
        assertEquals(
            "Guest access is not available on this server version.",
            (result as ApiResult.Failure).message
        )
    }

    @Test
    fun loginAsGuest_whenIssuedSessionIsRejectedReportsGuestStartupFailure() = runBlocking {
        val repository = AuthRepositoryImpl(
            FakeAuthApi(guestResponse = Response.success(validLoginResponse)),
            FakeUsersApi(profileResponse = errorResponse(401)),
            MemorySessionStore(),
            ApiErrorParser()
        )

        val result = repository.loginAsGuest()

        assertTrue(result is ApiResult.Failure)
        assertEquals(
            "Guest access could not be started. Please try again.",
            (result as ApiResult.Failure).message
        )
    }

    private class MemorySessionStore : AuthSessionStore {
        private var value: AuthSession? = null
        override fun read() = value
        override fun save(session: AuthSession) { value = session }
        override fun clear() { value = null }
    }

    private class FakeUsersApi(
        private val profileResponse: Response<UserProfileDto> = Response.success(guestProfile)
    ) : UsersApi {
        override suspend fun getCurrentUser(): Response<UserProfileDto> = profileResponse
        override suspend fun updateCurrentUser(request: UpdateUserProfileRequest): Response<UserProfileDto> =
            error("not used")
        override suspend fun deleteCurrentUser(): Response<Unit> = error("not used")
    }

    private class FakeAuthApi(
        private val guestResponse: Response<LoginResponseDto>
    ) : AuthApi {
        override suspend fun login(request: LoginRequest): Response<LoginResponseDto> = error("not used")
        override suspend fun guest(): Response<LoginResponseDto> = guestResponse
        override suspend fun register(request: RegisterRequest): Response<RegisterResponseDto> = error("not used")
        override suspend fun requestRegistrationOtp(
            request: RegistrationOtpRequest
        ): Response<MessageResponseDto> = error("not used")
        override suspend fun verifyRegistrationOtp(
            request: RegistrationOtpVerifyRequest
        ): Response<MessageResponseDto> = error("not used")
        override suspend fun google(request: GoogleLoginRequest): Response<LoginResponseDto> = error("not used")
        override suspend fun facebook(request: FacebookLoginRequest): Response<LoginResponseDto> = error("not used")
        override suspend fun facebookOidc(
            request: FacebookOidcLoginRequest
        ): Response<LoginResponseDto> = error("not used")
        override suspend fun me(): Response<AuthIdentityDto> = error("not used")
        override suspend fun forgotPassword(request: ForgotPasswordRequest): Response<MessageResponseDto> =
            error("not used")
        override suspend fun verifyPasswordResetOtp(
            request: PasswordOtpVerifyRequest
        ): Response<MessageResponseDto> = error("not used")
        override suspend fun resetPassword(request: ResetPasswordRequest): Response<MessageResponseDto> =
            error("not used")
        override suspend fun requestChangePasswordOtp(
            request: ChangePasswordOtpRequest
        ): Response<MessageResponseDto> = error("not used")
        override suspend fun verifyChangePasswordOtp(
            request: ChangePasswordOtpVerifyRequest
        ): Response<MessageResponseDto> = error("not used")
        override suspend fun changePassword(request: ChangePasswordRequest): Response<MessageResponseDto> =
            error("not used")
    }

    private companion object {
        val validLoginResponse = LoginResponseDto(
            apiKey = "guest-key",
            expiresAt = "2099-01-01T00:00:00Z",
            authenticationScheme = "ApiKey",
            headerName = "X-Api-Key"
        )

        val guestProfile = UserProfileDto(
            userId = "guest-user-id",
            firstName = "Guest",
            lastName = null,
            phoneNumber = null,
            role = "Guest",
            profileImageUrl = null,
            createdAt = "2026-08-23T00:00:00Z",
            updatedAt = null
        )

        inline fun <reified T : Any> errorResponse(statusCode: Int): Response<T> =
            Response.error(
                statusCode,
                "{}".toResponseBody("application/json".toMediaType())
            )
    }
}
