package com.example.frontend.auth

import kotlinx.coroutines.runBlocking
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class AuthRepositoryTest {
    @Test
    fun loginWithGoogle_whenBackendSucceeds_savesReturnedTukiCredential() = runBlocking {
        val store = FakeCredentialStore()
        val repository = AuthRepository(
            authApi = FakeAuthApi(Response.success(successfulLoginResponse)),
            credentialStore = store
        )

        val result = repository.loginWithGoogle("google-id-token")

        assertTrue(result is AuthResult.Success)
        assertEquals("TUKI_API_KEY", store.savedResponse?.apiKey)
        assertEquals("ApiKey", store.savedResponse?.authenticationScheme)
        assertEquals("X-Api-Key", store.savedResponse?.headerName)
    }

    @Test
    fun loginWithGoogle_whenBackendRejectsToken_doesNotSaveCredential() = runBlocking {
        val store = FakeCredentialStore()
        val repository = AuthRepository(
            authApi = FakeAuthApi(Response.error(401, "".toResponseBody(null))),
            credentialStore = store
        )

        val result = repository.loginWithGoogle("google-id-token")

        assertTrue(result is AuthResult.Failure)
        assertEquals("Google login was rejected. Try again.", (result as AuthResult.Failure).message)
        assertNull(store.savedResponse)
    }

    @Test
    fun loginWithGoogle_whenNetworkFails_returnsUserFacingNetworkError() = runBlocking {
        val store = FakeCredentialStore()
        val repository = AuthRepository(
            authApi = FakeAuthApi(failure = IOException()),
            credentialStore = store
        )

        val result = repository.loginWithGoogle("google-id-token")

        assertTrue(result is AuthResult.Failure)
        assertEquals(
            "Network error. Check your connection and try again.",
            (result as AuthResult.Failure).message
        )
        assertNull(store.savedResponse)
    }

    @Test
    fun loginWithFacebook_whenBackendSucceeds_savesReturnedTukiCredential() = runBlocking {
        val store = FakeCredentialStore()
        val api = FakeAuthApi(facebookResponse = Response.success(successfulLoginResponse))
        val repository = AuthRepository(
            authApi = api,
            credentialStore = store
        )

        val result = repository.loginWithFacebook("facebook-access-token")

        assertTrue(result is AuthResult.Success)
        assertEquals("facebook-access-token", api.lastFacebookRequest?.accessToken)
        assertEquals("TUKI_API_KEY", store.savedResponse?.apiKey)
        assertEquals("ApiKey", store.savedResponse?.authenticationScheme)
        assertEquals("X-Api-Key", store.savedResponse?.headerName)
    }

    @Test
    fun loginWithFacebook_whenBackendRejectsToken_doesNotSaveCredential() = runBlocking {
        val store = FakeCredentialStore()
        val repository = AuthRepository(
            authApi = FakeAuthApi(facebookResponse = Response.error(401, "".toResponseBody(null))),
            credentialStore = store
        )

        val result = repository.loginWithFacebook("facebook-access-token")

        assertTrue(result is AuthResult.Failure)
        assertEquals("Facebook login was rejected. Try again.", (result as AuthResult.Failure).message)
        assertNull(store.savedResponse)
    }

    @Test
    fun loginWithFacebook_whenNetworkFails_returnsUserFacingNetworkError() = runBlocking {
        val store = FakeCredentialStore()
        val repository = AuthRepository(
            authApi = FakeAuthApi(facebookFailure = IOException()),
            credentialStore = store
        )

        val result = repository.loginWithFacebook("facebook-access-token")

        assertTrue(result is AuthResult.Failure)
        assertEquals(
            "Network error. Check your connection and try again.",
            (result as AuthResult.Failure).message
        )
        assertNull(store.savedResponse)
    }

    @Test
    fun loginWithFacebook_whenBackendResponseIsMalformed_doesNotSaveCredential() = runBlocking {
        val store = FakeCredentialStore()
        val repository = AuthRepository(
            authApi = FakeAuthApi(
                facebookResponse = Response.success(
                    successfulLoginResponse.copy(apiKey = null)
                )
            ),
            credentialStore = store
        )

        val result = repository.loginWithFacebook("facebook-access-token")

        assertTrue(result is AuthResult.Failure)
        assertEquals(
            "The server returned an invalid login response.",
            (result as AuthResult.Failure).message
        )
        assertNull(store.savedResponse)
    }

    private class FakeAuthApi(
        private val response: Response<LoginResponse>? = null,
        private val failure: IOException? = null,
        private val facebookResponse: Response<LoginResponse>? = null,
        private val facebookFailure: IOException? = null
    ) : AuthApi {
        var lastFacebookRequest: FacebookLoginRequest? = null

        override suspend fun loginWithGoogle(request: GoogleLoginRequest): Response<LoginResponse> {
            failure?.let { throw it }
            return checkNotNull(response)
        }

        override suspend fun loginWithFacebook(
            request: FacebookLoginRequest
        ): Response<LoginResponse> {
            lastFacebookRequest = request
            facebookFailure?.let { throw it }
            return checkNotNull(facebookResponse)
        }
    }

    private class FakeCredentialStore : TukiCredentialStore {
        var savedResponse: LoginResponse? = null

        override val apiKey: String?
            get() = savedResponse?.apiKey

        override fun save(loginResponse: LoginResponse) {
            savedResponse = loginResponse
        }

        override fun clear() {
            savedResponse = null
        }
    }

    private companion object {
        val successfulLoginResponse = LoginResponse(
            apiKey = "TUKI_API_KEY",
            expiresAt = "2026-08-18T12:00:00Z",
            authenticationScheme = "ApiKey",
            headerName = "X-Api-Key"
        )
    }
}
