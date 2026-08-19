package com.example.frontend.core.network

import com.example.frontend.BuildConfig
import com.example.frontend.core.storage.AuthSessionStore
import android.util.Log
import com.google.gson.Gson
import com.google.gson.GsonBuilder
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

class ApiClient(
    sessionStore: AuthSessionStore,
    baseUrl: String = BuildConfig.BACKEND_BASE_URL,
    val gson: Gson = GsonBuilder().create()
) {
    private val normalizedBaseUrl = baseUrl.trim().let { configured ->
        require(configured.isNotBlank()) { "BuildConfig.BACKEND_BASE_URL must be configured." }
        if (configured.endsWith('/')) configured else "$configured/"
    }

    private val okHttpClient = OkHttpClient.Builder()
        .addInterceptor(AuthInterceptor(sessionStore))
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .build()

    private val retrofit = Retrofit.Builder()
        .baseUrl(normalizedBaseUrl)
        .client(okHttpClient)
        .addConverterFactory(GsonConverterFactory.create(gson))
        .build()

    fun <T> create(service: Class<T>): T = retrofit.create(service)
}

