package com.example.clothingrecycler.ui.screens.outbound

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.model.OrderType
import com.example.clothingrecycler.domain.model.OutboundRecord
import com.example.clothingrecycler.domain.usecase.AddOutboundRecordsUseCase
import com.example.clothingrecycler.domain.usecase.CreateOrderUseCase
import com.example.clothingrecycler.domain.usecase.GetAllCategoriesUseCase
import com.example.clothingrecycler.domain.usecase.OrderItem
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.launch
import javax.inject.Inject

data class OutboundUiState(
    val categories: List<Category> = emptyList(),
    val isLoading: Boolean = true,
    val error: String? = null
)

@HiltViewModel
class OutboundViewModel @Inject constructor(
    private val getAllCategoriesUseCase: GetAllCategoriesUseCase,
    private val addOutboundRecordsUseCase: AddOutboundRecordsUseCase,
    private val createOrderUseCase: CreateOrderUseCase
) : ViewModel() {
    
    private val _uiState = MutableStateFlow(OutboundUiState())
    val uiState: StateFlow<OutboundUiState> = _uiState.asStateFlow()
    
    init {
        loadCategories()
    }
    
    private fun loadCategories() {
        viewModelScope.launch {
            getAllCategoriesUseCase()
                .catch { e ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        error = e.message
                    )
                }
                .collect { categories ->
                    _uiState.value = _uiState.value.copy(
                        categories = categories,
                        isLoading = false
                    )
                }
        }
    }
    
    fun saveOutboundRecords(records: List<OutboundRecord>, customerId: Long? = null) {
        viewModelScope.launch {
            // 使用相同的 timestamp
            val timestamp = System.currentTimeMillis()
            val recordsWithTimestamp = records.map { it.copy(timestamp = timestamp) }

            // 先保存出库记录
            addOutboundRecordsUseCase(recordsWithTimestamp)

            // 创建订单
            val orderItems = records.map { record ->
                OrderItem(
                    categoryId = record.categoryId,
                    categoryName = record.categoryName,
                    weight = record.weight,
                    unitPrice = record.unitPrice
                )
            }
            createOrderUseCase(OrderType.OUTBOUND, orderItems, customerId = customerId, timestamp = timestamp)
        }
    }
}
