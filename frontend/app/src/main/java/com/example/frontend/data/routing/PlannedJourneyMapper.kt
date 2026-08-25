package com.example.frontend.data.routing

import com.example.frontend.model.CommuteStep
import com.example.frontend.model.RouteOption
import com.example.frontend.model.RoutePoint
import com.example.frontend.core.localization.TukiInterfaceText
import kotlin.math.roundToInt

/**
 * Single conversion used by both normal route results and Ask TUKI.
 * RouteOption.id remains the backend recommendationId used by Start Navigation.
 */
fun PlannedJourney.toRouteOption(origin: String, destination: String): RouteOption {
    val plan = journey
    val recommendationTags = plan.source.recommendationType
        .split(',')
        .map { it.trim().lowercase() }
        .filter { it.isNotBlank() }
    val walkMeters = (
        plan.source.originAccess.walkDistanceMeters +
            plan.source.destinationAccess.walkDistanceMeters +
            plan.source.transferWalkDistancesMeters.sum()
        ).roundToInt()
    val legRoutePoints = plan.legs.map { leg ->
        leg.geometry.map { point -> RoutePoint(point.latitude, point.longitude) }
    }
    val routePoints = buildList {
        legRoutePoints.forEach { legPoints ->
            legPoints.forEach { point -> if (lastOrNull() != point) add(point) }
        }
    }

    return RouteOption(
        id = recommendationId,
        label = formatRecommendationLabel(recommendationTags),
        totalMinutes = (plan.source.totalTimeSeconds / 60).roundToInt(),
        totalFare = plan.source.totalFarePesos,
        walkMeters = walkMeters,
        transfers = plan.source.transferCount,
        generalCost = plan.source.generalizedCostPesos,
        isRecommended = "efficient" in recommendationTags,
        routePoints = routePoints,
        legRoutePoints = legRoutePoints,
        legEndPoints = plan.legs.map { leg ->
            RoutePoint(leg.destination.latitude, leg.destination.longitude)
        },
        legRouteIds = plan.legs.map { leg ->
            if (leg.mode == TransitMode.Jeepney) leg.routeId?.toLongOrNull() else null
        },
        steps = plan.legs.mapIndexed { legIndex, leg ->
            val mode = when (leg.mode) {
                TransitMode.Walk -> "Walk"
                TransitMode.Trike -> "Tricycle"
                TransitMode.Jeepney -> "Jeepney"
                is TransitMode.Unknown -> "Transit"
            }
            CommuteStep(
                mode = mode,
                from = when {
                    legIndex == 0 -> origin
                    leg.routeName?.isNotBlank() == true -> leg.routeName
                    else -> "Transfer point"
                },
                to = when {
                    legIndex == plan.legs.lastIndex -> destination
                    leg.routeName?.isNotBlank() == true -> leg.routeName
                    else -> "Transfer point"
                },
                minutes = (leg.durationSeconds / 60).roundToInt(),
                fare = leg.farePesos
            )
        }
    )
}

private fun formatRecommendationLabel(tags: List<String>): String {
    val fastest = "fastest" in tags
    val cheapest = "cheapest" in tags
    val efficient = "efficient" in tags
    return when {
        efficient && fastest -> if (TukiInterfaceText.isFilipino) "Pinakamainam · Pinakamabilis" else "Best Overall · Fastest"
        efficient && cheapest -> if (TukiInterfaceText.isFilipino) "Pinakamainam · Pinakamura" else "Best Overall · Cheapest"
        efficient -> if (TukiInterfaceText.isFilipino) "Pinakamainam" else "Best Overall"
        fastest -> if (TukiInterfaceText.isFilipino) "Pinakamabilis" else "Fastest"
        cheapest -> if (TukiInterfaceText.isFilipino) "Pinakamura" else "Cheapest"
        else -> tags.joinToString(" · ") { tag -> tag.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() } }
            .ifBlank { if (TukiInterfaceText.isFilipino) "Opsyon ng Ruta" else "Route option" }
    }
}
