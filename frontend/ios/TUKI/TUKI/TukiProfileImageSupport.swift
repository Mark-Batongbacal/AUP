import SwiftUI
import UIKit

struct TukiProfileAvatar: View {
    let profileImageUrl: String?
    let initials: String
    let size: CGFloat

    var body: some View {
        ZStack {
            Circle().fill(TukiPalette.teal)
            if let value = profileImageUrl?.trimmingCharacters(in: .whitespacesAndNewlines),
               !value.isEmpty,
               let url = URL(string: value) {
                AsyncImage(url: url) { phase in
                    if case .success(let image) = phase {
                        image
                            .resizable()
                            .scaledToFill()
                    } else {
                        initialsView
                    }
                }
            } else {
                initialsView
            }
        }
        .frame(width: size, height: size)
        .clipShape(Circle())
    }

    private var initialsView: some View {
        Text(initials.isEmpty ? "?" : initials)
            .font(.system(size: size * 0.36, weight: .heavy))
            .foregroundStyle(.white)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

struct TukiProfileImageUploader {
    private let baseURL: URL
    private let credentialStore: TukiCredentialStore
    private let session: URLSession
    private let decoder = JSONDecoder()

    init(
        configuration: AppConfiguration,
        credentialStore: TukiCredentialStore = KeychainTukiCredentialStore(),
        session: URLSession = .shared
    ) {
        self.baseURL = configuration.backendBaseURL
        self.credentialStore = credentialStore
        self.session = session
    }

    static func configured() -> TukiProfileImageUploader? {
        guard let configuration = try? AppConfiguration.load() else { return nil }
        return TukiProfileImageUploader(configuration: configuration)
    }

    func upload(jpegData: Data) async -> Result<TukiUserProfile, TukiPlatformError> {
        guard !jpegData.isEmpty else {
            return .failure(.message("Choose a valid profile picture."))
        }
        guard jpegData.count <= 5 * 1024 * 1024 else {
            return .failure(.message("Profile pictures must be 5 MB or smaller."))
        }
        guard let credential = credentialStore.credential else {
            return .failure(.notAuthenticated)
        }

        let boundary = "TukiProfileImage-\(UUID().uuidString)"
        var body = Data()
        body.append("--\(boundary)\r\n")
        body.append("Content-Disposition: form-data; name=\"image\"; filename=\"profile.jpg\"\r\n")
        body.append("Content-Type: image/jpeg\r\n\r\n")
        body.append(jpegData)
        body.append("\r\n--\(boundary)--\r\n")

        var request = URLRequest(url: baseURL.appendingBackendPath("api/users/me/profile-image"))
        request.httpMethod = "POST"
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")
        request.setValue(credential.apiKey, forHTTPHeaderField: credential.headerName)
        request.httpBody = body

        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else {
                return .failure(.message("The server returned an invalid response."))
            }
            switch http.statusCode {
            case 200..<300:
                do {
                    return .success(try decoder.decode(TukiUserProfile.self, from: data))
                } catch {
                    return .failure(.message("The server returned data TUKI could not read."))
                }
            case 401:
                return .failure(.notAuthenticated)
            case 403:
                return .failure(.message("Guest profiles cannot have a profile picture."))
            default:
                return .failure(.message(Self.serverMessage(from: data) ?? "Profile photo upload failed (HTTP \(http.statusCode))."))
            }
        } catch let error as URLError {
            return .failure(.message(error.code == .timedOut
                ? "Network timeout. Check your connection and try again."
                : "Network error. Check your connection and try again."))
        } catch {
            return .failure(.message("Profile photo upload failed. Try again."))
        }
    }

    private static func serverMessage(from data: Data) -> String? {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
        if let message = object["message"] as? String, !message.isEmpty { return message }
        if let errors = object["errors"] as? [String], let first = errors.first, !first.isEmpty { return first }
        if let title = object["title"] as? String, !title.isEmpty { return title }
        return nil
    }
}

func tukiPreparedProfileJPEG(from data: Data) -> Data? {
    guard let image = UIImage(data: data) else { return nil }
    let maxDimension: CGFloat = 1024
    let originalSize = image.size
    let largestSide = max(originalSize.width, originalSize.height)
    guard largestSide > 0 else { return nil }

    let targetSize: CGSize
    if largestSide > maxDimension {
        let scale = maxDimension / largestSide
        targetSize = CGSize(
            width: max(1, floor(originalSize.width * scale)),
            height: max(1, floor(originalSize.height * scale))
        )
    } else {
        targetSize = originalSize
    }

    let renderer = UIGraphicsImageRenderer(size: targetSize)
    let normalized = renderer.image { _ in
        image.draw(in: CGRect(origin: .zero, size: targetSize))
    }
    return normalized.jpegData(compressionQuality: 0.85)
}

private extension Data {
    mutating func append(_ string: String) {
        if let value = string.data(using: .utf8) {
            append(value)
        }
    }
}
