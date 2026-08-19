package com.example.frontend.core.network

import android.util.Log
import com.example.frontend.core.storage.AuthSessionStore
import kotlinx.coroutines.CancellationException
import retrofit2.Response
import java.io.IOException

suspend fun <T : Any> apiCall(
    errorParser: ApiErrorParser,
    noContentValue: T? = null,
    request: suspend () -> Response<T>
): ApiResult<T> = try {
    val response = request()
    if (response.isSuccessful) {
        val body = response.body()
        when {
            body != null -> ApiResult.Success(body)
            response.code() == 204 && noContentValue != null -> ApiResult.Success(noContentValue)
            else -> ApiResult.Failure(response.code(), "The server returned an empty response.")
        }
    } else {
        errorParser.parse(response.code(), response.errorBody()?.string())
    }
} catch (exception: CancellationException) {
    throw exception
} catch (exception: IOException) {
    Log.e("ApiCall_DIAG", "IOException during API call", exception)
    ApiResult.Failure(null, "Network error. Check your connection and try again.", cause = exception)
} catch (exception: RuntimeException) {
    ApiResult.Failure(null, "The server response could not be processed.", cause = exception)
}

suspend fun <T : Any> authenticatedApiCall(
    sessionStore: AuthSessionStore,
    errorParser: ApiErrorParser,
    noContentValue: T? = null,
    request: suspend () -> Response<T>
): ApiResult<T> {
    if (sessionStore.validSession() == null) {
        return ApiResult.Failure(401, "Authentication is required or has expired.")
    }
    val result = apiCall(errorParser, noContentValue, request)
    if (result is ApiResult.Failure && result.statusCode == 401) sessionStore.clear()
    return result
}

