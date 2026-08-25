import SwiftUI
import UIKit

enum TukiParityAccountPage {
    case profile
    case editProfile
    case privacySecurity
    case changePassword
    case permissions
    case privacyPolicy
    case language
    case about
    case settings
}

struct TukiUnifiedProfileView: View {
    @ObservedObject var auth: AuthViewModel
    let onEdit: () -> Void
    let onPrivacy: () -> Void
    let onLanguage: () -> Void
    let onSettings: () -> Void
    let onLogout: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: 18) {
                let profile = auth.currentUserProfile
                let displayName = auth.isGuestAccount ? "Guest" : (profile?.displayName ?? "User")
                let email = auth.isGuestAccount ? "Guest mode" : (profile?.email ?? "")

                Text(initials(displayName))
                    .font(.system(size: 34, weight: .heavy))
                    .foregroundStyle(.white)
                    .frame(width: 96, height: 96)
                    .background(TukiPalette.teal)
                    .clipShape(Circle())

                VStack(spacing: 3) {
                    Text(displayName)
                        .font(.system(size: 22, weight: .heavy))
                        .foregroundStyle(TukiPalette.dark)
                    Text(email)
                        .font(.system(size: 13))
                        .foregroundStyle(TukiPalette.gray)
                }

                if auth.isGuestAccount {
                    HStack(spacing: 10) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Guest Mode · \(tukiGuestRemainingText(expiresAt: auth.sessionExpiresAt))")
                                .font(.system(size: 13, weight: .bold))
                                .foregroundStyle(TukiPalette.orange)
                            Text("Create an account to keep access without the guest time limit.")
                                .font(.system(size: 12))
                                .foregroundStyle(TukiPalette.gray)
                        }
                        Spacer(minLength: 0)
                    }
                    .padding(14)
                    .background(TukiPalette.orange.opacity(0.12))
                    .clipShape(RoundedRectangle(cornerRadius: 16))
                }

                HStack(spacing: 12) {
                    parityStat("\(profile?.tripsTaken ?? 0)", "TRIPS TAKEN")
                    parityStat("\(profile?.favoritesCount ?? 0)", "FAVORITES")
                }

                Button(action: onLogout) {
                    Text("Log Out")
                        .font(.system(size: 17, weight: .bold))
                        .foregroundStyle(.red)
                        .frame(maxWidth: .infinity)
                        .frame(height: 54)
                        .background(.white)
                        .clipShape(RoundedRectangle(cornerRadius: 16))
                }
                .buttonStyle(.plain)

                VStack(spacing: 0) {
                    if !auth.isGuestAccount {
                        accountRow("Edit Profile", subtitle: "Update your personal information", action: onEdit)
                        divider
                        accountRow("Privacy & Security", subtitle: "Password, permissions & privacy", action: onPrivacy)
                        divider
                    }
                    accountRow(TukiInterfaceText.language, subtitle: TukiLanguagePreference.shared.currentLanguage, action: onLanguage)
                    divider
                    accountRow(TukiInterfaceText.settings, subtitle: "Appearance and app preferences", action: onSettings)
                }
                .background(TukiPalette.creamCard)
                .clipShape(RoundedRectangle(cornerRadius: 18))
            }
            .padding(.horizontal, 30)
            .padding(.vertical, 26)
        }
        .background(TukiPalette.cream)
    }

    private var divider: some View {
        Rectangle().fill(TukiPalette.gray.opacity(0.16)).frame(height: 1).padding(.leading, 20)
    }

    private func parityStat(_ value: String, _ label: String) -> some View {
        VStack(spacing: 3) {
            Text(value).font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark)
            Text(label).font(.system(size: 10, weight: .bold)).foregroundStyle(TukiPalette.gray)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 16)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 14))
    }

    private func accountRow(_ title: String, subtitle: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(title).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark)
                    Text(subtitle).font(.system(size: 12)).foregroundStyle(TukiPalette.gray)
                }
                Spacer()
                Text("›").font(.system(size: 22, weight: .bold)).foregroundStyle(TukiPalette.gray)
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 15)
        }
        .buttonStyle(.plain)
    }

    private func initials(_ name: String) -> String {
        let value = name.split(separator: " ").prefix(2).compactMap(\.first).map { String($0).uppercased() }.joined()
        return value.isEmpty ? "?" : value
    }
}

