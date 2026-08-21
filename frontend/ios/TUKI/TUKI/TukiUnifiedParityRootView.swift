import CoreLocation
import SwiftUI

struct TukiUnifiedParityRootView: View {
    @StateObject private var auth = AuthViewModel()
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
                        onGuest: { auth.continueAsGuest() }
                    )
                case .signup:
                    UnifiedSignup(auth: auth) { entry = .login }
                case .forgotPassword:
                    UnifiedForgotPassword { entry = .login }
                }
            }
        }
        .preferredColorScheme(.light)
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
    case destination(String, CLLocationCoordinate2D?)
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
    @State private var recentLoading = false
    @State private var recentError: String?

    private let api: TukiPlatformAPI?
    private let historyAPI: TukiHistoryAPI?
    private let assistantAPI: TukiAssistantAPI?

    init(auth: AuthViewModel) {
        self.auth = auth
        let store = KeychainTukiCredentialStore()
        if let configuration = try? AppConfiguration.load() {
            api = TukiPlatformAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
            historyAPI = TukiHistoryAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
            assistantAPI = TukiAssistantAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
        } else {
            api = nil
            historyAPI = nil
            assistantAPI = nil
        }
    }

    var body: some View {
        Group {
            switch screen {
            case .tabs:
                tabView

            case .destination(let originName, let coordinate):
                TukiUnifiedDestinationSearchView(
                    api: api,
                    location: location,
                    initialOriginName: originName,
                    initialOrigin: coordinate,
                    onBack: { screen = .tabs },
                    onFind: { name, origin, destination in
                        screen = .routes(name, origin, destination, nil)
                    }
                )

            case .ai:
                TukiParityAIChat(
                    userName: auth.currentUserProfile?.greetingName ?? (auth.isGuest ? "Guest" : "User"),
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
                    location: location,
                    originName: originName,
                    destination: destination,
                    choice: choice,
                    initialSnapshot: snapshot,
                    isGuest: guest,
                    onEnded: { returnHome() }
                )

            case .commute(let commute):
                TukiUnifiedCommuteDetailView(commute: commute) { screen = .tabs }

            case .editProfile:
                TukiUnifiedEditProfileView(auth: auth) { screen = .tabs; tab = .profile }

            case .privacySecurity:
                TukiUnifiedPrivacySecurityView(
                    onBack: { screen = .tabs; tab = .profile },
                    onChangePassword: { screen = .changePassword },
                    onPermissions: { screen = .permissions },
                    onPrivacyPolicy: { screen = .privacyPolicy }
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
                TukiUnifiedAboutView { screen = .tabs; tab = .profile }

            case .settings:
                TukiUnifiedSettingsView(
                    onBack: { screen = .tabs; tab = .profile },
                    onPrivacyPolicy: { screen = .privacyPolicy },
                    onLanguage: { screen = .language },
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
                        name: auth.currentUserProfile?.greetingName ?? (auth.isGuest ? "Guest" : "User"),
                        currentLabel: currentLabel,
                        onPin: { screen = .destination(currentLabel, location.currentLocation?.coordinate) },
                        onAI: { screen = .ai }
                    )
                case .recent:
                    UnifiedRecent(
                        commutes: recent,
                        guest: auth.isGuest,
                        loading: recentLoading,
                        error: recentError,
                        onTap: { screen = .commute($0) }
                    )
                case .favorites:
                    UnifiedFavorites(routes: favorites, guest: auth.isGuest)
                case .profile:
                    TukiUnifiedProfileView(
                        auth: auth,
                        onEdit: { if !auth.isGuest { screen = .editProfile } },
                        onPrivacy: { if !auth.isGuest { screen = .privacySecurity } },
                        onLanguage: { screen = .language },
                        onAbout: { screen = .about },
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
    }

    private func returnHome() {
        screen = .tabs
        tab = .home
    }

    private func refreshLocation() async {
        guard let current = await location.requestCurrentLocation() else {
            currentLabel = location.errorMessage ?? "Unable to detect location"
            return
        }
        if let api,
           case .success(let place) = await api.reverseGeocode(
                lat: current.coordinate.latitude,
                lon: current.coordinate.longitude
           ) {
            currentLabel = place.name
        } else {
            currentLabel = "Current location"
        }
    }

    private func refreshTab() async {
        if tab == .profile {
            if !auth.isGuest { _ = await auth.refreshProfile() }
            return
        }
        guard !auth.isGuest else {
            recent = []
            favorites = []
            recentLoading = false
            recentError = nil
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
        } else if tab == .favorites, let historyAPI {
            if case .success(let values) = await historyAPI.favorites() { favorites = values }
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

                Button(action: onGuest) {
                    Text("Continue as Guest").font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark).frame(maxWidth: .infinity).frame(height: 56).overlay { RoundedRectangle(cornerRadius: 16).stroke(TukiPalette.border, lineWidth: 2) }
                }
                .buttonStyle(.plain).disabled(auth.isAuthenticating).padding(.top, 12)

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
    let onPin: () -> Void
    let onAI: () -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                Text("Hello, \(name) 👋").font(.system(size: 15, weight: .semibold)).foregroundStyle(TukiPalette.gray)
                Text("Where are you going?").font(.system(size: 25, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                Text("Pick a destination yourself, or tell our AI where you want to go.").font(.system(size: 12)).foregroundStyle(TukiPalette.gray)
                HStack(spacing: 10) {
                    Circle().fill(TukiPalette.teal).frame(width: 9, height: 9)
                    Text("\(currentLabel) (current location)").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
                }
                .padding(16).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14))
                actionCard("Pin your destination", "Search or drop a pin on the map if you already know where you're headed.", "🗺️ Open map", onPin)
                actionCard("Ask our AI", "Describe where you want to go, your budget, or whether you prefer the cheapest or fastest commute.", "✨ Ask AI", onAI)
            }
            .padding(24)
        }
        .background(TukiPalette.cream)
    }

    private func actionCard(_ title: String, _ subtitle: String, _ actionTitle: String, _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 12) {
                Text(title).font(.system(size: 17, weight: .bold))
                Text(subtitle).font(.system(size: 13)).opacity(0.75)
                Text(actionTitle).font(.system(size: 14, weight: .bold)).frame(maxWidth: .infinity).padding(14).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 14))
            }
            .foregroundStyle(.white).padding(18).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.dark).clipShape(RoundedRectangle(cornerRadius: 18))
        }
        .buttonStyle(.plain)
    }
}

