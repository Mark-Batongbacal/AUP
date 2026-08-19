package com.example.frontend.repository

interface AIRepository {
    suspend fun ask(message: String): ApiResult<String>
}
