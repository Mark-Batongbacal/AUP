package com.example.frontend.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable

private val TukiColorScheme = lightColorScheme(
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
    
    surface = TukiCream,
    onSurface = TukiInk,
    surfaceVariant = TukiTealSurface,
    onSurfaceVariant = TukiMuted,
    
    outline = TukiOutline,
    error = TukiDanger,
    onError = TukiOnDark
)

@Composable
fun FrontendTheme(
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = TukiColorScheme,
        typography = Typography,
        content = content
    )
}
