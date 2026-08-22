package com.example.frontend.navigation

import com.example.frontend.core.location.RouteCoordinate
import com.example.frontend.core.location.RouteCorridorDetector
import com.example.frontend.core.location.RouteMatch
import com.example.frontend.core.location.RouteMatcher
import com.example.frontend.data.navigation.NavigationInstructionDetailDto
import com.example.frontend.data.navigation.NavigationLandmarkDto
import kotlin.math.abs
import kotlin.math.max

enum class LocalLegProximity {
    NORMAL,
    APPROACHING,
    REACHED
}

enum class LocalGuidanceStage {
    ADVANCE,
    NOW
}

data class LocalNavigationGuidance(
    val sequence: Int,
    val type: String,
    val text: String,
    val streetName: String?,
    val anchorProgressMeters: Double,
    val distanceMeters: Double,
    val stage: LocalGuidanceStage
)

data class LocalNavigationLandmarkEvent(
    val name: String,
    val category: String,
    val role: String,
    val relation: String
)

data class LocalNavigationProgress(
    val rawLocation: RouteCoordinate,
    val matchedLocation: RouteCoordinate,
    val progressMeters: Double,
    val remainingMeters: Double,
    val distanceToRouteMeters: Double,
    val remainingRoute: List<RouteCoordinate>,
    val currentGuidance: LocalNavigationGuidance?,
    val followingGuidance: LocalNavigationGuidance?,
    val landmarkEvent: LocalNavigationLandmarkEvent?,
    val legProximity: LocalLegProximity,
    val shouldForceServerSync: Boolean
)

/**
 * Executes high-frequency navigation progress locally against the already planned leg geometry.
 * Server calls are reserved for meaningful state changes and confirmed route deviations.
 */
