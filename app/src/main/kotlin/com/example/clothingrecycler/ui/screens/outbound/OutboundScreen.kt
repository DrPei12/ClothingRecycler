package com.example.clothingrecycler.ui.screens.outbound

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Person
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import java.text.DecimalFormat
import androidx.hilt.navigation.compose.hiltViewModel
import com.example.clothingrecycler.data.local.database.ClothingRecyclerDatabase
import com.example.clothingrecycler.data.local.entity.CustomerEntity
import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.model.Customer
import com.example.clothingrecycler.domain.model.OutboundRecord
import com.example.clothingrecycler.domain.model.WeightUnit
import com.example.clothingrecycler.ui.components.WeightInputCard
import kotlinx.coroutines.launch

// 数据类用于存储选中的类别和重量
data class OutboundCategoryWeightItem(
    val category: Category,
    var weight: Double
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OutboundScreen(
    onNavigateBack: () -> Unit,
    viewModel: OutboundViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val scope = rememberCoroutineScope()
    val decimalFormat = DecimalFormat("#.##")
    val context = LocalContext.current
    val db = remember { ClothingRecyclerDatabase.getInstance(context) }

    val selectedItems = remember { mutableStateListOf<OutboundCategoryWeightItem>() }

    // 客户选择相关
    var showCustomerSelector by remember { mutableStateOf(false) }
    var selectedCustomer by remember { mutableStateOf<Customer?>(null) }
    var showAddCustomerDialog by remember { mutableStateOf(false) }

    var showConfirmDialog by remember { mutableStateOf(false) }
    var showCategorySelector by remember { mutableStateOf(false) }

    val sheetState = rememberModalBottomSheetState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "出库 - 卖出衣物",
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
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {
            // 客户选择卡片
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { showCustomerSelector = true },
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.surfaceVariant
                )
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.Default.Person,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary
                    )
                    Spacer(modifier = Modifier.width(12.dp))
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "客户（可选）",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = selectedCustomer?.name ?: "点击选择客户",
                            style = MaterialTheme.typography.bodyLarge
                        )
                    }
                    if (selectedCustomer != null) {
                        IconButton(onClick = { selectedCustomer = null }) {
                            Icon(
                                imageVector = Icons.Default.Close,
                                contentDescription = "清除",
                                tint = MaterialTheme.colorScheme.error
                            )
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                OutlinedButton(
                    onClick = { showCategorySelector = true },
                    modifier = Modifier.weight(1f)
                ) {
                    Text("+ 添加类别")
                }

                Button(
                    onClick = { showConfirmDialog = true },
                    enabled = selectedItems.isNotEmpty(),
                    modifier = Modifier.weight(1f)
                ) {
                    Icon(Icons.Default.Check, contentDescription = null)
                    Text(" 保存(${selectedItems.size})")
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            if (selectedItems.isEmpty()) {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.surfaceVariant
                    )
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(32.dp),
                        horizontalAlignment = Alignment.CenterHorizontally
                    ) {
                        Text(
                            text = "暂无出库物品",
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "点击「添加类别」开始出库",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
                        )
                    }
                }
            } else {
                LazyColumn(
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    items(selectedItems.toList()) { item ->
                        val unitLabel = when (item.category.unitType) {
                            WeightUnit.KILOGRAM -> "公斤"
                            WeightUnit.JIN -> "斤"
                            WeightUnit.PIECE -> "件"
                        }
                        WeightInputCard(
                            label = "${item.category.name} ($unitLabel)",
                            weight = item.weight,
                            onWeightChange = { newWeight ->
                                val index = selectedItems.indexOfFirst { it.category.id == item.category.id }
                                if (index >= 0) {
                                    if (newWeight > 0) {
                                        selectedItems[index] = selectedItems[index].copy(weight = newWeight)
                                    } else {
                                        selectedItems.removeAt(index)
                                    }
                                }
                            },
                            unit = unitLabel,
                            step = if (item.category.unitType == WeightUnit.PIECE) 1.0 else 0.1,
                            maxDecimals = if (item.category.unitType == WeightUnit.PIECE) 0 else 1
                        )
                    }

                    item {
                        Spacer(modifier = Modifier.height(8.dp))
                    }
                }
            }

            if (selectedItems.isNotEmpty()) {
                Spacer(modifier = Modifier.weight(1f))

                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.tertiaryContainer
                    )
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp)
                    ) {
                        Text(
                            text = "本次出库统计",
                            style = MaterialTheme.typography.titleSmall,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        val totalRevenue = selectedItems.sumOf { item ->
                            item.category.sellPrice * item.weight
                        }
                        Text(
                            text = "总收入: ¥${if (totalRevenue.isNaN() || totalRevenue.isInfinite()) "0" else decimalFormat.format(totalRevenue)}",
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.primary
                        )
                    }
                }
            }
        }

        // 客户选择底部弹窗
        if (showCustomerSelector) {
            ModalBottomSheet(
                onDismissRequest = { showCustomerSelector = false },
                sheetState = sheetState
            ) {
                OutboundCustomerSelectorContent(
                    onCustomerSelected = { customer ->
                        selectedCustomer = customer
                        showCustomerSelector = false
                    },
                    onAddCustomer = {
                        showCustomerSelector = false
                        showAddCustomerDialog = true
                    },
                    onDismiss = { showCustomerSelector = false }
                )
            }
        }

        // 新增客户对话框
        if (showAddCustomerDialog) {
            OutboundAddCustomerDialog(
                onDismiss = { showAddCustomerDialog = false },
                onCustomerCreated = { customer ->
                    selectedCustomer = customer
                    showAddCustomerDialog = false
                }
            )
        }

        if (showCategorySelector) {
            CategorySelectorDialog(
                categories = uiState.categories.filter { it.stock > 0 },
                selectedIds = selectedItems.map { it.category.id }.toSet(),
                onCategorySelected = { category ->
                    if (selectedItems.none { it.category.id == category.id }) {
                        selectedItems.add(OutboundCategoryWeightItem(category, 0.0))
                    }
                    showCategorySelector = false
                },
                onDismiss = { showCategorySelector = false }
            )
        }

        if (showConfirmDialog) {
            val totalRevenue = selectedItems.sumOf { item ->
                item.category.sellPrice * item.weight
            }

            AlertDialog(
                onDismissRequest = { showConfirmDialog = false },
                title = { Text("确认出库") },
                text = {
                    Column {
                        Text("客户: ${selectedCustomer?.name ?: "未选择"}")
                        Text("总收入: ¥${if (totalRevenue.isNaN() || totalRevenue.isInfinite()) "0" else decimalFormat.format(totalRevenue)}")
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "确认后将更新库存并生成记录",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                },
                confirmButton = {
                    Button(
                        onClick = {
                            scope.launch {
                                val records = selectedItems.map { item ->
                                    OutboundRecord(
                                        categoryId = item.category.id,
                                        categoryName = item.category.name,
                                        weight = item.weight,
                                        unitPrice = item.category.sellPrice,
                                        totalRevenue = item.category.sellPrice * item.weight,
                                        unitType = item.category.unitType
                                    )
                                }
                                viewModel.saveOutboundRecords(records, selectedCustomer?.id)

                                // 如果选择了客户，更新客户状态
                                selectedCustomer?.let { customer ->
                                    db.customerDao().updateOutboundStatus(customer.id, true)
                                }

                                selectedItems.clear()
                                showConfirmDialog = false
                                onNavigateBack()
                            }
                        }
                    ) {
                        Text("确认")
                    }
                },
                dismissButton = {
                    TextButton(onClick = { showConfirmDialog = false }) {
                        Text("取消")
                    }
                }
            )
        }
    }
}

