import SwiftUI

struct ContentView: View {
    @StateObject private var authViewModel = AuthViewModel()
    @State private var entryScreen = EntryScreen.onboarding

    var body: some View {
        Group {
            if authViewModel.canEnterApp {
                TukiMainView(isGuest: authViewModel.isGuest) {
                    authViewModel.signOut()
                    entryScreen = .login
                }
            } else {
                switch entryScreen {
                case .onboarding:
                    TukiOnboardingView {
                        withAnimation(.easeInOut(duration: 0.25)) {
                            entryScreen = .login
                        }
                    }
                case .login:
                    TukiLoginView(
                        viewModel: authViewModel,
                        onSignUp: { entryScreen = .signup },
                        onGuest: { authViewModel.continueAsGuest() }
                    )
                case .signup:
                    TukiSignupView(onLogin: { entryScreen = .login })
                }
            }
        }
        .preferredColorScheme(.light)
    }
}

private enum EntryScreen {
    case onboarding
    case login
    case signup
}

private struct TukiOnboardingView: View {
    let onLetsRide: () -> Void
    @State private var logoIsRaised = false

    var body: some View {
        ZStack {
            TukiPalette.teal.ignoresSafeArea()

            VStack(spacing: 0) {
                Spacer().frame(height: 170)

                Image("TukiLogo")
                    .resizable()
                    .scaledToFit()
                    .frame(width: 170, height: 170)
                    .offset(y: logoIsRaised ? -14 : 0)
                    .onAppear {
                        withAnimation(
                            .easeInOut(duration: 0.6)
                                .repeatForever(autoreverses: true)
                        ) {
                            logoIsRaised = true
                        }
                    }

                Text("TUKI.")
                    .font(.system(size: 46, weight: .heavy))
                    .foregroundStyle(.white)
                    .padding(.top, 5)

                Text("Commute smarter.")
                    .font(.system(size: 21))
                    .foregroundStyle(.white)
                    .padding(.top, 38)

                Text("Move easier.")
                    .font(.system(size: 21))
                    .foregroundStyle(.white)

                HStack(spacing: 8) {
                    Capsule()
                        .fill(TukiPalette.orange)
                        .frame(width: 30, height: 10)
                    Circle()
                        .fill(.white.opacity(0.3))
                        .frame(width: 10, height: 10)
                    Circle()
                        .fill(.white.opacity(0.3))
                        .frame(width: 10, height: 10)
                }
                .padding(.top, 28)

                Spacer().frame(height: 40)

                Button(action: onLetsRide) {
                    Text("Let's Ride")
                        .font(.system(size: 25, weight: .bold))
                        .foregroundStyle(.white)
                        .frame(maxWidth: .infinity)
                        .frame(height: 84)
                        .background(TukiPalette.orange)
                        .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
                }
                .buttonStyle(.plain)

                Spacer(minLength: 0)
            }
            .padding(.horizontal, 34)
            .padding(.bottom, 45)
        }
    }
}

private struct TukiLoginView: View {
    @ObservedObject var viewModel: AuthViewModel
    let onSignUp: () -> Void
    let onGuest: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                HStack(spacing: 10) {
                    Image("TukiLogo")
                        .resizable()
                        .scaledToFit()
                        .frame(width: 50, height: 50)
                    Text("TUKI.")
                        .font(.system(size: 30, weight: .heavy))
                        .foregroundStyle(TukiPalette.teal)
                }

                Text("Welcome back")
                    .font(.system(size: 24, weight: .heavy))
                    .foregroundStyle(.black)
                    .padding(.top, 20)

                Text("Log in to continue your commute")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundStyle(TukiPalette.gray)
                    .padding(.top, 4)

                VStack(spacing: 10) {
                    TukiFormField(
                        label: "Email",
                        text: $viewModel.userName,
                        keyboardType: .emailAddress,
                        textContentType: .username
                    )

                    TukiFormField(
                        label: "Password",
                        text: $viewModel.password,
                        isSecure: true,
                        textContentType: .password
                    )

                    Button("Forgot password?") {}
                        .font(.system(size: 17, weight: .bold))
                        .foregroundStyle(TukiPalette.teal)
                        .frame(maxWidth: .infinity, alignment: .trailing)
                        .buttonStyle(.plain)
                }
                .padding(.top, 25)

