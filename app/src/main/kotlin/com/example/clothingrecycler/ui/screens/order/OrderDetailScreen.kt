package com.example.clothingrecycler.ui.screens.order

import android.database.sqlite.SQLiteException
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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.SwapVert
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
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.clothingrecycler.data.local.database.ClothingRecyclerDatabase
import com.example.clothingrecycler.domain.model.Order
import com.example.clothingrecycler.domain.model.OrderType
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.text.DecimalFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private val decimalFormat = DecimalFormat("#.##")
private val dateFormat = SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.getDefault())

data class CategoryDetail(
    val name: String,
    val weight: Double,
    val unitPrice: Double,
    val totalAmount: Double
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrderDetailScreen(
    orderId: Long,
    onNavigateBack: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var isLoading by remember { mutableStateOf(true) }
    var error by remember { mutableStateOf<String?>(null) }
    var order by remember { mutableStateOf<Order?>(null) }
    var customerName by remember { mutableStateOf<String?>(null) }
    var categoryDetails by remember { mutableStateOf<List<CategoryDetail>>(emptyList()) }

    LaunchedEffect(orderId) {
        scope.launch {
            try {
                withContext(Dispatchers.IO) {
                    val db = ClothingRecyclerDatabase.getInstance(context)
                    
                    // 获取订单
                    val orderEntity = db.orderDao().getOrderById(orderId) ?: return@withContext
                    
                    val orderData = Order(
                        id = orderEntity.id,
                        orderNumber = orderEntity.orderNumber,
                        type = if (orderEntity.type == "INBOUND") OrderType.INBOUND else OrderType.OUTBOUND,
                        totalWeight = orderEntity.totalWeight,
                        totalAmount = orderEntity.totalAmount,
                        categoryCount = orderEntity.categoryCount,
                        itemCount = orderEntity.itemCount,
                        timestamp = orderEntity.timestamp,
                        note = orderEntity.note,
                        customerId = orderEntity.customerId
                    )
                    
                    // 获取客户名称
                    val customer = orderEntity.customerId?.let { db.customerDao().getCustomerById(it) }
                    
                    // 获取类别详情
                    val orderTimestamp = orderEntity.timestamp
                    val details = mutableListOf<CategoryDetail>()
                    
                    if (orderEntity.type == "INBOUND") {
                        val records = db.inboundRecordDao().getRecordsByTimeRangeSync(orderTimestamp, orderTimestamp + 60000)
                        for (record in records) {
                            val category = db.categoryDao().getCategoryById(record.categoryId)
                            details.add(
                                CategoryDetail(
                                    name = category?.name ?: "未知类别",
                                    weight = record.weight,
                                    unitPrice = record.unitPrice,
                                    totalAmount = record.totalCost
                                )
                            )
                        }
                    } else {
                        val records = db.outboundRecordDao().getRecordsByTimeRangeSync(orderTimestamp, orderTimestamp + 60000)
                        for (record in records) {
                            val category = db.categoryDao().getCategoryById(record.categoryId)
                            details.add(
                                CategoryDetail(
                                    name = category?.name ?: "未知类别",
                                    weight = record.weight,
                                    unitPrice = record.unitPrice,
                                    totalAmount = record.totalRevenue
                                )
                            )
                        }
                    }
                    
                    withContext(Dispatchers.Main) {
                        order = orderData
                        customerName = customer?.name
                        categoryDetails = details
                    }
                }
            } catch (e: SQLiteException) {
                withContext(Dispatchers.Main) {
                    error = "数据库错误: ${e.message}"
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    error = "错误: ${e.javaClass.simpleName} - ${e.message}"
                }
            } finally {
                withContext(Dispatchers.Main) {
                    isLoading = false
                }
            }
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("订单详情", fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "返回")
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
        when {
            isLoading -> {
                Box(modifier = Modifier.fillMaxSize().padding(paddingValues), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
            }
            error != null -> {
                Box(modifier = Modifier.fillMaxSize().padding(paddingValues), contentAlignment = Alignment.Center) {
                    Text("错误: $error")
                }
            }
            order == null -> {
                Box(modifier = Modifier.fillMaxSize().padding(paddingValues), contentAlignment = Alignment.Center) {
                    Text("订单不存在")
                }
            }
            else -> {
                val currentOrder = order!!
                LazyColumn(modifier = Modifier.fillMaxSize().padding(paddingValues).padding(16.dp)) {
                    // 客户信息
                    item {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
                        ) {
                            Row(modifier = Modifier.fillMaxWidth().padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Default.Person, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                                Spacer(modifier = Modifier.width(12.dp))
                                Column {
                                    Text("客户", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onPrimaryContainer)
                                    Text(customerName ?: "未关联客户", style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.Medium)
                                }
                            }
                        }
                        Spacer(modifier = Modifier.height(16.dp))
                    }

                    // 订单基本信息
                    item {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = if (currentOrder.type == OrderType.INBOUND)
                                    MaterialTheme.colorScheme.secondaryContainer
                                else
                                    MaterialTheme.colorScheme.tertiaryContainer
                            )
                        ) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Icon(Icons.Default.SwapVert, contentDescription = null)
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text(
                                        if (currentOrder.type == OrderType.INBOUND) "入库订单" else "出库订单",
                                        style = MaterialTheme.typography.titleLarge,
                                        fontWeight = FontWeight.Bold
                                    )
                                }
                                Spacer(modifier = Modifier.height(8.dp))
                                Text("订单号: ${currentOrder.orderNumber}", style = MaterialTheme.typography.bodyMedium)
                                Text("下单时间: ${dateFormat.format(Date(currentOrder.timestamp))}", style = MaterialTheme.typography.bodyMedium)
                                if (currentOrder.note.isNotEmpty()) {
                                    Spacer(modifier = Modifier.height(4.dp))
                                    Text("备注: ${currentOrder.note}", style = MaterialTheme.typography.bodySmall)
                                }
                            }
                        }
                        Spacer(modifier = Modifier.height(16.dp))
                    }

                    // 衣物品类详情
                    if (categoryDetails.isNotEmpty()) {
                        item {
                            Text(
                                if (currentOrder.type == OrderType.INBOUND) "入库类别" else "出库类别",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold
                            )
                            Spacer(modifier = Modifier.height(8.dp))
                        }

                        items(categoryDetails) { detail ->
                            Card(modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                                Row(modifier = Modifier.fillMaxWidth().padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(detail.name, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.Medium)
                                        Text(
                                            if (currentOrder.type == OrderType.INBOUND)
                                                "收购价: ¥${decimalFormat.format(detail.unitPrice)}/公斤"
                                            else
                                                "卖价: ¥${decimalFormat.format(detail.unitPrice)}/公斤",
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                    }
                                    Column(horizontalAlignment = Alignment.End) {
                                        Text("${decimalFormat.format(detail.weight)} 公斤", style = MaterialTheme.typography.bodyMedium)
                                        Text(
                                            "¥${decimalFormat.format(detail.totalAmount)}",
                                            style = MaterialTheme.typography.bodyMedium,
                                            fontWeight = FontWeight.Bold,
                                            color = if (currentOrder.type == OrderType.INBOUND)
                                                MaterialTheme.colorScheme.error
                                            else
                                                MaterialTheme.colorScheme.primary
                                        )
                                    }
                                }
                            }
                        }
                        item { Spacer(modifier = Modifier.height(16.dp)) }
                    }

                    // 订单统计
                    item {
                        Card(modifier = Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                Text("订单统计", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                                Spacer(modifier = Modifier.height(8.dp))
                                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                    Text("总重量", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f))
                                    Text("${decimalFormat.format(currentOrder.totalWeight)} 公斤", style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Bold)
                                }
                                Spacer(modifier = Modifier.height(4.dp))
                                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                    Text("总金额", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f))
                                    Text("¥${decimalFormat.format(currentOrder.totalAmount)}", style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.primary)
                                }
                                Spacer(modifier = Modifier.height(4.dp))
                                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                    Text("涉及类别", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f))
                                    Text("${currentOrder.categoryCount} 个", style = MaterialTheme.typography.bodyMedium)
                                }
                                Spacer(modifier = Modifier.height(4.dp))
                                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                    Text("记录条目", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f))
                                    Text("${currentOrder.itemCount} 条", style = MaterialTheme.typography.bodyMedium)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
