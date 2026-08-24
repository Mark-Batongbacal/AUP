package com.example.frontend.ui.theme

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color

// ============================================================
// TUKI BRAND COLORS (v2.0)
// ============================================================

// Brand colors stay recognizable in both themes.
val TukiTeal = Color(0xFF0D8B97)
val TukiDeepTeal = Color(0xFF076773)
val TukiForest = Color(0xFF0A5B48)
val TukiOrange = Color(0xFFF48B1F)
val TukiGold = Color(0xFFFABE3A)
val TukiDanger = Color(0xFFEE5B57)
val TukiOnDark = Color.White

private val TukiLightBackground = Color(0xFFFFF9E9)
private val TukiLightSurface = Color.White
private val TukiLightSecondarySurface = Color(0xFFDAF1F7)
private val TukiLightInk = Color(0xFF112E36)

private val TukiDarkBackground = Color(0xFF08171D)
private val TukiDarkSurface = Color(0xFF10242D)
private val TukiDarkSecondarySurface = Color(0xFF17333D)
private val TukiDarkInk = Color(0xFFF1F7F8)

/**
 * Runtime appearance flag used by the existing TUKI color constants.
 * Keeping the public color names stable lets old screens become theme-aware
 * without changing navigation/business logic or duplicating an entire UI tree.
 */
object TukiThemeRuntime {
    var darkMode by mutableStateOf(false)
}

val TukiCream: Color
    get() = if (TukiThemeRuntime.darkMode) TukiDarkBackground else TukiLightBackground

val TukiSky: Color
    get() = if (TukiThemeRuntime.darkMode) TukiDarkSecondarySurface else TukiLightSecondarySurface

val TukiInk: Color
    get() = if (TukiThemeRuntime.darkMode) TukiDarkInk else TukiLightInk

val TukiSurface: Color
    get() = TukiCream

val TukiSurfaceRaised: Color
    get() = if (TukiThemeRuntime.darkMode) TukiDarkSurface else TukiLightSurface

val TukiMuted: Color
    get() = TukiInk.copy(alpha = if (TukiThemeRuntime.darkMode) 0.72f else 0.62f)

val TukiSubtle: Color
    get() = TukiInk.copy(alpha = if (TukiThemeRuntime.darkMode) 0.48f else 0.36f)

val TukiOutline: Color
    get() = TukiInk.copy(alpha = if (TukiThemeRuntime.darkMode) 0.22f else 0.14f)

val TukiTealSurface: Color
    get() = if (TukiThemeRuntime.darkMode) Color(0xFF123842) else TukiSky.copy(alpha = 0.58f)

val TukiOrangeSurface: Color
    get() = TukiOrange.copy(alpha = if (TukiThemeRuntime.darkMode) 0.18f else 0.12f)

val TukiGoldSurface: Color
    get() = TukiGold.copy(alpha = if (TukiThemeRuntime.darkMode) 0.20f else 0.18f)

val TukiForestSurface: Color
    get() = if (TukiThemeRuntime.darkMode) Color(0xFF123029) else TukiForest.copy(alpha = 0.14f)