private struct UnifiedRecent: View {
    let commutes: [RecentCommute]
    let guest: Bool
    let loading: Bool
    let error: String?
    let onTap: (RecentCommute) -> Void
    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 12) {
                Text("Recent").font(.system(size: 27, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                if loading { ProgressView().tint(TukiPalette.teal) }
                if let error { Text(error).foregroundStyle(TukiPalette.error) }
                if commutes.isEmpty && !loading { Text(guest ? "Sign in to view your recent journeys." : "No completed or cancelled trips yet.").foregroundStyle(TukiPalette.gray) }
                ForEach(commutes) { commute in
                    Button { onTap(commute) } label: {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("\(commute.origin) to \(commute.destination)").font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark)
                            Text("\(commute.status) · \(commute.legs) legs · \(commute.minutes) min").font(.system(size: 13)).foregroundStyle(TukiPalette.teal)
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

private struct UnifiedFavorites: View {
    let routes: [FavoriteRoute]
    let guest: Bool
    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 12) {
                Text("Favorites").font(.system(size: 27, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                if routes.isEmpty { Text(guest ? "Sign in to save favorite routes." : "No favorite routes yet.").foregroundStyle(TukiPalette.gray) }
                ForEach(routes) { route in
                    VStack(alignment: .leading, spacing: 3) {
                        Text("\(route.origin) to \(route.destination)").font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark)
                        Text("Used \(route.timesUsed) times · \(route.note)").font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                    }
                    .padding(16).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 16))
                }
            }
            .padding(30)
        }
        .background(TukiPalette.cream)
    }
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
