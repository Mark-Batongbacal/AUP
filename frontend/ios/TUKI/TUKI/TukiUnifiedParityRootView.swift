import CoreLocation
import SwiftUI

struct TukiUnifiedParityRootView: View {
    @StateObject private var auth = AuthViewModel()
    @ObservedObject private var theme = TukiThemeRuntime.shared
    @ObservedObject private var language = TukiLanguagePreference.shared
    @State private var entry: Entry = .onboarding

    private enum Entry {
        case onboarding
        case login
        case signup
        case forgotPassword
    }

    var body: some View {
        Group {
            if auth.canEnterApp {
                TukiUnifiedMainView(auth: auth)
            } else {
                switch entry {
                case .onboarding:
                    UnifiedOnboarding { entry = .login }
                case .login:
                    UnifiedLogin(
                        auth: auth,
                        onSignUp: { entry = .signup },
                        onForgotPassword: { entry = .forgotPassword },
                        onGuest: { Task { await auth.continueAsGuest() } }
                    )
                case .signup:
                    UnifiedSignup(auth: auth) { entry = .login }
                case .forgotPassword:
                    UnifiedForgotPassword { entry = .login }
                }
            }
        }
        .preferredColorScheme(theme.isDarkMode ? .dark : .light)
    }
}

private enum UnifiedTab: CaseIterable {
    case home
    case recent
    case favorites
    case profile

    var label: String {
        switch self {
        case .home: "Home"
        case .recent: "Recent"
        case .favorites: "Favorites"
        case .profile: "Profile"
        }
    }

    var image: String {
        switch self {
        case .home: "HomeIcon"
        case .recent: "RecentIcon"
        case .favorites: "FavoriteIcon"
        case .profile: "ProfileIcon"
        }
    }
}

private enum UnifiedScreen {
    case tabs
    case pickOrigin
    case pickDestination
    case ai
    case routes(String, CLLocationCoordinate2D, TukiPlace, TukiRouteChoice?)
    case detail(String, CLLocationCoordinate2D, TukiPlace, TukiRouteChoice, Bool)
    case tracking(String, TukiPlace, TukiRouteChoice, TukiNavigationSnapshot, Bool)
    case commute(RecentCommute)
    case editProfile
    case privacySecurity
    case changePassword
    case permissions
    case privacyPolicy
    case language
    case about
    case helpCenter
    case sendFeedback
    case settings
}

private struct TukiUnifiedMainView: View {
    @ObservedObject var auth: AuthViewModel
    @StateObject private var location = TukiLocationService()
    @State private var tab: UnifiedTab = .home
    @State private var screen: UnifiedScreen = .tabs
    @State private var recent: [RecentCommute] = []
    @State private var favorites: [FavoriteRoute] = []
    @State private var currentLabel = "Locating you..."
    @State private var originCoordinate: CLLocationCoordinate2D?
    @State private var originAreaLabel = TukiInterfaceText.currentArea
    @State private var selectedDestination: TukiPlace?
    @State private var recentLoading = false
    @State private var recentError: String?
    @State private var favoriteRecommendationIds: Set<String> = []
    @State private var favoriteWorkingIds: Set<String> = []
    @State private var favoritesOpenError: String?

    private let api: TukiPlatformAPI?
    private let historyAPI: TukiHistoryAPI?
    private let assistantAPI: TukiAssistantAPI?
    private let infrastructureAPI: TukiInfrastructureAPI?

    init(auth: AuthViewModel) {
        self.auth = auth
        let store = KeychainTukiCredentialStore()
        if let configuration = try? AppConfiguration.load() {
            api = TukiPlatformAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
            historyAPI = TukiHistoryAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
            assistantAPI = TukiAssistantAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
            infrastructureAPI = TukiInfrastructureAPI(baseURL: configuration.backendBaseURL)
        } else {
            api = nil
            historyAPI = nil
            assistantAPI = nil
            infrastructureAPI = nil
        }
    }

