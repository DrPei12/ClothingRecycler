package com.example.clothingrecycler.ui.screens.order

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.FilterList
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.SwapVert
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.clothingrecycler.data.local.database.ClothingRecyclerDatabase
import com.example.clothingrecycler.data.local.entity.OrderEntity
import com.example.clothingrecycler.domain.model.Order
import com.example.clothingrecycler.domain.model.OrderType
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch
import java.text.DecimalFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private val decimalFormat = DecimalFormat("#.##")
private val dateFormat = SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.getDefault())

fun OrderEntity.toOrder(): Order {
    return Order(
        id = id,
        orderNumber = orderNumber,
        type = if (type == "INBOUND") OrderType.INBOUND else OrderType.OUTBOUND,
        totalWeight = totalWeight,
        totalAmount = totalAmount,
        categoryCount = categoryCount,
        itemCount = itemCount,
        timestamp = timestamp,
        note = note,
        customerId = customerId
    )
}

data class OrderFilterState(
    val searchKeyword: String = "",
    val showInbound: Boolean = true,
    val showOutbound: Boolean = true,
    val minAmount: String = "",
    val maxAmount: String = ""
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrderListScreen(
    onNavigateBack: () -> Unit,
    onOrderClick: (Long) -> Unit,
    viewModel: OrderListViewModel = hiltViewModel()
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val filterState by viewModel.filterState.collectAsState()

    val orders by ClothingRecyclerDatabase.getInstance(context).orderDao().getAllOrders()
        .map { entities -> entities.map { it.toOrder() } }
        .collectAsStateWithLifecycle(initialValue = emptyList())

    val customers by ClothingRecyclerDatabase.getInstance(context).customerDao().getAllCustomers()
        .collectAsStateWithLifecycle(initialValue = emptyList())

    val customerMap = remember(customers) {
        customers.associate { it.id to it.name }
    }

    var orderToDelete by remember { mutableStateOf<Order?>(null) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "订单记录",
                        fontWeight = FontWeight.Bold
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
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
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            // 搜索栏
            OutlinedTextField(
                value = filterState.searchKeyword,
                onValueChange = { viewModel.updateSearchKeyword(it) },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 8.dp),
                placeholder = { Text("搜索客户名称...") },
                leadingIcon = {
                    Icon(Icons.Default.Search, contentDescription = null)
                },
                trailingIcon = {
                    if (filterState.searchKeyword.isNotEmpty()) {
                        IconButton(onClick = { viewModel.updateSearchKeyword("") }) {
                            Icon(Icons.Default.Clear, contentDescription = "清除")
                        }
                    }
                },
                singleLine = true,
                shape = RoundedCornerShape(12.dp)
            )

            // 筛选标签和金额
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    FilterChip(
                        selected = filterState.showInbound,
                        onClick = { viewModel.updateShowInbound(!filterState.showInbound) },
                        label = { Text("入库") }
                    )
                    FilterChip(
                        selected = filterState.showOutbound,
                        onClick = { viewModel.updateShowOutbound(!filterState.showOutbound) },
                        label = { Text("出库") }
                    )
                }

                Spacer(modifier = Modifier.height(8.dp))

                // 金额范围输入
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = filterState.minAmount,
                        onValueChange = { viewModel.updateMinAmount(it) },
                        modifier = Modifier.weight(1f),
                        label = { Text("最小金额") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        singleLine = true,
                        shape = RoundedCornerShape(8.dp)
                    )
                    OutlinedTextField(
                        value = filterState.maxAmount,
                        onValueChange = { viewModel.updateMaxAmount(it) },
                        modifier = Modifier.weight(1f),
                        label = { Text("最大金额") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        singleLine = true,
                        shape = RoundedCornerShape(8.dp)
                    )
                }

                // 清除筛选按钮
                if (filterState.searchKeyword.isNotEmpty() || filterState.minAmount.isNotEmpty() || filterState.maxAmount.isNotEmpty() || !filterState.showInbound || !filterState.showOutbound) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.End
                    ) {
                        TextButton(onClick = { viewModel.resetFilters() }) {
                            Icon(Icons.Default.Clear, contentDescription = null)
                            Text("清除筛选")
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            // 订单列表
            val filteredOrders = orders.filter { order ->
                val customerName = order.customerId?.let { customerMap[it] } ?: ""
                val matchesSearch = filterState.searchKeyword.isEmpty() ||
                        customerName.contains(filterState.searchKeyword, ignoreCase = true)
                val matchesType = (filterState.showInbound && order.type == OrderType.INBOUND) ||
                        (filterState.showOutbound && order.type == OrderType.OUTBOUND)
                val matchesMinAmount = filterState.minAmount.isEmpty() ||
                        order.totalAmount >= (filterState.minAmount.toDoubleOrNull() ?: 0.0)
                val matchesMaxAmount = filterState.maxAmount.isEmpty() ||
                        order.totalAmount <= (filterState.maxAmount.toDoubleOrNull() ?: Double.MAX_VALUE)

                matchesSearch && matchesType && matchesMinAmount && matchesMaxAmount
            }

            if (filteredOrders.isEmpty()) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(16.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Text(
                            text = "暂无符合条件的订单",
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            } else {
                LazyColumn(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(horizontal = 16.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    items(filteredOrders) { order ->
                        OrderCard(
                            order = order,
                            customerMap = customerMap,
                            onClick = { onOrderClick(order.id) },
                            onDelete = { orderToDelete = order }
                        )
                    }
                    item { Spacer(modifier = Modifier.height(8.dp)) }
                }
            }
        }
    }

    // 删除确认对话框
    if (orderToDelete != null) {
        AlertDialog(
            onDismissRequest = { orderToDelete = null },
            title = { Text("确认删除") },
            text = {
                Column {
                    Text("确定要删除订单吗？")
                    Text(
                        text = orderToDelete!!.orderNumber,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        scope.launch {
                            ClothingRecyclerDatabase.getInstance(context).orderDao().deleteOrder(orderToDelete!!.id)
                            orderToDelete = null
                        }
                    }
                ) {
                    Text("删除", color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = {
                TextButton(onClick = { orderToDelete = null }) {
                    Text("取消")
                }
            }
        )
    }
}

@Composable
private fun OrderCard(
    order: Order,
    customerMap: Map<Long, String>,
    onClick: () -> Unit,
    onDelete: () -> Unit
) {
    val customerName = order.customerId?.let { customerMap[it] }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        colors = CardDefaults.cardColors(
            containerColor = if (order.type == OrderType.INBOUND)
                MaterialTheme.colorScheme.secondaryContainer
            else
                MaterialTheme.colorScheme.tertiaryContainer
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = Icons.Default.SwapVert,
                contentDescription = null,
                tint = if (order.type == OrderType.INBOUND)
                    MaterialTheme.colorScheme.onSecondaryContainer
                else
                    MaterialTheme.colorScheme.onTertiaryContainer
            )

            Spacer(modifier = Modifier.width(12.dp))

            Column(modifier = Modifier.weight(1f)) {
                Row(
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = if (order.type == OrderType.INBOUND) "入库" else "出库",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = if (order.type == OrderType.INBOUND)
                            MaterialTheme.colorScheme.onSecondaryContainer
                        else
                            MaterialTheme.colorScheme.onTertiaryContainer
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = order.orderNumber,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                Spacer(modifier = Modifier.height(4.dp))

                Text(
                    text = dateFormat.format(Date(order.timestamp)),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                Text(
                    text = "总重量: ${decimalFormat.format(order.totalWeight)}公斤 | 金额: ¥${decimalFormat.format(order.totalAmount)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                Text(
                    text = "涉及 ${order.categoryCount} 个类别，共 ${order.itemCount} 条记录",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
                )

                if (customerName != null) {
                    Text(
                        text = "客户: $customerName",
                        style = MaterialTheme.typography.bodySmall,
                        fontWeight = FontWeight.Medium,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
            }

            IconButton(onClick = onDelete) {
                Icon(
                    imageVector = Icons.Default.Delete,
                    contentDescription = "删除",
                    tint = MaterialTheme.colorScheme.error
                )
            }
        }
    }
}
