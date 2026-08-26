package com.example.frontend.navigation

import com.example.frontend.data.navigation.NavigationSnapshotDto

enum class AlightStatusRecoveryAction {
    ALREADY_OFF,
    STILL_RIDING
}

data class AlightStatusPrompt(
    val message: String = "Did you already get off?",
    val actions: List<AlightStatusRecoveryAction> = listOf(
        AlightStatusRecoveryAction.ALREADY_OFF,
        AlightStatusRecoveryAction.STILL_RIDING
    )
)

internal fun NavigationSnapshotDto.alightStatusPrompt(): AlightStatusPrompt? =
    if (status.equals("ALIGHT_STATUS_UNKNOWN", ignoreCase = true)) {
        AlightStatusPrompt()
    } else {
        null
    }
