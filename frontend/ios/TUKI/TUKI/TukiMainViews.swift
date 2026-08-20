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
                recentCommutes: recentCommutes,
                isLoadingRecent: isLoadingRecent,
                recentError: recentError,
                onSearch: { origin, destination in
                    overlay = .routes(origin: origin, destination: destination)
                },
                onCommute: { overlay = .commute($0) }
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
            TukiProfileView(isGuest: isGuest, onBack: { selectedTab = .home }, onSignOut: onSignOut)
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
    let recentCommutes: [RecentCommute]
    let isLoadingRecent: Bool
    let recentError: String?
    let onSearch: (String, String) -> Void
    let onCommute: (RecentCommute) -> Void

    @State private var destination = ""
    private let currentLocation = "Current location"

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                Text(isGuest ? "Hello, Guest" : "Hello")
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundStyle(TukiPalette.gray)

                Text("Where are you going?")
                    .font(.system(size: 27, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.top, 6)

                TukiLocationCard(
                    currentLocation: currentLocation,
                    destination: $destination,
                    onSearch: submitSearch
                )
                .padding(.top, 22)

                Text("RECENT COMMUTES")
                    .font(.system(size: 14, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.top, 30)
                    .padding(.bottom, 12)

                if isLoadingRecent {
                    Text("Loading recent journeys...")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.bottom, 14)
                } else if let recentError {
                    Text(recentError)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.bottom, 14)
                } else if recentCommutes.isEmpty {
                    Text(isGuest ? "Sign in to view completed and cancelled journeys." : "No completed or cancelled trips yet.")
                        .font(.system(size: 14))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.bottom, 14)
                } else {
                    ForEach(Array(recentCommutes.prefix(3))) { commute in
                        Button { onCommute(commute) } label: {
                            TukiRecentCommuteCard(commute: commute)
                        }
                        .buttonStyle(.plain)
                        .padding(.bottom, 14)
                    }
                }

                Button(action: {}) {
                    HStack {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("New here?")
                                .font(.system(size: 18, weight: .bold))
                            Text("Learn how “para po” works")
                                .font(.system(size: 14))
                                .opacity(0.85)
                        }
                        Spacer()
                        Text("→")
                            .font(.system(size: 22, weight: .bold))
                    }
                    .foregroundStyle(.white)
                    .padding(20)
                    .background(TukiPalette.teal)
                    .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
                }
                .buttonStyle(.plain)
                .padding(.top, 4)
            }
            .padding(.horizontal, 30)
            .padding(.top, 30)
            .padding(.bottom, 20)
        }
        .background(TukiPalette.cream)
        .scrollDismissesKeyboard(.interactively)
    }

    private func submitSearch() {
        let query = destination.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else { return }
        onSearch(currentLocation, query)
    }
}

private struct TukiLocationCard: View {
    let currentLocation: String
    @Binding var destination: String
    let onSearch: () -> Void

    var body: some View {
        VStack(spacing: 14) {
            HStack(spacing: 12) {
                Circle()
                    .fill(TukiPalette.teal)
                    .frame(width: 11, height: 11)
                Text("\(currentLocation) (current location)")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(TukiPalette.dark)
                Spacer()
            }

            HStack(spacing: 12) {
                RoundedRectangle(cornerRadius: 2)
                    .fill(TukiPalette.orange)
                    .frame(width: 11, height: 11)
                TextField("Type your destination...", text: $destination)
                    .font(.system(size: 15))
                    .foregroundStyle(TukiPalette.dark)
                    .submitLabel(.search)
                    .onSubmit(onSearch)
            }
        }
        .padding(18)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }
}

private struct TukiRecentCommuteCard: View {
    let commute: RecentCommute

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("\(commute.origin) to \(commute.destination)")
                .font(.system(size: 17, weight: .bold))
                .foregroundStyle(TukiPalette.dark)
            Text("\(commute.legs) legs · \(commute.minutes) min")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(TukiPalette.teal)
            if !commute.status.isEmpty || commute.wasRerouted {
                Text(metaText)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(TukiPalette.gray)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
    }

    private var metaText: String {
        var parts: [String] = []
        if !commute.status.isEmpty {
            parts.append(commute.status)
        }
        if commute.wasRerouted {
            parts.append(commute.rerouteCount > 1 ? "Rerouted \(commute.rerouteCount)x" : "Rerouted")
        }
        return parts.joined(separator: " · ")
    }
}

