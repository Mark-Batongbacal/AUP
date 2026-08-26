package com.example.frontend.navigation

import com.example.frontend.data.navigation.NavigationSnapshotDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class NavigationHapticsTest {
    @Test
    fun sameStableEvent_vibratesOnlyOnce() {
        val consumer = NavigationHapticEventConsumer()
        val calls = mutableListOf<NavigationHapticEventType>()
        val performer = NavigationHapticPerformer { calls += it }
        val event = NavigationHapticEvent(
            "session:revision:0:prepare",
            NavigationHapticEventType.PREPARE_TO_ALIGHT
        )

        assertTrue(consumer.consume(event, performer))
        assertFalse(consumer.consume(event, performer))
        assertEquals(listOf(NavigationHapticEventType.PREPARE_TO_ALIGHT), calls)
    }

    @Test
    fun newEventIdentity_canVibrateAgain() {
        val consumer = NavigationHapticEventConsumer()
        val calls = mutableListOf<NavigationHapticEventType>()
        val performer = NavigationHapticPerformer { calls += it }

        consumer.consume(
            NavigationHapticEvent("prepare", NavigationHapticEventType.PREPARE_TO_ALIGHT),
            performer
        )
        consumer.consume(
            NavigationHapticEvent("alight-now", NavigationHapticEventType.ALIGHT_NOW),
            performer
        )

        assertEquals(2, calls.size)
    }

    @Test
    fun prepareToAlight_isAutomaticWhenLocalApproachBecomesTrue() {
        val event = navigationHapticEvent(
            snapshot = snapshot("ON_ROUTE"),
            preparingToAlight = true,
            localGuidance = null
        )

        assertNotNull(event)
        assertEquals(NavigationHapticEventType.PREPARE_TO_ALIGHT, event?.type)
    }

    @Test
    fun alightConfirmation_hasPriorityOverPrepareHaptic() {
        val event = navigationHapticEvent(
            snapshot = snapshot("ApproachingAlightPoint", requiresAlightingConfirmation = true),
            preparingToAlight = true,
            localGuidance = null
        )

        assertNotNull(event)
        assertEquals(NavigationHapticEventType.ALIGHT_NOW, event?.type)
    }

    @Test
    fun ordinaryGpsState_doesNotCreateHapticEvent() {
        assertNull(navigationHapticEvent(snapshot("ON_ROUTE"), false, null))
    }

    @Test
    fun unknownAlight_exposesTwoExplicitActionsAndHapticEvent() {
        val snapshot = snapshot("ALIGHT_STATUS_UNKNOWN")

        val prompt = snapshot.alightStatusPrompt()

        assertNotNull(prompt)
        assertEquals(
            listOf(
                AlightStatusRecoveryAction.ALREADY_OFF,
                AlightStatusRecoveryAction.STILL_RIDING
            ),
            prompt?.actions
        )
        assertEquals(
            NavigationHapticEventType.ALIGHT_STATUS_UNKNOWN,
            navigationHapticEvent(snapshot, false, null)?.type
        )
    }

    private fun snapshot(
        status: String,
        requiresAlightingConfirmation: Boolean = false
    ) = NavigationSnapshotDto(
        sessionId = "session-1",
        state = "OnJeepney",
        currentLegIndex = 0,
        currentLeg = null,
        nextInstruction = null,
        spokenInstruction = null,
        remainingDistanceMeters = null,
        progressMeters = 0.0,
        boardInfo = null,
        alightInfo = null,
        landmark = null,
        requiresBoardingConfirmation = false,
        requiresAlightingConfirmation = requiresAlightingConfirmation,
        rerouteRequired = false,
        status = status,
        triggeredEvents = emptyList(),
        recommendationId = "revision-1"
    )
}
