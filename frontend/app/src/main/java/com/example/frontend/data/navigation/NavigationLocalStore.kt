package com.example.frontend.data.navigation

import android.content.Context
import com.example.frontend.core.storage.AuthSessionStore
import com.google.gson.Gson
import java.security.MessageDigest

interface NavigationLocalStore {
    fun readActiveSnapshot(): NavigationSnapshotDto?
    fun saveActiveSnapshot(snapshot: NavigationSnapshotDto)
    fun clearActiveSnapshot(sessionId: String? = null)
    fun readGeometry(cacheKey: String): NavigationGeometryResponseDto?
    fun saveGeometry(cacheKey: String, response: NavigationGeometryResponseDto)
    fun clearAll()
}

object NoOpNavigationLocalStore : NavigationLocalStore {
    override fun readActiveSnapshot(): NavigationSnapshotDto? = null
    override fun saveActiveSnapshot(snapshot: NavigationSnapshotDto) = Unit
    override fun clearActiveSnapshot(sessionId: String?) = Unit
    override fun readGeometry(cacheKey: String): NavigationGeometryResponseDto? = null
    override fun saveGeometry(cacheKey: String, response: NavigationGeometryResponseDto) = Unit
    override fun clearAll() = Unit
}

class SharedPreferencesNavigationLocalStore(
    context: Context,
    private val sessions: AuthSessionStore,
    private val gson: Gson
) : NavigationLocalStore {
    private val preferences = context.applicationContext.getSharedPreferences(
        "tuki_navigation_local",
        Context.MODE_PRIVATE
    )

    override fun readActiveSnapshot(): NavigationSnapshotDto? {
        val owner = preferences.getString(KEY_ACTIVE_OWNER, null) ?: return null
        val currentOwner = currentOwnerFingerprint()
        if (currentOwner == null || currentOwner != owner) {
            clearActiveSnapshot()
            return null
        }

        val json = preferences.getString(KEY_ACTIVE_SNAPSHOT, null) ?: return null
        val snapshot = runCatching {
            gson.fromJson(json, NavigationSnapshotDto::class.java)
        }.getOrNull()

        if (snapshot == null || !snapshot.isLocallyActive()) {
            clearActiveSnapshot()
            return null
        }
        return snapshot
    }

    override fun saveActiveSnapshot(snapshot: NavigationSnapshotDto) {
        if (!snapshot.isLocallyActive()) {
            clearActiveSnapshot(snapshot.sessionId)
            return
        }
        val owner = currentOwnerFingerprint() ?: return
        preferences.edit()
            .putString(KEY_ACTIVE_OWNER, owner)
            .putString(KEY_ACTIVE_SNAPSHOT, gson.toJson(snapshot))
            .apply()
    }

    override fun clearActiveSnapshot(sessionId: String?) {
        if (sessionId != null) {
            val existing = preferences.getString(KEY_ACTIVE_SNAPSHOT, null)
                ?.let { json ->
                    runCatching { gson.fromJson(json, NavigationSnapshotDto::class.java) }.getOrNull()
                }
            if (existing != null && existing.sessionId != sessionId) return
        }
        preferences.edit()
            .remove(KEY_ACTIVE_OWNER)
            .remove(KEY_ACTIVE_SNAPSHOT)
            .apply()
    }

    override fun readGeometry(cacheKey: String): NavigationGeometryResponseDto? {
        val json = preferences.getString(geometryPreferenceKey(cacheKey), null) ?: return null
        return runCatching {
            gson.fromJson(json, NavigationGeometryResponseDto::class.java)
        }.getOrNull()?.takeIf { it.points.size >= 2 }
    }

    override fun saveGeometry(cacheKey: String, response: NavigationGeometryResponseDto) {
        if (response.points.size < 2) return
        val preferenceKey = geometryPreferenceKey(cacheKey)
        val keys = geometryKeys().toMutableList().apply {
            remove(preferenceKey)
            add(preferenceKey)
        }
        val editor = preferences.edit().putString(preferenceKey, gson.toJson(response))
        while (keys.size > MAX_GEOMETRIES) {
            editor.remove(keys.removeAt(0))
        }
        editor.putString(KEY_GEOMETRY_INDEX, gson.toJson(keys.toTypedArray())).apply()
    }

    override fun clearAll() = preferences.edit().clear().apply()

    private fun geometryKeys(): List<String> {
        val json = preferences.getString(KEY_GEOMETRY_INDEX, null) ?: return emptyList()
        return runCatching { gson.fromJson(json, Array<String>::class.java).toList() }
            .getOrDefault(emptyList())
    }

    private fun currentOwnerFingerprint(): String? =
        sessions.validSession()?.apiKey?.takeIf { it.isNotBlank() }?.let(::sha256)

    private fun geometryPreferenceKey(cacheKey: String): String =
        "$GEOMETRY_PREFIX${sha256(cacheKey)}"

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(Charsets.UTF_8))
        .joinToString("") { byte -> "%02x".format(byte.toInt() and 0xff) }

    private companion object {
        const val KEY_ACTIVE_OWNER = "active_owner"
        const val KEY_ACTIVE_SNAPSHOT = "active_snapshot"
        const val KEY_GEOMETRY_INDEX = "geometry_index"
        const val GEOMETRY_PREFIX = "geometry_"
        const val MAX_GEOMETRIES = 12
    }
}

private fun NavigationSnapshotDto.isLocallyActive(): Boolean =
    !state.equals("Arrived", ignoreCase = true) &&
        !state.equals("Cancelled", ignoreCase = true)
