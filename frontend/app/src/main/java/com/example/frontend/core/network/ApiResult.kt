package com.example.frontend.core.network

sealed interface ApiResult<out T> {
    data class Success<T>(val data: T) : ApiResult<T>

    data class Failure(
        val statusCode: Int?,
        val message: String,
        val errors: List<String> = emptyList(),
        val cause: Throwable? = null
    ) : ApiResult<Nothing> {
        val isUnauthorized: Boolean get() = statusCode == 401
    }
}

