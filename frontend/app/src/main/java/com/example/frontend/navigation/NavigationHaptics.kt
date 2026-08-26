package com.example.frontend.navigation

import android.content.Context
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.util.Log
import com.example.frontend.data.navigation.NavigationSnapshotDto

private const val NavigationHapticsTag = "TukiNavigationHaptics"
private const val HapticOffAmplitude = 0
private const val HapticMaxAmplitude = 255

enum class NavigationHapticEventType {
    PREPARE_TO_ALIGHT,
    ALIGHT_NOW,
    MISSED_ALIGHT,
    ALIGHT_STATUS_UNKNOWN,
    REROUTE_SUCCEEDED,
    TURN_NOW
}

data class NavigationHapticEvent(
    val key: String,
    val type: NavigationHapticEventType
)

internal data class NavigationHapticPattern(
    val timings: LongArray,
    val amplitudes: IntArray
)

fun interface NavigationHapticPerformer {
    fun perform(type: NavigationHapticEventType)
}

/** Consumes each stable navigation event identity at most once. */
class NavigationHapticEventConsumer(
    private val maximumRememberedEvents: Int = 128
) {
    private val consumedKeys = LinkedHashSet<String>()

    fun consume(
        event: NavigationHapticEvent?,
        performer: NavigationHapticPerformer
    ): Boolean {
        if (event == null || !consumedKeys.add(event.key)) return false
        while (consumedKeys.size > maximumRememberedEvents) {
            consumedKeys.remove(consumedKeys.first())
        }
        performer.perform(event.type)
        return true
    }
}

class AndroidNavigationHapticPerformer(
    private val context: Context
) : NavigationHapticPerformer {
    override fun perform(type: NavigationHapticEventType) {
        val vibrator = runCatching {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                context.getSystemService(VibratorManager::class.java)?.defaultVibrator
                    ?: run {
                        @Suppress("DEPRECATION")
                        context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
                    }
            } else {
                @Suppress("DEPRECATION")
                context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
            }
        }.onFailure { error ->
            Log.w(NavigationHapticsTag, "Unable to resolve vibrator service for $type", error)
        }.getOrNull()

        if (vibrator == null) {
            Log.w(NavigationHapticsTag, "No vibrator service available for $type")
            return
        }
        if (!vibrator.hasVibrator()) {
            Log.w(NavigationHapticsTag, "Device reports no vibrator for $type")
            return
        }

        val pattern = navigationHapticPattern(type)
        runCatching {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator.vibrate(
                    VibrationEffect.createWaveform(
                        pattern.timings,
                        pattern.amplitudes,
                        -1
                    )
                )
            } else {
                @Suppress("DEPRECATION")
                vibrator.vibrate(pattern.timings, -1)
            }
        }.onSuccess {
            Log.d(NavigationHapticsTag, "Dispatched navigation haptic: $type")
        }.onFailure { error ->
            Log.w(NavigationHapticsTag, "Navigation haptic failed: $type", error)
        }
    }
}

internal fun navigationHapticPattern(type: NavigationHapticEventType): NavigationHapticPattern =
    when (type) {
        // This is intentionally the old ALIGHT_NOW pattern: prepare should be clearly noticeable,
        // but still short enough not to feel urgent yet.
        NavigationHapticEventType.PREPARE_TO_ALIGHT -> patternOf(
            0, 180, 80, 180
        )

        // Incoming-call style alarm: two long maximum-strength pulses followed by a pause,
        // repeated for roughly ten seconds. It is intentionally bounded rather than an infinite
        // vibration so a dropped UI/network state can never leave the motor running forever.
        NavigationHapticEventType.ALIGHT_NOW -> patternOf(
            0,
            520, 180, 520, 720,
            520, 180, 520, 720,
            520, 180, 520, 720,
            520, 180, 520, 720,
            520, 180, 520, 720
        )

        NavigationHapticEventType.MISSED_ALIGHT -> patternOf(
            0, 260, 100, 260, 100, 260
        )
        NavigationHapticEventType.ALIGHT_STATUS_UNKNOWN -> patternOf(
            0, 140, 90, 140, 90, 140
        )
        NavigationHapticEventType.REROUTE_SUCCEEDED -> patternOf(
            0, 70, 60, 140
        )
        NavigationHapticEventType.TURN_NOW -> patternOf(
            0, 120
        )
    }

private fun patternOf(vararg timings: Long): NavigationHapticPattern {
    val amplitudes = IntArray(timings.size) { index ->
        if (index % 2 == 0) HapticOffAmplitude else HapticMaxAmplitude
    }
    return NavigationHapticPattern(timings, amplitudes)
}

internal fun navigationHapticEvent(
    snapshot: NavigationSnapshotDto?,
    preparingToAlight: Boolean,
    localGuidance: LocalNavigationGuidance?
): NavigationHapticEvent? {
    snapshot ?: return null
    val revision = snapshot.recommendationId ?: buildString {
        append(snapshot.currentLeg?.routeId ?: "route")
        append(':')
        append(snapshot.currentLeg?.startLatitude ?: "start")
        append(':')
        append(snapshot.currentLeg?.endLatitude ?: "end")
    }
    val base = "${snapshot.sessionId}:$revision:${snapshot.currentLegIndex}"
    val status = snapshot.status.uppercase()

    val type = when {
        status == "ALIGHT_STATUS_UNKNOWN" -> NavigationHapticEventType.ALIGHT_STATUS_UNKNOWN
        status == "MISSED_ALIGHT" -> NavigationHapticEventType.MISSED_ALIGHT
        status == "REROUTE_SUCCEEDED" -> NavigationHapticEventType.REROUTE_SUCCEEDED
        snapshot.requiresAlightingConfirmation -> NavigationHapticEventType.ALIGHT_NOW
        preparingToAlight -> NavigationHapticEventType.PREPARE_TO_ALIGHT
        localGuidance?.stage == LocalGuidanceStage.NOW &&
            (localGuidance.type.equals("TurnLeft", true) ||
                localGuidance.type.equals("TurnRight", true) ||
                localGuidance.type.equals("Roundabout", true)) -> NavigationHapticEventType.TURN_NOW
        else -> null
    } ?: return null

    val detail = if (type == NavigationHapticEventType.TURN_NOW) {
        localGuidance?.sequence?.toString() ?: "turn"
    } else {
        snapshot.landmark?.name ?: snapshot.nextInstruction?.type ?: "event"
    }
    return NavigationHapticEvent("$base:$type:$detail", type)
}
