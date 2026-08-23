package com.example.frontend

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import com.example.frontend.auth.FacebookSignInClient
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.navigation.AppNavigation
import com.example.frontend.ui.theme.FrontendTheme

class MainActivity : ComponentActivity() {
    private val facebookSignInClient = FacebookSignInClient()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            FrontendTheme {
                TukiApp(facebookSignInClient)
            }
        }
    }

    @Deprecated("Deprecated in Java")
    @Suppress("DEPRECATION")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        facebookSignInClient.onActivityResult(requestCode, resultCode, data)
    }
}

@Composable
fun TukiApp(
    facebookSignInClient: FacebookSignInClient = FacebookSignInClient()
) {
    val context = LocalContext.current
    val dataProvider = remember { TukiDataProvider(context.applicationContext) }
    val selectedJeepneyRouteIds = TukiMapOverlayState.selectedJourneyJeepneyRouteIds

    LaunchedEffect(dataProvider) {
        TukiMapOverlayState.ensureTodaPoints(dataProvider)
    }

    LaunchedEffect(dataProvider, selectedJeepneyRouteIds) {
        TukiMapOverlayState.ensureSelectedJeepneyRoutes(dataProvider)
    }

    CompositionLocalProvider(LocalTukiDataProvider provides dataProvider) {
        AppNavigation(
            dataProvider = dataProvider,
            facebookSignInClient = facebookSignInClient
        )
    }
}
