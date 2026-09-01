package com.example.frontend.ui.theme

import android.content.Context

object AppearancePreferences {
    private const val PreferencesName = "tuki_appearance"
    private const val DarkModeKey = "dark_mode"

    fun isDarkMode(context: Context): Boolean =
        context.getSharedPreferences(PreferencesName, Context.MODE_PRIVATE)
            .getBoolean(DarkModeKey, false)

    fun setDarkMode(context: Context, enabled: Boolean) {
        context.getSharedPreferences(PreferencesName, Context.MODE_PRIVATE)
            .edit()
            .putBoolean(DarkModeKey, enabled)
            .apply()
    }
}
