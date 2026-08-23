package com.example.frontend.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private fun tukiLightColorScheme() = lightColorScheme(
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

private fun tukiDarkColorScheme() = darkColorScheme(
    primary = TukiTeal,
    onPrimary = TukiOnDark,
    primaryContainer = TukiTealSurface,
    onPrimaryContainer = TukiInk,
    secondary = Color(0xFF45B89E),
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

@Composable
fun FrontendTheme(
    darkTheme: Boolean = TukiThemeRuntime.darkMode,
    content: @Composable () -> Unit
) {
    TukiThemeRuntime.darkMode = darkTheme
    MaterialTheme(
        colorScheme = if (darkTheme) tukiDarkColorScheme() else tukiLightColorScheme(),
        typography = Typography,
        content = content
    )
}