    var body: some View {
        Group {
            switch screen {
            case .tabs:
                tabView

            case .pickOrigin:
                TukiUnifiedDestinationPickerScreen(
                    api: api,
                    mode: .origin,
                    focusLatitude: originCoordinate?.latitude,
                    focusLongitude: originCoordinate?.longitude,
                    initialSelection: originCoordinate.map { coordinate in
                        TukiPlace(
                            id: "origin-\(coordinate.latitude)-\(coordinate.longitude)",
                            name: currentLabel,
                            latitude: coordinate.latitude,
                            longitude: coordinate.longitude,
                            category: "origin",
                            source: "current",
                            address: nil,
                            locality: originAreaLabel
                        )
                    },
                    onBack: { screen = .tabs },
                    onDone: { place in
                        originCoordinate = CLLocationCoordinate2D(latitude: place.latitude, longitude: place.longitude)
                        currentLabel = place.name
                        if let locality = place.locality?.trimmingCharacters(in: .whitespaces), !locality.isEmpty {
                            originAreaLabel = locality
                        }
                        screen = .tabs
                    }
                )

            case .pickDestination:
                TukiUnifiedDestinationPickerScreen(
                    api: api,
                    mode: .destination,
                    focusLatitude: originCoordinate?.latitude,
                    focusLongitude: originCoordinate?.longitude,
                    initialSelection: selectedDestination,
                    onBack: { screen = .tabs },
                    onDone: { place in
                        selectedDestination = place
                        screen = .tabs
                    }
                )

            case .ai:
                TukiParityAIChat(
                    userName: auth.currentUserProfile?.greetingName ?? (auth.isGuestAccount ? "Guest" : "User"),
                    api: assistantAPI,
                    location: location,
                    onBack: { screen = .tabs },
                    onRouteSelected: { destination, choice in
                        Task {
                            guard let current = await location.requestCurrentLocation() else { return }
                            screen = .routes(currentLabel, current.coordinate, destination, choice)
                        }
                    }
                )

            case .routes(let originName, let origin, let destination, let preset):
                TukiUnifiedRouteResultsView(
                    api: api,
                    originName: originName,
                    origin: origin,
                    destination: destination,
                    presetRoute: preset,
                    onBack: { screen = .tabs },
                    onSelect: { choice in
                        screen = .detail(originName, origin, destination, choice, preset != nil)
                    }
                )

            case .detail(let originName, let origin, let destination, let choice, let fromAI):
                TukiUnifiedRouteDetailView(
                    api: api,
                    auth: auth,
                    originName: originName,
                    destination: destination,
                    choice: choice,
                    onBack: {
                        screen = .routes(originName, origin, destination, fromAI ? choice : nil)
                    },
                    onStarted: { snapshot, guest in
                        screen = .tracking(originName, destination, choice, snapshot, guest)
                    },
                    onEnded: { returnHome() }
                )

            case .tracking(let originName, let destination, let choice, let snapshot, let guest):
                TukiUnifiedTrackingView(
                    api: api,
                    infrastructureAPI: infrastructureAPI,
                    location: location,
                    originName: originName,
                    destination: destination,
                    choice: choice,
                    initialSnapshot: snapshot,
                    isGuest: guest,
                    onEnded: { returnHome() }
                )

            case .commute(let commute):
                ZStack(alignment: .bottom) {
                    TukiUnifiedCommuteDetailView(commute: commute) { screen = .tabs }
                    Button {
                        repeatTrip(commute)
                    } label: {
                        Text("Repeat Trip")
                            .font(.system(size: 16, weight: .bold))
                            .foregroundStyle(.white)
                            .frame(maxWidth: .infinity)
                            .frame(height: 52)
                            .background(TukiPalette.orange)
                            .clipShape(RoundedRectangle(cornerRadius: 16))
                    }
                    .buttonStyle(.plain)
                    .disabled(!canRepeat(commute))
                    .opacity(canRepeat(commute) ? 1 : 0.5)
                    .padding(.horizontal, 30)
                    .padding(.bottom, 24)
                }

            case .editProfile:
                TukiUnifiedEditProfileView(auth: auth) { screen = .tabs; tab = .profile }

            case .privacySecurity:
                TukiUnifiedPrivacySecurityView(
                    auth: auth,
                    onBack: { screen = .tabs; tab = .profile },
                    onChangePassword: { screen = .changePassword },
                    onPermissions: { screen = .permissions },
                    onPrivacyPolicy: { screen = .privacyPolicy },
                    onAccountDeleted: { auth.signOut() }
                )

            case .changePassword:
                TukiUnifiedChangePasswordView(auth: auth) { screen = .privacySecurity }

            case .permissions:
                TukiUnifiedPermissionsView { screen = .privacySecurity }

            case .privacyPolicy:
                TukiUnifiedPrivacyPolicyView { screen = .privacySecurity }

            case .language:
                TukiUnifiedLanguageView { screen = .tabs; tab = .profile }

            case .about:
                TukiUnifiedAboutView { screen = .settings }

            case .helpCenter:
                TukiUnifiedHelpCenterView { screen = .settings }

            case .sendFeedback:
                TukiUnifiedSendFeedbackView { screen = .settings }

            case .settings:
                TukiUnifiedSettingsView(
                    onBack: { screen = .tabs; tab = .profile },
                    onHelpCenter: { screen = .helpCenter },
                    onSendFeedback: { screen = .sendFeedback },
                    onAbout: { screen = .about },
                    onLogout: { auth.signOut() }
                )
            }
        }
        .task { await refreshLocation() }
    }

