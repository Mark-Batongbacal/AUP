package com.example.frontend.data.health

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.apiCall
import retrofit2.Response
import retrofit2.http.GET

data class HealthResponseDto(val status: String, val responseTimeMs: Double)

interface HealthApi {
    @GET("health") suspend fun getHealth(): Response<HealthResponseDto>
}

class HealthService(private val api: HealthApi, private val errors: ApiErrorParser) {
    suspend fun check(): ApiResult<HealthResponseDto> = apiCall(errors) { api.getHealth() }
}
