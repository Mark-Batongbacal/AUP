package com.example.frontend.core.network

import com.example.frontend.core.storage.AuthSessionStore
import okhttp3.Interceptor
import okhttp3.Response

class AuthInterceptor(private val sessionStore: AuthSessionStore) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val session = sessionStore.validSession()
        val request = if (session == null) {
            chain.request()
        } else {
            chain.request().newBuilder()
                .header(session.headerName, session.apiKey)
                .build()
        }
        return chain.proceed(request)
    }
}

