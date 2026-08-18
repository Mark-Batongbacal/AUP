package com.example.frontend.repository

import com.example.frontend.model.RecentCommute

interface CommuteRepository {
    suspend fun getRecentCommutes(): ApiResult<List<RecentCommute>>
}