    private var tabView: some View {
        VStack(spacing: 0) {
            Group {
                switch tab {
                case .home:
                    UnifiedHome(
                        name: auth.currentUserProfile?.greetingName ?? (auth.isGuestAccount ? "Guest" : "User"),
                        currentLabel: currentLabel,
                        areaLabel: originAreaLabel,
                        isLocating: currentLabel == "Locating you...",
                        selectedDestination: selectedDestination,
                        canFindRoutes: selectedDestination != nil && originCoordinate != nil,
                        onChangeOrigin: { screen = .pickOrigin },
                        onPinDestination: { screen = .pickDestination },
                        onFindRoutes: {
                            guard let selectedDestination, let originCoordinate else { return }
                            screen = .routes(currentLabel, originCoordinate, selectedDestination, nil)
                        },
                        onAI: { screen = .ai }
                    )
                case .recent:
                    UnifiedRecent(
                        commutes: recent,
                        guest: !auth.isAuthenticated,
                        loading: recentLoading,
                        error: recentError,
                        favoriteRecommendationIds: favoriteRecommendationIds,
                        favoriteWorkingIds: favoriteWorkingIds,
                        onToggleFavorite: { commute in Task { await toggleFavorite(commute) } },
                        onTap: { screen = .commute($0) }
                    )
                case .favorites:
                    UnifiedFavorites(
                        routes: favorites,
                        guest: !auth.isAuthenticated,
                        onTap: { route in Task { await openFavorite(route) } }
                    )
                case .profile:
                    TukiUnifiedProfileView(
                        auth: auth,
                        onEdit: { if !auth.isGuestAccount { screen = .editProfile } },
                        onPrivacy: { if !auth.isGuestAccount { screen = .privacySecurity } },
                        onLanguage: { screen = .language },
                        onSettings: { screen = .settings },
                        onLogout: { auth.signOut() }
                    )
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            UnifiedBottomBar(tab: $tab)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .task(id: tab) { await refreshTab() }
        .alert("Favorite unavailable", isPresented: Binding(
            get: { favoritesOpenError != nil },
            set: { if !$0 { favoritesOpenError = nil } }
        )) {
            Button("OK", role: .cancel) { favoritesOpenError = nil }
        } message: {
            Text(favoritesOpenError ?? "")
        }
    }

    private func returnHome() {
        screen = .tabs
        tab = .home
    }

    private func canRepeat(_ commute: RecentCommute) -> Bool {
        commute.originLatitude != nil &&
            commute.originLongitude != nil &&
            commute.destinationLatitude != nil &&
            commute.destinationLongitude != nil
    }

    private func repeatTrip(_ commute: RecentCommute) {
        guard let originLatitude = commute.originLatitude,
              let originLongitude = commute.originLongitude,
              let destinationLatitude = commute.destinationLatitude,
              let destinationLongitude = commute.destinationLongitude else { return }

        let origin = CLLocationCoordinate2D(
            latitude: originLatitude,
            longitude: originLongitude
        )
        let destination = TukiPlace(
            id: "history-\(commute.id)",
            name: commute.destination,
            latitude: destinationLatitude,
            longitude: destinationLongitude,
            category: "history",
            source: "history",
            address: nil
        )
        screen = .routes(commute.origin, origin, destination, nil)
    }

    private func refreshLocation() async {
        guard let current = await location.requestCurrentLocation() else {
            currentLabel = location.errorMessage ?? "Unable to detect location"
            return
        }
        originCoordinate = current.coordinate
        if let api,
           case .success(let place) = await api.reverseGeocode(
                lat: current.coordinate.latitude,
                lon: current.coordinate.longitude
           ) {
            currentLabel = place.name
            if let locality = place.locality?.trimmingCharacters(in: .whitespaces), !locality.isEmpty {
                originAreaLabel = locality
            }
        } else {
            currentLabel = "Current location"
        }
    }

    private func refreshTab() async {
        if tab == .profile {
            _ = await auth.refreshProfile()
            return
        }

        if tab == .recent, let historyAPI {
            recentLoading = true
            recentError = nil
            switch await historyAPI.history() {
            case .success(let values): recent = values
            case .failure(let error): recent = []; recentError = error.message
            }
            recentLoading = false
            if case .success(let values) = await historyAPI.favorites() {
                favorites = values
                favoriteRecommendationIds = Set(values.compactMap(\.recommendationId))
            }
        } else if tab == .favorites, let historyAPI {
            if case .success(let values) = await historyAPI.favorites() {
                favorites = values
                favoriteRecommendationIds = Set(values.compactMap(\.recommendationId))
            }
            if recent.isEmpty, case .success(let values) = await historyAPI.history() {
                recent = values
            }
        }
    }

    private func toggleFavorite(_ commute: RecentCommute) async {
        guard let historyAPI, let recommendationId = commute.recommendationId else { return }
        guard !favoriteWorkingIds.contains(recommendationId) else { return }
        favoriteWorkingIds.insert(recommendationId)
        defer { favoriteWorkingIds.remove(recommendationId) }

        if favoriteRecommendationIds.contains(recommendationId) {
            guard let favoriteTripId = favorites.first(where: { $0.recommendationId == recommendationId })?.id else { return }
            if case .success = await historyAPI.removeFavorite(favoriteTripId: favoriteTripId) {
                favorites.removeAll { $0.recommendationId == recommendationId }
                favoriteRecommendationIds.remove(recommendationId)
            }
        } else {
            if case .success(let route) = await historyAPI.addFavorite(recommendationId: recommendationId) {
                favorites.append(route)
                favoriteRecommendationIds.insert(recommendationId)
            }
        }
    }

    private func openFavorite(_ route: FavoriteRoute) async {
        if recent.isEmpty, let historyAPI, case .success(let values) = await historyAPI.history() {
            recent = values
        }
        if let match = recent.first(where: { $0.recommendationId != nil && $0.recommendationId == route.recommendationId }) {
            screen = .commute(match)
        } else {
            favoritesOpenError = "Route details aren't available for this favorite yet."
        }
    }
}

private struct UnifiedOnboarding: View {
    let onContinue: () -> Void
    var body: some View {
        ZStack {
            TukiPalette.teal.ignoresSafeArea()
            VStack(spacing: 18) {
                Spacer()
                Image("TukiLogo").resizable().scaledToFit().frame(width: 170, height: 170)
                Text("TUKI.").font(.system(size: 46, weight: .heavy)).foregroundStyle(.white)
                Text("Commute smarter.\nMove easier.").font(.system(size: 21)).multilineTextAlignment(.center).foregroundStyle(.white)
                Spacer()
                Button(action: onContinue) {
                    Text("Let's Ride").font(.system(size: 25, weight: .bold)).foregroundStyle(.white).frame(maxWidth: .infinity).frame(height: 72).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 22))
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 34)
            .padding(.bottom, 45)
        }
    }
}

private struct UnifiedLogin: View {
    @ObservedObject var auth: AuthViewModel
    let onSignUp: () -> Void
    let onForgotPassword: () -> Void
    let onGuest: () -> Void

    @State private var showGuestConfirmation = false

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                HStack(spacing: 10) {
                    Image("TukiLogo").resizable().scaledToFit().frame(width: 50, height: 50)
                    Text("TUKI.").font(.system(size: 30, weight: .heavy)).foregroundStyle(TukiPalette.teal)
                }
                Text("Welcome back").font(.system(size: 24, weight: .heavy)).padding(.top, 20)
                Text("Log in to continue your commute").font(.system(size: 16, weight: .semibold)).foregroundStyle(TukiPalette.gray).padding(.top, 4)

                VStack(spacing: 12) {
                    TukiFormField(label: "Email", text: $auth.userName, keyboardType: .emailAddress, textContentType: .username)
                    TukiFormField(label: "Password", text: $auth.password, isSecure: true, textContentType: .password)
                    Button("Forgot password?", action: onForgotPassword)
                        .font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.teal).frame(maxWidth: .infinity, alignment: .trailing).buttonStyle(.plain)
                }
                .padding(.top, 25)

                if let error = auth.errorMessage {
                    Text(error).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.error).multilineTextAlignment(.center).padding(.top, 12)
                }

