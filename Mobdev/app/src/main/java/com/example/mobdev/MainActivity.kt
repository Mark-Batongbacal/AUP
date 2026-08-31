package com.example.mobdev

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.mobdev.ui.theme.MobdevTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            MobdevTheme {

            }
        }
    }
}

@Composable
fun Greeting(name: String, modifier: Modifier = Modifier) {
    Text(
        text  = "Hello $name",
        fontSize = 24.sp,
        color = MaterialTheme.colorScheme.primary
    )
}

@Composable
fun GreetingTwo(){
    Text(
        text = "Hello again",
        modifier = Modifier
            .background(Color.Red)
            .padding(20.dp)
            .fillMaxWidth()
            .padding(20.dp)
    )
}

@Composable
fun StudentScreen(){
    Column(
        modifier = Modifier.fillMaxWidth()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(20.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ){
        Text("Student: Information")
        Text("Name: Mark")
        Text("Course: MOBDEV")

        Button(
            onClick = {

            }
        ){
            Text("Save")
        }
    }
    Row(
        modifier = Modifier.fillMaxSize()
            .padding(16.dp),
        horizontalArrangement = Arrangement.SpaceEvenly
    ){
        Text("Student Information")
        Spacer(Modifier.width(15.dp))
        Text("Name: Mark")
        Text("Course: MOBDEV")
    }
    Button(
        onClick = {

        }
    ){
        Text("Save")
    }

}


@Composable
fun BoxExample(){
    Box(
        modifier = Modifier.fillMaxSize()
            .background(Color.DarkGray)
    ){
        Text(
            "Centered Text",
            Modifier.align(Alignment.Center),
            Color.White
        )
        Text(
            "Top Left",
            Modifier.align(Alignment.TopStart)
                .padding(16.dp),
            Color.White
        )
        Text(
            "Bottom Right",
            Modifier.align(Alignment.BottomEnd),
            Color.White,

        )
    }
}

@Preview(showBackground = true)
@Composable
fun BoxExamplePreview(){
    BoxExample()
}

@Preview(showBackground = true)
@Composable
fun GreetingTwoPreview(){
    GreetingTwo()
}

@Preview(showBackground = true)
@Composable
fun StudentScreenPreview(){
    StudentScreen()
}