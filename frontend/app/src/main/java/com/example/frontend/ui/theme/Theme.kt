package com.example.frontend.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable

private val TukiLightColorScheme = lightColorScheme(
    primary = TukiTeal,
    onPrimary = TukiOnDark,
    primaryContainer = TukiSky,
    onPrimaryContainer = TukiDeepTeal,
    secondary = TukiForest,
    onSecondary = TukiOnDark,
    secondaryContainer = TukiForestSurface,
    onSecondaryContainer = TukiForest,
    tertiary = TukiOrange,
    onTertiary = TukiOnDark,
    tertiaryContainer = TukiOrangeSurface,
    onTertiaryContainer = TukiInk,
    background = TukiCream,
    onBackground = TukiInk,
    surface = TukiSurfaceRaised,
    onSurface = TukiInk,
    surfaceVariant = TukiTealSurface,
    onSurfaceVariant = TukiMuted,
    outline = TukiOutline,
    error = TukiDanger,
    onError = TukiOnDark
)

private val TukiDarkColorScheme = darkColorScheme(
    primary = TukiTeal,
    onPrimary = TukiOnDark,
    primaryContainer = TukiTealSurface,
    onPrimaryContainer = TukiInk,
    secondary = ColorTokens.darkSecondary,
    onSecondary = TukiOnDark,
    secondaryContainer = TukiForestSurface,
    onSecondaryContainer = TukiInk,
    tertiary = TukiOrange,
    onTertiary = TukiOnDark,
    tertiaryContainer = TukiOrangeSurface,
    onTertiaryContainer = TukiInk,
    background = TukiCream,
    onBackground = TukiInk,
    surface = TukiSurfaceRaised,
    onSurface = TukiInk,
    surfaceVariant = TukiSky,
    onSurfaceVariant = TukiMuted,
    outline = TukiOutline,
    error = TukiDanger,
    onError = TukiOnDark
)

private object ColorTokens {
    val darkSecondary = androidx.compose.ui.graphics.Color(0xFF45B89E)
}

@Composable
fun FrontendTheme(
    darkTheme: Boolean = TukiThemeRuntime.darkMode,
    content: @Composable () -> Unit
) {
    TukiThemeRuntime.darkMode = darkTheme
    MaterialTheme(
        colorScheme = if (darkTheme) TukiDarkColorScheme else TukiLightColorScheme,
        typography = Typography,
        content = content
    )
}
