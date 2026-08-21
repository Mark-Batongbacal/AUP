//
//  AppConfiguration.swift
//  TUKI
//

import Foundation

struct AppConfiguration {
    let backendBaseURL: URL
    let googleOAuth: GoogleOAuthConfiguration?
    let facebookOAuth: FacebookOAuthConfiguration?

    static func load(bundle: Bundle = .main) throws -> AppConfiguration {
        try load(infoDictionary: bundle.infoDictionary ?? [:])
    }

    static func load(infoDictionary: [String: Any]) throws -> AppConfiguration {
        guard
            let baseURLString = infoDictionary["TukiBackendBaseURL"] as? String,
            let backendBaseURL = URL(validBackendBaseURLString: baseURLString)
        else {
            throw AppConfigurationError.missingBackendBaseURL
        }

        let googleClientID = infoDictionary["GIDClientID"] as? String
        let googleServerClientID = infoDictionary["GIDServerClientID"] as? String
        let googleOAuth = GoogleOAuthConfiguration(
            clientID: googleClientID,
            serverClientID: googleServerClientID
        )
        let facebookOAuth = FacebookOAuthConfiguration(
            appID: infoDictionary["FacebookAppID"] as? String,
            clientToken: infoDictionary["FacebookClientToken"] as? String
        )

        return AppConfiguration(
            backendBaseURL: backendBaseURL,
            googleOAuth: googleOAuth,
            facebookOAuth: facebookOAuth
        )
    }
}

struct GoogleOAuthConfiguration: Equatable {
    let clientID: String
    let serverClientID: String

    init?(clientID: String?, serverClientID: String?) {
        guard let clientID, clientID.isConfiguredValue,
              let serverClientID, serverClientID.isConfiguredValue else {
            return nil
        }

        self.clientID = clientID
        self.serverClientID = serverClientID
    }
}

struct FacebookOAuthConfiguration: Equatable {
    let appID: String
    let clientToken: String

    init?(appID: String?, clientToken: String?) {
        guard let appID, appID.isConfiguredValue,
              let clientToken, clientToken.isConfiguredValue else {
            return nil
        }

        self.appID = appID
        self.clientToken = clientToken
    }
}

enum AppConfigurationError: Error, Equatable {
    case missingBackendBaseURL
}

extension URL {
    init?(validBackendBaseURLString rawValue: String) {
        let trimmed = rawValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty,
              !trimmed.hasPrefix("$("),
              let url = URL(string: trimmed.normalizedBaseURL),
              url.isAbsoluteHTTPURL else {
            return nil
        }

        self = url
    }

    var isAbsoluteHTTPURL: Bool {
        guard let scheme = scheme?.lowercased(),
              scheme == "http" || scheme == "https",
              host != nil else {
            return false
        }

        return true
    }

    func appendingBackendPath(_ path: String) -> URL {
        path
            .split(separator: "/")
            .reduce(self) { url, component in
                url.appendingPathComponent(String(component))
            }
    }
}

private extension String {
    var normalizedBaseURL: String {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.hasSuffix("/") ? trimmed : "\(trimmed)/"
    }

    var isConfiguredValue: Bool {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return !trimmed.isEmpty &&
            trimmed != "0" &&
            !trimmed.uppercased().hasPrefix("YOUR_") &&
            !trimmed.uppercased().hasPrefix("DEFAULT_")
    }
}
