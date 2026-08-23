package com.example.frontend.core.localization

object TukiInterfaceText {
    val isFilipino: Boolean get() = AppLanguagePreference.isFilipino()

    private fun pick(english: String, filipino: String): String =
        if (isFilipino) filipino else english

    val home: String get() = "Home"
    val recent: String get() = "Recent"
    val favorites: String get() = "Favorites"
    val profile: String get() = "Profile"
    val settings: String get() = "Settings"

    val hello: String get() = pick("Hello", "Kamusta")
    val whereToToday: String get() = pick("Where to today?", "Saan ka patungo?")
    val planTripOrAskAi: String get() = pick(
        "Plan your trip or ask our AI for the best way to go.",
        "Planuhin ang biyahe mo o magtanong sa AI para sa pinakamainam na ruta."
    )
    val currentLocation: String get() = pick("Current location", "Kasalukuyang lokasyon")
    val currentLocationUpper: String get() = pick("CURRENT LOCATION", "KASALUKUYANG LOKASYON")
    val currentArea: String get() = pick("Current area", "Kasalukuyang lugar")
    val locatingYou: String get() = pick("Locating you...", "Hinahanap ang lokasyon mo...")
    val tapToChange: String get() = pick("Tap to change", "I-tap para baguhin")
    val tapToChangeMultiline: String get() = pick("Tap to\nchange", "I-tap para\nbaguhin")
    val destination: String get() = pick("Destination", "Destinasyon")
    val destinationUpper: String get() = pick("DESTINATION", "DESTINASYON")
    val whereAreYouGoing: String get() = pick("Where are you going?", "Saan ka pupunta?")
    val searchOrEnterPlace: String get() = pick("Search or enter a place", "Maghanap o maglagay ng lugar")
    val tapToChangeDestination: String get() = pick("Tap to change destination", "I-tap para baguhin ang destinasyon")
    val findRoutes: String get() = pick("Find Routes", "Maghanap ng Ruta")
    val recentPlaces: String get() = pick("Recent places", "Mga kamakailang lugar")
    val viewAll: String get() = pick("View all", "Tingnan lahat")
    val findingRecentPlaces: String get() = pick("Finding your recent places...", "Hinahanap ang mga kamakailang lugar mo...")
    val addShortcut: String get() = pick("Add\nshortcut", "Magdagdag\nshortcut")
    val startJourneyWithTuki: String get() = pick("Start your journey with TUKI", "Simulan ang biyahe mo gamit ang TUKI")
    val pickDestinationRecentAppear: String get() = pick(
        "Pick a destination and your recent places will appear here.",
        "Pumili ng destinasyon at lalabas dito ang mga kamakailang lugar mo."
    )
    val askTukiAi: String get() = pick("Ask TUKI AI", "Magtanong sa TUKI AI")
    val letAiFindBestWay: String get() = pick("Let AI find the best way to go.", "Hayaan ang AI na hanapin ang pinakamainam na ruta.")
    val tripInProgress: String get() = pick("TRIP IN PROGRESS", "MAY BIYAHENG KASALUKUYAN")
    val resume: String get() = pick("Resume", "Ipagpatuloy")

    val recentTrips: String get() = pick("Recent Trips", "Mga Kamakailang Biyahe")
    val all: String get() = pick("All", "Lahat")
    val completed: String get() = pick("Completed", "Natapos")
    val cancelled: String get() = pick("Cancelled", "Kinansela")
    val waitingToBoard: String get() = pick("WaitingToBoard", "Naghihintay Sumakay")
    val noTripsYet: String get() = pick("No trips in this category yet.", "Wala pang biyahe sa kategoryang ito.")
    val signInToViewJourneys: String get() = pick("Sign in to view your recent journeys.", "Mag-sign in para makita ang mga kamakailang biyahe mo.")

    val saveFavoriteRoutes: String get() = pick(
        "Save your favorite routes\nfor quick access",
        "I-save ang paborito mong ruta\npara madaling balikan"
    )
    val howToAddFavorites: String get() = pick("How to add favorites?", "Paano magdagdag sa Favorites?")
    val tapStarToSave: String get() = pick(
        "Tap the star on any route to save it here.",
        "I-tap ang bituin sa anumang ruta para i-save ito rito."
    )
    val noFavoriteRoutes: String get() = pick("No favorite routes yet.", "Wala pang naka-save na favorite route.")
    val signInFavorites: String get() = pick("Sign in to save and view your favorite routes.", "Mag-sign in para mag-save at makita ang favorite routes mo.")

