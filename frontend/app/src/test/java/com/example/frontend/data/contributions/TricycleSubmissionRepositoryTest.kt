package com.example.frontend.data.contributions

import com.example.frontend.core.network.ApiClient
import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.storage.AuthSession
import com.example.frontend.core.storage.AuthSessionStore
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class TricycleSubmissionRepositoryTest {
    @Test
    fun uploadProof_sendsAuthenticatedMultipartImage() = runBlocking {
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setHeader("Content-Type", "application/json")
                .setBody("""{"proofImageUrl":"/api/tricycle-point-submissions/proof/owned.jpg"}""")
        )
        server.start()

        try {
            val repository = repository(server)
            val result = repository.uploadProof(
                imageBytes = byteArrayOf(0xFF.toByte(), 0xD8.toByte(), 0xFF.toByte(), 0x01),
                contentType = "image/jpeg",
                fileName = "proof.jpg"
            )

            assertTrue(result is ApiResult.Success)
            val success = result as ApiResult.Success<TricycleProofUploadResponse>
            assertEquals(
                "/api/tricycle-point-submissions/proof/owned.jpg",
                success.data.proofImageUrl
            )

            val request = server.takeRequest()
            assertEquals("POST", request.method)
            assertEquals("/api/tricycle-point-submissions/proof", request.path)
            assertEquals("secret-key", request.getHeader("X-Api-Key"))
            val body = request.body.readUtf8()
            assertTrue(body.contains("name=\"image\""))
            assertTrue(body.contains("filename=\"proof.jpg\""))
            assertTrue(body.contains("image/jpeg"))
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun getMine_parsesSubmissionStatusAndOptionalHints() = runBlocking {
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setHeader("Content-Type", "application/json")
                .setBody(
                    """[{"tricyclePointSubmissionId":12,"proofImageUrl":"/api/tricycle-point-submissions/proof/proof.jpg","latitude":15.1,"longitude":120.5,"accuracyMeters":7.5,"locationCapturedAt":"2026-08-26T10:00:00Z","suggestedTodaName":"Dau TODA","suggestedLandmark":"Near market","status":"Pending","createdAt":"2026-08-26T10:01:00Z","updatedAt":"2026-08-26T10:01:00Z","reviewedAt":null,"publishedTricyclePointId":null}]"""
                )
        )
        server.start()

        try {
            val result = repository(server).getMine()
            assertTrue(result is ApiResult.Success)
            val success = result as ApiResult.Success<List<TricyclePointSubmissionDto>>
            val item = success.data.single()
            assertEquals(12L, item.tricyclePointSubmissionId)
            assertEquals("Dau TODA", item.suggestedTodaName)
            assertEquals("Near market", item.suggestedLandmark)
            assertEquals("Pending", item.status)

            val request = server.takeRequest()
            assertEquals("GET", request.method)
            assertEquals("/api/tricycle-point-submissions/me", request.path)
            assertEquals("secret-key", request.getHeader("X-Api-Key"))
        } finally {
            server.shutdown()
        }
    }

    private fun repository(server: MockWebServer): TricycleSubmissionRepository {
        val store = MemorySessionStore(
            AuthSession(
                apiKey = "secret-key",
                expiresAt = "2099-01-01T00:00:00Z",
                authenticationScheme = "ApiKey",
                headerName = "X-Api-Key"
            )
        )
        val client = ApiClient(store, server.url("/").toString())
        return TricycleSubmissionRepositoryImpl(
            client.create(TricycleSubmissionsApi::class.java),
            store,
            ApiErrorParser(client.gson)
        )
    }

    private class MemorySessionStore(initial: AuthSession?) : AuthSessionStore {
        private var value = initial
        override fun read(): AuthSession? = value
        override fun save(session: AuthSession) { value = session }
        override fun clear() { value = null }
    }
}
