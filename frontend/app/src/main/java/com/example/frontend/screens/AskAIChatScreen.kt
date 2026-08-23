package com.example.frontend.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.IconButton
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
import com.example.frontend.core.location.LocationDetectionFailureMessage
import com.example.frontend.core.location.currentDeviceLocation
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.data.ai.AssistantJourneyDto
import com.example.frontend.data.ai.AssistantRequest
import com.example.frontend.data.places.DestinationSearchResultDto
import com.example.frontend.data.routing.PendingAiRouteSelection
import com.example.frontend.data.routing.PlannedJourney
import com.example.frontend.data.routing.toDomain
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

import androidx.compose.material3.MaterialTheme
import com.example.frontend.ui.theme.TukiTeal
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiDeepTeal

private val TukiChatBubble = Color(0xFF1F4B52)

private data class AiChatMessage(
    val id: Long,
    val text: String,
    val isFromUser: Boolean,
    val requestText: String? = null,
    val journeys: List<AssistantJourneyDto> = emptyList(),
    val destination: DestinationSearchResultDto? = null,
    val destinationChoices: List<DestinationSearchResultDto> = emptyList()
)

private val quickPrompts = listOf(
    "Cheapest route to SM City Clark",
    "Fastest route to Dau Terminal"
)

@Composable
fun AskAiChatScreen(
    userName: String = "Juan",
    onBack: () -> Unit = {},
    onDestinationConfirmed: (String) -> Unit = {},
    modifier: Modifier = Modifier
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val aiRepository = remember(context.applicationContext) {
        TukiDataProvider(context.applicationContext).aiRepository
    }

    var messages by remember {
        mutableStateOf(
            listOf(
                AiChatMessage(
                    id = 0L,
                    text = "Hi $userName! Tell me where you want to go, your budget, or whether you prefer the cheapest or fastest route.",
                    isFromUser = false
                )
            )
        )
    }
    var inputText by remember { mutableStateOf("") }
    var isThinking by remember { mutableStateOf(false) }
    val listState = rememberLazyListState()

    LaunchedEffect(messages.size, isThinking) {
        if (messages.isNotEmpty()) listState.animateScrollToItem(messages.lastIndex)
    }

    fun askAssistant(text: String, destinationId: String? = null) {
        if (text.isBlank() || isThinking) return
        val trimmed = text.trim()
        messages = messages + AiChatMessage(
            id = System.currentTimeMillis(),
            text = trimmed,
            isFromUser = true
        )
        inputText = ""
        isThinking = true

        scope.launch {
            val location = context.currentDeviceLocation()
            if (location == null) {
                messages = messages + AiChatMessage(
                    id = System.currentTimeMillis() + 1,
                    text = LocationDetectionFailureMessage,
                    isFromUser = false
                )
                isThinking = false
                return@launch
            }

            when (val result = aiRepository.ask(
                AssistantRequest(
                    message = trimmed,
                    originLatitude = location.latitude,
                    originLongitude = location.longitude,
                    destinationId = destinationId
                )
            )) {
                is ApiResult.Success -> {
                    val response = result.data
                    messages = messages + AiChatMessage(
                        id = System.currentTimeMillis() + 1,
                        text = response.message,
                        isFromUser = false,
                        requestText = trimmed,
                        journeys = response.journeys.orEmpty(),
                        destination = response.destination,
                        destinationChoices = response.destinations.orEmpty()
                    )
                }

                is ApiResult.Failure -> {
                    messages = messages + AiChatMessage(
                        id = System.currentTimeMillis() + 1,
                        text = result.message,
                        isFromUser = false
                    )
                }
            }
            isThinking = false
        }
    }

    Column(modifier = modifier.fillMaxSize().background(TukiCream)) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(horizontal = 20.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack, modifier = Modifier.size(40.dp)) {
                Text("←", color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
            Spacer(modifier = Modifier.width(4.dp))
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiTeal.copy(alpha = 0.12f), RoundedCornerShape(12.dp)),
                contentAlignment = Alignment.Center
            ) {
                Text("✨", fontSize = 18.sp)
            }
            Spacer(modifier = Modifier.width(10.dp))
            Column {
                Text(
                    "Ask our AI",
                    color = TukiInk,
                    style = MaterialTheme.typography.titleLarge
                )
                Text("Get TUKI route recommendations", color = TukiMuted, style = MaterialTheme.typography.bodySmall)
            }
        }

        LazyColumn(
            state = listState,
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 16.dp),
            contentPadding = PaddingValues(top = 8.dp, bottom = 16.dp)
        ) {
            items(messages, key = { it.id }) { message ->
                AiMessageBubble(
                    message = message,
                    onRouteSelected = { journey, destination ->
                        PendingAiRouteSelection.save(
                            destination.name,
                            PlannedJourney(journey.journeyId, journey.plan.toDomain())
                        )
                        onDestinationConfirmed(destination.name)
                    },
                    onDestinationSelected = { place ->
                        askAssistant(message.requestText ?: place.name, place.id)
                    }
                )
                Spacer(modifier = Modifier.height(12.dp))
            }

            if (isThinking) {
                item {
                    ThinkingBubble()
                    Spacer(modifier = Modifier.height(12.dp))
                }
            }

            if (messages.size <= 1) {
                item {
                    Column(modifier = Modifier.fillMaxWidth()) {
                        Text("Try asking:", color = TukiMuted, style = MaterialTheme.typography.labelSmall)
                        Spacer(modifier = Modifier.height(8.dp))
                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            quickPrompts.forEach { prompt ->
                                QuickPromptChip(text = prompt, onClick = { askAssistant(prompt) })
                            }
                        }
                    }
                }
            }
        }

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(TukiChatBubble)
                .navigationBarsPadding()
                .padding(horizontal = 12.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            TextField(
                value = inputText,
                onValueChange = { inputText = it },
                placeholder = { Text("Type your message...", color = TukiMuted, style = MaterialTheme.typography.bodyMedium) },
                singleLine = true,
                colors = TextFieldDefaults.colors(
                    focusedContainerColor = Color.White.copy(alpha = 0.08f),
                    unfocusedContainerColor = Color.White.copy(alpha = 0.08f),
                    disabledContainerColor = Color.Transparent,
                    focusedIndicatorColor = Color.Transparent,
                    unfocusedIndicatorColor = Color.Transparent,
                    disabledIndicatorColor = Color.Transparent,
                    focusedTextColor = Color.White,
                    unfocusedTextColor = Color.White
                ),
                shape = RoundedCornerShape(24.dp),
                modifier = Modifier.weight(1f).padding(end = 8.dp)
            )

            Box(
                modifier = Modifier
                    .size(44.dp)
                    .background(
                        if (inputText.isNotBlank() && !isThinking) TukiOrange else TukiOrange.copy(alpha = 0.45f),
                        CircleShape
                    )
                    .clickable(enabled = inputText.isNotBlank() && !isThinking) { askAssistant(inputText) },
                contentAlignment = Alignment.Center
            ) {
                Text("➤", color = Color.White, fontSize = 17.sp, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun AiMessageBubble(
    message: AiChatMessage,
    onRouteSelected: (AssistantJourneyDto, DestinationSearchResultDto) -> Unit,
    onDestinationSelected: (DestinationSearchResultDto) -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (message.isFromUser) Arrangement.End else Arrangement.Start
    ) {
        Column(
            modifier = Modifier.fillMaxWidth(if (message.isFromUser) 0.85f else 1f),
            horizontalAlignment = if (message.isFromUser) Alignment.End else Alignment.Start
        ) {
            Box(
                modifier = Modifier
                    .background(
                        if (message.isFromUser) TukiOrange else TukiChatBubble,
                        RoundedCornerShape(16.dp)
                    )
                    .padding(horizontal = 14.dp, vertical = 10.dp)
            ) {
                Text(message.text, color = Color.White, style = MaterialTheme.typography.bodyLarge)
            }

            if (!message.isFromUser && message.destinationChoices.isNotEmpty()) {
                Spacer(modifier = Modifier.height(8.dp))
                message.destinationChoices.forEach { place ->
                    DestinationChoiceCard(place = place, onClick = { onDestinationSelected(place) })
                    Spacer(modifier = Modifier.height(8.dp))
                }
            }

            val destination = message.destination
            if (!message.isFromUser && destination != null && message.journeys.isNotEmpty()) {
                Spacer(modifier = Modifier.height(10.dp))
                message.journeys.forEachIndexed { index, journey ->
                    AiRouteCard(
                        journey = journey,
                        fallbackAlternativeNumber = index + 1,
                        onClick = { onRouteSelected(journey, destination) }
                    )
                    Spacer(modifier = Modifier.height(10.dp))
                }
            }
        }
    }
}

@Composable
private fun AiRouteCard(
    journey: AssistantJourneyDto,
    fallbackAlternativeNumber: Int,
    onClick: () -> Unit
) {
    val tags = journey.recommendationType
        .split(',')
        .map { it.trim().lowercase() }
        .filter { it.isNotBlank() }
    val objectiveLabels = buildList {
        if ("efficient" in tags) add("Balanced")
        if ("cheapest" in tags) add("Cheapest")
        if ("fastest" in tags) add("Fastest")
    }
    val label = objectiveLabels.joinToString(" · ").ifBlank { "Alternative $fallbackAlternativeNumber" }
    val icon = when {
        "efficient" in tags -> "⚖️"
        "cheapest" in tags -> "₱"
        "fastest" in tags -> "⚡"
        else -> "🔄"
    }
    val modes = journey.legs.map { leg ->
        when (leg.mode.uppercase()) {
            "TRIKE" -> "Tricycle"
            "WALK" -> "Walk"
            "JEEPNEY" -> leg.routeName?.takeIf { it.isNotBlank() } ?: "Jeepney"
            else -> leg.routeName?.takeIf { it.isNotBlank() }
                ?: leg.mode.lowercase().replaceFirstChar { it.titlecase() }
        }
    }.joinToString(" → ")

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiChatBubble, RoundedCornerShape(18.dp))
            .clickable(onClick = onClick)
            .padding(16.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text("$icon $label", color = Color.White, style = MaterialTheme.typography.titleLarge)
            Text("View route ›", color = TukiOrange, style = MaterialTheme.typography.labelLarge)
        }
        Spacer(modifier = Modifier.height(10.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Text("₱${journey.farePesos.roundToInt()}", color = Color.White, style = MaterialTheme.typography.titleMedium)
            Text("~${(journey.durationSeconds / 60).roundToInt()} min", color = Color.White, style = MaterialTheme.typography.titleMedium)
        }
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            "${journey.walkingMeters.roundToInt()} m walk",
            color = Color.White.copy(alpha = 0.75f),
            style = MaterialTheme.typography.bodySmall
        )
        if (modes.isNotBlank()) {
            Spacer(modifier = Modifier.height(8.dp))
            Text(modes, color = Color.White.copy(alpha = 0.78f), style = MaterialTheme.typography.bodySmall)
        }
    }
}

@Composable
private fun DestinationChoiceCard(place: DestinationSearchResultDto, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiTeal, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text("📍", fontSize = 17.sp)
        Spacer(modifier = Modifier.width(10.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(place.name, color = Color.White, style = MaterialTheme.typography.titleMedium)
            place.address?.takeIf { it.isNotBlank() }?.let {
                Text(it, color = Color.White.copy(alpha = 0.75f), style = MaterialTheme.typography.bodySmall)
            }
        }
        Text("Select", color = Color.White, style = MaterialTheme.typography.labelLarge)
    }
}

@Composable
private fun ThinkingBubble() {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Start) {
        Box(
            modifier = Modifier
                .background(TukiChatBubble, RoundedCornerShape(16.dp))
                .padding(horizontal = 16.dp, vertical = 10.dp)
        ) {
            Text("•••", color = Color.White.copy(alpha = 0.7f), fontSize = 14.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun QuickPromptChip(text: String, onClick: () -> Unit) {
    Box(
        modifier = Modifier
            .background(TukiTeal.copy(alpha = 0.12f), RoundedCornerShape(20.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 9.dp)
    ) {
        Text(text, color = TukiInk, style = MaterialTheme.typography.bodyMedium)
    }
}
