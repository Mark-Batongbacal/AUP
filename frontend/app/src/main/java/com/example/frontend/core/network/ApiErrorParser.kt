package com.example.frontend.core.network

import com.google.gson.Gson
import com.google.gson.JsonElement
import com.google.gson.JsonObject
import com.google.gson.JsonParser

class ApiErrorParser(private val gson: Gson = Gson()) {
    fun parse(statusCode: Int, body: String?): ApiResult.Failure {
        val fallback = defaultMessage(statusCode)
        if (body.isNullOrBlank()) return ApiResult.Failure(statusCode, fallback)

        return runCatching {
            val root = JsonParser.parseString(body)
            if (!root.isJsonObject) return@runCatching ApiResult.Failure(statusCode, fallback)
            val objectBody = root.asJsonObject
            val errors = extractErrors(objectBody.get("errors"))
            val message = sequenceOf("message", "error", "detail", "title")
                .mapNotNull { objectBody.stringOrNull(it) }
                .firstOrNull()
                ?: errors.firstOrNull()
                ?: fallback
            ApiResult.Failure(statusCode, message, errors)
        }.getOrElse { ApiResult.Failure(statusCode, fallback) }
    }

    private fun extractErrors(element: JsonElement?): List<String> = when {
        element == null || element.isJsonNull -> emptyList()
        element.isJsonPrimitive -> listOf(element.asString)
        element.isJsonArray -> element.asJsonArray.flatMap(::extractErrors)
        element.isJsonObject -> element.asJsonObject.entrySet().flatMap { extractErrors(it.value) }
        else -> emptyList()
    }

    private fun JsonObject.stringOrNull(name: String): String? =
        get(name)?.takeIf { it.isJsonPrimitive }?.asString?.takeIf { it.isNotBlank() }

    private fun defaultMessage(statusCode: Int): String = when (statusCode) {
        400 -> "The request was invalid."
        401 -> "Authentication is required or has expired."
        403 -> "You do not have permission to perform this action."
        404 -> "The requested resource was not found."
        409 -> "The request conflicts with the current state."
        500 -> "The server encountered an error."
        502, 503 -> "The service is temporarily unavailable."
        else -> "The request failed with HTTP $statusCode."
    }
}

