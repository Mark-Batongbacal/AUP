package com.example.frontend.navigation

import android.content.Context
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import com.example.frontend.data.navigation.NavigationSnapshotDto

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
            } else {
                @Suppress("DEPRECATION")
                context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
            }
        }.getOrNull() ?: return
        if (!vibrator.hasVibrator()) return

        val timings = when (type) {
            NavigationHapticEventType.PREPARE_TO_ALIGHT -> longArrayOf(0, 90, 90, 90)
            NavigationHapticEventType.ALIGHT_NOW -> longArrayOf(0, 180, 80, 180)
            NavigationHapticEventType.MISSED_ALIGHT -> longArrayOf(0, 260, 100, 260, 100, 260)
            NavigationHapticEventType.ALIGHT_STATUS_UNKNOWN -> longArrayOf(0, 140, 90, 140, 90, 140)
            NavigationHapticEventType.REROUTE_SUCCEEDED -> longArrayOf(0, 70, 60, 140)
            NavigationHapticEventType.TURN_NOW -> longArrayOf(0, 120)
        }
        runCatching {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator.vibrate(VibrationEffect.createWaveform(timings, -1))
            } else {
                @Suppress("DEPRECATION")
                vibrator.vibrate(timings, -1)
            }
        }
    }
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

