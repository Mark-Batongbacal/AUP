package com.example.frontend.auth

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST

interface AuthApi {
    @POST("api/auth/google")
    suspend fun loginWithGoogle(@Body request: GoogleLoginRequest): Response<LoginResponse>
}

data class GoogleLoginRequest(
    val idToken: String
)

data class LoginResponse(
    val apiKey: String?,
    val expiresAt: String?,
    val authenticationScheme: String?,
    val headerName: String?
)
