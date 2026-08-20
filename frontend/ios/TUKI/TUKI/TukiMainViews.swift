import Foundation
import SwiftUI

struct TukiMainView: View {
    let isGuest: Bool
    let onSignOut: () -> Void

    @State private var selectedTab = TukiTab.home
    @State private var overlay: TukiMainOverlay?
    @State private var recentCommutes: [RecentCommute] = []
    @State private var favorites: [FavoriteRoute] = []
    @State private var isLoadingRecent = false
    @State private var isLoadingFavorites = false
    @State private var recentError: String?
    @State private var favoritesError: String?

    private let journeyAPI: TukiJourneyAPI?

    init(isGuest: Bool = false, onSignOut: @escaping () -> Void) {
        self.isGuest = isGuest
        self.onSignOut = onSignOut

        if let configuration = try? AppConfiguration.load() {
            self.journeyAPI = TukiJourneyAPI(
                baseURL: configuration.backendBaseURL,
                credentialStore: KeychainTukiCredentialStore()
            )
        } else {
            self.journeyAPI = nil
        }
    }

    var body: some View {
        Group {
            switch overlay {
            case .some(.commute(let commute)):
                TukiCommuteDetailView(commute: commute) { overlay = nil }
            case .some(.destinationSearch(let origin)):
                TukiDestinationSearchView(
                    origin: origin,
                    onBack: { overlay = nil },
                    onSearch: { destination in
                        overlay = .routes(origin: origin, destination: destination)
                    }
                )
            case .some(.askAI):
                TukiAskAIView { overlay = nil }
            case .some(.routes(let origin, let destination)):
                TukiRouteResultsView(
                    origin: origin,
                    destination: destination,
                    onBack: { overlay = nil },
                    onRouteSelected: { option in
                        overlay = .activeTrip(origin: origin, destination: destination, option: option)
                    }
                )
            case .some(.activeTrip(let origin, let destination, let option)):
                TukiActiveTripView(origin: origin, destination: destination, option: option) {
                    overlay = nil
                    selectedTab = .home
                }
            case .none:
                VStack(spacing: 0) {
                    tabContent
                        .frame(maxWidth: .infinity, maxHeight: .infinity)

                    TukiBottomBar(selectedTab: $selectedTab)
                }
                .background(TukiPalette.cream.ignoresSafeArea())
            }
        }
        .task(id: isGuest) {
            await loadPersonalData()
        }
    }

    @ViewBuilder
    private var tabContent: some View {
        switch selectedTab {
        case .home:
            TukiHomeView(
                isGuest: isGuest,
                onPinDestination: { origin in
                    overlay = .destinationSearch(origin: origin)
                },
                onAskAI: {
                    overlay = .askAI
                }
            )
        case .recent:
            TukiRecentView(
                commutes: recentCommutes,
                isGuest: isGuest,
                isLoading: isLoadingRecent,
                errorMessage: recentError,
                onCommute: { overlay = .commute($0) }
            )
        case .favorites:
            TukiFavoritesView(
                favorites: favorites,
                isGuest: isGuest,
                isLoading: isLoadingFavorites,
                errorMessage: favoritesError
            )
        case .profile:
            TukiProfileView(
                isGuest: isGuest,
                onSignOut: onSignOut
            )
        }
    }

    @MainActor
    private func loadPersonalData() async {
        guard !isGuest else {
            recentCommutes = []
            favorites = []
            recentError = nil
            favoritesError = nil
            isLoadingRecent = false
            isLoadingFavorites = false
            return
        }

        guard let journeyAPI else {
            recentError = "TUKI history is not configured."
            favoritesError = "TUKI favorites are not configured."
            return
        }

        isLoadingRecent = true
        switch await journeyAPI.recentJourneys() {
        case .success(let commutes):
            recentCommutes = commutes
            recentError = nil
        case .failure(let error):
            recentCommutes = []
            recentError = error.message
        }
        isLoadingRecent = false

        isLoadingFavorites = true
        switch await journeyAPI.favorites() {
        case .success(let loadedFavorites):
            favorites = loadedFavorites
            favoritesError = nil
        case .failure(let error):
            favorites = []
            favoritesError = error.message
        }
        isLoadingFavorites = false
    }
}

