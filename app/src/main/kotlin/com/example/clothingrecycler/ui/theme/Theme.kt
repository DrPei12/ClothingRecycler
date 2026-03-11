package com.example.clothingrecycler.ui.theme

import android.app.Activity
import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat

// 自定义颜色
val Primary = Color(0xFF1976D2)
val PrimaryDark = Color(0xFF0D47A1)
val Secondary = Color(0xFF4CAF50)
val SecondaryDark = Color(0xFF388E3C)
val Background = Color(0xFFFAFAFA)
val Surface = Color(0xFFFFFFFF)
val Error = Color(0xFFB00020)

private val LightColorScheme = lightColorScheme(
    primary = Primary,
    onPrimary = Color.White,
    primaryContainer = Color(0xFFBBDEFB),
    onPrimaryContainer = PrimaryDark,
    secondary = Secondary,
    onSecondary = Color.White,
    secondaryContainer = Color(0xFFC8E6C9),
    onSecondaryContainer = SecondaryDark,
    background = Background,
    onBackground = Color(0xFF212121),
    surface = Surface,
    onSurface = Color(0xFF212121),
    surfaceVariant = Color(0xFFF5F5F5),
    onSurfaceVariant = Color(0xFF757575),
    error = Error,
    onError = Color.White
)

private val DarkColorScheme = darkColorScheme(
    primary = Color(0xFF90CAF9),
    onPrimary = PrimaryDark,
    primaryContainer = Color(0xFF1565C0),
    onPrimaryContainer = Color(0xFFBBDEFB),
    secondary = Color(0xFFA5D6A7),
    onSecondary = SecondaryDark,
    secondaryContainer = Color(0xFF2E7D32),
    onSecondaryContainer = Color(0xFFC8E6C9),
    background = Color(0xFF121212),
    onBackground = Color(0xFFE0E0E0),
    surface = Color(0xFF1E1E1E),
    onSurface = Color(0xFFE0E0E0),
    surfaceVariant = Color(0xFF2C2C2C),
    onSurfaceVariant = Color(0xFFBDBDBD),
    error = Color(0xFFCF6679),
    onError = Color.Black
)

@Composable
fun ClothingRecyclerTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    dynamicColor: Boolean = false, // 禁用动态颜色，保持一致性
    content: @Composable () -> Unit
) {
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }
        darkTheme -> DarkColorScheme
        else -> LightColorScheme
    }
    
    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as Activity).window
            window.statusBarColor = PrimaryDark.toArgb()
            WindowCompat.getInsetsController(window, view).isAppearanceLightStatusBars = false
        }
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content
    )
}
