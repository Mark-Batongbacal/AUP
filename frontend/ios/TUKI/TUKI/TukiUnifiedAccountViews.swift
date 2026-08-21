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
    let onAbout: () -> Void
    let onSettings: () -> Void
    let onLogout: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: 18) {
                let profile = auth.currentUserProfile
                let displayName = auth.isGuest ? "Guest" : (profile?.displayName ?? "User")
                let email = auth.isGuest ? "Guest mode" : (profile?.email ?? "")

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
                    accountRow("Edit Profile", subtitle: "Update your personal information", action: onEdit)
                    divider
                    accountRow("Privacy & Security", subtitle: "Password, permissions & privacy", action: onPrivacy)
                    divider
                    accountRow("Language", subtitle: "English", action: onLanguage)
                    divider
                    accountRow("About TUKI", subtitle: "App information", action: onAbout)
                    divider
                    accountRow("Settings", subtitle: "Notifications, support & app settings", action: onSettings)
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
    let onBack: () -> Void
    let onChangePassword: () -> Void
    let onPermissions: () -> Void
    let onPrivacyPolicy: () -> Void

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
                Spacer()
            }
            .padding(.horizontal, 30)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
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
    @AppStorage("tuki.language") private var storedLanguage = TukiParityLanguage.english.rawValue
    @State private var selected = TukiParityLanguage.english

    var body: some View {
        VStack(spacing: 0) {
            pageHeader("Language", onBack: onBack)
            VStack(alignment: .leading, spacing: 12) {
                Text("SELECT LANGUAGE").font(.system(size: 12, weight: .bold)).foregroundStyle(TukiPalette.gray)
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
                TukiPrimaryButton(title: "Save") {
                    storedLanguage = selected.rawValue
                    onBack()
                }
            }
            .padding(.horizontal, 24)
            .padding(.bottom, 20)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .onAppear { selected = TukiParityLanguage(rawValue: storedLanguage) ?? .english }
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

struct TukiUnifiedSettingsView: View {
    let onBack: () -> Void
    let onPrivacyPolicy: () -> Void
    let onLanguage: () -> Void
    let onLogout: () -> Void
    @AppStorage("tuki.notifications") private var notifications = true
    @AppStorage("tuki.darkModePreference") private var darkMode = false

    var body: some View {
        VStack(spacing: 0) {
            pageHeader("Settings", onBack: onBack)
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    settingsSection("GENERAL") {
                        Toggle("Notifications", isOn: $notifications)
                        Toggle("Dark Mode", isOn: $darkMode)
                        navigationRow("Language", subtitle: "English", action: onLanguage)
                    }
                    settingsSection("SUPPORT") {
                        navigationRow("Help Center", subtitle: "FAQs and guides") {}
                        navigationRow("Report a Problem", subtitle: "Tell us what's wrong") {}
                    }
                    settingsSection("ABOUT") {
                        navigationRow("Privacy Policy", subtitle: "Data usage & safety", action: onPrivacyPolicy)
                        navigationRow("Terms of Service", subtitle: "Usage rules") {}
                        HStack { VStack(alignment: .leading) { Text("App Version").fontWeight(.bold); Text("1.0.0 (Beta)").font(.system(size: 13)).foregroundStyle(TukiPalette.gray) }; Spacer() }.padding(14)
                    }
                    Button(action: onLogout) {
                        Text("Log Out").font(.system(size: 17, weight: .bold)).foregroundStyle(.red).frame(maxWidth: .infinity).frame(height: 56).background(.white).clipShape(RoundedRectangle(cornerRadius: 16))
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
            Text(title).font(.system(size: 13, weight: .heavy)).foregroundStyle(TukiPalette.gray)
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
