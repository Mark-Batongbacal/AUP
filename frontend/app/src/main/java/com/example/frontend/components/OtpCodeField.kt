package com.example.frontend.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.material3.Text

@Composable
fun OtpCodeField(
    code: String,
    onCodeChange: (String) -> Unit,
    modifier: Modifier = Modifier,
    length: Int = 8,
    enabled: Boolean = true
) {
    val focusRequester = FocusRequester()
    val normalized = code.filter(Char::isDigit).take(length)

    BasicTextField(
        value = normalized,
        onValueChange = { onCodeChange(it.filter(Char::isDigit).take(length)) },
        modifier = modifier
            .fillMaxWidth()
            .focusRequester(focusRequester)
            .clickable(enabled = enabled) { focusRequester.requestFocus() },
        enabled = enabled,
        singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
        textStyle = TextStyle(color = Color.Transparent),
        cursorBrush = SolidColor(Color.Transparent),
        decorationBox = { innerTextField ->
            Box {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    repeat(length) { index ->
                        val digit = normalized.getOrNull(index)?.toString().orEmpty()
                        val isCurrent = index == normalized.length && normalized.length < length
                        Box(
                            modifier = Modifier
                                .size(width = 38.dp, height = 52.dp)
                                .background(
                                    color = if (isCurrent) Color(0xFFFFE9C4) else Color(0xFFFFF3D8),
                                    shape = RoundedCornerShape(12.dp)
                                ),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = digit,
                                color = Color(0xFF173B43),
                                fontSize = 21.sp,
                                fontWeight = FontWeight.ExtraBold
                            )
                        }
                    }
                }

                Box(modifier = Modifier.size(1.dp).alpha(0.01f)) {
                    innerTextField()
                }
            }
        }
    )
}
