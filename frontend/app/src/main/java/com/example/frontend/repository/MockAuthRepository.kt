package com.example.frontend.repository

import com.example.frontend.model.network.AuthResponse
import kotlinx.coroutines.delay

class MockAuthRepository : AuthRepository {
    override suspend fun login(userName: String, password: String): ApiResult<AuthResponse> {
        delay(1000)
        return if (userName.isNotBlank() && password.length >= 8) {
            ApiResult.Success(
                AuthResponse(
                    apiKey = "mock-api-key-12345",
                    expiresAt = "2026-12-31T23:59:59Z",
                    authenticationScheme = "ApiKey",
                    headerName = "X-Api-Key"
                )
            )
        } else {
            ApiResult.Error("Invalid username or password (mock error)")
        }
    }

    override suspend fun signUp(fullName: String, email: String, password: String): ApiResult<Unit> {
        delay(1000)
        return ApiResult.Success(Unit)
    }

    override suspend fun logout() {
        delay(500)
    }
}