                TukiPrimaryButton(title: auth.isAuthenticating ? "Logging in..." : "Log in", isLoading: auth.isAuthenticating, isEnabled: !auth.isAuthenticating, action: auth.loginWithPassword)
                    .padding(.top, 20)

                HStack(spacing: 14) {
                    Rectangle().fill(Color.gray.opacity(0.35)).frame(height: 1)
                    Text("OR").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.gray)
                    Rectangle().fill(Color.gray.opacity(0.35)).frame(height: 1)
                }
                .padding(.vertical, 15)

                HStack(spacing: 12) {
                    socialButton("Google", image: "GoogleLogo", action: auth.loginWithGoogle)
                    socialButton("Facebook", image: "FacebookLogo", action: auth.loginWithFacebook)
                }

                Button { showGuestConfirmation = true } label: {
                    Text("Continue as Guest").font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark).frame(maxWidth: .infinity).frame(height: 56).overlay { RoundedRectangle(cornerRadius: 16).stroke(TukiPalette.border, lineWidth: 2) }
                }
                .buttonStyle(.plain).disabled(auth.isAuthenticating).padding(.top, 12)
                .alert("Continue as Guest?", isPresented: $showGuestConfirmation) {
                    Button("Continue", action: onGuest)
                    Button("Cancel", role: .cancel) {}
                } message: {
                    Text("You can use TUKI for 24 hours, including navigation, history, and favorites. Create an account if you want access without the guest time limit.")
                }

                HStack(spacing: 0) {
                    Text("New to Tuki? ").foregroundStyle(TukiPalette.gray)
                    Button("Sign up", action: onSignUp).foregroundStyle(TukiPalette.orange).fontWeight(.bold).buttonStyle(.plain)
                }
                .font(.system(size: 17, weight: .semibold)).padding(.top, 12)
            }
            .padding(.horizontal, 34)
            .padding(.top, 25)
            .padding(.bottom, 20)
        }
        .background(.white)
    }

    private func socialButton(_ title: String, image: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 6) {
                Image(image).resizable().scaledToFit().frame(width: 20, height: 20)
                Text(title).font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
            }
            .frame(maxWidth: .infinity).frame(height: 56).overlay { RoundedRectangle(cornerRadius: 16).stroke(TukiPalette.border, lineWidth: 2) }
        }
        .buttonStyle(.plain).disabled(auth.isAuthenticating).opacity(auth.isAuthenticating ? 0.6 : 1)
    }
}

