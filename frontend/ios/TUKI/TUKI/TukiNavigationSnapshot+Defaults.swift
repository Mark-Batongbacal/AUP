import Foundation

extension TukiNavigationSnapshot {
    init(
        sessionId: String,
        state: String,
        currentLegIndex: Int,
        currentLeg: TukiNavigationLeg?,
        nextInstruction: TukiNavigationInstruction?,
        spokenInstruction: String?,
        remainingDistanceMeters: Double?,
        progressMeters: Double,
        boardInfo: TukiNavigationStopInfo?,
        alightInfo: TukiNavigationStopInfo?,
        landmark: TukiNavigationLandmark?,
        requiresBoardingConfirmation: Bool,
        requiresAlightingConfirmation: Bool,
        rerouteRequired: Bool,
        status: String,
        triggeredEvents: [TukiNavigationEvent]
    ) {
        self.init(
            sessionId: sessionId,
            state: state,
            currentLegIndex: currentLegIndex,
            currentLeg: currentLeg,
            nextInstruction: nextInstruction,
            spokenInstruction: spokenInstruction,
            remainingDistanceMeters: remainingDistanceMeters,
            progressMeters: progressMeters,
            boardInfo: boardInfo,
            alightInfo: alightInfo,
            landmark: landmark,
            requiresBoardingConfirmation: requiresBoardingConfirmation,
            requiresAlightingConfirmation: requiresAlightingConfirmation,
            rerouteRequired: rerouteRequired,
            status: status,
            triggeredEvents: triggeredEvents,
            currentLatitude: nil,
            currentLongitude: nil,
            approxFareSpent: 0,
            estimatedRemainingFare: 0,
            followingInstruction: nil,
            tripSummary: nil
        )
    }
}
