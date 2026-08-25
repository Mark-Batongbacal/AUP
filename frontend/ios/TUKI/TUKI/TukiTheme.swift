import Combine
import SwiftUI
import UIKit

/// Runtime-observable dark mode flag, mirroring Android's `TukiThemeRuntime` (ui/theme/Color.kt):
/// flipping this one flag updates every `TukiPalette` color everywhere it's read, so existing
/// screens become theme-aware without each one needing its own light/dark plumbing.
final class TukiThemeRuntime: ObservableObject {
    static let shared = TukiThemeRuntime()

    private static let defaultsKey = "tuki.appearance.darkMode"
    private let defaults: UserDefaults

    @Published var isDarkMode: Bool {
        didSet {
            guard isDarkMode != oldValue else { return }
            defaults.set(isDarkMode, forKey: Self.defaultsKey)
        }
    }

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        isDarkMode = defaults.bool(forKey: Self.defaultsKey)
    }
}

/// Brand/token values mirror Android's `ui/theme/Color.kt` exactly (light/dark hex pairs)
/// so both platforms render the same palette. `TukiPalette` members stay computed properties
/// (not stored constants) so every existing call site reacts to `TukiThemeRuntime` automatically.
enum TukiPalette {
    // Brand colors stay fixed across themes, per Android.
    static let teal = Color(red: 13 / 255, green: 139 / 255, blue: 151 / 255) // Android TukiTeal #0D8B97
    static let orange = Color(red: 244 / 255, green: 139 / 255, blue: 31 / 255) // Android TukiOrange #F48B1F
    static let error = Color(red: 238 / 255, green: 91 / 255, blue: 87 / 255) // Android TukiDanger #EE5B57

    private static var isDark: Bool { TukiThemeRuntime.shared.isDarkMode }

    static var cream: Color {
        isDark
            ? Color(red: 8 / 255, green: 23 / 255, blue: 29 / 255) // Android dark background #08171D
            : Color(red: 255 / 255, green: 249 / 255, blue: 233 / 255) // Android light background #FFF9E9
    }

    static var creamCard: Color {
        isDark
            ? Color(red: 16 / 255, green: 36 / 255, blue: 45 / 255) // Android dark raised surface #10242D
            : Color(red: 250 / 255, green: 235 / 255, blue: 199 / 255)
    }

    static var dark: Color {
        isDark
            ? Color(red: 241 / 255, green: 247 / 255, blue: 248 / 255) // Android dark ink #F1F7F8
            : Color(red: 17 / 255, green: 46 / 255, blue: 54 / 255) // Android light ink #112E36
    }

    static var gray: Color {
        isDark
            ? Color(red: 177 / 255, green: 184 / 255, blue: 187 / 255)
            : Color(red: 154 / 255, green: 166 / 255, blue: 169 / 255)
    }

    static var border: Color {
        isDark
            ? Color(red: 59 / 255, green: 72 / 255, blue: 78 / 255)
            : Color(red: 232 / 255, green: 232 / 255, blue: 232 / 255)
    }
}

struct TukiLogoHeader: View {
    var logoSize: CGFloat = 75
    var titleSize: CGFloat = 34
    var titleColor = TukiPalette.teal

    var body: some View {
        VStack(spacing: 0) {
            Image("TukiLogo")
                .resizable()
                .scaledToFit()
                .frame(width: logoSize, height: logoSize)

            Text("TUKI.")
                .font(.system(size: titleSize, weight: .heavy))
                .foregroundStyle(titleColor)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("TUKI")
    }
}

struct TukiPrimaryButton: View {
    let title: String
    var isLoading = false
    var isEnabled = true
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 10) {
                if isLoading {
                    ProgressView()
                        .tint(.white)
                }
                Text(title)
                    .font(.system(size: 25, weight: .bold))
            }
            .frame(maxWidth: .infinity)
            .frame(height: 60)
            .foregroundStyle(.white)
            .background(TukiPalette.orange.opacity(isEnabled ? 1 : 0.6))
            .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
        }
        .buttonStyle(.plain)
        .disabled(!isEnabled)
    }
}

struct TukiFormField: View {
    let label: String
    @Binding var text: String
    var isSecure = false
    var keyboardType: UIKeyboardType = .default
    var textContentType: UITextContentType?

    @State private var revealsSecureText = false

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(label)
                .font(.system(size: 18))
                .foregroundStyle(.black)

            HStack(spacing: 8) {
                Group {
                    if isSecure && !revealsSecureText {
                        SecureField("", text: $text)
                    } else {
                        TextField("", text: $text)
                    }
                }
                .textInputAutocapitalization(keyboardType == .emailAddress ? .never : .sentences)
                .autocorrectionDisabled(keyboardType == .emailAddress)
                .keyboardType(keyboardType)
                .textContentType(textContentType)
                .font(.system(size: 17))
                .foregroundStyle(TukiPalette.dark)

                if isSecure {
                    Button(revealsSecureText ? "HIDE" : "SHOW") {
                        revealsSecureText.toggle()
                    }
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(TukiPalette.teal)
                    .buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 16)
            .frame(height: 60)
            .background(TukiPalette.cream)
            .clipShape(RoundedRectangle(cornerRadius: 15, style: .continuous))
        }
    }
}

