package com.example.clothingrecycler.ui.screens.inbound

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.model.Customer
import com.example.clothingrecycler.domain.model.InboundRecord
import com.example.clothingrecycler.domain.model.OrderType
import com.example.clothingrecycler.domain.model.WeightUnit
import com.example.clothingrecycler.domain.usecase.AddInboundRecordsUseCase
import com.example.clothingrecycler.domain.usecase.CreateOrderUseCase
import com.example.clothingrecycler.domain.usecase.GetAllCategoriesUseCase
import com.example.clothingrecycler.domain.usecase.GetCustomerCategoryPriceUseCase
import com.example.clothingrecycler.domain.usecase.OrderItem
import com.example.clothingrecycler.domain.usecase.UpdateCustomerCategoryPriceUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * 入库记录的类别项目
 */
data class InboundCategoryItem(
    val category: Category,
    val weight: String = "",           // 录入的数量
    val unitPrice: String = "",
    val isExpanded: Boolean = false
) {
    val unitType: WeightUnit
        get() = category.unitType

    /**
     * 获取录入的数量（按类别的单位）
     */
    val quantity: Double
        get() = weight.toDoubleOrNull() ?: 0.0

    /**
     * 是否有效录入
     */
    val isValid: Boolean
        get() = quantity > 0

    /**
     * 计算总成本
     */
    val totalCost: Double
        get() = quantity * (unitPrice.toDoubleOrNull() ?: 0.0)

    /**
     * 获取显示的单位和数量
     */
    fun getDisplayQuantity(): Pair<Double, String> {
        return when (unitType) {
            WeightUnit.KILOGRAM -> quantity to "kg"
            WeightUnit.JIN -> quantity to "斤"
            WeightUnit.PIECE -> quantity.toInt().toDouble() to "件"
        }
    }
}

data class InboundUiState(
    val categories: List<Category> = emptyList(),
    val inboundItems: List<InboundCategoryItem> = emptyList(),
    val selectedCustomer: Customer? = null,
    val customers: List<Customer> = emptyList(),
    val customerPrices: Map<Long, Double> = emptyMap(),  // categoryId -> price
    val isLoading: Boolean = true,
    val isSaving: Boolean = false,
    val error: String? = null,
    val showCustomerSelector: Boolean = false
)

