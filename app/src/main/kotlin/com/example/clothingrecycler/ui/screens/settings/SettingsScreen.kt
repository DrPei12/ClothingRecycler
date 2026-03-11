package com.example.clothingrecycler.ui.screens.settings

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.model.WeightUnit
import java.text.DecimalFormat

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    onNavigateBack: () -> Unit,
    viewModel: SettingsViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    var showAddDialog by remember { mutableStateOf(false) }
    var editingCategory by remember { mutableStateOf<Category?>(null) }
    var showDeleteDialog by remember { mutableStateOf<Category?>(null) }
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "设置",
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
            // 称重单位设置
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.surface
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
                            text = "称重单位",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold
                        )
                        Text(
                            text = "当前: ${if (uiState.useKilogram) "公斤 (kg)" else "克 (g)"}",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Switch(
                        checked = uiState.useKilogram,
                        onCheckedChange = { viewModel.setUseKilogram(it) }
                    )
                }
            }
            
            Spacer(modifier = Modifier.height(24.dp))
            
            // 类别管理
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "衣物类别管理",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                
                FilledTonalButton(onClick = { showAddDialog = true }) {
                    Icon(Icons.Default.Add, contentDescription = null)
                    Text("添加类别")
                }
            }
            
            Spacer(modifier = Modifier.height(12.dp))
            
            if (uiState.categories.isEmpty()) {
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
                            text = "暂无类别",
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "点击上方按钮添加衣物类别",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
                        )
                    }
                }
            } else {
                LazyColumn(
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    items(uiState.categories) { category ->
                        CategoryEditCard(
                            category = category,
                            onEdit = { editingCategory = it },
                            onDelete = { showDeleteDialog = it }
                        )
                    }
                }
            }
        }
        
        // 添加类别对话框
        if (showAddDialog) {
            CategoryEditDialog(
                category = null,
                onSave = { category ->
                    viewModel.addCategory(category)
                    showAddDialog = false
                },
                onDismiss = { showAddDialog = false }
            )
        }
        
        // 编辑类别对话框
        editingCategory?.let { category ->
            CategoryEditDialog(
                category = category,
                onSave = { updatedCategory ->
                    viewModel.updateCategory(updatedCategory)
                    editingCategory = null
                },
                onDismiss = { editingCategory = null }
            )
        }
        
        // 删除确认对话框
        showDeleteDialog?.let { category ->
            AlertDialog(
                onDismissRequest = { showDeleteDialog = null },
                title = { Text("确认删除") },
                text = {
                    Column {
                        Text("确定要删除「${category.name}」吗？")
                        if (category.stock > 0) {
                            Spacer(modifier = Modifier.height(8.dp))
                            Text(
                                text = "注意：该类别当前有 ${DecimalFormat("#.##").format(category.stock)} 公斤库存",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.error
                            )
                        }
                    }
                },
                confirmButton = {
                    Button(
                        onClick = {
                            viewModel.deleteCategory(category.id)
                            showDeleteDialog = null
                        }
                    ) {
                        Text("删除")
                    }
                },
                dismissButton = {
                    TextButton(onClick = { showDeleteDialog = null }) {
                        Text("取消")
                    }
                }
            )
        }
    }
}