private struct UnifiedSignup: View {
    @ObservedObject var auth: AuthViewModel
    let onBack: () -> Void
    @State private var fullName = ""
    @State private var email = ""
    @State private var password = ""
    @State private var confirmation = ""
    @State private var localError: String?

    var body: some View {
        ScrollView {
            VStack(spacing: 12) {
                TukiLogoHeader(logoSize: 75, titleSize: 32)
                Text("Create an account").font(.system(size: 26, weight: .heavy))
                Text("Start your seamless commute today").foregroundStyle(TukiPalette.gray)
                TukiCompactFormField(label: "Full Name", text: $fullName)
                TukiCompactFormField(label: "Email", text: $email, keyboardType: .emailAddress)
                TukiCompactFormField(label: "Password", text: $password, isSecure: true)
                TukiCompactFormField(label: "Confirm Password", text: $confirmation, isSecure: true)
                if let error = localError ?? auth.errorMessage { Text(error).font(.system(size: 13, weight: .semibold)).foregroundStyle(TukiPalette.error) }
                TukiPrimaryButton(title: "Sign up", isLoading: auth.isAuthenticating, isEnabled: !auth.isAuthenticating) {
                    guard fullName.split(whereSeparator: { $0.isWhitespace }).count >= 2 else { localError = "Enter both your first and last name."; return }
                    guard email.contains("@") else { localError = "Enter a valid email address."; return }
                    guard password.count >= 8 else { localError = "Password must be at least 8 characters."; return }
                    guard password == confirmation else { localError = "Passwords do not match."; return }
                    localError = nil
                    Task { _ = await auth.register(fullName: fullName, email: email, password: password) }
                }
                Button("Already have an account? Log in", action: onBack).foregroundStyle(TukiPalette.orange).fontWeight(.bold).buttonStyle(.plain)
            }
            .padding(28)
        }
        .background(.white)
    }
}

private struct UnifiedForgotPassword: View {
    let onBack: () -> Void
    @State private var email = ""
    @State private var sent = false
    var body: some View {
        VStack(spacing: 20) {
            HStack { Button("‹", action: onBack); Spacer() }.font(.system(size: 22, weight: .bold))
            TukiLogoHeader()
            Text("Reset Password").font(.system(size: 26, weight: .heavy))
            Text("Enter your email to receive a reset link").foregroundStyle(TukiPalette.gray)
            TukiFormField(label: "Email", text: $email, keyboardType: .emailAddress)
            if sent { Text("Reset link sent! Check your inbox.").foregroundStyle(TukiPalette.teal).fontWeight(.semibold) }
            TukiPrimaryButton(title: sent ? "Sent!" : "Send Reset Link", isEnabled: !sent && email.contains("@")) { sent = true }
            Spacer()
        }
        .padding(34)
        .background(.white)
    }
}