struct TukiCompactFormField: View {
    let label: String
    @Binding var text: String
    var isSecure = false
    var keyboardType: UIKeyboardType = .default

    @State private var revealsSecureText = false

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label)
                .font(.system(size: 14, weight: .medium))
                .foregroundStyle(.black)

            HStack {
                Group {
                    if isSecure && !revealsSecureText {
                        SecureField("", text: $text)
                    } else {
                        TextField("", text: $text)
                    }
                }
                .textInputAutocapitalization(keyboardType == .emailAddress ? .never : .sentences)
                .autocorrectionDisabled(keyboardType == .emailAddress)
                .keyboardType(keyboardType)
                .font(.system(size: 16))

                if isSecure {
                    Button(revealsSecureText ? "HIDE" : "SHOW") {
                        revealsSecureText.toggle()
                    }
                    .font(.system(size: 11, weight: .bold))
                    .foregroundStyle(TukiPalette.teal)
                    .buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 14)
            .frame(height: 50)
            .background(TukiPalette.cream)
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
    }
}

struct RecentCommute: Identifiable, Hashable {
    let id: String
    let origin: String
    let destination: String
    let legs: Int
    let minutes: Int
    var status = ""
    var wasRerouted = false
    var rerouteCount = 0
    var dateGroup = ""
    var originLatitude: Double? = nil
    var originLongitude: Double? = nil
    var destinationLatitude: Double? = nil
    var destinationLongitude: Double? = nil
    var steps: [CommuteStep] = []
}

struct CommuteStep: Hashable {
    let mode: String
    let from: String
    let to: String
    let minutes: Int
    let fare: Double?
}

struct FavoriteRoute: Identifiable, Hashable {
    let id: String
    let origin: String
    let destination: String
    let timesUsed: Int
    let note: String
}

struct RouteOption: Identifiable, Hashable {
    let id: String
    let label: String
    let totalMinutes: Int
    let totalFare: Double
    let steps: [CommuteStep]
}

enum TukiSamples {
    static let recentCommutes = [
        RecentCommute(
            id: "1",
            origin: "Sta. Rita",
            destination: "Guagua Town",
            legs: 3,
            minutes: 22,
            dateGroup: "Today",
            steps: [
                CommuteStep(mode: "Jeepney", from: "Sta. Rita", to: "Guagua Plaza", minutes: 14, fare: 15),
                CommuteStep(mode: "Walk", from: "Guagua Plaza", to: "Terminal", minutes: 3, fare: nil),
                CommuteStep(mode: "Tricycle", from: "Terminal", to: "Guagua Town", minutes: 5, fare: 20)
            ]
        ),
        RecentCommute(id: "2", origin: "Guagua Town", destination: "Sta. Rita", legs: 3, minutes: 24, dateGroup: "Today"),
        RecentCommute(id: "3", origin: "Dolores", destination: "SM City Clark", legs: 2, minutes: 18, dateGroup: "Yesterday"),
        RecentCommute(id: "4", origin: "Porac", destination: "Dau Terminal", legs: 4, minutes: 35, dateGroup: "Earlier this week")
    ]

    static let favorites = [
        FavoriteRoute(id: "1", origin: "Porac", destination: "Angeles", timesUsed: 14, note: "daily commute"),
        FavoriteRoute(id: "2", origin: "Angeles", destination: "Porac", timesUsed: 12, note: "return trip"),
        FavoriteRoute(id: "3", origin: "Sta. Rita", destination: "Guagua Town", timesUsed: 6, note: "weekend market run")
    ]

    static func routes(origin: String, destination: String) -> [RouteOption] {
        [
            RouteOption(
                id: "1",
                label: "Fastest",
                totalMinutes: 22,
                totalFare: 35,
                steps: [
                    CommuteStep(mode: "Jeepney", from: origin, to: "San Fernando Terminal", minutes: 14, fare: 15),
                    CommuteStep(mode: "Tricycle", from: "San Fernando Terminal", to: destination, minutes: 8, fare: 20)
                ]
            ),
            RouteOption(
                id: "2",
                label: "Cheapest",
                totalMinutes: 35,
                totalFare: 22,
                steps: [
                    CommuteStep(mode: "Jeepney", from: origin, to: "Dolores Crossing", minutes: 20, fare: 12),
                    CommuteStep(mode: "Walk", from: "Dolores Crossing", to: "Guagua Terminal", minutes: 5, fare: nil),
                    CommuteStep(mode: "Jeepney", from: "Guagua Terminal", to: destination, minutes: 10, fare: 10)
                ]
            ),
            RouteOption(
                id: "3",
                label: "Fewest transfers",
                totalMinutes: 28,
                totalFare: 40,
                steps: [CommuteStep(mode: "Bus", from: origin, to: destination, minutes: 28, fare: 40)]
            )
        ]
    }
}