private struct TukiRecentView: View {
    let commutes: [RecentCommute]
    let isGuest: Bool
    let isLoading: Bool
    let errorMessage: String?
    let onCommute: (RecentCommute) -> Void
    private let sections = ["Today", "Yesterday", "Earlier"]

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                Text("Recent")
                    .font(.system(size: 27, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.bottom, 24)

                if isLoading {
                    Text("Loading recent journeys...")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.gray)
                } else if let errorMessage {
                    Text(errorMessage)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.gray)
                } else if commutes.isEmpty {
                    Text(isGuest ? "Sign in to view your recent journeys." : "No completed or cancelled trips yet.")
                        .font(.system(size: 14))
                        .foregroundStyle(TukiPalette.gray)
                } else {
                    ForEach(sections, id: \.self) { section in
                        let sectionCommutes = commutes.filter { $0.dateGroup == section }
                        if !sectionCommutes.isEmpty {
                            Text(section.uppercased())
                                .font(.system(size: 13, weight: .heavy))
                                .foregroundStyle(TukiPalette.gray)
                                .padding(.bottom, 10)

                            ForEach(sectionCommutes) { commute in
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
            }
            .padding(.horizontal, 30)
            .padding(.vertical, 30)
        }
        .background(TukiPalette.cream)
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
                } else if let errorMessage {
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
                                .renderingMode(.template)
                                .resizable()
                                .scaledToFit()
                                .foregroundStyle(TukiPalette.orange)
                                .frame(width: 22, height: 22)
                        }
                        .padding(16)
                        .background(TukiPalette.creamCard)
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
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
            .padding(.vertical, 30)
        }
        .background(TukiPalette.cream)
    }
}

private struct TukiProfileView: View {
    let isGuest: Bool
    let onBack: () -> Void
    let onSignOut: () -> Void

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                HStack(spacing: 14) {
                    Button(action: onBack) {
                        Text("‹")
                            .font(.system(size: 22, weight: .bold))
                            .foregroundStyle(TukiPalette.dark)
                            .frame(width: 38, height: 38)
                            .background(TukiPalette.creamCard)
                            .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
                    }
                    .buttonStyle(.plain)

                    Text("Profile")
                        .font(.system(size: 22, weight: .heavy))
                        .foregroundStyle(TukiPalette.dark)
                }

                VStack(spacing: 0) {
                    Text(isGuest ? "G" : "JD")
                        .font(.system(size: 30, weight: .heavy))
                        .foregroundStyle(.white)
                        .frame(width: 90, height: 90)
                        .background(TukiPalette.teal)
                        .clipShape(Circle())

                    Text(isGuest ? "Guest" : "Juan Dela Cruz")
                        .font(.system(size: 21, weight: .heavy))
                        .foregroundStyle(TukiPalette.dark)
                        .padding(.top, 14)

                    Text(isGuest ? "Guest mode" : "juan.delacruz@gmail.com")
                        .font(.system(size: 15))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.top, 4)
                }
                .frame(maxWidth: .infinity)
                .padding(.top, 28)

                HStack(spacing: 12) {
                    TukiProfileStat(value: isGuest ? "0" : "18", label: "TRIPS TAKEN")
                    TukiProfileStat(value: isGuest ? "0" : "2", label: "FAVORITES")
                    TukiProfileStat(value: isGuest ? "0" : "3", label: "SAVED")
                }
                .padding(.top, 24)

                Text("ACCOUNT")
                    .font(.system(size: 14, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.top, 28)
                    .padding(.bottom, 12)

                TukiAccountRow(imageName: "EditProfileIcon", title: "Edit Profile", subtitle: "Name, email, phone")
                TukiAccountRow(imageName: "PrivacyIcon", title: "Privacy & Security", subtitle: "Password, data settings")
                    .padding(.top, 12)
                TukiAccountRow(imageName: "LanguageIcon", title: "Language", subtitle: "English")
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
            .padding(.vertical, 30)
        }
        .background(TukiPalette.cream)
    }
}

private struct TukiProfileStat: View {
    let value: String
    let label: String

