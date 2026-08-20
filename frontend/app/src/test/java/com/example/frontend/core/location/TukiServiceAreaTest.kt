package com.example.frontend.core.location

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class TukiServiceAreaTest {
    @Test
    fun supportedArea_acceptsMvpCities() {
        assertTrue(isLocationSupported(15.0710, 120.5420)) // Porac
        assertTrue(isLocationSupported(15.1453, 120.5887)) // Angeles City
        assertTrue(isLocationSupported(15.1790, 120.5900)) // Dau
        assertTrue(isLocationSupported(15.2230, 120.5740)) // Mabalacat City
    }

    @Test
    fun supportedArea_rejectsClearlyOutsideRegion() {
        assertFalse(isLocationSupported(14.5995, 120.9842)) // Manila
        assertFalse(isLocationSupported(15.4865, 120.9734)) // Cabanatuan
    }

    @Test
    fun routeSupport_requiresOriginAndDestinationInsideArea() {
        assertTrue(isRouteSupported(15.1453, 120.5887, 15.1790, 120.5900))
        assertFalse(isRouteSupported(14.5995, 120.9842, 15.1790, 120.5900))
        assertFalse(isRouteSupported(15.1453, 120.5887, 14.5995, 120.9842))
    }
}
