package com.example.clothingrecycler.ui.screens.customer

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.clothingrecycler.domain.model.Customer
import com.example.clothingrecycler.domain.model.Order
import com.example.clothingrecycler.domain.usecase.AddCustomerUseCase
import com.example.clothingrecycler.domain.usecase.DeleteCustomerUseCase
import com.example.clothingrecycler.domain.usecase.GetAllCustomersUseCase
import com.example.clothingrecycler.domain.usecase.GetCustomerByIdUseCase
import com.example.clothingrecycler.domain.usecase.GetCustomerOrdersUseCase
import com.example.clothingrecycler.domain.usecase.GetInboundCustomersUseCase
import com.example.clothingrecycler.domain.usecase.GetOutboundCustomersUseCase
import com.example.clothingrecycler.domain.usecase.SearchCustomersUseCase
import com.example.clothingrecycler.domain.usecase.UpdateCustomerUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch
import javax.inject.Inject

enum class CustomerTab {
    ALL, INBOUND, OUTBOUND
}

data class CustomerListUiState(
    val customers: List<Customer> = emptyList(),
    val isLoading: Boolean = true,
    val error: String? = null,
    val searchQuery: String = "",
    val selectedTab: CustomerTab = CustomerTab.ALL
)

data class CustomerDetailUiState(
    val customer: Customer? = null,
    val orders: List<Order> = emptyList(),
    val isLoading: Boolean = true,
    val error: String? = null
)

data class AddCustomerUiState(
    val name: String = "",
    val phone: String = "",
    val email: String = "",
    val address: String = "",
    val note: String = "",
    val isLoading: Boolean = false,
    val error: String? = null,
    val isSuccess: Boolean = false
)

@HiltViewModel
class CustomerListViewModel @Inject constructor(
    private val getAllCustomersUseCase: GetAllCustomersUseCase,
    private val getInboundCustomersUseCase: GetInboundCustomersUseCase,
    private val getOutboundCustomersUseCase: GetOutboundCustomersUseCase,
    private val searchCustomersUseCase: SearchCustomersUseCase
) : ViewModel() {

    private val _uiState = MutableStateFlow(CustomerListUiState())
    val uiState: StateFlow<CustomerListUiState> = _uiState.asStateFlow()

    init {
        loadCustomers()
    }

    fun loadCustomers() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)

            try {
                val flow = when (_uiState.value.selectedTab) {
                    CustomerTab.ALL -> getAllCustomersUseCase()
                    CustomerTab.INBOUND -> getInboundCustomersUseCase()
                    CustomerTab.OUTBOUND -> getOutboundCustomersUseCase()
                }

                flow.collectLatest { customers ->
                    _uiState.value = _uiState.value.copy(
                        customers = customers,
                        isLoading = false
                    )
                }
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = e.message ?: "加载失败"
                )
            }
        }
    }

    fun selectTab(tab: CustomerTab) {
        _uiState.value = _uiState.value.copy(selectedTab = tab)
        loadCustomers()
    }

    fun updateSearchQuery(query: String) {
        _uiState.value = _uiState.value.copy(searchQuery = query)
        if (query.isBlank()) {
            loadCustomers()
        } else {
            searchCustomers(query)
        }
    }

    private fun searchCustomers(query: String) {
        viewModelScope.launch {
            try {
                searchCustomersUseCase(query).collectLatest { customers ->
                    _uiState.value = _uiState.value.copy(
                        customers = customers,
                        isLoading = false
                    )
                }
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = e.message
                )
            }
        }
    }
}

@HiltViewModel
class CustomerDetailViewModel @Inject constructor(
    private val getCustomerByIdUseCase: GetCustomerByIdUseCase,
    private val getCustomerOrdersUseCase: GetCustomerOrdersUseCase
) : ViewModel() {

    private val _uiState = MutableStateFlow(CustomerDetailUiState())
    val uiState: StateFlow<CustomerDetailUiState> = _uiState.asStateFlow()

    fun loadCustomer(customerId: Long) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)

            try {
                val customer = getCustomerByIdUseCase(customerId)
                _uiState.value = _uiState.value.copy(
                    customer = customer,
                    isLoading = false
                )

                // 加载该客户的订单
                getCustomerOrdersUseCase(customerId).collectLatest { orders ->
                    _uiState.value = _uiState.value.copy(
                        orders = orders,
                        isLoading = false
                    )
                }
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = e.message ?: "加载失败"
                )
            }
        }
    }
}

@HiltViewModel
class AddCustomerViewModel @Inject constructor(
    private val addCustomerUseCase: AddCustomerUseCase
) : ViewModel() {

    private val _uiState = MutableStateFlow(AddCustomerUiState())
    val uiState: StateFlow<AddCustomerUiState> = _uiState.asStateFlow()

    fun updateName(name: String) {
        _uiState.value = _uiState.value.copy(name = name, error = null)
    }

    fun updatePhone(phone: String) {
        _uiState.value = _uiState.value.copy(phone = phone)
    }

    fun updateEmail(email: String) {
        _uiState.value = _uiState.value.copy(email = email)
    }

    fun updateAddress(address: String) {
        _uiState.value = _uiState.value.copy(address = address)
    }

    fun updateNote(note: String) {
        _uiState.value = _uiState.value.copy(note = note)
    }

    fun addCustomer() {
        val name = _uiState.value.name.trim()
        if (name.isBlank()) {
            _uiState.value = _uiState.value.copy(error = "客户名称不能为空")
            return
        }

        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)

            try {
                addCustomerUseCase(
                    name = name,
                    phone = _uiState.value.phone,
                    email = _uiState.value.email,
                    address = _uiState.value.address,
                    note = _uiState.value.note
                )
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    isSuccess = true
                )
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = e.message ?: "添加失败"
                )
            }
        }
    }
}
