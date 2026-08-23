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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.ai.AssistantResponseDto
import kotlinx.coroutines.launch

private val NavigationAiSurface = com.example.frontend.ui.theme.TukiCream
private val NavigationAiDark = com.example.frontend.ui.theme.TukiInk
private val NavigationAiTeal = com.example.frontend.ui.theme.TukiTeal
private val NavigationAiOrange = com.example.frontend.ui.theme.TukiOrange
private val NavigationAiMuted = com.example.frontend.ui.theme.TukiMuted
private val NavigationAiBubble = com.example.frontend.ui.theme.TukiSurfaceRaised

private data class NavigationAiMessage(
    val id: Long,
    val text: String,
    val fromUser: Boolean
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NavigationAiSheet(
    language: String = com.example.frontend.core.localization.AppLanguagePreference.current(),
    onDismiss: () -> Unit,
    ask: suspend (String) -> ApiResult<AssistantResponseDto>
) {
    val filipino = language.equals("Filipino", ignoreCase = true)
    val quickPrompts = if (filipino) {
        listOf(
            "Tama pa ba yung route natin?",
            "Saan ako bababa?",
            "Ano yung next instruction?",
            "Lumagpas ba ako sa babaan?"
        )
    } else {
        listOf(
            "Am I still on the right route?",
            "Where do I get off?",
            "What's my next instruction?",
            "Did I miss my stop?"
        )
    }
    val intro = if (filipino) {
        "Magtanong ka lang tungkol sa active trip natin. Gagamitin ko yung current navigation state natin, hindi ako manghuhula."
    } else {
        "Ask me anything about your active trip. I’ll use the current navigation state instead of guessing."
    }
    val scope = rememberCoroutineScope()
    val listState = rememberLazyListState()
    var input by remember { mutableStateOf("") }
    var thinking by remember { mutableStateOf(false) }
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

    LaunchedEffect(messages.size, thinking) {
        if (messages.isNotEmpty()) listState.animateScrollToItem(messages.lastIndex)
    }

    fun send(text: String) {
        val trimmed = text.trim()
        if (trimmed.isEmpty() || thinking) return
        messages = messages + NavigationAiMessage(
            id = System.currentTimeMillis(),
            text = trimmed,
            fromUser = true
        )
        input = ""
        thinking = true
        scope.launch {
            when (val result = ask(trimmed)) {
                is ApiResult.Success -> {
                    messages = messages + NavigationAiMessage(
                        id = System.currentTimeMillis() + 1,
                        text = result.data.message,
                        fromUser = false
                    )
                }
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
                        if (filipino) "Mga tanong tungkol sa active trip natin" else "Questions about this active trip",
                        color = NavigationAiMuted,
                        fontSize = 12.sp
                    )
                }
            }

            Spacer(Modifier.height(12.dp))

            LazyColumn(
                state = listState,
                modifier = Modifier.fillMaxWidth().height(300.dp),
                verticalArrangement = Arrangement.spacedBy(9.dp)
            ) {
                items(messages, key = { it.id }) { message ->
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
                }

                if (thinking) {
                    item {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            CircularProgressIndicator(Modifier.size(16.dp), color = NavigationAiTeal, strokeWidth = 2.dp)
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
                                .clickable(enabled = !thinking) { send(prompt) }
                                .padding(horizontal = 12.dp, vertical = 9.dp)
                        ) {
                            Text(prompt, color = NavigationAiDark, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
                        }
                    }
                }
            }

            Spacer(Modifier.height(12.dp))
            Text(
                if (filipino) {
                    "May babaguhin sa route? Gamitin yung Trip options para malinaw na ipa-recalculate kay TUKI sa backend."
                } else {
                    "Need to change the route? Use Trip options so TUKI can explicitly recalculate it on the backend."
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
                    placeholder = {
                        Text(
                            if (filipino) "Magtanong tungkol sa trip…" else "Ask about your trip…",
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
                            if (input.isNotBlank() && !thinking) NavigationAiOrange else NavigationAiOrange.copy(alpha = 0.4f),
                            CircleShape
                        )
                        .clickable(enabled = input.isNotBlank() && !thinking) { send(input) },
                    contentAlignment = Alignment.Center
                ) {
                    Text("➤", color = Color.White, fontSize = 16.sp, fontWeight = FontWeight.Bold)
                }
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
