package com.example.frontend.data.contributions

import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.network.ApiResult
import com.example.frontend.core.network.authenticatedApiCall
import com.example.frontend.core.storage.AuthSessionStore
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.RequestBody.Companion.toRequestBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Multipart
import retrofit2.http.POST
import retrofit2.http.Part

data class TricycleProofUploadResponse(
    val proofImageUrl: String
)

data class CreateTricyclePointSubmissionRequest(
    val proofImageUrl: String,
    val latitude: Double,
    val longitude: Double,
    val accuracyMeters: Double? = null,
    val locationCapturedAt: String,
    val suggestedTodaName: String? = null,
    val suggestedLandmark: String? = null
)

data class TricyclePointSubmissionDto(
    val tricyclePointSubmissionId: Long,
    val proofImageUrl: String,
    val latitude: Double,
    val longitude: Double,
    val accuracyMeters: Double?,
    val locationCapturedAt: String,
    val suggestedTodaName: String?,
    val suggestedLandmark: String?,
    val status: String,
    val createdAt: String,
    val updatedAt: String,
    val reviewedAt: String?,
    val publishedTricyclePointId: Long?
)

interface TricycleSubmissionsApi {
    @Multipart
    @POST("api/tricycle-point-submissions/proof")
    suspend fun uploadProof(@Part image: MultipartBody.Part): Response<TricycleProofUploadResponse>

    @POST("api/tricycle-point-submissions")
    suspend fun createSubmission(
        @Body request: CreateTricyclePointSubmissionRequest
    ): Response<TricyclePointSubmissionDto>

    @GET("api/tricycle-point-submissions/me")
    suspend fun getMine(): Response<List<TricyclePointSubmissionDto>>
}

interface TricycleSubmissionRepository {
    suspend fun uploadProof(
        imageBytes: ByteArray,
        contentType: String,
        fileName: String
    ): ApiResult<TricycleProofUploadResponse>

    suspend fun createSubmission(
        request: CreateTricyclePointSubmissionRequest
    ): ApiResult<TricyclePointSubmissionDto>

    suspend fun getMine(): ApiResult<List<TricyclePointSubmissionDto>>
}

class TricycleSubmissionRepositoryImpl(
    private val api: TricycleSubmissionsApi,
    private val sessionStore: AuthSessionStore,
    private val errors: ApiErrorParser
) : TricycleSubmissionRepository {
    override suspend fun uploadProof(
        imageBytes: ByteArray,
        contentType: String,
        fileName: String
    ): ApiResult<TricycleProofUploadResponse> {
        val safeType = contentType.takeIf {
            it == "image/jpeg" || it == "image/png" || it == "image/webp"
        } ?: "image/jpeg"
        val safeName = fileName.substringAfterLast('/').substringAfterLast('\\').ifBlank { "proof.jpg" }
        val body = imageBytes.toRequestBody(safeType.toMediaType())
        val part = MultipartBody.Part.createFormData("image", safeName, body)
        return authenticatedApiCall(sessionStore, errors) { api.uploadProof(part) }
    }

    override suspend fun createSubmission(
        request: CreateTricyclePointSubmissionRequest
    ): ApiResult<TricyclePointSubmissionDto> =
        authenticatedApiCall(sessionStore, errors) { api.createSubmission(request) }

    override suspend fun getMine(): ApiResult<List<TricyclePointSubmissionDto>> =
        authenticatedApiCall(sessionStore, errors) { api.getMine() }
}
