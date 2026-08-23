package com.example.frontend.navigation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LocalNavigationSpeechTest {
    @Test
    fun template_usesCurrentLocalDistanceInsteadOfCachedServerNumber() {
        val rendered = LocalNavigationSpeech.renderTemplate(
            "Keep walking for {distance}. Almost there!",
            147.0
        )

        assertEquals("Keep walking for 150m. Almost there!", rendered)
    }

    @Test
    fun englishTurnGuidance_usesLocalDistanceAndStreet() {
        val text = LocalNavigationSpeech.guidanceText(
            LocalNavigationGuidance(
                sequence = 1,
                type = "TurnRight",
                text = "Turn right onto Mabini Street.",
                streetName = "Mabini Street",
                anchorProgressMeters = 500.0,
                distanceMeters = 52.0,
                stage = LocalGuidanceStage.ADVANCE
            ),
            language = "English"
        )

        assertEquals("In about 50m, turn right onto Mabini Street.", text)
    }

    @Test
    fun filipinoTurnGuidance_usesLocalDistanceAndStreet() {
        val text = LocalNavigationSpeech.guidanceText(
            LocalNavigationGuidance(
                sequence = 1,
                type = "TurnLeft",
                text = "Turn left onto Mabini Street.",
                streetName = "Mabini Street",
                anchorProgressMeters = 500.0,
                distanceMeters = 52.0,
                stage = LocalGuidanceStage.ADVANCE
            ),
            language = "Filipino"
        )

        assertTrue(text.contains("kaliwa"))
        assertTrue(text.contains("Mabini Street"))
        assertTrue(text.contains("50m"))
    }
}
