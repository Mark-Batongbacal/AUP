package com.example.frontend.core.storage

import android.content.Context
import java.time.Instant

data class AuthSession(
    val apiKey: String,
    val expiresAt: String,
    val authenticationScheme: String,
    val headerName: String
) {
    fun isExpired(now: Instant = Instant.now()): Boolean =
        runCatching { !Instant.parse(expiresAt).isAfter(now) }.getOrDefault(true)
}

interface AuthSessionStore {
    fun read(): AuthSession?
    fun save(session: AuthSession)
    fun clear()

    fun validSession(now: Instant = Instant.now()): AuthSession? {
        val session = read() ?: return null
        if (session.apiKey.isBlank() || session.headerName.isBlank() || session.isExpired(now)) {
            clear()
            return null
        }
        return session
    }
}

class SharedPreferencesAuthSessionStore(context: Context) : AuthSessionStore {
    private val preferences = context.applicationContext.getSharedPreferences(
        "tuki_auth",
        Context.MODE_PRIVATE
    )

    override fun read(): AuthSession? {
        val apiKey = preferences.getString(KEY_API_KEY, null) ?: return null
        val expiresAt = preferences.getString(KEY_EXPIRES_AT, null) ?: return null
        return AuthSession(
            apiKey = apiKey,
            expiresAt = expiresAt,
            authenticationScheme = preferences.getString(KEY_SCHEME, DEFAULT_SCHEME) ?: DEFAULT_SCHEME,
            headerName = preferences.getString(KEY_HEADER, DEFAULT_HEADER) ?: DEFAULT_HEADER
        )
    }

    override fun save(session: AuthSession) {
        preferences.edit()
            .putString(KEY_API_KEY, session.apiKey)
            .putString(KEY_EXPIRES_AT, session.expiresAt)
            .putString(KEY_SCHEME, session.authenticationScheme)
            .putString(KEY_HEADER, session.headerName)
            .apply()
    }

    override fun clear() = preferences.edit().clear().apply()

    private companion object {
        const val KEY_API_KEY = "api_key"
        const val KEY_EXPIRES_AT = "expires_at"
        const val KEY_SCHEME = "authentication_scheme"
        const val KEY_HEADER = "header_name"
        const val DEFAULT_SCHEME = "ApiKey"
        const val DEFAULT_HEADER = "X-Api-Key"
    }
}

