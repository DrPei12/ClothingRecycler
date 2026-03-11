package com.example.clothingrecycler.ui.screens.inbound

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.shrinkVertically
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
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.TrendingDown
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
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import kotlinx.coroutines.launch
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.example.clothingrecycler.data.local.database.ClothingRecyclerDatabase
import com.example.clothingrecycler.data.local.entity.CustomerEntity
import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.model.Customer
import java.text.DecimalFormat

private val decimalFormat = DecimalFormat("#.##")

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun InboundScreen(
    onNavigateBack: () -> Unit,
    viewModel: InboundViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val context = LocalContext.current
    val db = remember { ClothingRecyclerDatabase.getInstance(context) }
    val sheetState = rememberModalBottomSheetState()

    var showCategorySelector by remember { mutableStateOf(false) }
    var showConfirmDialog by remember { mutableStateOf(false) }
    var showAddCustomerDialog by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "入库 - 收购衣物",
                        fontWeight = FontWeight.Bold
                    )
                },
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
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {
            // 客户选择卡片
            CustomerCard(
                customer = uiState.selectedCustomer,
                onClick = { viewModel.showCustomerSelector() },
                onClear = { viewModel.clearCustomerSelection() }
            )

            Spacer(modifier = Modifier.height(12.dp))

            // 操作按钮
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                OutlinedButton(
                    onClick = { showCategorySelector = true },
                    modifier = Modifier.weight(1f)
                ) {
                    Icon(Icons.Default.Add, contentDescription = null)
                    Text(" 添加类别")
                }

                Button(
                    onClick = { showConfirmDialog = true },
                    enabled = uiState.inboundItems.any { it.isValid },
                    modifier = Modifier.weight(1f)
                ) {
                    Icon(Icons.Default.Check, contentDescription = null)
                    Text(" 保存")
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // 入库列表
            if (uiState.inboundItems.isEmpty()) {
                EmptyState()
            } else {
                LazyColumn(
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    items(uiState.inboundItems.filter { it.weight.isNotEmpty() || it.isExpanded }) { item ->
                        val hasMemoryPrice = uiState.customerPrices.containsKey(item.category.id)
                        val memoryPrice = uiState.customerPrices[item.category.id]

                        InboundItemCard(
                            item = item,
                            useMemoryPrice = hasMemoryPrice,
                            memoryPrice = memoryPrice,
                            defaultPrice = item.category.buyPrice,
                            onWeightChange = { viewModel.updateWeight(item.category.id, it) },
                            onPriceChange = { viewModel.updateUnitPrice(item.category.id, it) },
                            onExpand = { viewModel.toggleExpanded(item.category.id) },
                            onDelete = { viewModel.removeItem(item.category.id) }
                        )
                    }

                    item {
                        Spacer(modifier = Modifier.height(80.dp))
                    }
                }
            }

            // 底部统计
            AnimatedVisibility(
                visible = uiState.inboundItems.any { it.isValid },
                enter = expandVertically(),
                exit = shrinkVertically()
            ) {
                BottomSummary(
                    totalCost = viewModel.getTotalCost(),
                    itemCount = uiState.inboundItems.count { it.isValid }
                )
            }
        }

        // 客户选择底部弹窗
        if (uiState.showCustomerSelector) {
            ModalBottomSheet(
                onDismissRequest = { viewModel.hideCustomerSelector() },
                sheetState = sheetState
            ) {
                CustomerSelectorContent(
                    onCustomerSelected = { viewModel.selectCustomer(it) },
                    onAddCustomer = {
                        viewModel.hideCustomerSelector()
                        showAddCustomerDialog = true
                    },
                    onDismiss = { viewModel.hideCustomerSelector() }
                )
            }
        }

        // 类别选择器对话框
        if (showCategorySelector) {
            CategorySelectorDialog(
                categories = uiState.categories,
                selectedIds = uiState.inboundItems.filter { it.weight.isNotEmpty() }.map { it.category.id }.toSet(),
                onCategorySelected = { category -> viewModel.toggleExpanded(category.id) },
                onDismiss = { showCategorySelector = false }
            )
        }

        // 确认保存对话框
        if (showConfirmDialog) {
            ConfirmSaveDialog(
                customer = uiState.selectedCustomer,
                totalCost = viewModel.getTotalCost(),
                itemCount = uiState.inboundItems.count { it.isValid },
                onConfirm = {
                    viewModel.saveInboundRecords {
                        showConfirmDialog = false
                        onNavigateBack()
                    }
                },
                onDismiss = { showConfirmDialog = false }
            )
        }

        // 新增客户对话框
        if (showAddCustomerDialog) {
            AddCustomerDialog(
                onDismiss = { showAddCustomerDialog = false },
                onCustomerCreated = { viewModel.selectCustomer(it) }
            )
        }
    }
}

@Composable
private fun CustomerCard(
    customer: Customer?,
    onClick: () -> Unit,
    onClear: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
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
                    text = "供应商（可选）",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = customer?.name ?: "点击选择供应商",
                    style = MaterialTheme.typography.bodyLarge
                )
                if (customer != null) {
                    Text(
                        text = "将使用该供应商的历史价格",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
            }
            if (customer != null) {
                IconButton(onClick = onClear) {
                    Icon(
                        Icons.Default.Close,
                        contentDescription = "清除",
                        tint = MaterialTheme.colorScheme.error
                    )
                }
            }
        }
    }
}

