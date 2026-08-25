package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.core.location.NavigationSyncSignal
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.ai.ActiveTripAssistantRequest
import com.example.frontend.data.ai.AssistantDestinationCandidateDto
import com.example.frontend.data.ai.AssistantJourneyDto
import com.example.frontend.data.ai.AssistantResponseDto
import com.example.frontend.data.navigation.NavigationSnapshotDto
import com.example.frontend.data.places.DestinationSearchResultDto
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

private val NavigationAiSurface = com.example.frontend.ui.theme.TukiCream
private val NavigationAiDark = com.example.frontend.ui.theme.TukiInk
private val NavigationAiTeal = com.example.frontend.ui.theme.TukiTeal
private val NavigationAiOrange = com.example.frontend.ui.theme.TukiOrange
private val NavigationAiMuted = com.example.frontend.ui.theme.TukiMuted
private val NavigationAiBubble = com.example.frontend.ui.theme.TukiSurfaceRaised

private data class NavigationAiMessage(
    val id: Long,
    val text: String,
    val fromUser: Boolean,
    val requestText: String? = null,
    val journeys: List<AssistantJourneyDto> = emptyList(),
    val destinationChoices: List<DestinationSearchResultDto> = emptyList(),
    val destination: DestinationSearchResultDto? = null,
    val tripSessionId: String? = null,
    val conversationId: String? = null
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NavigationAiSheet(
    language: String = com.example.frontend.core.localization.AppLanguagePreference.current(),
    onDismiss: () -> Unit,
    ask: suspend (String, String?) -> ApiResult<AssistantResponseDto>,
    confirmReplan: (suspend (String) -> ApiResult<NavigationSnapshotDto>)? = null,
    onReplanApplied: (NavigationSnapshotDto) -> Unit = {}
) {
    val context = LocalContext.current
    val provider = remember(context.applicationContext) {
        TukiDataProvider(context.applicationContext)
    }
    val filipino = language.equals("Filipino", ignoreCase = true)
    val quickPrompts = if (filipino) {
        listOf(
            "Tama pa ba yung route natin?",
            "Saan ako bababa?",
            "₱30 na lang pera ko",
            "Ayoko mag-trike",
            "Pagod na ako, less walking sana"
        )
    } else {
        listOf(
            "Am I still on the right route?",
            "Where do I get off?",
            "I only have ₱30 left",
            "I don't want to take a tricycle",
            "I'm tired, less walking please"
        )
    }
    val intro = if (filipino) {
        "Magtanong ka lang tungkol sa active trip natin. Kung may gusto kang baguhin, ipapakita ko muna yung bagong route bago natin palitan yung current trip."
    } else {
        "Ask me anything about this active trip. If you want to change something, I’ll show the replacement route first before changing the current trip."
    }
    val scope = rememberCoroutineScope()
    val listState = rememberLazyListState()
    var input by remember { mutableStateOf("") }
    var thinking by remember { mutableStateOf(false) }
    var applyingRecommendationId by remember { mutableStateOf<String?>(null) }
    var messages by remember(language) {
        mutableStateOf(
            listOf(
                NavigationAiMessage(
                    id = 0L,
                    text = intro,
                    fromUser = false
                )
            )
        )
    }

    LaunchedEffect(messages.size, thinking, applyingRecommendationId) {
        if (messages.isNotEmpty()) listState.animateScrollToItem(messages.lastIndex)
    }

    fun appendAssistantResponse(response: AssistantResponseDto, requestText: String) {
        messages = messages + NavigationAiMessage(
            id = System.currentTimeMillis() + 1,
            text = response.message,
            fromUser = false,
            requestText = requestText,
            journeys = response.journeys.orEmpty(),
            destinationChoices = response.destinations.orEmpty().map { it.toDestinationSearchResult() },
            destination = response.destination?.toDestinationSearchResult(),
            tripSessionId = response.action?.tripSessionId ?: response.navigation?.tripSessionId,
            conversationId = response.conversationId
        )
    }

    fun send(text: String, destinationId: String? = null) {
        val trimmed = text.trim()
        if (trimmed.isEmpty() || thinking || applyingRecommendationId != null) return
        messages = messages + NavigationAiMessage(
            id = System.currentTimeMillis(),
            text = trimmed,
            fromUser = true
        )
        input = ""
        thinking = true
        scope.launch {
            when (val result = ask(trimmed, destinationId)) {
                is ApiResult.Success -> appendAssistantResponse(result.data, trimmed)
                is ApiResult.Failure -> {
                    messages = messages + NavigationAiMessage(
                        id = System.currentTimeMillis() + 1,
                        text = result.message,
                        fromUser = false
                    )
                }
            }
            thinking = false
        }
    }

    fun selectDestination(message: NavigationAiMessage, place: DestinationSearchResultDto) {
        val requestText = message.requestText ?: place.name
        val sessionId = message.tripSessionId
        if (sessionId == null) {
            send(requestText, place.id)
            return
        }
        if (thinking || applyingRecommendationId != null) return

        messages = messages + NavigationAiMessage(
            id = System.currentTimeMillis(),
            text = place.name,
            fromUser = true
        )
        thinking = true
        scope.launch {
            when (val result = provider.aiRepository.askTrip(
                sessionId,
                ActiveTripAssistantRequest(
                    message = requestText,
                    destinationId = place.id,
                    conversationId = message.conversationId
                )
            )) {
                is ApiResult.Success -> appendAssistantResponse(result.data, requestText)
                is ApiResult.Failure -> {
                    messages = messages + NavigationAiMessage(
                        id = System.currentTimeMillis() + 1,
                        text = result.message,
                        fromUser = false
                    )
                }
            }
            thinking = false
        }
    }

    fun applyReplan(journey: AssistantJourneyDto, tripSessionId: String?) {
        if (thinking || applyingRecommendationId != null) return
        applyingRecommendationId = journey.journeyId
        scope.launch {
            val result = if (confirmReplan != null) {
                confirmReplan(journey.journeyId)
            } else if (tripSessionId != null) {
                when (val confirmation = provider.aiRepository.confirmTripReplan(
                    tripSessionId,
                    journey.journeyId
                )) {
                    is ApiResult.Failure -> confirmation
                    is ApiResult.Success -> {
                        NavigationSyncSignal.requestImmediateSync(samples = 1)
                        provider.navigationRepository.getActiveNavigation()
                    }
                }
            } else {
                ApiResult.Failure(null, "This route proposal is missing its active-trip context.")
            }

            when (result) {
                is ApiResult.Success -> {
                    onReplanApplied(result.data)
                    messages = messages + NavigationAiMessage(
                        id = System.currentTimeMillis() + 2,
                        text = if (filipino) {
                            "Okay, applied na yung pinili mong route. Yung bagong navigation state na ang susundan natin."
                        } else {
                            "Done. I applied the route you selected, and navigation is now following the updated trip."
                        },
                        fromUser = false
                    )
                }
                is ApiResult.Failure -> {
                    messages = messages + NavigationAiMessage(
                        id = System.currentTimeMillis() + 2,
                        text = result.message,
                        fromUser = false
                    )
                }
            }
            applyingRecommendationId = null
        }
    }

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = NavigationAiSurface,
        contentColor = NavigationAiDark,
        shape = RoundedCornerShape(topStart = 30.dp, topEnd = 30.dp)
    ) {
        Column(
            Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp)
                .padding(bottom = 18.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    Modifier.size(42.dp).background(NavigationAiTeal.copy(alpha = 0.14f), CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Text("✨", fontSize = 20.sp)
                }
                Spacer(Modifier.width(10.dp))
                Column {
                    Text("Ask TUKI", color = NavigationAiDark, fontSize = 22.sp, fontWeight = FontWeight.ExtraBold)
                    Text(
                        if (filipino) "Tanong at fine-tuning para sa active trip" else "Questions and fine-tuning for this active trip",
                        color = NavigationAiMuted,
                        fontSize = 12.sp
                    )
                }
            }

            Spacer(Modifier.height(12.dp))

            LazyColumn(
                state = listState,
                modifier = Modifier.fillMaxWidth().height(360.dp),
                verticalArrangement = Arrangement.spacedBy(9.dp)
            ) {
                items(messages, key = { it.id }) { message ->
                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalAlignment = if (message.fromUser) Alignment.End else Alignment.Start
                    ) {
                        Row(
                            Modifier.fillMaxWidth(),
                            horizontalArrangement = if (message.fromUser) Arrangement.End else Arrangement.Start
                        ) {
                            Box(
                                Modifier
                                    .fillMaxWidth(0.88f)
                                    .background(
                                        if (message.fromUser) NavigationAiOrange else NavigationAiBubble,
                                        RoundedCornerShape(16.dp)
                                    )
                                    .padding(horizontal = 13.dp, vertical = 10.dp)
                            ) {
                                Text(
                                    message.text,
                                    color = if (message.fromUser) Color.White else NavigationAiDark,
                                    fontSize = 13.sp,
                                    lineHeight = 18.sp
                                )
                            }
                        }

                        if (!message.fromUser && message.destinationChoices.isNotEmpty()) {
                            Spacer(Modifier.height(7.dp))
                            message.destinationChoices.forEach { place ->
                                NavigationDestinationChoiceCard(
                                    place = place,
                                    enabled = !thinking && applyingRecommendationId == null,
                                    onClick = { selectDestination(message, place) }
                                )
                                Spacer(Modifier.height(7.dp))
                            }
                        }

                        if (!message.fromUser && message.journeys.isNotEmpty()) {
                            Spacer(Modifier.height(8.dp))
                            message.journeys.forEachIndexed { index, journey ->
                                NavigationReplanCard(
                                    journey = journey,
                                    alternativeNumber = index + 1,
                                    applying = applyingRecommendationId == journey.journeyId,
                                    enabled = !thinking &&
                                        (applyingRecommendationId == null ||
                                            applyingRecommendationId == journey.journeyId),
                                    onApply = { applyReplan(journey, message.tripSessionId) }
                                )
                                Spacer(Modifier.height(8.dp))
                            }
                        }
                    }
                }

                if (thinking) {
                    item {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            CircularProgressIndicator(
                                Modifier.size(16.dp),
                                color = NavigationAiTeal,
                                strokeWidth = 2.dp
                            )
                            Spacer(Modifier.width(8.dp))
                            Text(
                                if (filipino) "Tinitingnan ni TUKI yung trip natin…" else "TUKI is checking your trip…",
                                color = NavigationAiMuted,
                                fontSize = 12.sp
                            )
                        }
                    }
                }
            }

            if (messages.size == 1) {
                Spacer(Modifier.height(10.dp))
                Column(verticalArrangement = Arrangement.spacedBy(7.dp)) {
                    quickPrompts.forEach { prompt ->
                        Box(
                            Modifier
                                .fillMaxWidth()
                                .background(NavigationAiTeal.copy(alpha = 0.09f), RoundedCornerShape(14.dp))
                                .clickable(enabled = !thinking && applyingRecommendationId == null) {
                                    send(prompt)
                                }
                                .padding(horizontal = 12.dp, vertical = 9.dp)
                        ) {
                            Text(
                                prompt,
                                color = NavigationAiDark,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.SemiBold
                            )
                        }
                    }
                }
            }

            Spacer(Modifier.height(12.dp))
            Text(
                if (filipino) {
                    "Hindi awtomatikong papalitan ni TUKI ang active route. Kapag meaningful yung pagbabago, pipili ka muna ng exact route proposal."
                } else {
                    "TUKI will not silently replace your active route. For meaningful changes, you choose the exact route proposal first."
                },
                color = NavigationAiMuted,
                fontSize = 11.sp,
                lineHeight = 15.sp
            )
            Spacer(Modifier.height(9.dp))

            Row(verticalAlignment = Alignment.CenterVertically) {
                TextField(
                    value = input,
                    onValueChange = { input = it },
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                    enabled = !thinking && applyingRecommendationId == null,
                    placeholder = {
                        Text(
                            if (filipino) "Magtanong o mag-fine-tune…" else "Ask or fine-tune the trip…",
                            color = NavigationAiMuted,
                            fontSize = 13.sp
                        )
                    },
                    shape = RoundedCornerShape(22.dp),
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = Color.White,
                        unfocusedContainerColor = Color.White,
                        focusedIndicatorColor = Color.Transparent,
                        unfocusedIndicatorColor = Color.Transparent,
                        focusedTextColor = NavigationAiDark,
                        unfocusedTextColor = NavigationAiDark
                    )
                )
                Spacer(Modifier.width(8.dp))
                Box(
                    Modifier
                        .size(44.dp)
                        .background(
                            if (input.isNotBlank() && !thinking && applyingRecommendationId == null) {
                                NavigationAiOrange
                            } else {
                                NavigationAiOrange.copy(alpha = 0.4f)
                            },
                            CircleShape
                        )
                        .clickable(
                            enabled = input.isNotBlank() && !thinking && applyingRecommendationId == null
                        ) { send(input) },
                    contentAlignment = Alignment.Center
                ) {
                    Text("➤", color = Color.White, fontSize = 16.sp, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

private fun AssistantDestinationCandidateDto.toDestinationSearchResult() =
    DestinationSearchResultDto(
        id = candidateId,
        name = name,
        latitude = latitude,
        longitude = longitude,
        category = category,
        source = "assistant",
        address = address
    )

// Compatibility overload for the existing TripTrackingScreen call site.
@Composable
fun NavigationAiSheet(
    language: String = com.example.frontend.core.localization.AppLanguagePreference.current(),
    onDismiss: () -> Unit,
    ask: suspend (String) -> ApiResult<AssistantResponseDto>
) = NavigationAiSheet(
    language = language,
    onDismiss = onDismiss,
    ask = { message, _ -> ask(message) }
)

@Composable
private fun NavigationDestinationChoiceCard(
    place: DestinationSearchResultDto,
    enabled: Boolean,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth(0.92f)
            .background(NavigationAiTeal, RoundedCornerShape(14.dp))
            .clickable(enabled = enabled, onClick = onClick)
            .padding(horizontal = 13.dp, vertical = 11.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text("📍", fontSize = 16.sp)
        Spacer(Modifier.width(9.dp))
        Column(Modifier.weight(1f)) {
            Text(place.name, color = Color.White, fontSize = 13.sp, fontWeight = FontWeight.Bold)
            place.address?.takeIf { it.isNotBlank() }?.let {
                Text(it, color = Color.White.copy(alpha = 0.75f), fontSize = 10.sp)
            }
        }
        Text("Select", color = Color.White, fontSize = 11.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun NavigationReplanCard(
    journey: AssistantJourneyDto,
    alternativeNumber: Int,
    applying: Boolean,
    enabled: Boolean,
    onApply: () -> Unit
) {
    val tags = journey.recommendationType
        .split(',')
        .map { it.trim().lowercase() }
        .filter { it.isNotBlank() }
    val label = buildList {
        if ("efficient" in tags) add("Balanced")
        if ("cheapest" in tags) add("Cheapest")
        if ("fastest" in tags) add("Fastest")
    }.joinToString(" · ").ifBlank { "Alternative $alternativeNumber" }
    val modes = journey.legs.joinToString(" → ") { leg ->
        when (leg.mode.uppercase()) {
            "TRIKE", "TRICYCLE" -> "Tricycle"
            "WALK" -> "Walk"
            "JEEPNEY" -> leg.routeName?.takeIf { it.isNotBlank() } ?: "Jeepney"
            else -> leg.routeName?.takeIf { it.isNotBlank() } ?: leg.mode
        }
    }

    Column(
        Modifier
            .fillMaxWidth(0.94f)
            .background(NavigationAiDark, RoundedCornerShape(17.dp))
            .padding(14.dp)
    ) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(label, color = Color.White, fontSize = 15.sp, fontWeight = FontWeight.ExtraBold)
            Text(
                "₱${journey.farePesos.roundToInt()}",
                color = Color.White,
                fontSize = 14.sp,
                fontWeight = FontWeight.Bold
            )
        }
        Spacer(Modifier.height(5.dp))
        Text(
            "~${(journey.durationSeconds / 60).roundToInt()} min  •  ${journey.walkingMeters.roundToInt()}m walk",
            color = Color.White.copy(alpha = 0.78f),
            fontSize = 11.sp
        )
        if (modes.isNotBlank()) {
            Spacer(Modifier.height(5.dp))
            Text(modes, color = Color.White.copy(alpha = 0.72f), fontSize = 11.sp)
        }
        Spacer(Modifier.height(10.dp))
        Box(
            Modifier
                .fillMaxWidth()
                .background(
                    if (enabled) NavigationAiOrange else NavigationAiMuted.copy(alpha = 0.45f),
                    RoundedCornerShape(12.dp)
                )
                .clickable(enabled = enabled && !applying, onClick = onApply)
                .padding(vertical = 10.dp),
            contentAlignment = Alignment.Center
        ) {
            if (applying) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    CircularProgressIndicator(
                        Modifier.size(15.dp),
                        color = Color.White,
                        strokeWidth = 2.dp
                    )
                    Spacer(Modifier.width(7.dp))
                    Text("Applying…", color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                }
            } else {
                Text(
                    "Apply this route",
                    color = Color.White,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }
    }
}

@Composable
fun NavigationAiButton(
    enabled: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Box(
        modifier
            .size(48.dp)
            .background(
                if (enabled) NavigationAiOrange else NavigationAiOrange.copy(alpha = 0.4f),
                CircleShape
            )
            .clickable(enabled = enabled, onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Text("✨", fontSize = 20.sp)
    }
}
