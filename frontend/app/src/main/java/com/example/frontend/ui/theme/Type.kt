package com.example.frontend.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import com.example.frontend.R

// DISPLAY: Baloo 2 - Warm & Rounded
val TukiDisplayFontFamily = FontFamily(
    Font(R.font.baloo_2, FontWeight.Medium),    // 500
    Font(R.font.baloo_2, FontWeight.SemiBold),  // 600
    Font(R.font.baloo_2, FontWeight.Bold),      // 700
    Font(R.font.baloo_2, FontWeight.ExtraBold)  // 800
)

// BODY / UI: Plus Jakarta Sans - Crisp Grotesque
val TukiBodyFontFamily = FontFamily(
    Font(R.font.plus_jakarta_sans, FontWeight.Normal),   // 400
    Font(R.font.plus_jakarta_sans, FontWeight.Medium),   // 500
    Font(R.font.plus_jakarta_sans, FontWeight.SemiBold), // 600
    Font(R.font.plus_jakarta_sans, FontWeight.Bold)      // 700
)

// UTILITY: IBM Plex Mono - Fares, Codes, ETAs
val TukiUtilityFontFamily = FontFamily(
    Font(R.font.ibm_plex_mono_regular, FontWeight.Normal),
    Font(R.font.ibm_plex_mono_semibold, FontWeight.SemiBold)
)

val Typography = Typography(
    // Large Headlines & Hero (Baloo 2)
    displayLarge = TextStyle(
        fontFamily = TukiDisplayFontFamily,
        fontWeight = FontWeight.ExtraBold,
        fontSize = 40.sp,
        lineHeight = 48.sp,
        letterSpacing = (-0.5).sp
    ),
    displayMedium = TextStyle(
        fontFamily = TukiDisplayFontFamily,
        fontWeight = FontWeight.Bold,
        fontSize = 32.sp,
        lineHeight = 40.sp
    ),
    displaySmall = TextStyle(
        fontFamily = TukiDisplayFontFamily,
        fontWeight = FontWeight.Bold,
        fontSize = 24.sp,
        lineHeight = 32.sp
    ),

    // Subheads (Baloo 2)
    headlineMedium = TextStyle(
        fontFamily = TukiDisplayFontFamily,
        fontWeight = FontWeight.SemiBold,
        fontSize = 20.sp,
        lineHeight = 28.sp
    ),

    // UI Titles (Plus Jakarta Sans)
    titleLarge = TextStyle(
        fontFamily = TukiBodyFontFamily,
        fontWeight = FontWeight.Bold,
        fontSize = 18.sp,
        lineHeight = 24.sp
    ),
    titleMedium = TextStyle(
        fontFamily = TukiBodyFontFamily,
        fontWeight = FontWeight.SemiBold,
        fontSize = 16.sp,
        lineHeight = 22.sp
    ),

    // Primary Body (Plus Jakarta Sans)
    bodyLarge = TextStyle(
        fontFamily = TukiBodyFontFamily,
        fontWeight = FontWeight.Normal,
        fontSize = 16.sp,
        lineHeight = 24.sp
    ),
    bodyMedium = TextStyle(
        fontFamily = TukiBodyFontFamily,
        fontWeight = FontWeight.Normal,
        fontSize = 14.sp,
        lineHeight = 20.sp
    ),
    bodySmall = TextStyle(
        fontFamily = TukiBodyFontFamily,
        fontWeight = FontWeight.Normal,
        fontSize = 12.sp,
        lineHeight = 16.sp
    ),

    // UI Elements (Plus Jakarta Sans)
    labelLarge = TextStyle(
        fontFamily = TukiBodyFontFamily,
        fontWeight = FontWeight.SemiBold,
        fontSize = 14.sp,
        lineHeight = 20.sp
    ),
    
    // Utility Styles (IBM Plex Mono)
    labelSmall = TextStyle(
        fontFamily = TukiUtilityFontFamily,
        fontWeight = FontWeight.SemiBold,
        fontSize = 11.sp,
        lineHeight = 16.sp,
        letterSpacing = 0.5.sp
    )
)
