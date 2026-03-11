package com.example.clothingrecycler.ui.screens.stock

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.example.clothingrecycler.domain.model.StockCategory
import com.example.clothingrecycler.domain.model.WeightUnit
import java.text.DecimalFormat

private val decimalFormat = DecimalFormat("#.##")

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StockScreen(
    onNavigateBack: () -> Unit,
    viewModel: StockViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "库存管理",
                        fontWeight = FontWeight.Bold
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.Default.ArrowBack,
                            contentDescription = "返回"
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    navigationIconContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        }
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            when {
                uiState.isLoading -> {
                    CircularProgressIndicator(Modifier.align(Alignment.Center))
                }
                uiState.categories.isEmpty() -> {
                    Text(
                        text = "暂无库存数据\n请在设置中添加类别并入库",
                        modifier = Modifier.align(Alignment.Center)
                    )
                }
                else -> {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(16.dp)
                    ) {
                        // 总成本卡片
                        item {
                            Card(
                                modifier = Modifier.fillMaxWidth(),
                                colors = CardDefaults.cardColors(
                                    containerColor = MaterialTheme.colorScheme.secondaryContainer
                                )
                            ) {
                                Column(modifier = Modifier.padding(20.dp)) {
                                    Text(
                                        text = "总成本",
                                        style = MaterialTheme.typography.titleMedium,
                                        color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f)
                                    )
                                    Text(
                                        text = "¥${decimalFormat.format(uiState.totalCost)}",
                                        style = MaterialTheme.typography.headlineMedium,
                                        fontWeight = FontWeight.Bold,
                                        color = MaterialTheme.colorScheme.onSecondaryContainer
                                    )
                                }
                            }
                        }

                        item {
                            Text(
                                text = "各类别库存详情",
                                style = MaterialTheme.typography.titleSmall,
                                fontWeight = FontWeight.Medium,
                                modifier = Modifier.padding(vertical = 12.dp)
                            )
                        }

                        items(uiState.categories) { category ->
                            StockCategoryCard(category = category)
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun StockCategoryCard(category: StockCategory) {
    // 根据单位类型获取显示格式
    val (displayStock, displayUnit) = when (category.unitType) {
        WeightUnit.KILOGRAM -> category.stock to "kg"
        WeightUnit.JIN -> category.stockInJin to "斤"
        WeightUnit.PIECE -> category.stockInPieces.toDouble() to "件"
    }

    val priceUnit = when (category.unitType) {
        WeightUnit.KILOGRAM -> "kg"
        WeightUnit.JIN -> "斤"
        WeightUnit.PIECE -> "件"
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = category.name,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "(${displayUnit})",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(start = 8.dp)
                )
            }

            Text(
                text = "库存: ${decimalFormat.format(displayStock)}$displayUnit",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )

            if (category.averageBuyPrice > 0) {
                Text(
                    text = "平均收购价: ¥${decimalFormat.format(category.averageBuyPrice)}/$priceUnit",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.secondary,
                    fontWeight = FontWeight.Medium
                )
            }

            // 显示库存成本
            val stockCost = when (category.unitType) {
                WeightUnit.KILOGRAM -> category.stock * category.averageBuyPrice
                WeightUnit.JIN -> category.stockInJin * category.averageBuyPrice
                WeightUnit.PIECE -> category.stockInPieces * category.averageBuyPrice
            }
            if (stockCost > 0) {
                Text(
                    text = "库存成本: ¥${decimalFormat.format(stockCost)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
                )
            }
        }
    }
}
