package com.example.frontend.repository

import kotlinx.coroutines.delay

class MockAIRepository : AIRepository {
    override suspend fun ask(message: String): ApiResult<String> {
        delay(1500)
        return ApiResult.Success("Tuki Bot: I understand you want to go to '$message'. I can help you find the best jeepney route!")
    }
}
