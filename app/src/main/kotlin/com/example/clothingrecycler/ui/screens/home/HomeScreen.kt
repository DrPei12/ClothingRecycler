package com.example.clothingrecycler.ui.screens.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.ArrowUpward
import androidx.compose.material.icons.filled.AttachMoney
import androidx.compose.material.icons.filled.BarChart
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Inventory
import androidx.compose.material.icons.filled.People
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.clothingrecycler.data.local.database.ClothingRecyclerDatabase
import com.example.clothingrecycler.domain.model.WeightUnit
import java.text.DecimalFormat

private val decimalFormat = DecimalFormat("#.##")

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HomeScreen(
    onNavigateToInbound: () -> Unit,
    onNavigateToOutbound: () -> Unit,
    onNavigateToStock: () -> Unit,
    onNavigateToOrders: () -> Unit,
    onNavigateToStatistics: () -> Unit,
    onNavigateToSettings: () -> Unit,
    onNavigateToCustomers: () -> Unit,
    onNavigateToRevenue: () -> Unit
) {
    val context = LocalContext.current
    val db = remember { ClothingRecyclerDatabase.getInstance(context) }
    val categories by db.categoryDao().getAllCategories().collectAsStateWithLifecycle(initialValue = emptyList())
    
    // 根据单位类型计算总预期收入
    val totalRevenue = categories.sumOf { category ->
        val unitType = try {
            WeightUnit.valueOf(category.unitType)
        } catch (e: Exception) {
            WeightUnit.KILOGRAM
        }
        when (unitType) {
            WeightUnit.KILOGRAM -> category.stock * category.sellPrice
            WeightUnit.JIN -> category.stockInJin * category.sellPrice
            WeightUnit.PIECE -> category.stockInPieces * category.sellPrice
        }
    }
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "衣物回收管理",
                        fontWeight = FontWeight.Bold
                    )
                },
                actions = {
                    IconButton(onClick = onNavigateToStatistics) {
                        Icon(
                            imageVector = Icons.Default.BarChart,
                            contentDescription = "数据统计"
                        )
                    }
                    IconButton(onClick = onNavigateToCustomers) {
                        Icon(
                            imageVector = Icons.Default.People,
                            contentDescription = "客户管理"
                        )
                    }
                    IconButton(onClick = onNavigateToOrders) {
                        Icon(
                            imageVector = Icons.Default.Description,
                            contentDescription = "订单记录"
                        )
                    }
                    IconButton(onClick = onNavigateToSettings) {
                        Icon(
                            imageVector = Icons.Default.Settings,
                            contentDescription = "设置"
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    actionIconContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {
            // 三大功能入口
            Text(
                text = "功能入口",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.padding(bottom = 12.dp)
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                FunctionCard(
                    title = "入库",
                    subtitle = "收购衣物",
                    icon = Icons.Default.ArrowDownward,
                    backgroundColor = MaterialTheme.colorScheme.secondaryContainer,
                    onClick = onNavigateToInbound,
                    modifier = Modifier.weight(1f)
                )

                FunctionCard(
                    title = "出库",
                    subtitle = "卖出衣物",
                    icon = Icons.Default.ArrowUpward,
                    backgroundColor = MaterialTheme.colorScheme.tertiaryContainer,
                    onClick = onNavigateToOutbound,
                    modifier = Modifier.weight(1f)
                )
            }

            Spacer(modifier = Modifier.height(12.dp))

            FunctionCard(
                title = "库存管理",
                subtitle = "查看库存详情",
                icon = Icons.Default.Inventory,
                backgroundColor = MaterialTheme.colorScheme.primaryContainer,
                onClick = onNavigateToStock,
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(modifier = Modifier.height(12.dp))

            FunctionCard(
                title = "预计收入",
                subtitle = "¥${decimalFormat.format(totalRevenue)}",
                icon = Icons.Default.AttachMoney,
                backgroundColor = MaterialTheme.colorScheme.secondaryContainer,
                onClick = onNavigateToRevenue,
                modifier = Modifier.fillMaxWidth()
            )
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun FunctionCard(
    title: String,
    subtitle: String,
    icon: ImageVector,
    backgroundColor: androidx.compose.ui.graphics.Color,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Card(
        onClick = onClick,
        modifier = modifier.height(120.dp),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(
            containerColor = backgroundColor
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                modifier = Modifier.size(40.dp),
                tint = MaterialTheme.colorScheme.onPrimaryContainer
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = title,
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Center,
                color = MaterialTheme.colorScheme.onPrimaryContainer
            )
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodySmall,
                textAlign = TextAlign.Center,
                color = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.7f)
            )
        }
    }
}