private enum TukiMainOverlay {
    case commute(RecentCommute)
    case destinationSearch(origin: String)
    case askAI
    case routes(origin: String, destination: String)
    case activeTrip(origin: String, destination: String, option: RouteOption)
}

private enum TukiTab: CaseIterable, Hashable {
    case home
    case recent
    case favorites
    case profile

    var label: String {
        switch self {
        case .home: return "Home"
        case .recent: return "Recent"
        case .favorites: return "Favorites"
        case .profile: return "Profile"
        }
    }

    var imageName: String {
        switch self {
        case .home: return "HomeIcon"
        case .recent: return "RecentIcon"
        case .favorites: return "FavoriteIcon"
        case .profile: return "ProfileIcon"
        }
    }
}

private struct TukiBottomBar: View {
    @Binding var selectedTab: TukiTab

    var body: some View {
        HStack(spacing: 0) {
            ForEach(TukiTab.allCases, id: \.self) { tab in
                Button {
                    selectedTab = tab
                } label: {
                    VStack(spacing: 4) {
                        Image(tab.imageName)
                            .renderingMode(.template)
                            .resizable()
                            .scaledToFit()
                            .frame(width: 24, height: 24)

                        Text(tab.label)
                            .font(.system(size: 12, weight: .semibold))
                    }
                    .foregroundStyle(selectedTab == tab ? TukiPalette.teal : TukiPalette.gray)
                    .frame(maxWidth: .infinity)
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityAddTraits(selectedTab == tab ? .isSelected : [])
            }
        }
        .padding(.horizontal, 24)
        .padding(.top, 14)
        .padding(.bottom, 8)
        .background(.white)
    }
}

private struct TukiHomeView: View {
    let isGuest: Bool
    let onPinDestination: (String) -> Void
    let onAskAI: () -> Void

    private let currentLocation = "Current location"

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(isGuest ? "Hello, Guest 👋" : "Hello, User 👋")
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(TukiPalette.gray)
                .padding(.top, 24)

            Text("Where are you going?")
                .font(.system(size: 25, weight: .heavy))
                .foregroundStyle(TukiPalette.dark)
                .padding(.top, 4)

            Text("Pick a destination yourself, or tell our AI where you want to go.")
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(TukiPalette.gray)
                .padding(.top, 6)

            TukiCurrentLocationPill(label: currentLocation)
                .padding(.top, 14)

            Button {
                onPinDestination(currentLocation)
            } label: {
                TukiPinDestinationCard()
            }
            .buttonStyle(.plain)
            .padding(.top, 12)

            Button(action: onAskAI) {
                TukiAskAICard()
            }
            .buttonStyle(.plain)
            .padding(.top, 12)

            Spacer(minLength: 0)
        }
        .padding(.horizontal, 24)
        .background(TukiPalette.cream)
    }
}

private struct TukiCurrentLocationPill: View {
    let label: String

    var body: some View {
        HStack(spacing: 12) {
            Circle()
                .fill(TukiPalette.teal)
                .frame(width: 11, height: 11)

            Text("\(label) (current location)")
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(TukiPalette.dark)

            Spacer()
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
    }
}

private struct TukiPinDestinationCard: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 12) {
                TukiIconBadge(emoji: "📍")

