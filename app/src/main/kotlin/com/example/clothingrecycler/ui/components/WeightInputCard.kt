package com.example.clothingrecycler.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import java.text.DecimalFormat

@Composable
fun WeightInputCard(
    label: String,
    weight: Double,
    onWeightChange: (Double) -> Unit,
    modifier: Modifier = Modifier,
    step: Double = 0.1,
    maxDecimals: Int = 1,
    unit: String = "公斤"
) {
    val decimalFormat = DecimalFormat("#.${"0".repeat(maxDecimals)}")
    var showInputDialog by remember { mutableStateOf(false) }
    var inputText by remember { mutableStateOf(decimalFormat.format(weight)) }
    
    Card(
        modifier = modifier.fillMaxWidth(),
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
            Text(
                text = label,
                style = MaterialTheme.typography.titleMedium,
                modifier = Modifier.weight(1f)
            )
            
            IconButton(
                onClick = {
                    val newWeight = (weight - step).coerceAtLeast(0.0)
                    onWeightChange(newWeight)
                }
            ) {
                Icon(
                    imageVector = Icons.Default.Remove,
                    contentDescription = "减少",
                    tint = MaterialTheme.colorScheme.primary
                )
            }
            
            Text(
                text = "${decimalFormat.format(weight)}$unit",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
                modifier = Modifier
                    .width(100.dp)
                    .clickable {
                        inputText = decimalFormat.format(weight)
                        showInputDialog = true
                    }
            )
            
            IconButton(
                onClick = {
                    val newWeight = weight + step
                    onWeightChange(newWeight)
                }
            ) {
                Icon(
                    imageVector = Icons.Default.Add,
                    contentDescription = "增加",
                    tint = MaterialTheme.colorScheme.primary
                )
            }
        }
    }
    
    // 输入对话框
    if (showInputDialog) {
        AlertDialog(
            onDismissRequest = { showInputDialog = false },
            title = { Text("输入${label}") },
            text = {
                OutlinedTextField(
                    value = inputText,
                    onValueChange = { inputText = it },
                    label = { Text("请输入数量 (${unit})") },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    singleLine = true
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        val newWeight = inputText.toDoubleOrNull() ?: 0.0
                        onWeightChange(newWeight.coerceAtLeast(0.0))
                        showInputDialog = false
                    }
                ) {
                    Text("确定")
                }
            },
            dismissButton = {
                TextButton(onClick = { showInputDialog = false }) {
                    Text("取消")
                }
            }
        )
    }
}
