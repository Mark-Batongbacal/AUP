package com.example.frontend.auth

import android.content.Context

interface TukiCredentialStore {
    val apiKey: String?

    fun save(loginResponse: LoginResponse)

    fun clear()
}

class SharedPreferencesTukiCredentialStore(
    context: Context
) : TukiCredentialStore {
    private val preferences = context.applicationContext.getSharedPreferences(
        "tuki_auth",
        Context.MODE_PRIVATE
    )

    override val apiKey: String?
        get() = preferences.getString(KEY_API_KEY, null)

    override fun save(loginResponse: LoginResponse) {
        preferences.edit()
            .putString(KEY_API_KEY, loginResponse.apiKey)
            .putString(KEY_EXPIRES_AT, loginResponse.expiresAt)
            .putString(KEY_AUTHENTICATION_SCHEME, loginResponse.authenticationScheme)
            .putString(KEY_HEADER_NAME, loginResponse.headerName)
            .apply()
    }

    override fun clear() {
        preferences.edit().clear().apply()
    }

    private companion object {
        const val KEY_API_KEY = "api_key"
        const val KEY_EXPIRES_AT = "expires_at"
        const val KEY_AUTHENTICATION_SCHEME = "authentication_scheme"
        const val KEY_HEADER_NAME = "header_name"
    }
}