@Composable
private fun InboundItemCard(
    item: InboundCategoryItem,
    useMemoryPrice: Boolean,
    memoryPrice: Double?,
    defaultPrice: Double,
    onWeightChange: (String) -> Unit,
    onPriceChange: (String) -> Unit,
    onExpand: () -> Unit,
    onDelete: () -> Unit
) {
    val currentPrice = item.unitPrice.toDoubleOrNull() ?: defaultPrice
    val isUsingMemory = useMemoryPrice && currentPrice == memoryPrice

    // 根据单位类型获取显示的标签
    val (quantityLabel, unitLabel) = when (item.unitType) {
        com.example.clothingrecycler.domain.model.WeightUnit.KILOGRAM -> "重量(kg)" to "kg"
        com.example.clothingrecycler.domain.model.WeightUnit.JIN -> "重量(斤)" to "斤"
        com.example.clothingrecycler.domain.model.WeightUnit.PIECE -> "数量(件)" to "件"
    }

    Card(
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = item.category.name,
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = "(${unitLabel})",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = "¥${decimalFormat.format(currentPrice)}/kg",
                            style = MaterialTheme.typography.bodyMedium,
                            color = if (isUsingMemory) MaterialTheme.colorScheme.primary
                            else MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        if (isUsingMemory && memoryPrice != null) {
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(
                                text = "(记忆)",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.primary
                            )
                        }
                    }
                }

                Row {
                    IconButton(onClick = onDelete) {
                        Icon(
                            Icons.Default.Delete,
                            contentDescription = "删除",
                            tint = MaterialTheme.colorScheme.error
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            // 数量和单价输入
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                OutlinedTextField(
                    value = item.weight,
                    onValueChange = onWeightChange,
                    label = { Text(quantityLabel) },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    modifier = Modifier.weight(1f),
                    singleLine = true
                )

                OutlinedTextField(
                    value = if (item.unitPrice.isNotEmpty()) item.unitPrice else "",
                    onValueChange = onPriceChange,
                    label = { Text("单价(¥/$unitLabel)") },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    modifier = Modifier.weight(1f),
                    singleLine = true
                )
            }

            // 小计
            if (item.isValid) {
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    text = "小计: ¥${decimalFormat.format(item.totalCost)}",
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.align(Alignment.End)
                )
            }
        }
    }
}

@Composable
private fun EmptyState() {
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
            Icon(
                imageVector = Icons.Default.TrendingDown,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.height(48.dp)
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "暂无入库物品",
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Text(
                text = "点击「添加类别」开始入库",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
            )
        }
    }
}

@Composable
private fun BottomSummary(
    totalCost: Double,
    itemCount: Int
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.secondaryContainer
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column {
                Text(
                    text = "本次入库 ($itemCount 项)",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f)
                )
                Text(
                    text = "总支出",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSecondaryContainer
                )
            }
            Text(
                text = "¥${decimalFormat.format(totalCost)}",
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.error
            )
        }
    }
}

@Composable
private fun ConfirmSaveDialog(
    customer: Customer?,
    totalCost: Double,
    itemCount: Int,
    onConfirm: () -> Unit,
    onDismiss: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("确认入库") },
        text = {
            Column {
                Text("供应商: ${customer?.name ?: "未选择"}")
                Text("物品数量: $itemCount 项")
                Text("总支出: ¥${decimalFormat.format(totalCost)}")
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    text = "确认后将更新库存并生成记录${if (customer != null) "，并更新该供应商的价格记忆" else ""}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        },
        confirmButton = {
            Button(onClick = onConfirm) {
                Text("确认入库")
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
private fun CustomerSelectorContent(
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
                text = "选择供应商",
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.Bold
            )
            TextButton(onClick = onDismiss) {
                Text("关闭")
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        OutlinedButton(
            onClick = onAddCustomer,
            modifier = Modifier.fillMaxWidth()
        ) {
            Icon(Icons.Default.Add, contentDescription = null)
            Text(" 新增供应商")
        }

        Spacer(modifier = Modifier.height(16.dp))

        if (customers.isEmpty()) {
            Text(
                text = "暂无供应商",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
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
                            Text(
                                text = customer.name,
                                style = MaterialTheme.typography.bodyLarge,
                                fontWeight = FontWeight.Medium
                            )
                        }
                    }
                }
                item { Spacer(modifier = Modifier.height(32.dp)) }
            }
        }
    }
}

@Composable
private fun AddCustomerDialog(
    onDismiss: () -> Unit,
    onCustomerCreated: (Customer) -> Unit
) {
    var name by remember { mutableStateOf("") }
    var phone by remember { mutableStateOf("") }
    var error by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    val context = LocalContext.current
    val db = remember { ClothingRecyclerDatabase.getInstance(context) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("新增供应商") },
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
                                hasInboundOrders = true
                            )
                        )
                        onCustomerCreated(
                            Customer(
                                id = customerId,
                                name = name.trim(),
                                phone = phone.trim(),
                                hasInboundOrders = true
                            )
                        )
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
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("选择类别") },
        text = {
            LazyColumn {
                items(categories) { category ->
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
                            Text(
                                text = category.name,
                                style = MaterialTheme.typography.bodyLarge
                            )
                            Text(
                                text = "初始价: ¥${decimalFormat.format(category.buyPrice)}/kg",
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