struct TukiUnifiedEditProfileView: View {
    @ObservedObject var auth: AuthViewModel
    let onBack: () -> Void

    @State private var fullName = ""
    @State private var phone = ""
    @State private var saving = false
    @State private var message: String?
    @State private var error: String?
    @State private var photoInfo = false

    var body: some View {
        VStack(spacing: 0) {
            pageHeader("Edit profile", onBack: onBack)
            ScrollView {
                VStack(spacing: 18) {
                    let name = fullName.isEmpty ? (auth.currentUserProfile?.displayName ?? "User") : fullName
                    ZStack(alignment: .bottomTrailing) {
                        Text(initials(name))
                            .font(.system(size: 34, weight: .heavy))
                            .foregroundStyle(.white)
                            .frame(width: 100, height: 100)
                            .background(TukiPalette.teal)
                            .clipShape(Circle())
                        Button("📷") { photoInfo = true }
                            .frame(width: 34, height: 34)
                            .background(TukiPalette.orange)
                            .clipShape(Circle())
                            .buttonStyle(.plain)
                    }
                    Button("Change photo") { photoInfo = true }
                        .font(.system(size: 15, weight: .bold))
                        .foregroundStyle(TukiPalette.teal)
                        .buttonStyle(.plain)

                    parityField("Full name", text: $fullName)
                    VStack(alignment: .leading, spacing: 6) {
                        Text("Email").font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.dark)
                        Text(auth.currentUserProfile?.email ?? "")
                            .foregroundStyle(TukiPalette.gray)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(14)
                            .background(TukiPalette.creamCard.opacity(0.7))
                            .clipShape(RoundedRectangle(cornerRadius: 14))
                        Text("Email is tied to your login and can't be changed here yet.")
                            .font(.system(size: 11)).foregroundStyle(TukiPalette.gray)
                    }
                    parityField("Phone", text: $phone, keyboard: .phonePad)

                    if let message { Text(message).foregroundStyle(TukiPalette.teal).font(.system(size: 13, weight: .semibold)) }
                    if let error { Text(error).foregroundStyle(TukiPalette.error).font(.system(size: 13, weight: .semibold)) }

                    TukiPrimaryButton(
                        title: saving ? "Saving..." : "Save changes",
                        isLoading: saving,
                        isEnabled: !saving && !fullName.trimmingCharacters(in: .whitespaces).isEmpty && !phone.trimmingCharacters(in: .whitespaces).isEmpty
                    ) {
                        Task { await save() }
                    }
                }
                .padding(.horizontal, 30)
                .padding(.bottom, 30)
            }
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .task {
            fullName = auth.currentUserProfile?.displayName ?? ""
            phone = auth.currentUserProfile?.phoneNumber ?? ""
        }
        .alert("Change photo", isPresented: $photoInfo) {
            Button("OK", role: .cancel) {}
        } message: {
            Text("Profile photo upload is not available in the current backend yet.")
        }
    }

    private func save() async {
        guard !saving else { return }
        saving = true
        message = nil
        error = nil
        defer { saving = false }
        switch await auth.updateProfile(fullName: fullName.trimmingCharacters(in: .whitespacesAndNewlines), phone: phone.trimmingCharacters(in: .whitespacesAndNewlines)) {
        case .success: message = "Profile updated."
        case .failure(let value): error = value.message
        }
    }

    private func initials(_ name: String) -> String {
        let value = name.split(separator: " ").prefix(2).compactMap(\.first).map { String($0).uppercased() }.joined()
        return value.isEmpty ? "?" : value
    }
}

struct TukiUnifiedPrivacySecurityView: View {
    @ObservedObject var auth: AuthViewModel
    let onBack: () -> Void
    let onChangePassword: () -> Void
    let onPermissions: () -> Void
    let onPrivacyPolicy: () -> Void
    let onAccountDeleted: () -> Void

    @State private var showDeleteDialog = false
    @State private var isDeleting = false
    @State private var deleteError: String?