class LocalNavigationEngine(
    private val backtrackAllowanceMeters: Double = 30.0,
    private val baseMatchToleranceMeters: Double = 30.0,
    private val maximumMatchToleranceMeters: Double = 55.0,
    private val maximumUsableAccuracyMeters: Double = 75.0,
    private val requiredEndFixes: Int = 2
) {
    private var activeLegIndex: Int? = null
    private var activeRouteKey: String? = null
    private var lastProgressMeters = 0.0
    private var lastAcceptedMatch: RouteMatch? = null
    private var consecutiveEndFixes = 0
    private val consumedInstructionSequences = mutableSetOf<Int>()
    private val consumedLandmarks = mutableSetOf<String>()
    private var corridorDetector = RouteCorridorDetector()

    fun update(
        raw: RouteCoordinate,
        accuracyMeters: Double?,
        legIndex: Int,
        transportMode: String?,
        route: List<RouteCoordinate>,
        instructions: List<NavigationInstructionDetailDto>,
        landmarks: List<NavigationLandmarkDto>
    ): LocalNavigationProgress? {
        if (route.size < 2) return null
        resetIfLegChanged(legIndex, route)

        val previousProgress = lastProgressMeters
        val routeMatch = RouteMatcher.match(
            raw = raw,
            route = route,
            minimumProgressMeters = (lastProgressMeters - backtrackAllowanceMeters).coerceAtLeast(0.0)
        )
        val toleranceMeters = max(
            baseMatchToleranceMeters,
            (accuracyMeters ?: 0.0).coerceAtLeast(0.0) * 1.25
        ).coerceAtMost(maximumMatchToleranceMeters)
        val accepted = routeMatch?.takeIf { it.distanceToRouteMeters <= toleranceMeters }
        if (accepted != null) {
            if (accepted.progressMeters > lastProgressMeters) lastProgressMeters = accepted.progressMeters
            lastAcceptedMatch = accepted
        }

        val totalDistanceMeters = route.zipWithNext { start, end ->
            RouteMatcher.distanceMeters(start, end)
        }.sum()
        val remainingMeters = (totalDistanceMeters - lastProgressMeters).coerceAtLeast(0.0)
        val displayMatch = accepted ?: lastAcceptedMatch
        val matchedLocation = accepted?.coordinate ?: raw
        val remainingRoute = RouteMatcher.remainingRoute(route, displayMatch)
        val distanceToRoute = routeMatch?.distanceToRouteMeters ?: Double.POSITIVE_INFINITY

        val corridorDecision = corridorDetector.update(distanceToRoute, accuracyMeters)
        val proximity = updateLegProximity(
            remainingMeters = remainingMeters,
            transportMode = transportMode,
            accuracyMeters = accuracyMeters,
            hasAcceptedMatch = accepted != null
        )
        val guidance = selectGuidance(route, instructions, lastProgressMeters)
        val following = guidance?.let { selected ->
            selectGuidance(
                route = route,
                instructions = instructions.filter { it.sequence > selected.sequence },
                progressMeters = lastProgressMeters,
                consumePassed = false
            )
        }
        val landmark = detectLandmark(
            route = route,
            landmarks = landmarks,
            previousProgressMeters = previousProgress,
            currentProgressMeters = lastProgressMeters
        )

        return LocalNavigationProgress(
            rawLocation = raw,
            matchedLocation = matchedLocation,
            progressMeters = lastProgressMeters,
            remainingMeters = remainingMeters,
            distanceToRouteMeters = distanceToRoute,
            remainingRoute = remainingRoute,
            currentGuidance = guidance,
            followingGuidance = following,
            landmarkEvent = landmark,
            legProximity = proximity,
            shouldForceServerSync = corridorDecision.shouldForceSync || proximity == LocalLegProximity.REACHED
        )
    }

    fun reset() {
        activeLegIndex = null
        activeRouteKey = null
        lastProgressMeters = 0.0
        lastAcceptedMatch = null
        consecutiveEndFixes = 0
        consumedInstructionSequences.clear()
        consumedLandmarks.clear()
        corridorDetector.reset()
    }

    private fun resetIfLegChanged(legIndex: Int, route: List<RouteCoordinate>) {
        val first = route.first()
        val last = route.last()
        val routeKey = "$legIndex:${route.size}:${"%.6f".format(first.latitude)}:${"%.6f".format(first.longitude)}:${"%.6f".format(last.latitude)}:${"%.6f".format(last.longitude)}"
        if (activeLegIndex == legIndex && activeRouteKey == routeKey) return

        activeLegIndex = legIndex
        activeRouteKey = routeKey
        lastProgressMeters = 0.0
        lastAcceptedMatch = null
        consecutiveEndFixes = 0
        consumedInstructionSequences.clear()
        consumedLandmarks.clear()
        corridorDetector = RouteCorridorDetector()
    }

    private fun updateLegProximity(
        remainingMeters: Double,
        transportMode: String?,
        accuracyMeters: Double?,
        hasAcceptedMatch: Boolean
    ): LocalLegProximity {
        val transit = transportMode.equals("JEEPNEY", true) ||
            transportMode.equals("TRICYCLE", true) ||
            transportMode.equals("TRIKE", true)
        val approachingMeters = if (transit) 400.0 else 120.0
        val baseReachedMeters = if (transit) 75.0 else 35.0
        val accuracy = (accuracyMeters ?: 0.0).coerceAtLeast(0.0)
        val reachedMeters = max(baseReachedMeters, accuracy * 1.25)
            .coerceAtMost(if (transit) 120.0 else 60.0)
        val trustworthyFix = hasAcceptedMatch && accuracy <= maximumUsableAccuracyMeters

        consecutiveEndFixes = if (trustworthyFix && remainingMeters <= reachedMeters) {
            consecutiveEndFixes + 1
        } else {
            0
        }

        return when {
            consecutiveEndFixes >= requiredEndFixes -> LocalLegProximity.REACHED
            remainingMeters <= approachingMeters -> LocalLegProximity.APPROACHING
            else -> LocalLegProximity.NORMAL
        }
    }

    private fun selectGuidance(
        route: List<RouteCoordinate>,
        instructions: List<NavigationInstructionDetailDto>,
        progressMeters: Double,
        consumePassed: Boolean = true
    ): LocalNavigationGuidance? {
        val navigable = instructions
            .filter { instruction ->
                instruction.type.equals("Continue", true) ||
                    instruction.type.equals("TurnLeft", true) ||
                    instruction.type.equals("TurnRight", true) ||
                    instruction.type.equals("Roundabout", true)
            }
            .sortedBy { it.sequence }

        for (instruction in navigable) {
            if (instruction.sequence in consumedInstructionSequences) continue
            val anchor = instructionAnchor(route, instruction) ?: continue
            val passMargin = max(20.0, instruction.triggerDistanceMeters.coerceAtLeast(0.0))
            if (progressMeters > anchor + passMargin) {
                if (consumePassed) consumedInstructionSequences += instruction.sequence
                continue
            }

            val distance = (anchor - progressMeters).coerceAtLeast(0.0)
            val trigger = max(15.0, instruction.triggerDistanceMeters.coerceAtLeast(0.0))
            return LocalNavigationGuidance(
                sequence = instruction.sequence,
                type = instruction.type,
                text = instruction.text,
                streetName = instruction.streetName,
                anchorProgressMeters = anchor,
                distanceMeters = distance,
                stage = if (distance <= trigger) LocalGuidanceStage.NOW else LocalGuidanceStage.ADVANCE
            )
        }
        return null
    }

    private fun instructionAnchor(
        route: List<RouteCoordinate>,
        instruction: NavigationInstructionDetailDto
    ): Double? {
        val supplied = instruction.distanceFromLegStartMeters
        val projected = if (instruction.latitude != null && instruction.longitude != null) {
            RouteMatcher.match(
                raw = RouteCoordinate(instruction.latitude, instruction.longitude),
                route = route
            )?.progressMeters
        } else {
            null
        }
        return when {
            projected != null && supplied != null && abs(projected - supplied) <= 150.0 -> projected
            supplied != null -> supplied
            else -> projected
        }
    }

    private fun detectLandmark(
        route: List<RouteCoordinate>,
        landmarks: List<NavigationLandmarkDto>,
        previousProgressMeters: Double,
        currentProgressMeters: Double
    ): LocalNavigationLandmarkEvent? {
        if (currentProgressMeters <= previousProgressMeters) return null
        val candidates = landmarks
            .filter { it.role.equals("PROGRESS_REFERENCE", true) }
            .mapNotNull { landmark ->
                val key = landmarkKey(landmark)
                if (key in consumedLandmarks) return@mapNotNull null
                val anchor = RouteMatcher.match(
                    raw = RouteCoordinate(landmark.latitude, landmark.longitude),
                    route = route
                )?.progressMeters ?: return@mapNotNull null
                Triple(landmark, key, anchor)
            }
            .sortedBy { it.third }

        for ((landmark, key, anchor) in candidates) {
            val before = landmark.triggerBeforeMeters.coerceAtLeast(0.0)
            val after = landmark.triggerAfterMeters.coerceAtLeast(0.0)
            val crossed = anchor >= previousProgressMeters - after &&
                anchor <= currentProgressMeters + before
            if (!crossed) continue
            consumedLandmarks += key
            return LocalNavigationLandmarkEvent(
                name = landmark.name,
                category = landmark.category,
                role = landmark.role,
                relation = landmark.relation
            )
        }
        return null
    }

    private fun landmarkKey(landmark: NavigationLandmarkDto): String =
        "${landmark.name}:${"%.5f".format(landmark.latitude)}:${"%.5f".format(landmark.longitude)}"
}
