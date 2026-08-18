package com.example.frontend.repository

import com.example.frontend.model.network.AuthResponse

interface AuthRepository {
    suspend fun login(userName: String, password: String): ApiResult<AuthResponse>
    suspend fun signUp(fullName: String, email: String, password: String): ApiResult<Unit>
    suspend fun logout()
}
