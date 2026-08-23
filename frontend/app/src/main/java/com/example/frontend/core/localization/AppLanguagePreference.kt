package com.example.frontend.core.localization

import android.content.Context

object AppLanguagePreference {
    private const val PreferencesName = "tuki_language_preferences"
    private const val LanguageKey = "preferred_language"

    @Volatile
    private var cachedLanguage: String = "English"

    fun initialize(context: Context) {
        cachedLanguage = normalize(
            context.applicationContext
                .getSharedPreferences(PreferencesName, Context.MODE_PRIVATE)
                .getString(LanguageKey, null)
        )
    }

    fun update(context: Context, language: String?) {
        val normalized = normalize(language)
        cachedLanguage = normalized
        context.applicationContext
            .getSharedPreferences(PreferencesName, Context.MODE_PRIVATE)
            .edit()
            .putString(LanguageKey, normalized)
            .apply()
    }

    fun current(): String = cachedLanguage

    fun isFilipino(language: String? = cachedLanguage): Boolean =
        normalize(language) == "Filipino"

    private fun normalize(language: String?): String {
        val value = language?.trim()?.lowercase()
        return when {
            value == "filipino" || value == "tagalog" || value?.startsWith("fil-") == true -> "Filipino"
            else -> "English"
        }
    }
}
