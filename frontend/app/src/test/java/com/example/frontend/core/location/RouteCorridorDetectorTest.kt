package com.example.frontend.core.location

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RouteCorridorDetectorTest {
    @Test
    fun requiresSeveralOutsideFixesBeforeForcingServerSync() {
        val detector = RouteCorridorDetector(requiredOutsideFixes = 3)

        assertFalse(detector.update(70.0, 5.0).shouldForceSync)
        assertFalse(detector.update(72.0, 5.0).shouldForceSync)
        assertTrue(detector.update(75.0, 5.0).shouldForceSync)
    }

    @Test
    fun goodFixResetsOutsideCounter() {
        val detector = RouteCorridorDetector(requiredOutsideFixes = 3)

        detector.update(70.0, 5.0)
        detector.update(70.0, 5.0)
        detector.update(10.0, 5.0)

        assertFalse(detector.update(70.0, 5.0).shouldForceSync)
    }

    @Test
    fun accuracyExpandsToleranceWithoutAllowingUnlimitedDrift() {
        val detector = RouteCorridorDetector(requiredOutsideFixes = 1)

        assertFalse(detector.update(70.0, 60.0).shouldForceSync)
        assertTrue(detector.update(95.0, 200.0).shouldForceSync)
    }
}