                Text("Pin your destination")
                    .font(.system(size: 17, weight: .bold))
                    .foregroundStyle(.white)
            }

            Text("Search or drop a pin on the map if you already know where you're headed.")
                .font(.system(size: 13))
                .foregroundStyle(.white.opacity(0.75))
                .padding(.top, 10)

            HStack(spacing: 10) {
                Text("🔍")
                    .font(.system(size: 14))
                Text("Type or search a place")
                    .font(.system(size: 14))
                    .foregroundStyle(.white.opacity(0.85))
                Spacer()
            }
            .padding(.horizontal, 14)
            .frame(height: 48)
            .background(.white.opacity(0.08))
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .padding(.top, 16)

            HStack {
                Spacer()
                Text("🗺️ Open map")
                    .font(.system(size: 14))
                    .foregroundStyle(.white.opacity(0.85))
                Spacer()
            }
            .frame(height: 48)
            .background(.white.opacity(0.08))
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .padding(.top, 10)
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(TukiPalette.dark)
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }
}

private struct TukiAskAICard: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 12) {
                TukiIconBadge(emoji: "✨")

                Text("Ask our AI")
                    .font(.system(size: 17, weight: .bold))
                    .foregroundStyle(.white)

                Text("NEW")
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 3)
                    .background(TukiPalette.orange)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
            }

            Text("Describe where you want to go and we'll figure out the location and commute.")
                .font(.system(size: 13))
                .foregroundStyle(.white.opacity(0.75))
                .padding(.top, 10)

            HStack(spacing: 10) {
                Text("💬")
                    .font(.system(size: 14))
                Text("\"Yung malapit sa SM Clark...\"")
                    .font(.system(size: 13))
                    .foregroundStyle(.white.opacity(0.85))
                Spacer()
            }
            .padding(.horizontal, 14)
            .frame(height: 48)
            .background(TukiPalette.teal.opacity(0.35))
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .padding(.top, 16)

            HStack {
                Spacer()
                Text("✨ Ask AI")
                    .font(.system(size: 14, weight: .bold))
                    .foregroundStyle(.white)
                Spacer()
            }
            .frame(height: 48)
            .background(TukiPalette.orange)
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .padding(.top, 10)
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(TukiPalette.dark)
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }
}

private struct TukiIconBadge: View {
    let emoji: String

    var body: some View {
        Text(emoji)
            .font(.system(size: 16))
            .frame(width: 34, height: 34)
            .background(.white.opacity(0.12))
            .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}

private struct TukiRecentCommuteCard: View {
    let commute: RecentCommute

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("\(commute.origin) to \(commute.destination)")
                .font(.system(size: 17, weight: .bold))
                .foregroundStyle(TukiPalette.dark)

            Text(summary)
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(TukiPalette.teal)

            if commute.wasRerouted {
                Text(commute.rerouteCount > 1 ? "Rerouted \(commute.rerouteCount) times" : "Rerouted")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(TukiPalette.gray)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
    }

    private var summary: String {
        var parts: [String] = []
        if !commute.status.isEmpty {
            parts.append(commute.status)
        }
        parts.append("\(commute.legs) legs")
        parts.append("\(commute.minutes) min")
        return parts.joined(separator: " · ")
    }
}

private struct TukiRecentSection: Identifiable {
    let title: String
    let items: [RecentCommute]

    var id: String { title }
}

private struct TukiRecentView: View {
    let commutes: [RecentCommute]
    let isGuest: Bool
    let isLoading: Bool
    let errorMessage: String?
    let onCommute: (RecentCommute) -> Void

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                Text("Recent")
                    .font(.system(size: 27, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.bottom, 24)

                if isLoading {
                    HStack {
                        Spacer()
                        ProgressView().tint(TukiPalette.teal)
                        Spacer()
                    }
                    .padding(.vertical, 48)
                } else if let errorMessage, !errorMessage.isEmpty {
                    Text(errorMessage)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.error)
                } else if commutes.isEmpty {
                    Text(isGuest ? "Sign in to view your recent journeys." : "No completed or cancelled trips yet.")
                        .font(.system(size: 14))
                        .foregroundStyle(TukiPalette.gray)
                } else {
                    ForEach(groupedSections) { section in
                        Text(section.title.uppercased())
                            .font(.system(size: 13, weight: .heavy))
                            .foregroundStyle(TukiPalette.gray)
                            .padding(.bottom, 10)

                        ForEach(section.items) { commute in
                            Button { onCommute(commute) } label: {
                                TukiRecentCommuteCard(commute: commute)
                            }
                            .buttonStyle(.plain)
                            .padding(.bottom, 12)
                        }

                        Spacer().frame(height: 10)
                    }
                }
            }
            .padding(.horizontal, 30)
            .padding(.top, 30)
            .padding(.bottom, 20)
        }
        .background(TukiPalette.cream)
    }

    private var groupedSections: [TukiRecentSection] {
        var order: [String] = []
        var grouped: [String: [RecentCommute]] = [:]
        for commute in commutes {
            let key = commute.dateGroup.isEmpty ? "Earlier" : commute.dateGroup
            if grouped[key] == nil {
                order.append(key)
                grouped[key] = []
            }
            grouped[key, default: []].append(commute)
        }
        return order.map { TukiRecentSection(title: $0, items: grouped[$0] ?? []) }
    }
}

