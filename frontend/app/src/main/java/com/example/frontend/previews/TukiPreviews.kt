package com.example.frontend.previews

import androidx.compose.runtime.Composable
import androidx.compose.ui.tooling.preview.Preview
import com.example.frontend.model.RecentCommute
import com.example.frontend.screens.CommuteDetailScreen
import com.example.frontend.screens.FavoritesScreen
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.OnboardingScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.ui.theme.FrontendTheme
import com.example.frontend.screens.HomeScreen
import com.example.frontend.screens.ProfileScreen
import com.example.frontend.screens.RecentScreen
import com.example.frontend.screens.RouteResultsScreen
import com.example.frontend.screens.SettingsScreen
import com.example.frontend.screens.TripTrackingScreen
import com.example.frontend.TemporaryMapSamples
import com.example.frontend.repository.MockCommuteRepository
import com.example.frontend.repository.MockRouteRepository
import com.example.frontend.repository.MockAuthRepository

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
            authRepository = MockAuthRepository(),
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
            authRepository = MockAuthRepository(),
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
        HomeScreen(commuteRepository = MockCommuteRepository())
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
            destinationQuery = "Guagua Town",
            routeRepository = MockRouteRepository()
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Recent"
)
@Composable
fun RecentPreview() {
    FrontendTheme {
        RecentScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Favorites"
)
@Composable
fun FavoritesPreview() {
    FrontendTheme {
        FavoritesScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Profile"
)
@Composable
fun ProfilePreview() {
    FrontendTheme {
        ProfileScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Settings"
)
@Composable
fun SettingsPreview() {
    FrontendTheme {
        SettingsScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Trip Tracking"
)
@Composable
fun TripTrackingPreview() {
    FrontendTheme {
        TripTrackingScreen(
            origin = "Sta. Rita",
            destination = "Guagua Town",
            routePoints = TemporaryMapSamples.routePoints
        )
    }
}