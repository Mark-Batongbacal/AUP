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
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiChatBubble = Color(0xFF1F4B52)

private data class ChatMessage(
    val id: Long,
    val text: String,
    val isFromUser: Boolean,
    val place: PlaceSuggestion? = null
)

private data class PlaceSuggestion(
    val name: String,
    val address: String
)

private val quickPrompts = listOf(
    "near the church in Angeles",
    "my lola's place sa Dau"
)

@Composable
fun AskAiChatScreen(
    userName: String = "Juan",
    onBack: () -> Unit = {},
    onDestinationConfirmed: (String) -> Unit = {},
    modifier: Modifier = Modifier
) {
    var messages by remember {
        mutableStateOf(
            listOf(
                ChatMessage(
                    id = 0L,
                    text = "Hi $userName! Where would you like to go? You can describe it in your own words.",
                    isFromUser = false
                )
            )
        )
    }

    var inputText by remember {
        mutableStateOf("")
    }

    var isThinking by remember {
        mutableStateOf(false)
    }

    val listState = rememberLazyListState()

    // auto scroll to msg
    LaunchedEffect(messages.size) {
        if (messages.isNotEmpty()) {
            listState.animateScrollToItem(messages.lastIndex)
        }
    }

    // mock ai response
    LaunchedEffect(messages.lastOrNull()?.id) {
        val lastMessage = messages.lastOrNull()

        if (lastMessage != null && lastMessage.isFromUser) {

            delay(700)

            val suggestion = PlaceSuggestion(
                name = "Jollibee SM Clark",
                address = "Clark Freeport Zone, Pampanga"
            )

            messages = messages + ChatMessage(
                id = System.currentTimeMillis() + 1,
                text = "Got it — found a Jollibee near SM Clark, Clark Freeport Zone. Is this the one?",
                isFromUser = false,
                place = suggestion
            )

            isThinking = false
        }
    }

    fun sendMessage(text: String) {

        if (text.isBlank() || isThinking) {
            return
        }

        val userMessage = ChatMessage(
            id = System.currentTimeMillis(),
            text = text.trim(),
            isFromUser = true
        )

        messages = messages + userMessage
        inputText = ""
        isThinking = true
    }

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(TukiCream)
    ) {

        // Top Header pushed down past the status bar & notch
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(
                    horizontal = 20.dp,
                    vertical = 14.dp
                ),
            verticalAlignment = Alignment.CenterVertically
        ) {

            IconButton(
                onClick = onBack,
                modifier = Modifier.size(40.dp)
            ) {
                Text(
                    text = "←",
                    color = TukiDark,
                    fontSize = 24.sp,
                    fontWeight = FontWeight.Bold
                )
            }

            Spacer(modifier = Modifier.width(4.dp))

            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(
                        color = TukiTeal.copy(alpha = 0.12f),
                        shape = RoundedCornerShape(12.dp)
                    ),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "✨",
                    fontSize = 18.sp
                )
            }

            Spacer(modifier = Modifier.width(10.dp))

            Column {

                Text(
                    text = "Ask our AI",
                    color = TukiDark,
                    fontSize = 20.sp,
                    fontWeight = FontWeight.ExtraBold
                )

                Text(
                    text = "Tell me where you want to go",
                    color = TukiGray,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Medium
                )
            }
        }

        LazyColumn(
            state = listState,
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 16.dp),

            contentPadding = PaddingValues(
                top = 8.dp,
                bottom = 16.dp
            )
        ) {

            items(
                items = messages,
                key = { it.id }
            ) { message ->

                ChatBubble(
                    message = message,

                    onConfirmPlace = { place ->
                        onDestinationConfirmed(place.name)
                    },

                    onRejectPlace = {
                        sendMessage(
                            "Not quite, let me try again"
                        )
                    }
                )

                Spacer(
                    modifier = Modifier.height(12.dp)
                )
            }

            if (isThinking) {

                item {

                    ThinkingBubble()

                    Spacer(
                        modifier = Modifier.height(12.dp)
                    )
                }
            }

            if (messages.size <= 1) {

                item {

                    Column(
                        modifier = Modifier.fillMaxWidth()
                    ) {

                        Text(
                            text = "Try asking:",
                            color = TukiGray,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Bold
                        )

                        Spacer(
                            modifier = Modifier.height(8.dp)
                        )

                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {

                            quickPrompts.forEach { prompt ->

                                QuickPromptChip(
                                    text = prompt,
                                    onClick = {
                                        sendMessage(prompt)
                                    }
                                )
                            }
                        }
                    }
                }
            }
        }

        // Bottom Input Bar raised above system gesture handle
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(TukiDark)
                .navigationBarsPadding()
                .padding(
                    horizontal = 12.dp,
                    vertical = 10.dp
                ),
            verticalAlignment = Alignment.CenterVertically
        ) {

            TextField(
                value = inputText,

                onValueChange = {
                    inputText = it
                },

                placeholder = {
                    Text(
                        text = "Type your message...",
                        color = TukiGray,
                        fontSize = 14.sp
                    )
                },

                singleLine = true,

                colors = TextFieldDefaults.colors(
                    focusedContainerColor =
                        Color.White.copy(alpha = 0.08f),

                    unfocusedContainerColor =
                        Color.White.copy(alpha = 0.08f),

                    disabledContainerColor =
                        Color.Transparent,

                    focusedIndicatorColor =
                        Color.Transparent,

                    unfocusedIndicatorColor =
                        Color.Transparent,

                    disabledIndicatorColor =
                        Color.Transparent,

                    focusedTextColor =
                        Color.White,

                    unfocusedTextColor =
                        Color.White
                ),

                shape = RoundedCornerShape(24.dp),

                modifier = Modifier
                    .weight(1f)
                    .padding(end = 8.dp)
            )

            Box(
                modifier = Modifier
                    .size(44.dp)
                    .background(
                        color = if (inputText.isNotBlank() && !isThinking) {
                            TukiOrange
                        } else {
                            TukiOrange.copy(alpha = 0.45f)
                        },
                        shape = CircleShape
                    )
                    .clickable(
                        enabled = inputText.isNotBlank() && !isThinking
                    ) {
                        sendMessage(inputText)
                    },
                contentAlignment = Alignment.Center
            ) {

                Text(
                    text = "➤",
                    color = Color.White,
                    fontSize = 17.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }
    }
}