private struct TukiFavoritesView: View {
    let favorites: [FavoriteRoute]
    let isGuest: Bool
    let isLoading: Bool
    let errorMessage: String?

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                Text("Favorites")
                    .font(.system(size: 27, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.bottom, 24)

                Text("STARRED ROUTES")
                    .font(.system(size: 13, weight: .heavy))
                    .foregroundStyle(TukiPalette.gray)
                    .padding(.bottom, 10)

                if isLoading {
                    Text("Loading favorite routes...")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.bottom, 12)
                } else if let errorMessage, !errorMessage.isEmpty {
                    Text(errorMessage)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.bottom, 12)
                } else if isGuest {
                    Text("Sign in to save favorite routes.")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.bottom, 12)
                } else if favorites.isEmpty {
                    Text("No favorite routes yet.")
                        .font(.system(size: 14))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.bottom, 12)
                } else {
                    ForEach(favorites) { route in
                        TukiFavoriteRow(route: route)
                            .padding(.bottom, 12)
                    }
                }

                VStack(alignment: .leading, spacing: 4) {
                    Text("Tip")
                        .font(.system(size: 18, weight: .bold))
                    Text("Tap the star on any route to save it here")
                        .font(.system(size: 14))
                        .opacity(0.85)
                }
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(20)
                .background(TukiPalette.teal)
                .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
                .padding(.top, 10)
            }
            .padding(.horizontal, 30)
            .padding(.top, 30)
            .padding(.bottom, 20)
        }
        .background(TukiPalette.cream)
    }
}

private struct TukiFavoriteRow: View {
    let route: FavoriteRoute

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text("\(route.origin) to \(route.destination)")
                    .font(.system(size: 17, weight: .bold))
                    .foregroundStyle(TukiPalette.dark)
                Text("Used \(route.timesUsed) times · \(route.note)")
                    .font(.system(size: 13))
                    .foregroundStyle(TukiPalette.gray)
            }
            Spacer()
            Image("FavoriteIcon")
                .resizable()
                .scaledToFit()
                .frame(width: 22, height: 22)
        }
        .padding(16)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
    }
}

private enum TukiProfilePage {
    case overview
    case editProfile
    case privacySecurity
    case changePassword
    case language
}

private struct TukiProfileView: View {
    let isGuest: Bool
    let onSignOut: () -> Void

    @State private var page = TukiProfilePage.overview
    @State private var fullName = "Juan Dela Cruz"
    @State private var email = "juan.delacruz@gmail.com"
    @State private var phone = ""

    var body: some View {
        switch page {
        case .overview:
            overview
        case .editProfile:
            TukiEditProfileView(fullName: $fullName, email: email, phone: $phone, onBack: { page = .overview })
        case .privacySecurity:
            TukiPrivacySecurityView(onBack: { page = .overview }, onChangePassword: { page = .changePassword })
        case .changePassword:
            TukiChangePasswordView(onBack: { page = .privacySecurity })
        case .language:
            TukiLanguageView(onBack: { page = .overview })
        }
    }