@Composable
private fun CategoryEditCard(
    category: Category,
    onEdit: (Category) -> Unit,
    onDelete: (Category) -> Unit
) {
    val decimalFormat = DecimalFormat("#.##")
    
    val unitLabel = when (category.unitType) {
        WeightUnit.KILOGRAM -> "kg"
        WeightUnit.JIN -> "斤"
        WeightUnit.PIECE -> "件"
    }
    
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = category.name,
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Bold
                    )
                    Spacer(modifier = Modifier.padding(horizontal = 4.dp))
                    Text(
                        text = "($unitLabel)",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                Text(
                    text = "收购: ¥${if (category.buyPrice.isNaN() || category.buyPrice.isInfinite()) "0" else decimalFormat.format(category.buyPrice)} | 卖: ¥${if (category.sellPrice.isNaN() || category.sellPrice.isInfinite()) "0" else decimalFormat.format(category.sellPrice)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                if (category.stock > 0) {
                    val displayStock = when (category.unitType) {
                        WeightUnit.KILOGRAM -> category.stock
                        WeightUnit.JIN -> category.stockInJin
                        WeightUnit.PIECE -> category.stockInPieces.toDouble()
                    }
                    Text(
                        text = "库存: ${decimalFormat.format(displayStock)}$unitLabel",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
            }
            
            Row {
                IconButton(onClick = { onEdit(category) }) {
                    Icon(
                        imageVector = Icons.Default.Edit,
                        contentDescription = "编辑",
                        tint = MaterialTheme.colorScheme.primary
                    )
                }
                IconButton(onClick = { onDelete(category) }) {
                    Icon(
                        imageVector = Icons.Default.Delete,
                        contentDescription = "删除",
                        tint = MaterialTheme.colorScheme.error
                    )
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CategoryEditDialog(
    category: Category?,
    onSave: (Category) -> Unit,
    onDismiss: () -> Unit
) {
    var name by remember { mutableStateOf(category?.name ?: "") }
    var buyPrice by remember { mutableStateOf(category?.buyPrice?.toString() ?: "") }
    var sellPrice by remember { mutableStateOf(category?.sellPrice?.toString() ?: "") }
    var stock by remember { mutableStateOf(category?.stock?.toString() ?: "0") }
    var unitType by remember { mutableStateOf(category?.unitType ?: WeightUnit.KILOGRAM) }
    var unitDropdownExpanded by remember { mutableStateOf(false) }
    
    var nameError by remember { mutableStateOf(false) }
    var priceError by remember { mutableStateOf(false) }
    
    val unitLabel = when (unitType) {
        WeightUnit.KILOGRAM -> "公斤 (kg)"
        WeightUnit.JIN -> "斤"
        WeightUnit.PIECE -> "件"
    }
    
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (category == null) "添加类别" else "编辑类别") },
        text = {
            Column(
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                OutlinedTextField(
                    value = name,
                    onValueChange = {
                        name = it
                        nameError = false
                    },
                    label = { Text("类别名称") },
                    isError = nameError,
                    supportingText = if (nameError) {{ Text("请输入类别名称") }} else null,
                    modifier = Modifier.fillMaxWidth()
                )
                
                Row(
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    OutlinedTextField(
                        value = buyPrice,
                        onValueChange = {
                            buyPrice = it.filter { char -> char.isDigit() || char == '.' }
                            priceError = false
                        },
                        label = { Text("收购价") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        isError = priceError,
                        modifier = Modifier.weight(1f)
                    )
                    
                    OutlinedTextField(
                        value = sellPrice,
                        onValueChange = {
                            sellPrice = it.filter { char -> char.isDigit() || char == '.' }
                            priceError = false
                        },
                        label = { Text("卖价") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        isError = priceError,
                        modifier = Modifier.weight(1f)
                    )
                }
                
                // 计量单位选择
                ExposedDropdownMenuBox(
                    expanded = unitDropdownExpanded,
                    onExpandedChange = { unitDropdownExpanded = it }
                ) {
                    OutlinedTextField(
                        value = unitLabel,
                        onValueChange = {},
                        readOnly = true,
                        label = { Text("计量单位") },
                        trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = unitDropdownExpanded) },
                        modifier = Modifier
                            .fillMaxWidth()
                            .menuAnchor()
                    )
                    ExposedDropdownMenu(
                        expanded = unitDropdownExpanded,
                        onDismissRequest = { unitDropdownExpanded = false }
                    ) {
                        WeightUnit.entries.forEach { unit ->
                            DropdownMenuItem(
                                text = {
                                    Text(
                                        when (unit) {
                                            WeightUnit.KILOGRAM -> "公斤 (kg)"
                                            WeightUnit.JIN -> "斤"
                                            WeightUnit.PIECE -> "件"
                                        }
                                    )
                                },
                                onClick = {
                                    unitType = unit
                                    unitDropdownExpanded = false
                                }
                            )
                        }
                    }
                }
                
                if (category == null) {
                    OutlinedTextField(
                        value = stock,
                        onValueChange = { stock = it.filter { char -> char.isDigit() || char == '.' } },
                        label = { Text("初始库存（$unitLabel）") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        modifier = Modifier.fillMaxWidth()
                    )
                } else {
                    val (displayStock, stockUnit) = when (category.unitType) {
                        WeightUnit.KILOGRAM -> category.stock to "kg"
                        WeightUnit.JIN -> category.stockInJin to "斤"
                        WeightUnit.PIECE -> category.stockInPieces.toDouble() to "件"
                    }
                    Text(
                        text = "当前库存: ${DecimalFormat("#.##").format(displayStock)}$stockUnit",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    nameError = name.isBlank()
                    priceError = buyPrice.isBlank() || sellPrice.isBlank() || 
                                 (buyPrice.toDoubleOrNull() ?: 0.0) <= 0 ||
                                 (sellPrice.toDoubleOrNull() ?: 0.0) <= 0
                    
                    if (!nameError && !priceError) {
                        if (category == null) {
                            // 添加新类别
                            val initialStock = stock.toDoubleOrNull() ?: 0.0
                            onSave(
                                Category(
                                    id = 0,
                                    name = name.trim(),
                                    buyPrice = buyPrice.toDoubleOrNull() ?: 0.0,
                                    sellPrice = sellPrice.toDoubleOrNull() ?: 0.0,
                                    stock = if (unitType == WeightUnit.KILOGRAM) initialStock else 0.0,
                                    stockInJin = if (unitType == WeightUnit.JIN) initialStock else 0.0,
                                    stockInPieces = if (unitType == WeightUnit.PIECE) initialStock.toInt() else 0,
                                    unitType = unitType
                                )
                            )
                        } else {
                            // 编辑现有类别，保留现有库存
                            onSave(
                                category.copy(
                                    name = name.trim(),
                                    buyPrice = buyPrice.toDoubleOrNull() ?: 0.0,
                                    sellPrice = sellPrice.toDoubleOrNull() ?: 0.0,
                                    unitType = unitType
                                )
                            )
                        }
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
