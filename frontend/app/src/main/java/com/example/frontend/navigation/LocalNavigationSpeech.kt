package com.example.frontend.navigation

import kotlin.math.max

object LocalNavigationSpeech {
    private const val DistanceToken = "{distance}"

    fun renderTemplate(template: String?, remainingMeters: Double?): String? {
        val value = template?.takeIf { it.isNotBlank() } ?: return null
        if (!value.contains(DistanceToken)) return value
        return value.replace(DistanceToken, formatDynamicDistance(remainingMeters))
    }

    fun guidanceText(guidance: LocalNavigationGuidance): String {
        val street = guidance.streetName?.takeIf { it.isNotBlank() }
        return when (guidance.type.lowercase()) {
            "turnleft" -> if (guidance.stage == LocalGuidanceStage.NOW) {
                street?.let { "Kaliwa tayo dito sa $it." } ?: "Kaliwa tayo dito."
            } else {
                street?.let { "Mga ${formatDynamicDistance(guidance.distanceMeters)} pa, kaliwa tayo sa $it." }
                    ?: "Mga ${formatDynamicDistance(guidance.distanceMeters)} pa, kaliwa tayo."
            }
            "turnright" -> if (guidance.stage == LocalGuidanceStage.NOW) {
                street?.let { "Kanan tayo dito sa $it." } ?: "Kanan tayo dito."
            } else {
                street?.let { "Mga ${formatDynamicDistance(guidance.distanceMeters)} pa, kanan tayo sa $it." }
                    ?: "Mga ${formatDynamicDistance(guidance.distanceMeters)} pa, kanan tayo."
            }
            "roundabout" -> if (guidance.stage == LocalGuidanceStage.NOW) {
                "Sa rotonda tayo dito — sundan natin yung planned exit."
            } else {
                "May rotonda in around ${formatDynamicDistance(guidance.distanceMeters)}."
            }
            else -> guidance.text.takeIf { it.isNotBlank() }
                ?: "Diretso lang muna tayo."
        }
    }

    fun formatDynamicDistance(distanceMeters: Double?): String {
        val safe = max(0.0, distanceMeters ?: 0.0)
        if (safe >= 1_000) return "%.1f km".format(safe / 1_000.0)
        val bucket = when {
            safe >= 500 -> 100.0
            safe >= 200 -> 50.0
            safe >= 100 -> 25.0
            else -> 10.0
        }
        val rounded = max(bucket, kotlin.math.round(safe / bucket) * bucket)
        return "${rounded.toInt()}m"
    }
}