    private var overview: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                VStack(spacing: 0) {
                    Text(initials)
                        .font(.system(size: 30, weight: .heavy))
                        .foregroundStyle(.white)
                        .frame(width: 90, height: 90)
                        .background(TukiPalette.teal)
                        .clipShape(Circle())

                    Text(isGuest ? "Guest" : fullName)
                        .font(.system(size: 21, weight: .heavy))
                        .foregroundStyle(TukiPalette.dark)
                        .padding(.top, 14)

                    Text(isGuest ? "Guest mode" : email)
                        .font(.system(size: 15))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.top, 4)
                }
                .frame(maxWidth: .infinity)
                .padding(.bottom, 24)

                HStack(spacing: 12) {
                    TukiProfileStat(value: isGuest ? "0" : "18", label: "TRIPS TAKEN")
                    TukiProfileStat(value: isGuest ? "0" : "2", label: "FAVORITES")
                }

                Text("ACCOUNT")
                    .font(.system(size: 14, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.top, 28)
                    .padding(.bottom, 12)

                TukiAccountRow(imageName: "EditProfileIcon", title: "Edit Profile", subtitle: "Name, email, phone", action: { page = .editProfile })
                TukiAccountRow(imageName: "PrivacyIcon", title: "Privacy & Security", subtitle: "Password, data settings", action: { page = .privacySecurity })
                    .padding(.top, 12)
                TukiAccountRow(imageName: "LanguageIcon", title: "Language", subtitle: "English", action: { page = .language })
                    .padding(.top, 12)

                Button(action: onSignOut) {
                    Text("Sign Out")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(TukiPalette.orange)
                        .frame(maxWidth: .infinity)
                        .frame(height: 48)
                        .background(TukiPalette.creamCard)
                        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                }
                .buttonStyle(.plain)
                .padding(.top, 12)
            }
            .padding(.horizontal, 30)
            .padding(.top, 30)
            .padding(.bottom, 20)
        }
        .background(TukiPalette.cream)
    }

    private var initials: String {
        if isGuest { return "G" }
        let parts = fullName.split(separator: " ")
        return parts.prefix(2).compactMap { $0.first }.map(String.init).joined().uppercased()
    }
}

private struct TukiProfileStat: View {
    let value: String
    let label: String
    var body: some View {
        VStack(spacing: 2) {
            Text(value).font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark)
            Text(label).font(.system(size: 11, weight: .semibold)).foregroundStyle(TukiPalette.gray).lineLimit(1)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 16)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
    }
}

private struct TukiAccountRow: View {
    let imageName: String
    let title: String
    let subtitle: String
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 14) {
                Image(imageName).resizable().scaledToFit().frame(width: 40, height: 40)
                VStack(alignment: .leading, spacing: 2) {
                    Text(title).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark)
                    Text(subtitle).font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                }
                Spacer()
                Text("›").font(.system(size: 20, weight: .bold)).foregroundStyle(TukiPalette.gray)
            }
            .padding(14)
            .background(TukiPalette.creamCard)
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
        .buttonStyle(.plain)
    }
}

private struct TukiEditProfileView: View {
    @Binding var fullName: String
    let email: String
    @Binding var phone: String
    let onBack: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            TukiSubpageHeader(title: "Edit profile", onBack: onBack)
            ScrollView {
                VStack(spacing: 0) {
                    ZStack(alignment: .bottomTrailing) {
                        Text(initials).font(.system(size: 34, weight: .heavy)).foregroundStyle(.white)
                            .frame(width: 100, height: 100).background(TukiPalette.teal).clipShape(Circle())
                        Text("📷").font(.system(size: 14)).frame(width: 32, height: 32)
                            .background(TukiPalette.orange).clipShape(Circle())
                    }
                    Text("Change photo").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.teal)
                        .padding(.top, 10).padding(.bottom, 28)
                    TukiEditableField(label: "Full name", text: $fullName)
                    TukiDisabledField(label: "Email", value: email).padding(.top, 18)
                    Text("Email is tied to your login and can't be changed here yet.")
                        .font(.system(size: 11)).foregroundStyle(TukiPalette.gray)
                        .frame(maxWidth: .infinity, alignment: .leading).padding(.top, 4)
                    TukiEditableField(label: "Phone", text: $phone).padding(.top, 18)
                    Button(action: onBack) {
                        Text("Save changes").font(.system(size: 16, weight: .bold)).foregroundStyle(.white)
                            .frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange)
                            .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                    }
                    .buttonStyle(.plain).padding(.top, 28)
                }
                .padding(.horizontal, 30).padding(.bottom, 30)
            }
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }

    private var initials: String {
        let parts = fullName.split(separator: " ")
        let result = parts.prefix(2).compactMap { $0.first }.map(String.init).joined().uppercased()
        return result.isEmpty ? "?" : result
    }
}

