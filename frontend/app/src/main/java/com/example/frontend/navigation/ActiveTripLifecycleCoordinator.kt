package com.example.frontend.navigation

import kotlinx.coroutines.sync.Mutex

enum class ActiveTripLifecycleOperation {
    IDLE,
    CANCELING_CURRENT_TRIP,
    STARTING_REPLACEMENT_TRIP
}

sealed interface ActiveTripLifecycleStep<out T> {
    data class Success<T>(val value: T) : ActiveTripLifecycleStep<T>
    data class Failure(val message: String) : ActiveTripLifecycleStep<Nothing>
}

sealed interface ActiveTripReplacementResult<out T> {
    data class Started<T>(val value: T) : ActiveTripReplacementResult<T>
    data class CancelFailed(val message: String) : ActiveTripReplacementResult<Nothing>
    data class StartFailed(val message: String) : ActiveTripReplacementResult<Nothing>
    data object Busy : ActiveTripReplacementResult<Nothing>
}

/**
 * Serializes the destructive part of replacing an active trip.
 *
 * Navigation UI owns the selected route and screen transitions. This coordinator guarantees that
 * the current trip is canceled once, the replacement is not started after a failed cancellation,
 * and repeated taps cannot launch a second replacement operation.
 */
class ActiveTripLifecycleCoordinator {
    private val operationMutex = Mutex()

    suspend fun <T> replaceActiveTrip(
        cancelCurrentTrip: suspend () -> ActiveTripLifecycleStep<Unit>,
        startReplacementTrip: suspend () -> ActiveTripLifecycleStep<T>,
        onOperationChanged: (ActiveTripLifecycleOperation) -> Unit = {}
    ): ActiveTripReplacementResult<T> {
        if (!operationMutex.tryLock()) return ActiveTripReplacementResult.Busy

        return try {
            onOperationChanged(ActiveTripLifecycleOperation.CANCELING_CURRENT_TRIP)
            when (val cancelResult = cancelCurrentTrip()) {
                is ActiveTripLifecycleStep.Failure ->
                    ActiveTripReplacementResult.CancelFailed(cancelResult.message)

                is ActiveTripLifecycleStep.Success -> {
                    onOperationChanged(ActiveTripLifecycleOperation.STARTING_REPLACEMENT_TRIP)
                    when (val startResult = startReplacementTrip()) {
                        is ActiveTripLifecycleStep.Success ->
                            ActiveTripReplacementResult.Started(startResult.value)

                        is ActiveTripLifecycleStep.Failure ->
                            ActiveTripReplacementResult.StartFailed(startResult.message)
                    }
                }
            }
        } finally {
            onOperationChanged(ActiveTripLifecycleOperation.IDLE)
            operationMutex.unlock()
        }
    }
}