private struct UnifiedHome: View {
    let name: String
    let currentLabel: String
    let areaLabel: String
    let isLocating: Bool
    let selectedDestination: TukiPlace?
    let canFindRoutes: Bool
    let onChangeOrigin: () -> Void
    let onPinDestination: () -> Void
    let onFindRoutes: () -> Void
    let onAI: () -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                Text("\(TukiInterfaceText.hello), \(name) 👋").font(.system(size: 15, weight: .semibold)).foregroundStyle(TukiPalette.gray)
                Text(TukiInterfaceText.whereToToday).font(.system(size: 25, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                Text(TukiInterfaceText.planTripOrAskAi).font(.system(size: 12)).foregroundStyle(TukiPalette.gray)

                currentLocationCard
                destinationCard
                aiCard
            }
            .padding(24)
        }
        .background(TukiPalette.cream)
    }

    // Ported from Android's `CurrentLocationCard` (screens/HomeScreen.kt).
    private var currentLocationCard: some View {
        HStack(spacing: 12) {
            ZStack {
                RoundedRectangle(cornerRadius: 18).fill(Color.white.opacity(0.42)).frame(width: 52, height: 52)
                Text("⊙").font(.system(size: 30, weight: .bold)).foregroundStyle(TukiPalette.teal)
            }
            VStack(alignment: .leading, spacing: 3) {
                Text(TukiInterfaceText.currentLocationUpper).font(.system(size: 11, weight: .heavy)).foregroundStyle(TukiPalette.teal)
                if isLocating {
                    HStack(spacing: 7) {
                        ProgressView().tint(TukiPalette.teal)
                        Text(TukiInterfaceText.locatingYou).font(.system(size: 16, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                    }
                } else {
                    Text(currentLabel).font(.system(size: 19, weight: .heavy)).foregroundStyle(TukiPalette.dark).lineLimit(1)
                }
                Text(areaLabel.isEmpty ? TukiInterfaceText.currentArea : areaLabel)
                    .font(.system(size: 13)).foregroundStyle(TukiPalette.gray).lineLimit(1)
            }
            Spacer(minLength: 0)
            Rectangle().fill(TukiPalette.teal.opacity(0.18)).frame(width: 1, height: 50)
            Button(action: onChangeOrigin) {
                VStack(spacing: 4) {
                    Text("✎").font(.system(size: 22, weight: .bold)).foregroundStyle(TukiPalette.teal)
                    Text(TukiInterfaceText.tapToChangeMultiline)
                        .font(.system(size: 11, weight: .heavy))
                        .foregroundStyle(TukiPalette.teal)
                        .multilineTextAlignment(.center)
                }
                .frame(width: 68)
            }
            .buttonStyle(.plain)
        }
        .padding(14)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 22))
    }

    // Ported from Android's `DestinationCard` (screens/HomeScreen.kt).
    private var destinationCard: some View {
        VStack(alignment: .leading, spacing: 0) {
            Button(action: onPinDestination) {
                HStack(alignment: .top, spacing: 12) {
                    ZStack {
                        RoundedRectangle(cornerRadius: 18).fill(TukiPalette.orange.opacity(0.16)).frame(width: 52, height: 52)
                        Text("📍").font(.system(size: 24))
                    }
                    VStack(alignment: .leading, spacing: 6) {
                        Text(TukiInterfaceText.destinationUpper).font(.system(size: 11, weight: .heavy)).foregroundStyle(TukiPalette.orange)
                        Text(selectedDestination?.name ?? TukiInterfaceText.whereAreYouGoing)
                            .font(.system(size: 18, weight: .heavy)).foregroundStyle(TukiPalette.dark).lineLimit(1)
                        HStack(spacing: 8) {
                            Text("⌕").font(.system(size: 16)).foregroundStyle(TukiPalette.dark)
                            Text(selectedDestination == nil ? TukiInterfaceText.searchOrEnterPlace : TukiInterfaceText.tapToChangeDestination)
                                .font(.system(size: 13)).foregroundStyle(TukiPalette.gray).lineLimit(1)
                            Spacer(minLength: 0)
                        }
                        .padding(.horizontal, 13)
                        .frame(height: 40)
                        .background(Color.white.opacity(0.7))
                        .clipShape(RoundedRectangle(cornerRadius: 14))
                    }
                    Spacer(minLength: 0)
                    Text("›").font(.system(size: 28, weight: .bold)).foregroundStyle(TukiPalette.dark)
                }
                .padding(14)
            }
            .buttonStyle(.plain)

            if selectedDestination != nil {
                Button(action: onFindRoutes) {
                    Text(TukiInterfaceText.findRoutes)
                        .font(.system(size: 15, weight: .heavy))
                        .foregroundStyle(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 11)
                        .background(canFindRoutes ? TukiPalette.orange : TukiPalette.orange.opacity(0.45))
                        .clipShape(RoundedRectangle(cornerRadius: 14))
                }
                .buttonStyle(.plain)
                .disabled(!canFindRoutes)
                .padding(.horizontal, 14)
                .padding(.bottom, 14)
            }
        }
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 22))
    }

    private var aiCard: some View {
        Button(action: onAI) {
            VStack(alignment: .leading, spacing: 12) {
                Text(TukiInterfaceText.askTukiAi).font(.system(size: 17, weight: .bold))
                Text(TukiInterfaceText.letAiFindBestWay).font(.system(size: 13)).opacity(0.75)
                Text("✨ Ask AI").font(.system(size: 14, weight: .bold)).frame(maxWidth: .infinity).padding(14).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 14))
            }
            .foregroundStyle(.white).padding(18).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.accentSurface).clipShape(RoundedRectangle(cornerRadius: 18))
        }
        .buttonStyle(.plain)
    }
}