private struct TukiEditableField: View {
    let label: String
    @Binding var text: String
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(label).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.dark)
            TextField("", text: $text).font(.system(size: 16)).foregroundStyle(TukiPalette.dark)
                .padding(.horizontal, 14).frame(height: 50).background(TukiPalette.creamCard)
                .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
    }
}

private struct TukiDisabledField: View {
    let label: String
    let value: String
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(label).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.dark)
            Text(value).font(.system(size: 16)).foregroundStyle(TukiPalette.gray)
                .frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal, 14).frame(height: 50)
                .background(TukiPalette.creamCard.opacity(0.6)).clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
    }
}

private struct TukiPrivacySecurityView: View {
    let onBack: () -> Void
    let onChangePassword: () -> Void
    @State private var twoFactorEnabled = false
    var body: some View {
        VStack(spacing: 0) {
            TukiSubpageHeader(title: "Privacy & Security", onBack: onBack)
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    TukiSettingsActionRow(title: "Change password", subtitle: "Update your account password", action: onChangePassword)
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Two-factor authentication").font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark)
                            Text("Add an extra layer of security").font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                        }
                        Spacer()
                        Toggle("", isOn: $twoFactorEnabled).labelsHidden().tint(TukiPalette.teal)
                    }
                    .padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                    VStack(alignment: .leading, spacing: 8) {
                        Text("DATA & ACCOUNT").font(.system(size: 13, weight: .heavy)).foregroundStyle(TukiPalette.gray)
                        Button(action: {}) {
                            Text("Delete account").font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.error)
                                .frame(maxWidth: .infinity, alignment: .leading).padding(14).background(TukiPalette.creamCard)
                                .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                        }.buttonStyle(.plain)
                    }.padding(.top, 16)
                }
                .padding(.horizontal, 30).padding(.bottom, 30)
            }
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiChangePasswordView: View {
    let onBack: () -> Void
    @State private var currentPassword = ""
    @State private var newPassword = ""
    @State private var confirmPassword = ""
    var body: some View {
        VStack(spacing: 0) {
            TukiSubpageHeader(title: "Change password", onBack: onBack)
            ScrollView {
                VStack(spacing: 18) {
                    TukiSecureSettingsField(label: "Current password", text: $currentPassword)
                    TukiSecureSettingsField(label: "New password", text: $newPassword)
                    TukiSecureSettingsField(label: "Confirm new password", text: $confirmPassword)
                    Button(action: onBack) {
                        Text("Change password").font(.system(size: 16, weight: .bold)).foregroundStyle(.white)
                            .frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange)
                            .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                    }.buttonStyle(.plain).padding(.top, 8)
                }
                .padding(.horizontal, 30).padding(.bottom, 30)
            }
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiSecureSettingsField: View {
    let label: String
    @Binding var text: String
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(label).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.dark)
            SecureField("", text: $text).padding(.horizontal, 14).frame(height: 50).background(TukiPalette.creamCard)
                .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
    }
}

