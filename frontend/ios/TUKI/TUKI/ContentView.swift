//
//  ContentView.swift
//  TUKI
//
//  Created by Stephen Kurl Pinacate on 8/18/26.
//

import SwiftUI

struct ContentView: View {
    @StateObject private var authViewModel = AuthViewModel()

    var body: some View {
        Group {
            if authViewModel.isAuthenticated {
                AuthenticatedHomeView(
                    onSignOut: authViewModel.signOut
                )
            } else {
                LoginView(viewModel: authViewModel)
            }
        }
        #if DEBUG
        .safeAreaInset(edge: .bottom) {
            if let diagnostic = authViewModel.facebookLoginDiagnostic {
                FacebookLoginDiagnosticView(diagnostic: diagnostic)
            }
        }
        #endif
    }
}

private struct LoginView: View {
    @ObservedObject var viewModel: AuthViewModel

    var body: some View {
        NavigationStack {
            VStack(spacing: 28) {
                VStack(spacing: 8) {
                    Image(systemName: "figure.wave.circle.fill")
                        .font(.system(size: 54))
                        .foregroundStyle(.teal)

                    Text("TUKI")
                        .font(.largeTitle)
                        .fontWeight(.bold)

                    Text("Sign in to continue your commute.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }

                VStack(spacing: 14) {
                    TextField("Username", text: $viewModel.userName)
                        .textContentType(.username)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .submitLabel(.next)
                        .textFieldStyle(.roundedBorder)

                    SecureField("Password", text: $viewModel.password)
                        .textContentType(.password)
                        .submitLabel(.go)
                        .textFieldStyle(.roundedBorder)
                        .onSubmit(viewModel.loginWithPassword)

                    Button(action: viewModel.loginWithPassword) {
                        HStack {
                            if viewModel.isAuthenticating {
                                ProgressView()
                            }
                            Text("Log In")
                                .fontWeight(.semibold)
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .disabled(viewModel.isAuthenticating)
                }

                HStack {
                    Divider()
                    Text("or")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                    Divider()
                }

                Button(action: viewModel.loginWithGoogle) {
                    HStack(spacing: 10) {
                        Text("G")
                            .font(.headline)
                            .fontWeight(.semibold)
                        Text("Continue with Google")
                            .fontWeight(.semibold)
                    }
                    .frame(maxWidth: .infinity)
                }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .disabled(viewModel.isAuthenticating)

                Button(action: viewModel.loginWithFacebook) {
                    HStack(spacing: 10) {
                        Image(systemName: "f.circle.fill")
                            .font(.headline)
                        Text("Continue with Facebook")
                            .fontWeight(.semibold)
                    }
                    .frame(maxWidth: .infinity)
                }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .disabled(viewModel.isAuthenticating)

                if let errorMessage = viewModel.errorMessage {
                    Text(errorMessage)
                        .font(.footnote)
                        .foregroundStyle(.red)
                        .multilineTextAlignment(.center)
                        .accessibilityIdentifier("login-error-message")
                }
            }
            .padding(24)
            .navigationBarHidden(true)
        }
    }
}

private struct AuthenticatedHomeView: View {
    let onSignOut: () -> Void

    var body: some View {
        NavigationStack {
            VStack(spacing: 16) {
                Image(systemName: "checkmark.seal.fill")
                    .font(.system(size: 48))
                    .foregroundStyle(.teal)
                Text("You're signed in")
                    .font(.title2)
                    .fontWeight(.semibold)
                Text("TUKI is ready for authenticated API requests.")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }
            .padding()
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Sign Out", action: onSignOut)
                }
            }
        }
    }
}

#if DEBUG
private struct FacebookLoginDiagnosticView: View {
    let diagnostic: FacebookLoginDiagnosticReport

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Facebook Login Debug")
                .font(.caption)
                .fontWeight(.semibold)

            ForEach(diagnostic.lines, id: \.self) { line in
                Text(line)
                    .font(.caption2)
                    .monospaced()
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
        .background(.thinMaterial)
        .overlay(alignment: .top) {
            Divider()
        }
        .accessibilityIdentifier("facebook-login-debug-diagnostic")
    }
}
#endif

#Preview {
    ContentView()
}