    var body: some View {
        VStack(spacing: 0) {
            pageHeader("Privacy & Security", onBack: onBack)
            VStack(alignment: .leading, spacing: 12) {
                Text("Manage your account security and privacy settings.")
                    .font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                VStack(spacing: 0) {
                    navigationRow("Change Password", subtitle: "Update your account password", action: onChangePassword)
                    thinDivider
                    navigationRow("App Permissions", subtitle: "Location and notification access", action: onPermissions)
                    thinDivider
                    navigationRow("Privacy Policy", subtitle: "How TUKI uses and protects data", action: onPrivacyPolicy)
                }
                .background(TukiPalette.creamCard)
                .clipShape(RoundedRectangle(cornerRadius: 18))

                Button {
                    deleteError = nil
                    showDeleteDialog = true
                } label: {
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Delete account").font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.error)
                            Text("Permanently remove your data").font(.system(size: 12)).foregroundStyle(TukiPalette.gray)
                        }
                        Spacer()
                        Text("›").font(.system(size: 20, weight: .bold)).foregroundStyle(TukiPalette.gray)
                    }
                    .padding(.horizontal, 20).padding(.vertical, 14)
                    .background(TukiPalette.creamCard)
                    .clipShape(RoundedRectangle(cornerRadius: 16))
                }
                .buttonStyle(.plain)
                if let deleteError {
                    Text(deleteError).font(.system(size: 12)).foregroundStyle(TukiPalette.error)
                }

                Spacer()
            }
            .padding(.horizontal, 30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .alert("Delete your account?", isPresented: $showDeleteDialog) {
            Button("Delete", role: .destructive) { Task { await confirmDelete() } }
                .disabled(isDeleting)
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("This will permanently delete your account and all of your data, including trip history and favorites. This can't be undone.")
        }
    }

    private func confirmDelete() async {
        guard !isDeleting else { return }
        isDeleting = true
        defer { isDeleting = false }
        switch await auth.deleteAccount() {
        case .success:
            onAccountDeleted()
        case .failure(let error):
            deleteError = error.message
        }
    }
}

struct TukiUnifiedChangePasswordView: View {
    @ObservedObject var auth: AuthViewModel
    let onBack: () -> Void
    @State private var current = ""
    @State private var new = ""
    @State private var confirmation = ""
    @State private var working = false
    @State private var error: String?
    @State private var success = false

