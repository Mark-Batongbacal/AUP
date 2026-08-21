import CoreLocation
import SwiftUI

private struct ParityAIMessage: Identifiable {
    let id = UUID()
    let text: String
    let isFromUser: Bool
    let requestText: String?
    let journeys: [TukiAssistantRoute]
    let destination: TukiPlace?
    let destinationChoices: [TukiPlace]

    init(
        text: String,
        isFromUser: Bool,
        requestText: String? = nil,
        journeys: [TukiAssistantRoute] = [],
        destination: TukiPlace? = nil,
        destinationChoices: [TukiPlace] = []
    ) {
        self.text = text
        self.isFromUser = isFromUser
        self.requestText = requestText
        self.journeys = journeys
        self.destination = destination
        self.destinationChoices = destinationChoices
    }
}

struct TukiParityAIChat: View {
    let userName: String
    let api: TukiAssistantAPI?
    @ObservedObject var location: TukiLocationService
    let onBack: () -> Void
    let onRouteSelected: (TukiPlace, TukiRouteChoice) -> Void

    @State private var messages: [ParityAIMessage] = []
    @State private var input = ""
    @State private var thinking = false

    private let quickPrompts = [
        "Cheapest route to SM City Clark",
        "Fastest route to Dau Terminal"
    ]

