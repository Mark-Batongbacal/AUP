import java.util.Properties

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.google.secrets)
}

val localConfig = Properties().apply {
    val defaultsFile = rootProject.file("local.defaults.properties")
    if (defaultsFile.exists()) {
        defaultsFile.inputStream().use(::load)
    }

    val localFile = rootProject.file("local.properties")
    if (localFile.exists()) {
        localFile.inputStream().use(::load)
    }
}

fun localConfigValue(name: String): String = localConfig.getProperty(name, "")

fun String.asBuildConfigString(): String =
    "\"" + replace("\\", "\\\\").replace("\"", "\\\"") + "\""

android {
    namespace = "com.example.frontend"
    compileSdk {
        version = release(37) {
            minorApiLevel = 1
        }
    }

    defaultConfig {
        applicationId = "com.example.frontend"
        minSdk = 26
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        buildConfigField(
            "String",
            "BACKEND_BASE_URL",
            localConfigValue("BACKEND_BASE_URL").asBuildConfigString()
        )
        resValue(
            "string",
            "google_server_client_id",
            localConfigValue("GOOGLE_SERVER_CLIENT_ID")
        )
        resValue(
            "string",
            "facebook_app_id",
            localConfigValue("FACEBOOK_APP_ID")
        )
        resValue(
            "string",
            "facebook_client_token",
            localConfigValue("FACEBOOK_CLIENT_TOKEN")
        )
        resValue(
            "string",
            "facebook_app_id",
            localConfigValue("FACEBOOK_APP_ID")
        )
        resValue(
            "string",
            "fb_login_protocol_scheme",
            "fb${localConfigValue("FACEBOOK_APP_ID")}"
        )
        resValue(
            "string",
            "facebook_client_token",
            localConfigValue("FACEBOOK_CLIENT_TOKEN")
        )
    }

    buildTypes {
        release {
            optimization {
                enable = false
            }
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
    buildFeatures {
        compose = true
        buildConfig = true
        resValues = true
    }
}

dependencies {
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.material3.adaptive.navigation.suite)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.ui.tooling.preview)
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.credentials)
    implementation(libs.androidx.credentials.play.services.auth)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.google.identity.googleid)
    implementation("com.google.android.gms:play-services-location:21.4.0")
    implementation("org.maplibre.gl:android-sdk-opengl:13.0.2")
    implementation(libs.facebook.login)
    implementation(libs.retrofit)
    implementation(libs.retrofit.converter.gson)
    implementation(libs.lottie.compose)
    implementation("androidx.navigation:navigation-compose:2.8.0")
    testImplementation(libs.junit)
    testImplementation("com.squareup.okhttp3:mockwebserver:4.12.0")
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(libs.androidx.junit)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
    debugImplementation(libs.androidx.compose.ui.tooling)
}

secrets {
    propertiesFileName = "local.properties"
    defaultPropertiesFileName = "local.defaults.properties"
}
