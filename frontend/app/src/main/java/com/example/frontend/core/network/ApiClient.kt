package com.example.frontend.core.network

import com.example.frontend.BuildConfig
import com.example.frontend.core.storage.AuthSessionStore
import com.google.gson.Gson
import com.google.gson.GsonBuilder
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

class ApiClient(
    sessionStore: AuthSessionStore,
    baseUrl: String = BuildConfig.BACKEND_BASE_URL,
    val gson: Gson = GsonBuilder().create()
) {
    private val normalizedBaseUrl = baseUrl.trim().let { configured ->
        require(configured.isNotBlank()) { "BuildConfig.BACKEND_BASE_URL must be configured." }
        if (configured.endsWith('/')) configured else "$configured/"
    }

    private val retrofit = Retrofit.Builder()
        .baseUrl(normalizedBaseUrl)
        .client(OkHttpClient.Builder().addInterceptor(AuthInterceptor(sessionStore)).build())
        .addConverterFactory(GsonConverterFactory.create(gson))
        .build()

    fun <T> create(service: Class<T>): T = retrofit.create(service)
}