private struct TukiLanguageView: View {
    let onBack: () -> Void
    @State private var language = "English"
    var body: some View {
        VStack(spacing: 0) {
            TukiSubpageHeader(title: "Language", onBack: onBack)
            VStack(alignment: .leading, spacing: 12) {
                Text("APP LANGUAGE").font(.system(size: 13, weight: .heavy)).foregroundStyle(TukiPalette.gray)
                ForEach(["English", "Filipino"], id: \.self) { option in
                    Button { language = option } label: {
                        HStack {
                            Text(option).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark)
                            Spacer()
                            Circle().stroke(TukiPalette.teal, lineWidth: 2).frame(width: 20, height: 20).overlay {
                                if language == option { Circle().fill(TukiPalette.teal).frame(width: 12, height: 12) }
                            }
                        }
                        .padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                    }.buttonStyle(.plain)
                }
                Button(action: onBack) {
                    Text("Save language").font(.system(size: 16, weight: .bold)).foregroundStyle(.white)
                        .frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange)
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                }.buttonStyle(.plain).padding(.top, 12)
                Spacer()
            }
            .padding(.horizontal, 30).padding(.bottom, 30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiSettingsActionRow: View {
    let title: String
    let subtitle: String
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(title).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark)
                    Text(subtitle).font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                }
                Spacer()
                Text("›").font(.system(size: 20, weight: .bold)).foregroundStyle(TukiPalette.gray)
            }
            .padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }.buttonStyle(.plain)
    }
}

private struct TukiSubpageHeader: View {
    let title: String
    let onBack: () -> Void
    var body: some View {
        HStack(spacing: 14) {
            Button(action: onBack) {
                Text("‹").font(.system(size: 22, weight: .bold)).foregroundStyle(TukiPalette.dark)
                    .frame(width: 38, height: 38).background(TukiPalette.creamCard)
                    .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
            }.buttonStyle(.plain)
            Text(title).font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark)
            Spacer()
        }
        .padding(.horizontal, 30).padding(.top, 30).padding(.bottom, 28)
    }
}