    val routeDetails: String get() = pick("Route Details", "Detalye ng Ruta")
    val stepByStepGuide: String get() = "Step-by-step guide"
    val startTrip: String get() = "Start Trip"
    val endTrip: String get() = "End Trip"
    val fullRoute: String get() = pick("Full route", "Buong ruta")
    val yourCompleteRoute: String get() = pick("Your complete route", "Buong ruta mo")
    val selectedTravelSegment: String get() = pick("Selected travel segment", "Napiling bahagi ng biyahe")
    val tapStepInspect: String get() = pick("Tap a step to inspect its route", "I-tap ang hakbang para makita ang ruta")
    val walkTo: String get() = pick("Walk to", "Maglakad papunta sa")
    val rideTricycle: String get() = pick("Ride Tricycle", "Sumakay ng Tricycle")
    val rideJeepney: String get() = pick("Ride Jeepney", "Sumakay ng Jeepney")
    val tipPrepareFare: String get() = pick(
        "Tip: Prepare exact fare or have small bills for a smoother ride.",
        "Tip: Maghanda ng eksaktong pamasahe o maliliit na pera para mas maayos ang biyahe."
    )

    val language: String get() = pick("Language", "Wika")
    val selectLanguage: String get() = pick("SELECT LANGUAGE", "PUMILI NG WIKA")
    val save: String get() = pick("Save", "I-save")
    val account: String get() = "ACCOUNT"
    val appearance: String get() = pick("APPEARANCE", "ITSURA")
    val support: String get() = pick("SUPPORT", "SUPPORT")
    val editProfile: String get() = pick("Edit Profile", "I-edit ang Profile")
    val privacySecurity: String get() = pick("Privacy & Security", "Privacy at Security")
    val appearancePreferences: String get() = pick("Appearance and app preferences", "Itsura at mga preference ng app")
    val darkMode: String get() = pick("Dark Mode", "Dark Mode")
    val darkModeSubtitle: String get() = pick("Switch between light and dark theme", "Palitan ang light at dark theme")
    val helpCenter: String get() = pick("Help Center", "Help Center")
    val sendFeedback: String get() = pick("Send Feedback", "Magpadala ng Feedback")
    val aboutTuki: String get() = pick("About TUKI", "Tungkol sa TUKI")
    val logOut: String get() = "Log out"

    val logIn: String get() = "Log in"
    val continueAsGuest: String get() = "Continue as Guest"
    val welcomeBack: String get() = pick("Welcome back", "Maligayang pagbabalik")
    val loginSubtitle: String get() = pick("Log in to continue your commute", "Mag-log in para ipagpatuloy ang biyahe")
    val email: String get() = "Email"
    val password: String get() = "Password"
    val forgotPassword: String get() = pick("Forgot password?", "Nakalimutan ang password?")
    val newToTuki: String get() = pick("New to Tuki?", "Bago sa TUKI?")
    val signUp: String get() = "Sign up"
    val createAccount: String get() = pick("Create an account", "Gumawa ng account")
    val fullName: String get() = pick("Full Name", "Buong Pangalan")
    val sendOtp: String get() = pick("Send OTP", "Ipadala ang OTP")
    val verifyOtp: String get() = pick("Verify OTP", "I-verify ang OTP")
    val resetPassword: String get() = pick("Reset Password", "I-reset ang Password")
    val changePassword: String get() = pick("Change password", "Palitan ang password")
    val checkYourEmail: String get() = pick("Check your email", "Tingnan ang email mo")
    val newPassword: String get() = pick("New password", "Bagong password")
    val confirmNewPassword: String get() = pick("Confirm new password", "Kumpirmahin ang bagong password")

    fun status(raw: String): String = when {
        !isFilipino -> raw
        raw.equals("Completed", true) -> completed
        raw.equals("Cancelled", true) -> cancelled
        raw.equals("WaitingToBoard", true) -> waitingToBoard
        else -> raw
    }
}
