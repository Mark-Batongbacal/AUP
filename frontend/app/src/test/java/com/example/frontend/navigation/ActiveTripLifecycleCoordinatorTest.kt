package com.example.frontend.navigation

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.async
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ActiveTripLifecycleCoordinatorTest {
    @Test
    fun successfulReplacement_cancelsOnceThenStartsPendingTrip() = runBlocking {
        val coordinator = ActiveTripLifecycleCoordinator()
        val calls = mutableListOf<String>()
        val operations = mutableListOf<ActiveTripLifecycleOperation>()

        val result = coordinator.replaceActiveTrip(
            cancelCurrentTrip = {
                calls += "cancel"
                ActiveTripLifecycleStep.Success(Unit)
            },
            startReplacementTrip = {
                calls += "start:new-recommendation"
                ActiveTripLifecycleStep.Success("new-session")
            },
            onOperationChanged = operations::add
        )

        assertEquals(listOf("cancel", "start:new-recommendation"), calls)
        assertEquals(
            listOf(
                ActiveTripLifecycleOperation.CANCELING_CURRENT_TRIP,
                ActiveTripLifecycleOperation.STARTING_REPLACEMENT_TRIP,
                ActiveTripLifecycleOperation.IDLE
            ),
            operations
        )
        assertEquals(
            "new-session",
            (result as ActiveTripReplacementResult.Started).value
        )
    }

    @Test
    fun failedCancellation_keepsCurrentTripAndNeverStartsReplacement() = runBlocking {
        val coordinator = ActiveTripLifecycleCoordinator()
        var startCalls = 0

        val result = coordinator.replaceActiveTrip(
            cancelCurrentTrip = {
                ActiveTripLifecycleStep.Failure("Could not end the current trip.")
            },
            startReplacementTrip = {
                startCalls++
                ActiveTripLifecycleStep.Success("unexpected")
            }
        )

        assertEquals(0, startCalls)
        assertTrue(result is ActiveTripReplacementResult.CancelFailed)
    }

    @Test
    fun failedStart_reportsRetryableFailureAfterSuccessfulCancellation() = runBlocking {
        val coordinator = ActiveTripLifecycleCoordinator()
        var cancelCalls = 0

        val result = coordinator.replaceActiveTrip(
            cancelCurrentTrip = {
                cancelCalls++
                ActiveTripLifecycleStep.Success(Unit)
            },
            startReplacementTrip = {
                ActiveTripLifecycleStep.Failure("The new trip could not start.")
            }
        )

        assertEquals(1, cancelCalls)
        assertTrue(result is ActiveTripReplacementResult.StartFailed)
    }

    @Test
    fun repeatedReplacementTap_isRejectedWhileFirstOperationIsRunning() = runBlocking {
        val coordinator = ActiveTripLifecycleCoordinator()
        val allowFirstCancelToFinish = CompletableDeferred<Unit>()
        var cancelCalls = 0
        var startCalls = 0

        val first = async {
            coordinator.replaceActiveTrip(
                cancelCurrentTrip = {
                    cancelCalls++
                    allowFirstCancelToFinish.await()
                    ActiveTripLifecycleStep.Success(Unit)
                },
                startReplacementTrip = {
                    startCalls++
                    ActiveTripLifecycleStep.Success("new-session")
                }
            )
        }

        while (cancelCalls == 0) {
            kotlinx.coroutines.yield()
        }

        val repeated = coordinator.replaceActiveTrip(
            cancelCurrentTrip = {
                cancelCalls++
                ActiveTripLifecycleStep.Success(Unit)
            },
            startReplacementTrip = {
                startCalls++
                ActiveTripLifecycleStep.Success("duplicate-session")
            }
        )

        assertTrue(repeated is ActiveTripReplacementResult.Busy)
        allowFirstCancelToFinish.complete(Unit)
        assertTrue(first.await() is ActiveTripReplacementResult.Started)
        assertEquals(1, cancelCalls)
        assertEquals(1, startCalls)
    }
}