private struct TukiDestinationSearchView: View {
    let origin: String
    let onBack: () -> Void
    let onSearch: (String) -> Void
    @State private var query = ""
    var body: some View {
        VStack(spacing: 0) {
            TukiSubpageHeader(title: "Choose destination", onBack: onBack)
            VStack(alignment: .leading, spacing: 12) {
                Text("FROM").font(.system(size: 12, weight: .heavy)).foregroundStyle(TukiPalette.gray)
                TukiCurrentLocationPill(label: origin)
                Text("DESTINATION").font(.system(size: 12, weight: .heavy)).foregroundStyle(TukiPalette.gray).padding(.top, 8)
                HStack(spacing: 10) {
                    Text("🔍")
                    TextField("Type or search a place", text: $query).submitLabel(.search).onSubmit(submit)
                }
                .padding(.horizontal, 14).frame(height: 52).background(TukiPalette.creamCard)
                .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                Button(action: submit) {
                    Text("Search routes").font(.system(size: 16, weight: .bold)).foregroundStyle(.white)
                        .frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange)
                        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                }.buttonStyle(.plain)
                Spacer()
            }
            .padding(.horizontal, 30).padding(.bottom, 30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
    private func submit() {
        let destination = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !destination.isEmpty else { return }
        onSearch(destination)
    }
}

private struct TukiAskAIView: View {
    let onBack: () -> Void
    @State private var message = ""
    var body: some View {
        VStack(spacing: 0) {
            TukiSubpageHeader(title: "Ask our AI", onBack: onBack)
            VStack(alignment: .leading, spacing: 12) {
                Text("Tell TUKI where you want to go.").font(.system(size: 15, weight: .semibold)).foregroundStyle(TukiPalette.gray)
                TextField("\"Yung malapit sa SM Clark...\"", text: $message, axis: .vertical)
                    .lineLimit(3...6).padding(14).background(TukiPalette.creamCard)
                    .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                Button(action: {}) {
                    Text("✨ Ask AI").font(.system(size: 16, weight: .bold)).foregroundStyle(.white)
                        .frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange)
                        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                }.buttonStyle(.plain)
                Spacer()
            }
            .padding(.horizontal, 30).padding(.bottom, 30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiCommuteDetailView: View {
    let commute: RecentCommute
    let onBack: () -> Void
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                TukiBackButton(action: onBack)
                Text("\(commute.origin) → \(commute.destination)").font(.system(size: 24, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.top, 20)
                Text("\(commute.legs) legs · \(commute.minutes) min total").font(.system(size: 16, weight: .semibold)).foregroundStyle(TukiPalette.teal).padding(.top, 6)
                if commute.steps.isEmpty {
                    Text("No step-by-step breakdown saved for this trip yet.").font(.system(size: 15)).foregroundStyle(TukiPalette.gray).padding(.top, 24)
                } else {
                    ForEach(Array(commute.steps.enumerated()), id: \.offset) { indexedStep in
                        TukiStepRow(step: indexedStep.element).padding(.top, 10)
                    }.padding(.top, 14)
                }
            }.padding(30)
        }.background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiStepRow: View {
    let step: CommuteStep
    var body: some View {
        HStack(spacing: 12) {
            Capsule().fill(TukiPalette.orange).frame(width: 6, height: 36)
            VStack(alignment: .leading, spacing: 2) {
                Text("\(step.mode): \(step.from) → \(step.to)").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
                Text(detailText).font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading).padding(14).background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
    }
    private var detailText: String {
        guard let fare = step.fare else { return "\(step.minutes) min" }
        return "\(step.minutes) min · ₱\(Int(fare.rounded()))"
    }
}

private struct TukiRouteResultsView: View {
    let origin: String
    let destination: String
    let onBack: () -> Void
    let onRouteSelected: (RouteOption) -> Void
    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                TukiBackButton(action: onBack)
                Text(destination).font(.system(size: 24, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.top, 20)
                Text("from \(origin)").font(.system(size: 15, weight: .semibold)).foregroundStyle(TukiPalette.gray).padding(.top, 4).padding(.bottom, 24)
                ForEach(TukiSamples.routes(origin: origin, destination: destination)) { option in
                    Button { onRouteSelected(option) } label: {
                        VStack(alignment: .leading, spacing: 0) {
                            HStack {
                                Text(option.label).font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark)
                                Spacer()
                                Text("₱\(Int(option.totalFare.rounded()))").font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.orange)
                            }
                            Text("\(option.steps.count) legs · \(option.totalMinutes) min").font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.teal).padding(.top, 4)
                            Text(option.steps.map(\.mode).joined(separator: "  →  ")).font(.system(size: 12, weight: .semibold)).foregroundStyle(TukiPalette.dark).padding(.top, 10)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading).padding(16).background(TukiPalette.creamCard)
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                    }.buttonStyle(.plain).padding(.bottom, 14)
                }
            }.padding(30)
        }.background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiActiveTripView: View {
    let origin: String
    let destination: String
    let option: RouteOption
    let onCancel: () -> Void
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Text("Current Trip").font(.system(size: 24, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                Text("\(origin) → \(destination)").font(.system(size: 16, weight: .semibold)).foregroundStyle(TukiPalette.teal).padding(.top, 6)
                Text("\(option.steps.count) legs · \(option.totalMinutes) min · ₱\(Int(option.totalFare.rounded()))")
                    .font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.gray).padding(.top, 4)
                ForEach(Array(option.steps.enumerated()), id: \.offset) { indexedStep in
                    TukiStepRow(step: indexedStep.element).padding(.top, 10)
                }.padding(.top, 18)
                Button(action: onCancel) {
                    Text("Cancel Trip").font(.system(size: 16, weight: .bold)).foregroundStyle(.white)
                        .frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange)
                        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                }.buttonStyle(.plain).padding(.top, 24)
            }.padding(30)
        }.background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiBackButton: View {
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            Text("← Back").font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.teal)
        }.buttonStyle(.plain)
    }
}
