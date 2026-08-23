package com.example.frontend.core.localization

import android.content.Context
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue

object AppLanguagePreference {
    private const val PreferencesName = "tuki_language_preferences"
    private const val LanguageKey = "preferred_language"

    var currentLanguage by mutableStateOf("English")
        private set

    fun initialize(context: Context) {
        currentLanguage = normalize(
            context.applicationContext
                .getSharedPreferences(PreferencesName, Context.MODE_PRIVATE)
                .getString(LanguageKey, null)
        )
    }

    fun update(context: Context, language: String?) {
        val normalized = normalize(language)
        currentLanguage = normalized
        context.applicationContext
            .getSharedPreferences(PreferencesName, Context.MODE_PRIVATE)
            .edit()
            .putString(LanguageKey, normalized)
            .apply()
    }

    fun current(): String = currentLanguage

    fun isFilipino(language: String? = currentLanguage): Boolean =
        normalize(language) == "Filipino"

    private fun normalize(language: String?): String {
        val value = language?.trim()?.lowercase()
        return when {
            value == "filipino" || value == "tagalog" || value?.startsWith("fil-") == true -> "Filipino"
            else -> "English"
        }
    }
}
