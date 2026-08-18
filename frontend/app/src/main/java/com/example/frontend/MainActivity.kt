package com.example.frontend

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalContext
import androidx.credentials.CredentialManager
import com.example.frontend.auth.FacebookSignInClient
import com.example.frontend.data.TukiDataProvider
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
    
    com.example.frontend.navigation.AppNavigation(
        dataProvider = dataProvider,
        facebookSignInClient = facebookSignInClient
    )
}
