package com.example.frontend.auth

import kotlinx.coroutines.CancellationException
import java.io.IOException

class AuthRepository(
    private val authApi: AuthApi,
    private val credentialStore: TukiCredentialStore
) {
    suspend fun loginWithGoogle(idToken: String): AuthResult {
        return try {
            val response = authApi.loginWithGoogle(GoogleLoginRequest(idToken))
            handleSocialLoginResponse(
                response = response,
                rejectedMessage = "Google login was rejected. Try again.",
                unavailableMessage = "Google login is unavailable. Try again later."
            )
        } catch (_: IOException) {
            AuthResult.Failure("Network error. Check your connection and try again.")
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: RuntimeException) {
            AuthResult.Failure("Google login failed. Try again.")
        }
    }

    suspend fun loginWithFacebook(accessToken: String): AuthResult {
        return try {
            val response = authApi.loginWithFacebook(FacebookLoginRequest(accessToken))
            handleSocialLoginResponse(
                response = response,
                rejectedMessage = "Facebook login was rejected. Try again.",
                unavailableMessage = "Facebook login is unavailable. Try again later."
            )
        } catch (_: IOException) {
            AuthResult.Failure("Network error. Check your connection and try again.")
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: RuntimeException) {
            AuthResult.Failure("Facebook login failed. Try again.")
        }
    }

    private fun handleSocialLoginResponse(
        response: retrofit2.Response<LoginResponse>,
        rejectedMessage: String,
        unavailableMessage: String
    ): AuthResult {
        return if (response.isSuccessful) {
            val body = response.body()
            if (body == null || body.apiKey.isNullOrBlank()) {
                AuthResult.Failure("The server returned an invalid login response.")
            } else {
                credentialStore.save(body)
                AuthResult.Success
            }
        } else if (response.code() == 401) {
            AuthResult.Failure(rejectedMessage)
        } else {
            AuthResult.Failure(unavailableMessage)
        }
    }
}

sealed interface AuthResult {
    data object Success : AuthResult
    data class Failure(val message: String) : AuthResult
}
