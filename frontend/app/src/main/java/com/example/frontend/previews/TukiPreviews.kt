package com.example.frontend.previews

import androidx.compose.runtime.Composable
import androidx.compose.ui.tooling.preview.Preview
import com.example.frontend.model.RecentCommute
import com.example.frontend.screens.CommuteDetailScreen
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.OnboardingScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.ui.theme.FrontendTheme
import com.example.frontend.screens.HomeScreen
import com.example.frontend.screens.RouteResultsScreen

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Onboarding"
)
@Composable
fun OnboardingPreview() {
    FrontendTheme {
        OnboardingScreen(
            onLetsRideClick = {}
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Login"
)
@Composable
fun LoginPreview() {
    FrontendTheme {
        LoginScreen(
            onBack = {}
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "SignUp"
)
@Composable
fun SignUpPreview() {
    FrontendTheme {
        SignupScreen(
            onBack = {}
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Home"
)
@Composable
fun HomePreview() {
    FrontendTheme {
        HomeScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Commute Detail"
)
@Composable
fun CommuteDetailPreview() {
    FrontendTheme {
        CommuteDetailScreen(
            commute = RecentCommute(
                id = "1",
                origin = "Sta. Rita",
                destination = "Guagua Town",
                legs = 3,
                minutes = 22
            )
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Route Results"
)
@Composable
fun RouteResultsPreview() {
    FrontendTheme {
        RouteResultsScreen(
            origin = "Brgy. Sta. Rita",
            destinationQuery = "Guagua Town"
        )
    }
}
