package com.example.clothingrecycler.ui.screens.order

import androidx.lifecycle.ViewModel
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import javax.inject.Inject

@HiltViewModel
class OrderListViewModel @Inject constructor() : ViewModel() {

    private val _filterState = MutableStateFlow(OrderFilterState())
    val filterState: StateFlow<OrderFilterState> = _filterState.asStateFlow()

    fun updateSearchKeyword(keyword: String) {
        _filterState.update { it.copy(searchKeyword = keyword) }
    }

    fun updateShowInbound(show: Boolean) {
        _filterState.update { it.copy(showInbound = show) }
    }

    fun updateShowOutbound(show: Boolean) {
        _filterState.update { it.copy(showOutbound = show) }
    }

    fun updateMinAmount(amount: String) {
        _filterState.update { it.copy(minAmount = amount) }
    }

    fun updateMaxAmount(amount: String) {
        _filterState.update { it.copy(maxAmount = amount) }
    }

    fun updateFilter(filter: OrderFilterState.() -> Unit) {
        _filterState.update { it.apply(filter) }
    }

    fun resetFilters() {
        _filterState.value = OrderFilterState()
    }
}