@Composable
private fun OutboundCustomerSelectorContent(
    onCustomerSelected: (Customer) -> Unit,
    onAddCustomer: () -> Unit,
    onDismiss: () -> Unit
) {
    val context = LocalContext.current
    val db = remember { ClothingRecyclerDatabase.getInstance(context) }
    val customers by db.customerDao().getAllCustomers().collectAsState(initial = emptyList())

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(16.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = "选择客户",
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.Bold
            )
            TextButton(onClick = onDismiss) {
                Text("关闭")
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        // 新增客户按钮
        OutlinedButton(
            onClick = onAddCustomer,
            modifier = Modifier.fillMaxWidth()
        ) {
            Icon(Icons.Default.Add, contentDescription = null)
            Text(" 新增客户")
        }

        Spacer(modifier = Modifier.height(16.dp))

        // 客户列表
        if (customers.isEmpty()) {
            Text(
                text = "暂无客户，点击上方按钮添加",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        } else {
            LazyColumn(
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(customers) { customer ->
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clickable {
                                onCustomerSelected(
                                    Customer(
                                        id = customer.id,
                                        name = customer.name,
                                        phone = customer.phone,
                                        email = customer.email,
                                        address = customer.address,
                                        hasInboundOrders = customer.hasInboundOrders,
                                        hasOutboundOrders = customer.hasOutboundOrders
                                    )
                                )
                            }
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Icon(
                                imageVector = Icons.Default.Person,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.primary
                            )
                            Spacer(modifier = Modifier.width(12.dp))
                            Column {
                                Text(
                                    text = customer.name,
                                    style = MaterialTheme.typography.bodyLarge,
                                    fontWeight = FontWeight.Medium
                                )
                                if (customer.phone.isNotBlank()) {
                                    Text(
                                        text = customer.phone,
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }
                        }
                    }
                }

                item {
                    Spacer(modifier = Modifier.height(32.dp))
                }
            }
        }
    }
}

@Composable
private fun OutboundAddCustomerDialog(
    onDismiss: () -> Unit,
    onCustomerCreated: (Customer) -> Unit
) {
    var name by remember { mutableStateOf("") }
    var phone by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var address by remember { mutableStateOf("") }
    var error by remember { mutableStateOf<String?>(null) }

    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val db = remember { ClothingRecyclerDatabase.getInstance(context) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("新增客户") },
        text = {
            Column {
                OutlinedTextField(
                    value = name,
                    onValueChange = {
                        name = it
                        error = null
                    },
                    label = { Text("名称 *") },
                    isError = error != null && name.isBlank(),
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )

                Spacer(modifier = Modifier.height(8.dp))

                OutlinedTextField(
                    value = phone,
                    onValueChange = { phone = it },
                    label = { Text("电话") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )

                Spacer(modifier = Modifier.height(8.dp))

                OutlinedTextField(
                    value = email,
                    onValueChange = { email = it },
                    label = { Text("邮箱") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )

                Spacer(modifier = Modifier.height(8.dp))

                OutlinedTextField(
                    value = address,
                    onValueChange = { address = it },
                    label = { Text("地址") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )

                if (error != null) {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = error!!,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    if (name.isBlank()) {
                        error = "名称不能为空"
                        return@Button
                    }
                    scope.launch {
                        val customerId = db.customerDao().insertCustomer(
                            CustomerEntity(
                                name = name.trim(),
                                phone = phone.trim(),
                                email = email.trim(),
                                address = address.trim(),
                                hasOutboundOrders = true
                            )
                        )
                        val customer = Customer(
                            id = customerId,
                            name = name.trim(),
                            phone = phone.trim(),
                            email = email.trim(),
                            address = address.trim(),
                            hasOutboundOrders = true
                        )
                        onCustomerCreated(customer)
                    }
                }
            ) {
                Text("保存")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("取消")
            }
        }
    )
}

@Composable
private fun CategorySelectorDialog(
    categories: List<Category>,
    selectedIds: Set<Long>,
    onCategorySelected: (Category) -> Unit,
    onDismiss: () -> Unit
) {
    val decimalFormat = remember { DecimalFormat("#.##") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("选择类别（有库存的类别）") },
        text = {
            LazyColumn {
                items(categories) { category ->
                    val (displayStock, stockUnit) = when (category.unitType) {
                        WeightUnit.KILOGRAM -> category.stock to "公斤"
                        WeightUnit.JIN -> category.stockInJin to "斤"
                        WeightUnit.PIECE -> category.stockInPieces.toDouble() to "件"
                    }
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 4.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Checkbox(
                            checked = category.id in selectedIds,
                            onCheckedChange = {
                                if (!selectedIds.contains(category.id)) {
                                    onCategorySelected(category)
                                }
                            }
                        )
                        Column(modifier = Modifier.weight(1f)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(
                                    text = category.name,
                                    style = MaterialTheme.typography.bodyLarge
                                )
                                Text(
                                    text = "($stockUnit)",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    modifier = Modifier.padding(start = 4.dp)
                                )
                            }
                            Text(
                                text = "库存: ${decimalFormat.format(displayStock)}$stockUnit | 卖价: ¥${decimalFormat.format(category.sellPrice)}/$stockUnit",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text("关闭")
            }
        }
    )
}
