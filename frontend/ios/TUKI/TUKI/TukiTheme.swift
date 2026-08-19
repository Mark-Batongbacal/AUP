import SwiftUI
import UIKit

enum TukiPalette {
    static let teal = Color(red: 21 / 255, green: 145 / 255, blue: 155 / 255)
    static let orange = Color(red: 255 / 255, green: 147 / 255, blue: 24 / 255)
    static let cream = Color(red: 255 / 255, green: 248 / 255, blue: 232 / 255)
    static let creamCard = Color(red: 250 / 255, green: 235 / 255, blue: 199 / 255)
    static let dark = Color(red: 23 / 255, green: 59 / 255, blue: 67 / 255)
    static let gray = Color(red: 154 / 255, green: 166 / 255, blue: 169 / 255)
    static let border = Color(red: 232 / 255, green: 232 / 255, blue: 232 / 255)
    static let error = Color(red: 176 / 255, green: 0, blue: 32 / 255)
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
    var dateGroup = ""
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
