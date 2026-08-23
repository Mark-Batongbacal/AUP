package com.example.frontend.ui.theme

import androidx.compose.ui.graphics.Color

// ============================================================
// TUKI BRAND COLORS (v2.0)
// ============================================================

// PRIMARY
val TukiTeal = Color(0xFF0D8B97)     // System, links, active
val TukiDeepTeal = Color(0xFF076773) // Headlines, wordmark, pressed

// SECONDARY
val TukiForest = Color(0xFF0A5B48)   // Success, live tracking

// ACCENT
val TukiOrange = Color(0xFFF48B1F)   // Personality, energy, alerts
val TukiGold = Color(0xFFFABE3A)     // Ratings, badges

// BACKGROUND
val TukiCream = Color(0xFFFFF9E9)    // Main app background
val TukiSky = Color(0xFFDAF1F7)      // Secondary surfaces, maps

// TEXT
val TukiInk = Color(0xFF112E36)      // Body text

// UTILITY / SEMANTIC
val TukiDanger = Color(0xFFEE5B57)
val TukiOnDark = Color.White
val TukiSurface = TukiCream
val TukiSurfaceRaised = Color.White
val TukiMuted = TukiInk.copy(alpha = 0.62f)
val TukiSubtle = TukiInk.copy(alpha = 0.36f)
val TukiOutline = TukiInk.copy(alpha = 0.14f)

// OVERLAYS / TINT SURFACES
val TukiTealSurface = TukiSky.copy(alpha = 0.58f)
val TukiOrangeSurface = TukiOrange.copy(alpha = 0.12f)
val TukiGoldSurface = TukiGold.copy(alpha = 0.18f)
val TukiForestSurface = TukiForest.copy(alpha = 0.14f)
