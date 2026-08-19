package com.example.frontend.data.common

import java.math.BigDecimal

data class TransportModeSummaryDto(
    val transportModeId: Int,
    val code: String,
    val name: String,
    val isMotorized: Boolean,
    val allowsLiveDriver: Boolean,
    val iconName: String?
)

data class TransportStopSummaryDto(
    val stopId: Long,
    val stopCode: String?,
    val name: String,
    val description: String?,
    val stopType: String,
    val address: String?,
    val latitude: Double,
    val longitude: Double
)

data class DriverVehicleDto(
    val vehicleId: String,
    val driverId: String,
    val transportModeId: Int,
    val transportMode: TransportModeSummaryDto?,
    val plateNumber: String?,
    val bodyNumber: String?,
    val color: String?,
    val capacity: Int,
    val isActive: Boolean,
    val createdAt: String
)

data class DriverAvailabilitySessionDto(
    val sessionId: Long,
    val driverId: String,
    val vehicleId: String?,
    val vehicle: DriverVehicleDto?,
    val destinationStopId: Long?,
    val destinationStop: TransportStopSummaryDto?,
    val destinationName: String?,
    val destinationLatitude: Double?,
    val destinationLongitude: Double?,
    val availableSeats: Int,
    val maximumDetourMeters: BigDecimal,
    val status: String,
    val startedAt: String,
    val endedAt: String?
)

data class DriverLocationDto(
    val driverId: String,
    val latitude: Double,
    val longitude: Double,
    val headingDegrees: Double?,
    val speedKph: Double?,
    val accuracyMeters: Double?,
    val updatedAt: String
)
