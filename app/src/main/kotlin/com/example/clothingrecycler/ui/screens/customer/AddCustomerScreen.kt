package com.example.clothingrecycler.ui.screens.customer

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Note
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AddCustomerScreen(
    onNavigateBack: () -> Unit,
    onCustomerAdded: (Long) -> Unit,
    viewModel: AddCustomerViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(uiState.isSuccess) {
        if (uiState.isSuccess) {
            // TODO: 返回时传递新客户ID
            onNavigateBack()
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "新增客户",
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
                .verticalScroll(rememberScrollState())
        ) {
            // 名称（必填）
            OutlinedTextField(
                value = uiState.name,
                onValueChange = { viewModel.updateName(it) },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("客户名称 *") },
                placeholder = { Text("请输入客户名称") },
                isError = uiState.error != null && uiState.name.isBlank(),
                singleLine = true
            )

            Spacer(modifier = Modifier.height(16.dp))

            // 电话（选填）
            OutlinedTextField(
                value = uiState.phone,
                onValueChange = { viewModel.updatePhone(it) },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("联系电话") },
                placeholder = { Text("请输入联系电话") },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Default.Phone,
                        contentDescription = null
                    )
                },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone),
                singleLine = true
            )

            Spacer(modifier = Modifier.height(16.dp))

            // 邮箱（选填）
            OutlinedTextField(
                value = uiState.email,
                onValueChange = { viewModel.updateEmail(it) },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("电子邮箱") },
                placeholder = { Text("请输入电子邮箱") },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Default.Email,
                        contentDescription = null
                    )
                },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email),
                singleLine = true
            )

            Spacer(modifier = Modifier.height(16.dp))

            // 地址（选填）
            OutlinedTextField(
                value = uiState.address,
                onValueChange = { viewModel.updateAddress(it) },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("地址") },
                placeholder = { Text("请输入客户地址") },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Default.Home,
                        contentDescription = null
                    )
                },
                singleLine = true
            )

            Spacer(modifier = Modifier.height(16.dp))

            // 备注（选填）
            OutlinedTextField(
                value = uiState.note,
                onValueChange = { viewModel.updateNote(it) },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("备注") },
                placeholder = { Text("请输入备注信息") },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Default.Note,
                        contentDescription = null
                    )
                },
                minLines = 3,
                maxLines = 5
            )

            Spacer(modifier = Modifier.height(24.dp))

            // 错误提示
            if (uiState.error != null) {
                Text(
                    text = uiState.error!!,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodySmall
                )
                Spacer(modifier = Modifier.height(8.dp))
            }

            // 保存按钮
            Button(
                onClick = { viewModel.addCustomer() },
                modifier = Modifier.fillMaxWidth(),
                enabled = !uiState.isLoading
            ) {
                Text(
                    text = if (uiState.isLoading) "保存中..." else "保存客户"
                )
            }
        }
    }
}
