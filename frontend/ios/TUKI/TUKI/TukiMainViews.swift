import Foundation
import SwiftUI

struct TukiMainView: View {
    let onSignOut: () -> Void

    @State private var selectedTab = TukiTab.home
    @State private var overlay: TukiMainOverlay?

    var body: some View {
        Group {
            switch overlay {
            case .some(.commute(let commute)):
                TukiCommuteDetailView(commute: commute) { overlay = nil }
            case .some(.routes(let origin, let destination)):
                TukiRouteResultsView(origin: origin, destination: destination) { overlay = nil }
            case .none:
                VStack(spacing: 0) {
                    tabContent
                        .frame(maxWidth: .infinity, maxHeight: .infinity)

                    TukiBottomBar(selectedTab: $selectedTab)
                }
                .background(TukiPalette.cream.ignoresSafeArea())
            }
        }
    }

    @ViewBuilder
    private var tabContent: some View {
        switch selectedTab {
        case .home:
            TukiHomeView(
                onSearch: { origin, destination in
                    overlay = .routes(origin: origin, destination: destination)
                },
                onCommute: { overlay = .commute($0) }
            )
        case .recent:
            TukiRecentView(onCommute: { overlay = .commute($0) })
        case .favorites:
            TukiFavoritesView()
        case .profile:
            TukiProfileView(onBack: { selectedTab = .home }, onSignOut: onSignOut)
        }
    }
}

private enum TukiMainOverlay {
    case commute(RecentCommute)
    case routes(origin: String, destination: String)
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
    let onSearch: (String, String) -> Void
    let onCommute: (RecentCommute) -> Void

    @State private var destination = ""
    private let currentLocation = "Current location"

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                Text("Hello, Juan 👋")
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

                ForEach(Array(TukiSamples.recentCommutes.prefix(3))) { commute in
                    Button { onCommute(commute) } label: {
                        TukiRecentCommuteCard(commute: commute)
                    }
                    .buttonStyle(.plain)
                    .padding(.bottom, 14)
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
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
    }
}

private struct TukiRecentView: View {
    let onCommute: (RecentCommute) -> Void
    private let sections = ["Today", "Yesterday", "Earlier this week"]

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                Text("Recent")
                    .font(.system(size: 27, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.bottom, 24)

                ForEach(sections, id: \.self) { section in
                    let commutes = TukiSamples.recentCommutes.filter { $0.dateGroup == section }
                    if !commutes.isEmpty {
                        Text(section.uppercased())
                            .font(.system(size: 13, weight: .heavy))
                            .foregroundStyle(TukiPalette.gray)
                            .padding(.bottom, 10)

                        ForEach(commutes) { commute in
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
            .padding(.vertical, 30)
        }
        .background(TukiPalette.cream)
    }
}

private struct TukiFavoritesView: View {
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

                ForEach(TukiSamples.favorites) { route in
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
                    Text("JD")
                        .font(.system(size: 30, weight: .heavy))
                        .foregroundStyle(.white)
                        .frame(width: 90, height: 90)
                        .background(TukiPalette.teal)
                        .clipShape(Circle())

                    Text("Juan Dela Cruz")
                        .font(.system(size: 21, weight: .heavy))
                        .foregroundStyle(TukiPalette.dark)
                        .padding(.top, 14)

                    Text("juan.delacruz@gmail.com")
                        .font(.system(size: 15))
                        .foregroundStyle(TukiPalette.gray)
                        .padding(.top, 4)
                }
                .frame(maxWidth: .infinity)
                .padding(.top, 28)

                HStack(spacing: 12) {
                    TukiProfileStat(value: "18", label: "TRIPS TAKEN")
                    TukiProfileStat(value: "2", label: "FAVORITES")
                    TukiProfileStat(value: "3", label: "SAVED")
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
                    Button(action: {}) {
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
