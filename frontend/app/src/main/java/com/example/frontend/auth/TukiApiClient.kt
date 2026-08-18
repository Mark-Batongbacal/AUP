package com.example.frontend.auth

import com.example.frontend.BuildConfig
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

object TukiApiClient {
    fun createAuthApi(): AuthApi = createRetrofit().create(AuthApi::class.java)

    fun createAuthenticatedRetrofit(credentialStore: TukiCredentialStore): Retrofit {
        val client = OkHttpClient.Builder()
            .addInterceptor(ApiKeyInterceptor(credentialStore))
            .build()

        return createRetrofit(client)
    }

    private fun createRetrofit(client: OkHttpClient? = null): Retrofit {
        val builder = Retrofit.Builder()
            .baseUrl(normalizedBaseUrl())
            .addConverterFactory(GsonConverterFactory.create())

        if (client != null) {
            builder.client(client)
        }

        return builder.build()
    }

    private fun normalizedBaseUrl(): String {
        val configuredUrl = BuildConfig.BACKEND_BASE_URL.trim()
        val baseUrl = configuredUrl.ifBlank { "https://aup-0mjy.onrender.com/" }
        return if (baseUrl.endsWith("/")) baseUrl else "$baseUrl/"
    }
}

private class ApiKeyInterceptor(
    private val credentialStore: TukiCredentialStore
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): okhttp3.Response {
        val apiKey = credentialStore.apiKey
        val request = if (apiKey.isNullOrBlank()) {
            chain.request()
        } else {
            chain.request()
                .newBuilder()
                .header("X-API-Key", apiKey)
                .build()
        }

        return chain.proceed(request)
    }
}