    var body: some View {
        VStack(spacing: 0) {
            pageHeader("Change Password", onBack: onBack)
            VStack(spacing: 18) {
                paritySecureField("Current password", text: $current)
                paritySecureField("New password", text: $new)
                paritySecureField("Confirm new password", text: $confirmation)
                if let error { Text(error).foregroundStyle(TukiPalette.error).font(.system(size: 13, weight: .semibold)).frame(maxWidth: .infinity, alignment: .leading) }
                TukiPrimaryButton(title: working ? "Updating..." : "Update password", isLoading: working, isEnabled: canSubmit) {
                    Task { await submit() }
                }
                Spacer()
            }
            .padding(.horizontal, 30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .alert("Password updated", isPresented: $success) { Button("OK", role: .cancel, action: onBack) } message: { Text("Your password was changed successfully.") }
    }

    private var canSubmit: Bool {
        !working && current.count >= 8 && new.count >= 8 && new == confirmation
    }

    private func submit() async {
        guard canSubmit else { return }
        working = true
        error = nil
        defer { working = false }
        switch await auth.changePassword(current: current, new: new) {
        case .success: success = true
        case .failure(let value): error = value.message
        }
    }
}

struct TukiUnifiedPermissionsView: View {
    let onBack: () -> Void
    var body: some View {
        VStack(spacing: 0) {
            pageHeader("App Permissions", onBack: onBack)
            VStack(alignment: .leading, spacing: 16) {
                Text("TUKI uses location access for routing, live navigation, camera follow, arrival detection, and nearby transport guidance.")
                    .font(.system(size: 14)).foregroundStyle(TukiPalette.gray)
                navigationRow("Location", subtitle: "Required for route and navigation features") {
                    if let url = URL(string: UIApplication.openSettingsURLString) { UIApplication.shared.open(url) }
                }
                navigationRow("Notifications", subtitle: "Used for trip and alighting alerts") {
                    if let url = URL(string: UIApplication.openSettingsURLString) { UIApplication.shared.open(url) }
                }
                Spacer()
            }
            .padding(.horizontal, 30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

struct TukiUnifiedPrivacyPolicyView: View {
    let onBack: () -> Void
    var body: some View {
        VStack(spacing: 0) {
            pageHeader("Privacy Policy", onBack: onBack)
            ScrollView {
                VStack(alignment: .leading, spacing: 16) {
                    policySection("Data we use", "TUKI uses account information, route searches, saved trips, and device location when needed to provide transportation and navigation features.")
                    policySection("Location", "Location is used to calculate supported routes, track active navigation progress, determine boarding and alighting proximity, and recenter the navigation map.")
                    policySection("Account security", "Authentication credentials are stored using the platform's secure credential storage. TUKI does not display your password.")
                    policySection("Route recommendations", "AI-assisted recommendations are constrained to verified TUKI routing results. The AI does not create unverified transport routes.")
                    policySection("Your choices", "You can manage app permissions in device settings and can sign out when you no longer want the app to use your authenticated account session.")
                }
                .padding(.horizontal, 30)
                .padding(.bottom, 30)
            }
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

enum TukiParityLanguage: String, CaseIterable, Identifiable {
    case english = "English"
    case filipino = "Filipino"
    var id: String { rawValue }
    var subtitle: String { self == .english ? "English (United States)" : "Tagalog" }
}

struct TukiUnifiedLanguageView: View {
    let onBack: () -> Void
    @ObservedObject private var languageStore = TukiLanguagePreference.shared
    @State private var selected = TukiParityLanguage.english

    var body: some View {
        VStack(spacing: 0) {
            pageHeader(TukiInterfaceText.language, onBack: onBack)
            VStack(alignment: .leading, spacing: 12) {
                Text(TukiInterfaceText.selectLanguage).font(.system(size: 12, weight: .bold)).foregroundStyle(TukiPalette.gray)
                ForEach(TukiParityLanguage.allCases) { option in
                    Button { selected = option } label: {
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text(option.rawValue).font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark)
                                Text(option.subtitle).font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                            }
                            Spacer()
                            if selected == option { Text("✓").font(.system(size: 18, weight: .bold)).foregroundStyle(TukiPalette.orange) }
                        }
                        .padding(18)
                        .background(TukiPalette.creamCard)
                        .overlay { RoundedRectangle(cornerRadius: 18).stroke(selected == option ? TukiPalette.orange : .clear, lineWidth: 2) }
                        .clipShape(RoundedRectangle(cornerRadius: 18))
                    }
                    .buttonStyle(.plain)
                }
                Spacer()
                TukiPrimaryButton(title: TukiInterfaceText.save) {
                    languageStore.update(selected.rawValue)
                    onBack()
                }
            }
            .padding(.horizontal, 24)
            .padding(.bottom, 20)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .onAppear { selected = TukiParityLanguage(rawValue: languageStore.currentLanguage) ?? .english }
    }
}

private func supportCopy(_ english: String, _ filipino: String) -> String {
    TukiInterfaceText.isFilipino ? filipino : english
}

/// Ported from Android's `HelpCenterScreen` (screens/SupportScreens.kt): same five FAQ
/// entries, same expand/collapse behavior, same footer nudge to Send Feedback.
struct TukiUnifiedHelpCenterView: View {
    let onBack: () -> Void
    @State private var expandedIndex: Int?

    private var faqItems: [(String, String)] {
        [
            (supportCopy("How do I plan a trip?", "Paano ako magpaplano ng biyahe?"),
             supportCopy(
                "From Home, set your current location and destination, then choose Find Routes. TUKI will show available commute options when route data is available.",
                "Sa Home, itakda ang kasalukuyang lokasyon at destinasyon, pagkatapos piliin ang Maghanap ng Ruta. Ipapakita ng TUKI ang available na commute options kapag may route data.")),
            (supportCopy("How do Favorites work?", "Paano gumagana ang Favorites?"),
             supportCopy(
                "Tap the star on a route to save it. Your saved routes appear in Favorites for quicker access later.",
                "I-tap ang bituin sa isang ruta para i-save ito. Lalabas ang mga naka-save mong ruta sa Favorites para mas mabilis itong balikan.")),
            (supportCopy("What appears in Recent Trips?", "Ano ang makikita sa Recent Trips?"),
             supportCopy(
                "Recent Trips shows your saved journey history and its status, such as completed or cancelled trips.",
                "Ipinapakita ng Recent Trips ang iyong journey history at status nito, gaya ng natapos o kinanselang biyahe.")),
            (supportCopy("How do I change the app language?", "Paano palitan ang wika ng app?"),
             supportCopy(
                "Open Profile, tap Language, choose English or Filipino, then save your selection.",
                "Buksan ang Profile, i-tap ang Language, piliin ang English o Filipino, at i-save ang napili.")),
            (supportCopy("How do I switch Light and Dark Mode?", "Paano magpalit ng Light at Dark Mode?"),
             supportCopy(
                "Open Profile > Settings and use the Dark Mode switch under Appearance.",
                "Buksan ang Profile > Settings at gamitin ang Dark Mode switch sa Appearance."))
        ]
    }

    var body: some View {
        VStack(spacing: 0) {
            pageHeader(TukiInterfaceText.helpCenter, onBack: onBack)
            ScrollView {
                VStack(alignment: .leading, spacing: 10) {
                    Text(supportCopy("Find quick answers about using TUKI.", "Makahanap ng mabilis na sagot tungkol sa paggamit ng TUKI."))
                        .font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                        .padding(.bottom, 8)
                    ForEach(Array(faqItems.enumerated()), id: \.offset) { index, item in
                        Button {
                            expandedIndex = expandedIndex == index ? nil : index
                        } label: {
                            VStack(alignment: .leading, spacing: 12) {
                                HStack(spacing: 12) {
                                    ZStack {
                                        RoundedRectangle(cornerRadius: 11).fill(Color(red: 1, green: 0.94, blue: 0.835)).frame(width: 36, height: 36)
                                        Text("?").font(.system(size: 15, weight: .bold)).foregroundStyle(Color(red: 0x15 / 255, green: 0x3E / 255, blue: 0x4B / 255))
                                    }
                                    Text(item.0).font(.system(size: 15, weight: .semibold)).foregroundStyle(TukiPalette.dark)
                                    Spacer(minLength: 0)
                                    Text(expandedIndex == index ? "⌃" : "⌄").foregroundStyle(TukiPalette.gray)
                                }
                                if expandedIndex == index {
                                    Text(item.1).font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                                }
                            }
                            .padding(16)
                            .background(TukiPalette.creamCard)
                            .clipShape(RoundedRectangle(cornerRadius: 16))
                        }
                        .buttonStyle(.plain)
                    }
                    Text(supportCopy("Still need help? Go back to Settings and choose Send Feedback.", "Kailangan pa ng tulong? Bumalik sa Settings at piliin ang Send Feedback."))
                        .font(.system(size: 13)).foregroundStyle(TukiPalette.dark)
                        .padding(16).frame(maxWidth: .infinity, alignment: .leading)
                        .background(TukiPalette.teal.opacity(0.1)).clipShape(RoundedRectangle(cornerRadius: 16))
                        .padding(.top, 8)
                }
                .padding(.horizontal, 30)
                .padding(.bottom, 30)
            }
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

/// Ported from Android's `SendFeedbackScreen` (screens/SupportScreens.kt): category chips,
/// a message field, and a "Send Feedback" button that opens the device's mail app with both
/// TUKI recipients pre-filled — same recipients, same subject/body format.
struct TukiUnifiedSendFeedbackView: View {
    let onBack: () -> Void

    @State private var category: String
    @State private var message = ""
    @State private var shareError: String?

    private static let feedbackEmails = "pinacate.stephen@gmail.com,batongbacalmark@gmail.com"
    private let categories: [String]

    init(onBack: @escaping () -> Void) {
        self.onBack = onBack
        let categories = TukiInterfaceText.isFilipino
            ? ["Pangkalahatan", "Mga Ruta", "Problema sa App", "Mungkahi"]
            : ["General", "Routes", "App issue", "Suggestion"]
        self.categories = categories
        _category = State(initialValue: categories[0])
    }

    private var canSend: Bool { !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

    var body: some View {
        VStack(spacing: 0) {
            pageHeader(TukiInterfaceText.sendFeedback, onBack: onBack)
            ScrollView {
                VStack(alignment: .leading, spacing: 10) {
                    Text(supportCopy(
                        "Tell us what worked, what went wrong, or what you would like TUKI to improve.",
                        "Ibahagi kung ano ang gumana, ano ang naging problema, o ano ang gusto mong mapahusay sa TUKI."
                    )).font(.system(size: 13)).foregroundStyle(TukiPalette.gray)

                    Text(supportCopy("CATEGORY", "KATEGORYA")).font(.system(size: 11, weight: .bold)).foregroundStyle(TukiPalette.dark).padding(.top, 10)
                    categoryGrid

                    Text(supportCopy("YOUR FEEDBACK", "IYONG FEEDBACK")).font(.system(size: 11, weight: .bold)).foregroundStyle(TukiPalette.dark).padding(.top, 10)
                    TextEditor(text: $message)
                        .frame(height: 160)
                        .padding(8)
                        .background(TukiPalette.creamCard)
                        .clipShape(RoundedRectangle(cornerRadius: 16))
                        .onChange(of: message) { _, _ in shareError = nil }

                    Text(supportCopy(
                        "Send Feedback opens your email app with both TUKI feedback recipients already filled in.",
                        "Bubuksan ng Send Feedback ang email app na nakalagay na ang dalawang TUKI feedback recipients."
                    )).font(.system(size: 11)).foregroundStyle(TukiPalette.gray)

                    if let shareError {
                        Text(shareError).font(.system(size: 12)).foregroundStyle(TukiPalette.error)
                    }
                }
                .padding(.horizontal, 30)
                .padding(.bottom, 16)
            }
            Button(action: send) {
                Text(TukiInterfaceText.sendFeedback)
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity)
                    .frame(height: 54)
                    .background(canSend ? TukiPalette.orange : TukiPalette.orange.opacity(0.35))
                    .clipShape(RoundedRectangle(cornerRadius: 18))
            }
            .buttonStyle(.plain)
            .disabled(!canSend)
            .padding(.horizontal, 30)
            .padding(.bottom, 16)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }

    private var categoryGrid: some View {
        let rows = stride(from: 0, to: categories.count, by: 2).map { Array(categories[$0..<min($0 + 2, categories.count)]) }
        return VStack(spacing: 8) {
            ForEach(Array(rows.enumerated()), id: \.offset) { _, row in
                HStack(spacing: 8) {
                    ForEach(row, id: \.self) { item in
                        Button { category = item } label: {
                            Text(item)
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundStyle(category == item ? .white : TukiPalette.dark)
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 11)
                                .background(category == item ? TukiPalette.teal : TukiPalette.creamCard)
                                .clipShape(RoundedRectangle(cornerRadius: 14))
                        }
                        .buttonStyle(.plain)
                    }
                    if row.count == 1 { Spacer(minLength: 0) }
                }
            }
        }
    }

    private func send() {
        let trimmed = message.trimmingCharacters(in: .whitespacesAndNewlines)
        let subject = "TUKI Feedback - \(category)"
        let body = "TUKI Feedback\nCategory: \(category)\n\n\(trimmed)"
        var components = URLComponents()
        components.scheme = "mailto"
        components.path = Self.feedbackEmails
        components.queryItems = [
            URLQueryItem(name: "subject", value: subject),
            URLQueryItem(name: "body", value: body)
        ]
        guard let url = components.url, UIApplication.shared.canOpenURL(url) else {
            shareError = supportCopy("No compatible email app was found on this device.", "Walang compatible na email app na nakita sa device na ito.")
            return
        }
        UIApplication.shared.open(url)
    }
}

struct TukiUnifiedAboutView: View {
    let onBack: () -> Void
    var body: some View {
        VStack(spacing: 0) {
            pageHeader("About TUKI", onBack: onBack)
            VStack(spacing: 16) {
                Image("TukiLogo").resizable().scaledToFit().frame(width: 92, height: 92)
                Text("TUKI.").font(.system(size: 30, weight: .heavy)).foregroundStyle(TukiPalette.teal)
                Text("A smart public-transport companion for supported routes in Porac, Angeles City, Dau, and Mabalacat.")
                    .multilineTextAlignment(.center).foregroundStyle(TukiPalette.gray)
                Text("Version 1.0.0 (Beta)").font(.system(size: 13, weight: .semibold)).foregroundStyle(TukiPalette.dark)
                Spacer()
            }
            .padding(30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

/// Matches Android's `SettingsScreen.kt` exactly: just Appearance (Dark Mode) and Support
/// (Help Center / Send Feedback / About TUKI) — Android has no Notifications toggle,
/// Language row, Privacy Policy, or Terms of Service here (Language and Privacy Policy
/// live under Profile only, matching Android's single navigation path to each).
struct TukiUnifiedSettingsView: View {
    let onBack: () -> Void
    let onHelpCenter: () -> Void
    let onSendFeedback: () -> Void
    let onAbout: () -> Void
    let onLogout: () -> Void
    @ObservedObject private var theme = TukiThemeRuntime.shared

    var body: some View {
        VStack(spacing: 0) {
            pageHeader(TukiInterfaceText.settings, onBack: onBack)
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    settingsSection(TukiInterfaceText.appearance) {
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text(TukiInterfaceText.darkMode).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark)
                                Text(TukiInterfaceText.darkModeSubtitle).font(.system(size: 12)).foregroundStyle(TukiPalette.gray)
                            }
                            Spacer()
                            Toggle("", isOn: $theme.isDarkMode).labelsHidden().tint(TukiPalette.teal)
                        }
                        .padding(.horizontal, 6).padding(.vertical, 6)
                    }
                    settingsSection(TukiInterfaceText.support) {
                        navigationRow(TukiInterfaceText.helpCenter, subtitle: TukiInterfaceText.isFilipino ? "Mga FAQ at gabay" : "FAQs and guides", action: onHelpCenter)
                        thinDivider
                        navigationRow(TukiInterfaceText.sendFeedback, subtitle: TukiInterfaceText.isFilipino ? "Tulungan kaming mapahusay ang TUKI" : "Help us improve TUKI", action: onSendFeedback)
                        thinDivider
                        navigationRow(TukiInterfaceText.aboutTuki, subtitle: "Version 1.0.0", action: onAbout)
                    }
                    Button(action: onLogout) {
                        Text(TukiInterfaceText.logOut).font(.system(size: 17, weight: .bold)).foregroundStyle(.red).frame(maxWidth: .infinity).frame(height: 56).background(.white).clipShape(RoundedRectangle(cornerRadius: 16))
                    }
                    .buttonStyle(.plain)
                }
                .padding(.horizontal, 30)
                .padding(.bottom, 30)
            }
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }

    private func settingsSection<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(title.uppercased()).font(.system(size: 13, weight: .heavy)).foregroundStyle(TukiPalette.gray)
            VStack(spacing: 0) { content() }.padding(.horizontal, 10).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 18))
        }
    }
}

private func pageHeader(_ title: String, onBack: @escaping () -> Void) -> some View {
    HStack(spacing: 14) {
        Button(action: onBack) {
            Text("‹").font(.system(size: 22, weight: .bold)).foregroundStyle(TukiPalette.dark).frame(width: 40, height: 40).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 12))
        }
        .buttonStyle(.plain)
        Text(title).font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark)
        Spacer()
    }
    .padding(.horizontal, 30)
    .padding(.vertical, 24)
}

private func navigationRow(_ title: String, subtitle: String, action: @escaping () -> Void) -> some View {
    Button(action: action) {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark)
                Text(subtitle).font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
            }
            Spacer()
            Text("›").font(.system(size: 20, weight: .bold)).foregroundStyle(TukiPalette.gray)
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 14)
        .background(TukiPalette.creamCard)
        .clipShape(RoundedRectangle(cornerRadius: 16))
    }
    .buttonStyle(.plain)
}