@Composable
private fun ChatBubble(
    message: ChatMessage,
    onConfirmPlace: (PlaceSuggestion) -> Unit,
    onRejectPlace: () -> Unit
) {

    Row(
        modifier = Modifier.fillMaxWidth(),

        horizontalArrangement =
            if (message.isFromUser) {
                Arrangement.End
            } else {
                Arrangement.Start
            }
    ) {

        Column(
            horizontalAlignment =
                if (message.isFromUser) {
                    Alignment.End
                } else {
                    Alignment.Start
                }
        ) {

            // message bubble
            Box(
                modifier = Modifier
                    .background(
                        color =
                            if (message.isFromUser) {
                                TukiOrange
                            } else {
                                TukiChatBubble
                            },
                        shape = RoundedCornerShape(16.dp)
                    )
                    .padding(
                        horizontal = 14.dp,
                        vertical = 10.dp
                    )
            ) {

                Text(
                    text = message.text,
                    color = Color.White,
                    fontSize = 14.sp
                )
            }

            val place = message.place

            if (place != null) {

                Spacer(
                    modifier = Modifier.height(8.dp)
                )

                Row(
                    modifier = Modifier
                        .fillMaxWidth(0.9f)
                        .background(
                            color = TukiTeal,
                            shape = RoundedCornerShape(14.dp)
                        )
                        .padding(
                            horizontal = 14.dp,
                            vertical = 12.dp
                        ),
                    verticalAlignment = Alignment.CenterVertically
                ) {

                    Box(
                        modifier = Modifier
                            .size(38.dp)
                            .background(
                                color = Color.White.copy(alpha = 0.12f),
                                shape = RoundedCornerShape(10.dp)
                            ),
                        contentAlignment = Alignment.Center
                    ) {

                        Text(
                            text = "📍",
                            fontSize = 17.sp
                        )
                    }

                    Spacer(
                        modifier = Modifier.width(10.dp)
                    )

                    Column {

                        Text(
                            text = place.name,
                            color = Color.White,
                            fontSize = 14.sp,
                            fontWeight = FontWeight.Bold
                        )

                        Spacer(
                            modifier = Modifier.height(2.dp)
                        )

                        Text(
                            text = place.address,
                            color = Color.White.copy(alpha = 0.75f),
                            fontSize = 11.sp
                        )
                    }
                }

                Spacer(
                    modifier = Modifier.height(8.dp)
                )

                Row {

                    Box(
                        modifier = Modifier
                            .background(
                                color = TukiTeal,
                                shape = RoundedCornerShape(20.dp)
                            )
                            .clickable {
                                onConfirmPlace(place)
                            }
                            .padding(
                                horizontal = 14.dp,
                                vertical = 8.dp
                            )
                    ) {

                        Text(
                            text = "Yes, that's it",
                            color = Color.White,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }

                    Spacer(
                        modifier = Modifier.width(8.dp)
                    )

                    Box(
                        modifier = Modifier
                            .background(
                                color = TukiChatBubble,
                                shape = RoundedCornerShape(20.dp)
                            )
                            .clickable {
                                onRejectPlace()
                            }
                            .padding(
                                horizontal = 14.dp,
                                vertical = 8.dp
                            )
                    ) {

                        Text(
                            text = "Not quite",
                            color = Color.White,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun ThinkingBubble() {

    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.Start
    ) {

        Box(
            modifier = Modifier
                .background(
                    color = TukiChatBubble,
                    shape = RoundedCornerShape(16.dp)
                )
                .padding(
                    horizontal = 16.dp,
                    vertical = 10.dp
                )
        ) {

            Text(
                text = "•••",
                color = Color.White.copy(alpha = 0.7f),
                fontSize = 14.sp,
                fontWeight = FontWeight.Bold
            )
        }
    }
}

@Composable
private fun QuickPromptChip(
    text: String,
    onClick: () -> Unit
) {

    Box(
        modifier = Modifier
            .background(
                color = TukiTeal.copy(alpha = 0.12f),
                shape = RoundedCornerShape(20.dp)
            )
            .clickable(
                onClick = onClick
            )
            .padding(
                horizontal = 14.dp,
                vertical = 9.dp
            )
    ) {

        Text(
            text = text,
            color = TukiDark,
            fontSize = 12.sp,
            fontWeight = FontWeight.Medium
        )
    }
}