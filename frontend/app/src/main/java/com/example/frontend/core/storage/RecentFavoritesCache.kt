package com.example.frontend.core.storage

import android.content.Context
import com.example.frontend.model.FavoriteRoute
import com.example.frontend.model.RecentCommute
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import java.lang.reflect.Type
import java.security.MessageDigest

/**
 * Small, user-scoped disk cache for the Recent and Favorites screens.
 *
 * The current API-key session is fingerprinted before it becomes part of a
 * SharedPreferences key, so cached journey data from one signed-in session is
 * never shown to another session and the API key itself is not persisted here.
 */
class RecentFavoritesCache(
    context: Context,
    private val sessions: AuthSessionStore,
    private val gson: Gson = Gson()
) {
    private val preferences = context.applicationContext.getSharedPreferences(
        PREFERENCES_NAME,
        Context.MODE_PRIVATE
    )

    fun readRecents(): List<RecentCommute> = readList(
        prefix = KEY_RECENTS,
        type = object : TypeToken<List<RecentCommute>>() {}.type
    )

    fun writeRecents(items: List<RecentCommute>) {
        writeList(KEY_RECENTS, items)
    }

    fun readFavorites(): List<FavoriteRoute> = readList(
        prefix = KEY_FAVORITES,
        type = object : TypeToken<List<FavoriteRoute>>() {}.type
    )

    fun writeFavorites(items: List<FavoriteRoute>) {
        writeList(KEY_FAVORITES, items)
    }

    fun clearCurrentSession() {
        val recentsKey = namespacedKey(KEY_RECENTS) ?: return
        val favoritesKey = namespacedKey(KEY_FAVORITES) ?: return
        preferences.edit()
            .remove(recentsKey)
            .remove(favoritesKey)
            .apply()
    }

    private fun <T> readList(prefix: String, type: Type): List<T> {
        val key = namespacedKey(prefix) ?: return emptyList()
        val json = preferences.getString(key, null) ?: return emptyList()
        return runCatching {
            gson.fromJson<List<T>>(json, type).orEmpty()
        }.getOrDefault(emptyList())
    }

    private fun writeList(prefix: String, items: List<*>) {
        val key = namespacedKey(prefix) ?: return
        runCatching { gson.toJson(items) }
            .onSuccess { json -> preferences.edit().putString(key, json).apply() }
    }

    private fun namespacedKey(prefix: String): String? {
        val apiKey = sessions.validSession()?.apiKey ?: return null
        val digest = MessageDigest.getInstance("SHA-256")
            .digest(apiKey.toByteArray(Charsets.UTF_8))
            .take(12)
            .joinToString(separator = "") { byte -> "%02x".format(byte.toInt() and 0xff) }
        return "$prefix:$digest"
    }

    private companion object {
        const val PREFERENCES_NAME = "tuki_recent_favorites_cache"
        const val KEY_RECENTS = "recents"
        const val KEY_FAVORITES = "favorites"
    }
}