                if let errorMessage = viewModel.errorMessage {
                    Text(errorMessage)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.error)
                        .multilineTextAlignment(.center)
                        .padding(.top, 12)
                        .accessibilityIdentifier("login-error-message")
                }

                TukiPrimaryButton(
                    title: viewModel.isAuthenticating ? "Logging in..." : "Log in",
                    isLoading: viewModel.isAuthenticating,
                    isEnabled: !viewModel.isAuthenticating,
                    action: viewModel.loginWithPassword
                )
                .padding(.top, 20)

                TukiDividerLabel(text: "OR")
                    .padding(.vertical, 15)

                HStack(spacing: 12) {
                    TukiCompactSocialButton(
                        title: "Google",
                        imageName: "GoogleLogo",
                        isEnabled: !viewModel.isAuthenticating,
                        action: viewModel.loginWithGoogle
                    )

                    TukiCompactSocialButton(
                        title: "Facebook",
                        imageName: "FacebookLogo",
                        isEnabled: !viewModel.isAuthenticating,
                        action: viewModel.loginWithFacebook
                    )
                }

                Button(action: onGuest) {
                    Text("Continue as Guest")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(TukiPalette.dark)
                        .frame(maxWidth: .infinity)
                        .frame(height: 56)
                        .background(.white)
                        .overlay {
                            RoundedRectangle(cornerRadius: 16, style: .continuous)
                                .stroke(TukiPalette.border, lineWidth: 2)
                        }
                }
                .buttonStyle(.plain)
                .disabled(viewModel.isAuthenticating)
                .opacity(viewModel.isAuthenticating ? 0.65 : 1)
                .padding(.top, 12)

                #if DEBUG
                if let diagnostic = viewModel.facebookLoginDiagnostic {
                    FacebookLoginDiagnosticView(diagnostic: diagnostic)
                        .padding(.top, 10)
                }
                #endif

                HStack(spacing: 0) {
                    Text("New to Tuki? ")
                        .foregroundStyle(TukiPalette.gray)
                        .fontWeight(.semibold)
                    Button("Sign up", action: onSignUp)
                        .foregroundStyle(TukiPalette.orange)
                        .fontWeight(.bold)
                        .buttonStyle(.plain)
                }
                .font(.system(size: 17))
                .padding(.top, 8)
            }
            .padding(.horizontal, 34)
            .padding(.top, 25)
            .padding(.bottom, 15)
        }
        .background(.white)
        .scrollDismissesKeyboard(.interactively)
    }
}

private struct TukiDividerLabel: View {
    let text: String

    var body: some View {
        HStack(spacing: 14) {
            Rectangle()
                .fill(Color.gray.opacity(0.35))
                .frame(height: 1)
            Text(text)
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(TukiPalette.gray)
                .fixedSize()
            Rectangle()
                .fill(Color.gray.opacity(0.35))
                .frame(height: 1)
        }
    }
}

private struct TukiCompactSocialButton: View {
    let title: String
    let imageName: String
    let isEnabled: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 6) {
                Image(imageName)
                    .resizable()
                    .scaledToFit()
                    .frame(width: 20, height: 20)
                Text(title)
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(TukiPalette.dark)
            }
            .frame(maxWidth: .infinity)
            .frame(height: 56)
            .background(.white)
            .overlay {
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .stroke(TukiPalette.border, lineWidth: 2)
            }
        }
        .buttonStyle(.plain)
        .disabled(!isEnabled)
        .opacity(isEnabled ? 1 : 0.65)
    }
}

private struct TukiSignupView: View {
    let onLogin: () -> Void

    @State private var fullName = ""
    @State private var email = ""
    @State private var password = ""
    @State private var confirmPassword = ""

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                TukiLogoHeader(logoSize: 75, titleSize: 32)

                Text("Create an account")
                    .font(.system(size: 26, weight: .heavy))
                    .foregroundStyle(.black)
                    .padding(.top, 16)

                Text("Start your seamless commute today")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundStyle(TukiPalette.gray)
                    .padding(.top, 4)

                VStack(spacing: 10) {
                    TukiCompactFormField(label: "Full Name", text: $fullName)
                    TukiCompactFormField(label: "Email", text: $email, keyboardType: .emailAddress)
                    TukiCompactFormField(label: "Password", text: $password, isSecure: true)
                    TukiCompactFormField(label: "Confirm Password", text: $confirmPassword, isSecure: true)
                }
                .padding(.top, 20)

                TukiPrimaryButton(title: "Sign up", action: {})
                    .padding(.top, 20)

                HStack(spacing: 0) {
                    Text("Already have an account? ")
                        .foregroundStyle(TukiPalette.gray)
                        .fontWeight(.medium)
                    Button("Log in", action: onLogin)
                        .foregroundStyle(TukiPalette.orange)
                        .fontWeight(.bold)
                        .buttonStyle(.plain)
                }
                .font(.system(size: 17))
                .padding(.vertical, 16)
            }
            .padding(.horizontal, 28)
            .padding(.top, 20)
        }
        .background(.white)
        .scrollDismissesKeyboard(.interactively)
    }
}

#if DEBUG
private struct FacebookLoginDiagnosticView: View {
    let diagnostic: FacebookLoginDiagnosticReport

    var body: some View {
        VStack(alignment: .leading, spacing: 5) {
            Text("Facebook Login Debug")
                .font(.caption.weight(.semibold))
            ForEach(diagnostic.lines, id: \.self) { line in
                Text(line)
                    .font(.caption2.monospaced())
            }
        }
        .foregroundStyle(TukiPalette.dark)
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(TukiPalette.cream)
        .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
        .accessibilityIdentifier("facebook-login-debug-diagnostic")
    }
}
#endif

#Preview {
    ContentView()
}
