package com.example.frontend.repository

import com.example.frontend.model.RecentCommute
import kotlinx.coroutines.delay

class MockCommuteRepository : CommuteRepository {
    override suspend fun getRecentCommutes(): ApiResult<List<RecentCommute>> {
        delay(500) // simulate network
        return ApiResult.Success(listOf(
            RecentCommute(id = "1", origin = "Sta. Rita", destination = "Guagua Town", legs = 3, minutes = 22),
            RecentCommute(id = "2", origin = "Dolores", destination = "SM City Clark", legs = 2, minutes = 18),
            RecentCommute(id = "3", origin = "Porac", destination = "Dau Terminal", legs = 4, minutes = 35)
        ))
    }
}
