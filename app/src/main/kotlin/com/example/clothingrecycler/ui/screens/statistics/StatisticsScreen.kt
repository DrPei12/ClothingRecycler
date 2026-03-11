package com.example.clothingrecycler.ui.screens.statistics

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.ArrowUpward
import androidx.compose.material.icons.filled.AttachMoney
import androidx.compose.material.icons.filled.Inventory
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.example.clothingrecycler.domain.repository.OrderStatistics
import java.text.DecimalFormat

private val decimalFormat = DecimalFormat("#.##")

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StatisticsScreen(
    onNavigateBack: () -> Unit,
    viewModel: StatisticsViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "数据统计",
                        fontWeight = FontWeight.Bold
                    )
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
                .verticalScroll(rememberScrollState())
        ) {
            // 周期选择器
            PeriodSelector(
                selectedPeriod = uiState.period,
                onPeriodSelected = { viewModel.setPeriod(it) }
            )

            Spacer(modifier = Modifier.height(16.dp))

            // 统计卡片
            StatisticsCards(statistics = uiState.statistics)

            Spacer(modifier = Modifier.height(16.dp))

            // 详细数据展示
            DetailedStatsSection(statistics = uiState.statistics)
        }
    }
}

@Composable
private fun PeriodSelector(
    selectedPeriod: StatisticsPeriod,
    onPeriodSelected: (StatisticsPeriod) -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        StatisticsPeriod.entries.take(4).forEach { period ->
            FilterChip(
                selected = selectedPeriod == period,
                onClick = { onPeriodSelected(period) },
                label = {
                    Text(
                        text = when (period) {
                            StatisticsPeriod.TODAY -> "今日"
                            StatisticsPeriod.THIS_WEEK -> "本周"
                            StatisticsPeriod.THIS_MONTH -> "本月"
                            StatisticsPeriod.THIS_YEAR -> "本年"
                            StatisticsPeriod.CUSTOM -> "自定义"
                        }
                    )
                },
                colors = FilterChipDefaults.filterChipColors(
                    selectedContainerColor = MaterialTheme.colorScheme.primary,
                    selectedLabelColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        }
    }
}

@Composable
private fun StatisticsCards(statistics: OrderStatistics) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        // 总览卡片
        StatCard(
            title = "订单总数",
            value = "${statistics.orderCount}",
            icon = Icons.Default.Inventory,
            backgroundColor = MaterialTheme.colorScheme.primaryContainer
        )

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatCard(
                title = "入库总量",
                value = "${decimalFormat.format(statistics.inboundWeight)} kg",
                icon = Icons.Default.ArrowDownward,
                backgroundColor = MaterialTheme.colorScheme.secondaryContainer,
                modifier = Modifier.weight(1f)
            )
            StatCard(
                title = "出库总量",
                value = "${decimalFormat.format(statistics.outboundWeight)} kg",
                icon = Icons.Default.ArrowUpward,
                backgroundColor = MaterialTheme.colorScheme.tertiaryContainer,
                modifier = Modifier.weight(1f)
            )
        }

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatCard(
                title = "入库金额",
                value = "¥${decimalFormat.format(statistics.inboundAmount)}",
                icon = Icons.Default.AttachMoney,
                backgroundColor = Color(0xFFE3F2FD),
                modifier = Modifier.weight(1f)
            )
            StatCard(
                title = "出库金额",
                value = "¥${decimalFormat.format(statistics.outboundAmount)}",
                icon = Icons.Default.AttachMoney,
                backgroundColor = Color(0xFFFFF3E0),
                modifier = Modifier.weight(1f)
            )
        }

        // 净利润卡片
        val netProfit = statistics.outboundAmount - statistics.inboundAmount
        StatCard(
            title = "净利润",
            value = "¥${decimalFormat.format(netProfit)}",
            icon = Icons.Default.AttachMoney,
            backgroundColor = if (netProfit >= 0) Color(0xFFE8F5E9) else Color(0xFFFFEBEE),
            valueColor = if (netProfit >= 0) Color(0xFF2E7D32) else Color(0xFFC62828)
        )
    }
}

@Composable
private fun StatCard(
    title: String,
    value: String,
    icon: ImageVector,
    backgroundColor: Color,
    modifier: Modifier = Modifier,
    valueColor: Color = MaterialTheme.colorScheme.onSurface
) {
    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = backgroundColor),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onPrimaryContainer
            )
            Spacer(modifier = Modifier.width(12.dp))
            Column {
                Text(
                    text = title,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.7f)
                )
                Text(
                    text = value,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                    color = valueColor
                )
            }
        }
    }
}

@Composable
private fun DetailedStatsSection(statistics: OrderStatistics) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp)
        ) {
            Text(
                text = "详细数据",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold
            )

            Spacer(modifier = Modifier.height(12.dp))

            DetailRow(label = "入库均价", value = "¥${decimalFormat.format(if (statistics.inboundWeight > 0) statistics.inboundAmount / statistics.inboundWeight else 0.0)}/kg")
            DetailRow(label = "出库均价", value = "¥${decimalFormat.format(if (statistics.outboundWeight > 0) statistics.outboundAmount / statistics.outboundWeight else 0.0)}/kg")
            DetailRow(label = "毛利率", value = "${decimalFormat.format(if (statistics.inboundAmount > 0) ((statistics.outboundAmount - statistics.inboundAmount) / statistics.inboundAmount * 100) else 0.0)}%")
            DetailRow(label = "库存周转", value = "${decimalFormat.format(statistics.totalWeight)} kg")
        }
    }
}

@Composable
private fun DetailRow(label: String, value: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.7f)
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium
        )
    }
}
