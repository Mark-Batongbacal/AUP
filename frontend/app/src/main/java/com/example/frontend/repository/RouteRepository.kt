package com.example.frontend.repository

import com.example.frontend.model.RouteOption

interface RouteRepository {
    suspend fun getRoutes(origin: String, destination: String): ApiResult<List<RouteOption>>
}
