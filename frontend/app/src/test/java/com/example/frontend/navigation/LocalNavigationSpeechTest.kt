package com.example.frontend.navigation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LocalNavigationSpeechTest {
    @Test
    fun template_usesCurrentLocalDistanceInsteadOfCachedServerNumber() {
        val rendered = LocalNavigationSpeech.renderTemplate(
            "Sige, lakad pa tayo nang {distance}. Konti na lang!",
            147.0
        )

        assertEquals("Sige, lakad pa tayo nang 150m. Konti na lang!", rendered)
    }

    @Test
    fun turnGuidance_usesLocalDistanceAndStreet() {
        val text = LocalNavigationSpeech.guidanceText(
            LocalNavigationGuidance(
                sequence = 1,
                type = "TurnRight",
                text = "Turn right onto Mabini Street.",
                streetName = "Mabini Street",
                anchorProgressMeters = 500.0,
                distanceMeters = 52.0,
                stage = LocalGuidanceStage.ADVANCE
            )
        )

        assertTrue(text.contains("Mabini Street"))
        assertTrue(text.contains("50m"))
    }
}