private var thinDivider: some View {
    Rectangle().fill(TukiPalette.gray.opacity(0.15)).frame(height: 1).padding(.horizontal, 20)
}

private func parityField(_ title: String, text: Binding<String>, keyboard: UIKeyboardType = .default) -> some View {
    VStack(alignment: .leading, spacing: 8) {
        Text(title).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.dark)
        TextField(title, text: text)
            .keyboardType(keyboard)
            .padding(14)
            .background(TukiPalette.creamCard)
            .clipShape(RoundedRectangle(cornerRadius: 14))
    }
}

private func paritySecureField(_ title: String, text: Binding<String>) -> some View {
    VStack(alignment: .leading, spacing: 8) {
        Text(title).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.dark)
        SecureField(title, text: text)
            .textContentType(.password)
            .padding(14)
            .background(TukiPalette.creamCard)
            .clipShape(RoundedRectangle(cornerRadius: 14))
    }
}

private func policySection(_ title: String, _ body: String) -> some View {
    VStack(alignment: .leading, spacing: 6) {
        Text(title).font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark)
        Text(body).font(.system(size: 14)).foregroundStyle(TukiPalette.gray)
    }
    .frame(maxWidth: .infinity, alignment: .leading)
    .padding(16)
    .background(TukiPalette.creamCard)
    .clipShape(RoundedRectangle(cornerRadius: 16))
}