private enum RecentFilterTab: CaseIterable {
    case all, completed, cancelled

    var label: String {
        switch self {
        case .all: TukiInterfaceText.all
        case .completed: TukiInterfaceText.completed
        case .cancelled: TukiInterfaceText.cancelled
        }
    }
}

/// Ported from Android's `RecentScreen.kt`: All/Completed/Cancelled filter tabs and an
/// inline favorite-star toggle (with a remove-confirmation dialog), replacing the earlier
/// plain list with no filtering and no favoriting at all.
private struct UnifiedRecent: View {
    let commutes: [RecentCommute]
    let guest: Bool
    let loading: Bool
    let error: String?
    let favoriteRecommendationIds: Set<String>
    let favoriteWorkingIds: Set<String>
    let onToggleFavorite: (RecentCommute) -> Void
    let onTap: (RecentCommute) -> Void

    @State private var filter: RecentFilterTab = .all
    @State private var pendingRemoval: RecentCommute?

    private var filtered: [RecentCommute] {
        switch filter {
        case .all: commutes
        case .completed: commutes.filter { $0.status.caseInsensitiveCompare("Completed") == .orderedSame }
        case .cancelled: commutes.filter { $0.status.caseInsensitiveCompare("Cancelled") == .orderedSame }
        }
    }

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 12) {
                Text(TukiInterfaceText.recentTrips).font(.system(size: 27, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                tabs
                if loading { ProgressView().tint(TukiPalette.teal) }
                if let error { Text(error).foregroundStyle(TukiPalette.error) }
                if filtered.isEmpty && !loading {
                    Text(guest ? TukiInterfaceText.signInToViewJourneys : TukiInterfaceText.noTripsYet).foregroundStyle(TukiPalette.gray)
                }
                ForEach(filtered) { commute in card(commute) }
            }
            .padding(30)
        }
        .background(TukiPalette.cream)
        .alert("Remove from favorites?", isPresented: Binding(
            get: { pendingRemoval != nil },
            set: { if !$0 { pendingRemoval = nil } }
        )) {
            Button("Remove", role: .destructive) {
                if let commute = pendingRemoval { onToggleFavorite(commute) }
                pendingRemoval = nil
            }
            Button("Keep Favorite", role: .cancel) { pendingRemoval = nil }
        } message: {
            Text("Are you sure you want to remove \(pendingRemoval?.origin ?? "") \u{2192} \(pendingRemoval?.destination ?? "") from your favorites?")
        }
    }

    private var tabs: some View {
        HStack(spacing: 2) {
            ForEach(RecentFilterTab.allCases, id: \.self) { tab in
                Button { filter = tab } label: {
                    Text(tab.label)
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(filter == tab ? .white : TukiPalette.dark)
                        .frame(maxWidth: .infinity)
                        .frame(height: 38)
                        .background(filter == tab ? TukiPalette.teal : Color.clear)
                        .clipShape(RoundedRectangle(cornerRadius: 19))
                }
                .buttonStyle(.plain)
            }
        }
        .padding(3)
        .background(TukiPalette.teal.opacity(0.12))
        .clipShape(RoundedRectangle(cornerRadius: 22))
    }

    private func card(_ commute: RecentCommute) -> some View {
        let completed = commute.status.caseInsensitiveCompare("Completed") == .orderedSame
        let canFavorite = !guest && commute.recommendationId != nil
        let isFavorite = commute.recommendationId.map(favoriteRecommendationIds.contains) ?? false
        let working = commute.recommendationId.map(favoriteWorkingIds.contains) ?? false

        return HStack(spacing: 8) {
            Button { onTap(commute) } label: {
                VStack(alignment: .leading, spacing: 4) {
                    Text("\(commute.origin) \u{2192} \(commute.destination)")
                        .font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark).lineLimit(1)
                    Text("\(recentDateText(commute.endedAt)) \u{00B7} \(commute.minutes) min \u{00B7} \u{20B1}\(Int(commute.totalFare.rounded()))")
                        .font(.system(size: 12)).foregroundStyle(TukiPalette.gray).lineLimit(1)
                    Text(TukiInterfaceText.status(commute.status.isEmpty ? (completed ? "Completed" : "Cancelled") : commute.status))
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(completed ? TukiPalette.teal : TukiPalette.error)
                        .padding(.horizontal, 10).padding(.vertical, 3)
                        .background((completed ? TukiPalette.teal : TukiPalette.error).opacity(0.12))
                        .clipShape(RoundedRectangle(cornerRadius: 10))
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.plain)

            Button {
                guard canFavorite, !working else { return }
                if isFavorite { pendingRemoval = commute } else { onToggleFavorite(commute) }
            } label: {
                if working {
                    ProgressView().frame(width: 26, height: 26)
                } else {
                    Text(isFavorite ? "\u{2605}" : "\u{2606}").font(.system(size: 22)).foregroundStyle(TukiPalette.orange)
                        .frame(width: 26, height: 26)
                }
            }
            .buttonStyle(.plain)
            .opacity(canFavorite ? 1 : 0.3)
            .disabled(!canFavorite || working)
        }
        .padding(14)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 16))
    }
}