    var body: some View {
        VStack(spacing: 2) {
            Text(value)
                .font(.system(size: 22, weight: .heavy))
                .foregroundStyle(TukiPalette.dark)
            Text(label)
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(TukiPalette.gray)
                .lineLimit(1)
                .minimumScaleFactor(0.75)
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

    var body: some View {
        Button(action: {}) {
            HStack(spacing: 14) {
                Image(imageName)
                    .resizable()
                    .scaledToFit()
                    .frame(width: 40, height: 40)
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(TukiPalette.dark)
                    Text(subtitle)
                        .font(.system(size: 13))
                        .foregroundStyle(TukiPalette.gray)
                }
                Spacer()
                Text("›")
                    .font(.system(size: 20, weight: .bold))
                    .foregroundStyle(TukiPalette.gray)
            }
            .padding(14)
            .background(TukiPalette.creamCard)
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
        .buttonStyle(.plain)
    }
}

private struct TukiCommuteDetailView: View {
    let commute: RecentCommute
    let onBack: () -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                TukiBackButton(action: onBack)

                Text("\(commute.origin) → \(commute.destination)")
                    .font(.system(size: 24, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.top, 20)

                Text("\(commute.legs) legs · \(commute.minutes) min total")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundStyle(TukiPalette.teal)
                    .padding(.top, 6)

                if commute.steps.isEmpty {
                    Text("No step-by-step breakdown saved for this trip yet.")
                        .font(.system(size: 15))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.top, 24)
                } else {
                    ForEach(Array(commute.steps.enumerated()), id: \.offset) { indexedStep in
                        TukiStepRow(step: indexedStep.element)
                            .padding(.top, 10)
                    }
                    .padding(.top, 14)
                }
            }
            .padding(30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiStepRow: View {
    let step: CommuteStep

    var body: some View {
        HStack(spacing: 12) {
            Capsule()
                .fill(TukiPalette.orange)
                .frame(width: 6, height: 36)
            VStack(alignment: .leading, spacing: 2) {
                Text("\(step.mode): \(step.from) → \(step.to)")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(TukiPalette.dark)
                Text(detailText)
                    .font(.system(size: 13))
                    .foregroundStyle(TukiPalette.gray)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(14)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
    }

    private var detailText: String {
        guard let fare = step.fare else { return "\(step.minutes) min" }
        return "\(step.minutes) min · ₱\(String(format: "%.0f", fare))"
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

                Text(destination)
                    .font(.system(size: 24, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.top, 20)

                Text("from \(origin)")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(TukiPalette.gray)
                    .padding(.top, 4)
                    .padding(.bottom, 24)

                ForEach(TukiSamples.routes(origin: origin, destination: destination)) { option in
                    Button { onRouteSelected(option) } label: {
                        VStack(alignment: .leading, spacing: 0) {
                            HStack {
                                Text(option.label)
                                    .font(.system(size: 17, weight: .bold))
                                    .foregroundStyle(TukiPalette.dark)
                                Spacer()
                                Text("₱\(String(format: "%.0f", option.totalFare))")
                                    .font(.system(size: 16, weight: .bold))
                                    .foregroundStyle(TukiPalette.orange)
                            }

                            Text("\(option.steps.count) legs · \(option.totalMinutes) min")
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundStyle(TukiPalette.teal)
                                .padding(.top, 4)

                            Text(option.steps.map(\.mode).joined(separator: "  →  "))
                                .font(.system(size: 12, weight: .semibold))
                                .foregroundStyle(TukiPalette.dark)
                                .padding(.top, 10)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(16)
                        .background(TukiPalette.creamCard)
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                    }
                    .buttonStyle(.plain)
                    .padding(.bottom, 14)
                }
            }
            .padding(30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
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
                Text("Current Trip")
                    .font(.system(size: 24, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)

                Text("\(origin) → \(destination)")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundStyle(TukiPalette.teal)
                    .padding(.top, 6)

                Text("\(option.steps.count) legs · \(option.totalMinutes) min · ₱\(String(format: "%.0f", option.totalFare))")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(TukiPalette.gray)
                    .padding(.top, 4)

                ForEach(Array(option.steps.enumerated()), id: \.offset) { indexedStep in
                    TukiStepRow(step: indexedStep.element)
                        .padding(.top, 10)
                }
                .padding(.top, 18)

                Button(action: onCancel) {
                    Text("Cancel Trip")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(.white)
                        .frame(maxWidth: .infinity)
                        .frame(height: 52)
                        .background(TukiPalette.orange)
                        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                }
                .buttonStyle(.plain)
                .padding(.top, 24)
            }
            .padding(30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

private struct TukiBackButton: View {
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text("← Back")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(TukiPalette.teal)
        }
        .buttonStyle(.plain)
    }
}