@HiltViewModel
class InboundViewModel @Inject constructor(
    private val getAllCategoriesUseCase: GetAllCategoriesUseCase,
    private val addInboundRecordsUseCase: AddInboundRecordsUseCase,
    private val createOrderUseCase: CreateOrderUseCase,
    private val getCustomerCategoryPriceUseCase: GetCustomerCategoryPriceUseCase,
    private val updateCustomerCategoryPriceUseCase: UpdateCustomerCategoryPriceUseCase
) : ViewModel() {

    private val _uiState = MutableStateFlow(InboundUiState())
    val uiState: StateFlow<InboundUiState> = _uiState.asStateFlow()

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
                        inboundItems = categories.map { InboundCategoryItem(category = it) },
                        isLoading = false
                    )
                    // 加载客户价格
                    _uiState.value.selectedCustomer?.let { customer ->
                        loadCustomerPrices(customer.id)
                    }
                }
        }
    }

    fun showCustomerSelector() {
        _uiState.value = _uiState.value.copy(showCustomerSelector = true)
    }

    fun hideCustomerSelector() {
        _uiState.value = _uiState.value.copy(showCustomerSelector = false)
    }

    fun selectCustomer(customer: Customer) {
        _uiState.value = _uiState.value.copy(
            selectedCustomer = customer,
            showCustomerSelector = false,
            customerPrices = emptyMap()
        )
        loadCustomerPrices(customer.id)
    }

    fun clearCustomerSelection() {
        _uiState.value = _uiState.value.copy(
            selectedCustomer = null,
            customerPrices = emptyMap()
        )
    }

    private fun loadCustomerPrices(customerId: Long) {
        viewModelScope.launch {
            val categoryPrices = mutableMapOf<Long, Double>()
            val items = _uiState.value.inboundItems.map { item ->
                val price = getCustomerCategoryPriceUseCase(customerId, item.category.id)
                if (price != null) {
                    categoryPrices[item.category.id] = price
                    if (item.unitPrice.isEmpty()) {
                        item.copy(unitPrice = price.toString())
                    } else {
                        item
                    }
                } else {
                    item
                }
            }

            _uiState.value = _uiState.value.copy(
                inboundItems = items,
                customerPrices = categoryPrices
            )
        }
    }

    fun updateWeight(categoryId: Long, weight: String) {
        updateItem(categoryId) { it.copy(weight = weight) }
    }

    fun updateUnitPrice(categoryId: Long, price: String) {
        updateItem(categoryId) { it.copy(unitPrice = price) }
    }

    fun toggleExpanded(categoryId: Long) {
        updateItem(categoryId) { it.copy(isExpanded = !it.isExpanded) }
    }

    private fun updateItem(categoryId: Long, transform: (InboundCategoryItem) -> InboundCategoryItem) {
        val items = _uiState.value.inboundItems.map { item ->
            if (item.category.id == categoryId) transform(item) else item
        }
        _uiState.value = _uiState.value.copy(inboundItems = items)
    }

    fun removeItem(categoryId: Long) {
        val items = _uiState.value.inboundItems.map { item ->
            if (item.category.id == categoryId) {
                item.copy(weight = "", unitPrice = "", isExpanded = false)
            } else {
                item
            }
        }
        _uiState.value = _uiState.value.copy(inboundItems = items)
    }

    fun getValidRecords(): List<InboundRecord> {
        return _uiState.value.inboundItems
            .filter { it.isValid }
            .map { item ->
                InboundRecord(
                    categoryId = item.category.id,
                    categoryName = item.category.name,
                    weight = item.quantity,
                    unitPrice = item.unitPrice.toDoubleOrNull() ?: item.category.buyPrice,
                    totalCost = item.totalCost,
                    unitType = item.unitType
                )
            }
    }

    fun getTotalCost(): Double {
        return getValidRecords().sumOf { it.totalCost }
    }

    fun saveInboundRecords(onComplete: () -> Unit) {
        viewModelScope.launch {
            val state = _uiState.value
            val records = getValidRecords()

            if (records.isEmpty()) {
                _uiState.value = _uiState.value.copy(error = "请至少输入一条有效的入库记录")
                return@launch
            }

            _uiState.value = _uiState.value.copy(isSaving = true, error = null)

            try {
                val timestamp = System.currentTimeMillis()

                addInboundRecordsUseCase(records.map { it.copy(timestamp = timestamp) })

                // 更新客户-类别价格记忆
                state.selectedCustomer?.let { customer ->
                    records.forEach { record ->
                        updateCustomerCategoryPriceUseCase(
                            customerId = customer.id,
                            categoryId = record.categoryId,
                            price = record.unitPrice
                        )
                    }
                }

                // 创建订单
                val orderItems = records.map { record ->
                    OrderItem(
                        categoryId = record.categoryId,
                        categoryName = record.categoryName,
                        weight = record.weight,
                        unitPrice = record.unitPrice
                    )
                }
                createOrderUseCase(
                    type = OrderType.INBOUND,
                    items = orderItems,
                    customerId = state.selectedCustomer?.id,
                    timestamp = timestamp
                )

                resetForm()
                onComplete()
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    isSaving = false,
                    error = e.message ?: "保存失败"
                )
            }
        }
    }

    private fun resetForm() {
        val items = _uiState.value.categories.map { InboundCategoryItem(category = it) }
        _uiState.value = InboundUiState(
            categories = _uiState.value.categories,
            inboundItems = items
        )
    }
}