/// Ported from Android's `FavoritesScreen.kt`: rows are tappable (previously not at all on
/// iOS), resolving into the matching completed trip's full detail via history lookup —
/// mirrors `FavoriteRouteDetailsHost`.
private struct UnifiedFavorites: View {
    let routes: [FavoriteRoute]
    let guest: Bool
    let onTap: (FavoriteRoute) -> Void

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 12) {
                Text(TukiInterfaceText.favorites).font(.system(size: 27, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                if routes.isEmpty {
                    Text(guest ? TukiInterfaceText.signInFavorites : TukiInterfaceText.noFavoriteRoutes).foregroundStyle(TukiPalette.gray)
                }
                ForEach(routes) { route in
                    Button { onTap(route) } label: {
                        HStack {
                            VStack(alignment: .leading, spacing: 3) {
                                Text("\(route.origin) \u{2192} \(route.destination)").font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark).lineLimit(1)
                                Text("\(route.totalMinutes) min \u{00B7} \u{20B1}\(Int(route.totalFare.rounded())) \u{00B7} Used \(route.timesUsed)\u{00D7}")
                                    .font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                            }
                            Spacer(minLength: 0)
                            Text("\u{203A}").font(.system(size: 22, weight: .bold)).foregroundStyle(TukiPalette.gray)
                        }
                        .padding(16).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 16))
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(30)
        }
        .background(TukiPalette.cream)
    }
}

func recentDateText(_ value: String?) -> String {
    guard let value, !value.isEmpty else {
        return TukiInterfaceText.isFilipino ? "Kamakailang biyahe" : "Recent trip"
    }
    let formatter = ISO8601DateFormatter()
    var date = formatter.date(from: value)
    if date == nil {
        formatter.formatOptions.insert(.withFractionalSeconds)
        date = formatter.date(from: value)
    }
    guard let date else {
        return TukiInterfaceText.isFilipino ? "Kamakailang biyahe" : "Recent trip"
    }
    let display = DateFormatter()
    display.dateFormat = "MMM d, yyyy"
    return display.string(from: date)
}

private struct UnifiedBottomBar: View {
    @Binding var tab: UnifiedTab
    var body: some View {
        HStack {
            ForEach(UnifiedTab.allCases, id: \.self) { item in
                Button { tab = item } label: {
                    VStack(spacing: 4) {
                        Image(item.image).renderingMode(.template).resizable().scaledToFit().frame(width: 24, height: 24)
                        Text(item.label).font(.system(size: 12, weight: .semibold))
                    }
                    .foregroundStyle(tab == item ? TukiPalette.teal : TukiPalette.gray).frame(maxWidth: .infinity)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.vertical, 12)
        .background(.white)
    }
}