    var body: some View {
        VStack(spacing: 0) {
            header
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(spacing: 12) {
                        ForEach(messages) { message in
                            messageView(message)
                                .id(message.id)
                        }
                        if thinking {
                            HStack {
                                Text("•••")
                                    .font(.system(size: 14, weight: .bold))
                                    .foregroundStyle(.white.opacity(0.7))
                                    .padding(.horizontal, 16)
                                    .padding(.vertical, 10)
                                    .background(TukiPalette.dark)
                                    .clipShape(RoundedRectangle(cornerRadius: 16))
                                Spacer()
                            }
                        }
                        if messages.count <= 1 {
                            quickPromptSection
                        }
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 8)
                }
                .onChange(of: messages.count) { _, _ in
                    if let id = messages.last?.id {
                        withAnimation { proxy.scrollTo(id, anchor: .bottom) }
                    }
                }
            }
            inputBar
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .onAppear {
            guard messages.isEmpty else { return }
            messages = [ParityAIMessage(
                text: "Hi \(userName)! Tell me where you want to go, your budget, or whether you prefer the cheapest or fastest route.",
                isFromUser: false
            )]
        }
    }

    private var header: some View {
        HStack(spacing: 10) {
            Button(action: onBack) {
                Text("←")
                    .font(.system(size: 24, weight: .bold))
                    .foregroundStyle(TukiPalette.dark)
                    .frame(width: 40, height: 40)
            }
            .buttonStyle(.plain)

            Text("✨")
                .font(.system(size: 18))
                .frame(width: 38, height: 38)
                .background(TukiPalette.teal.opacity(0.12))
                .clipShape(RoundedRectangle(cornerRadius: 12))

            VStack(alignment: .leading, spacing: 1) {
                Text("Ask our AI")
                    .font(.system(size: 20, weight: .heavy))
                    .foregroundStyle(TukiPalette.dark)
                Text("Get TUKI route recommendations")
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(TukiPalette.gray)
            }
            Spacer()
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 14)
        .background(TukiPalette.cream)
    }

    @ViewBuilder
    private func messageView(_ message: ParityAIMessage) -> some View {
        HStack {
            if message.isFromUser { Spacer(minLength: 48) }
            VStack(alignment: message.isFromUser ? .trailing : .leading, spacing: 8) {
                Text(message.text)
                    .font(.system(size: 14))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 14)
                    .padding(.vertical, 10)
                    .background(message.isFromUser ? TukiPalette.orange : TukiPalette.dark)
                    .clipShape(RoundedRectangle(cornerRadius: 16))

                if !message.isFromUser {
                    ForEach(message.destinationChoices) { place in
                        destinationChoice(place, requestText: message.requestText ?? place.name)
                    }

                    if let destination = message.destination {
                        ForEach(Array(message.journeys.enumerated()), id: \.element.id) { index, journey in
                            routeCard(journey, alternativeNumber: index + 1) {
                                onRouteSelected(destination, destinationChoice(journey.choice, destination: destination))
                            }
                        }
                    }
                }
            }
            if !message.isFromUser { Spacer(minLength: 0) }
        }
        .frame(maxWidth: .infinity)
    }

    private func destinationChoice(_ place: TukiPlace, requestText: String) -> some View {
        Button {
            send(requestText, destinationId: place.id)
        } label: {
            HStack(spacing: 10) {
                Text("📍").font(.system(size: 17))
                VStack(alignment: .leading, spacing: 2) {
                    Text(place.name)
                        .font(.system(size: 14, weight: .bold))
                    if let address = place.address, !address.isEmpty {
                        Text(address)
                            .font(.system(size: 11))
                            .opacity(0.75)
                    }
                }
                Spacer()
                Text("Select")
                    .font(.system(size: 12, weight: .bold))
            }
            .foregroundStyle(.white)
            .padding(.horizontal, 14)
            .padding(.vertical, 12)
            .background(TukiPalette.teal)
            .clipShape(RoundedRectangle(cornerRadius: 14))
        }
        .buttonStyle(.plain)
    }

    private func routeCard(
        _ journey: TukiAssistantRoute,
        alternativeNumber: Int,
        action: @escaping () -> Void
    ) -> some View {
        let tags = journey.recommendationType
            .split(separator: ",")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
        var labels: [String] = []
        if tags.contains("efficient") { labels.append("Balanced") }
        if tags.contains("cheapest") { labels.append("Cheapest") }
        if tags.contains("fastest") { labels.append("Fastest") }
        let label = labels.isEmpty ? "Alternative \(alternativeNumber)" : labels.joined(separator: " · ")
        let icon = tags.contains("efficient") ? "⚖️" : tags.contains("cheapest") ? "₱" : tags.contains("fastest") ? "⚡" : "🔄"

        return Button(action: action) {
            VStack(alignment: .leading, spacing: 9) {
                HStack {
                    Text("\(icon) \(label)")
                        .font(.system(size: 17, weight: .heavy))
                    Spacer()
                    Text("View route ›")
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(TukiPalette.orange)
                }
                HStack(spacing: 16) {
                    Text("₱\(Int(journey.farePesos.rounded()))")
                        .font(.system(size: 15, weight: .bold))
                    Text("~\(Int((journey.durationSeconds / 60).rounded())) min")
                        .font(.system(size: 15, weight: .bold))
                }
                Text("\(Int(journey.walkingMeters.rounded())) m walk")
                    .font(.system(size: 12))
                    .opacity(0.75)
                if !journey.routeNames.isEmpty {
                    Text(journey.routeNames.joined(separator: " → "))
                        .font(.system(size: 12))
                        .opacity(0.78)
                }
            }
            .foregroundStyle(.white)
            .padding(16)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(TukiPalette.dark)
            .clipShape(RoundedRectangle(cornerRadius: 18))
        }
        .buttonStyle(.plain)
    }

    private var quickPromptSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Try asking:")
                .font(.system(size: 12, weight: .bold))
                .foregroundStyle(TukiPalette.gray)
            ForEach(quickPrompts, id: \.self) { prompt in
                Button(prompt) { send(prompt) }
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(TukiPalette.dark)
                    .padding(.horizontal, 14)
                    .padding(.vertical, 9)
                    .background(TukiPalette.teal.opacity(0.12))
                    .clipShape(Capsule())
                    .buttonStyle(.plain)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var inputBar: some View {
        HStack(spacing: 8) {
            TextField("Type your message...", text: $input)
                .foregroundStyle(.white)
                .padding(.horizontal, 15)
                .frame(height: 44)
                .background(.white.opacity(0.08))
                .clipShape(Capsule())
                .disabled(thinking)

            Button {
                send(input)
            } label: {
                Text("➤")
                    .font(.system(size: 17, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 44, height: 44)
                    .background(TukiPalette.orange.opacity(canSend ? 1 : 0.45))
                    .clipShape(Circle())
            }
            .buttonStyle(.plain)
            .disabled(!canSend)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .background(TukiPalette.dark)
    }

    private var canSend: Bool {
        !input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !thinking
    }

    private func send(_ text: String, destinationId: String? = nil) {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty, !thinking else { return }

        messages.append(ParityAIMessage(text: value, isFromUser: true))
        input = ""
        thinking = true

        Task {
            defer { thinking = false }
            guard let api else {
                messages.append(ParityAIMessage(
                    text: "TUKI could not load the assistant configuration.",
                    isFromUser: false
                ))
                return
            }
            guard let current = await location.requestCurrentLocation() else {
                messages.append(ParityAIMessage(
                    text: TukiServiceArea.locationFailureMessage,
                    isFromUser: false
                ))
                return
            }

            switch await api.ask(TukiAssistantRequest(
                message: value,
                originLatitude: current.coordinate.latitude,
                originLongitude: current.coordinate.longitude,
                destinationId: destinationId
            )) {
            case .success(let response):
                messages.append(ParityAIMessage(
                    text: response.message,
                    isFromUser: false,
                    requestText: value,
                    journeys: response.journeys,
                    destination: response.destination,
                    destinationChoices: response.destinations
                ))
            case .failure(let error):
                messages.append(ParityAIMessage(text: error.message, isFromUser: false))
            }
        }
    }

    private func destinationChoice(_ route: TukiRouteChoice, destination: TukiPlace) -> TukiRouteChoice {
        let steps = route.steps.enumerated().map { index, step in
            CommuteStep(
                mode: step.mode,
                from: step.from,
                to: index == route.steps.count - 1 ? destination.name : step.to,
                minutes: step.minutes,
                fare: step.fare
            )
        }
        return TukiRouteChoice(
            id: route.id,
            label: route.label,
            totalMinutes: route.totalMinutes,
            totalFare: route.totalFare,
            walkMeters: route.walkMeters,
            transfers: route.transfers,
            generalCost: route.generalCost,
            isRecommended: route.isRecommended,
            steps: steps,
            legRoutePoints: route.legRoutePoints,
            legEndPoints: route.legEndPoints
        )
    }
}
