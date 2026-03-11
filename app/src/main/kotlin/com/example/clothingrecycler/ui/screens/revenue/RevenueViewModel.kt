package com.example.clothingrecycler.ui.screens.revenue

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.clothingrecycler.domain.model.StockCategory
import com.example.clothingrecycler.domain.model.WeightUnit
import com.example.clothingrecycler.domain.repository.InboundRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

data class RevenueUiState(
    val categories: List<StockCategory> = emptyList(),
    val totalRevenue: Double = 0.0,
    val isLoading: Boolean = false
)

@HiltViewModel
class RevenueViewModel @Inject constructor(
    private val inboundRepository: InboundRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(RevenueUiState(isLoading = true))
    val uiState: StateFlow<RevenueUiState> = _uiState.asStateFlow()

    init {
        loadRevenueData()
    }

    fun loadRevenueData() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true)

            try {
                val stockCategories = inboundRepository.getStockCategories()
                val totalRevenue = stockCategories.sumOf { category ->
                    when (category.unitType) {
                        WeightUnit.KILOGRAM -> category.stock * category.sellPrice
                        WeightUnit.JIN -> category.stockInJin * category.sellPrice
                        WeightUnit.PIECE -> category.stockInPieces * category.sellPrice
                    }
                }

                _uiState.value = RevenueUiState(
                    categories = stockCategories,
                    totalRevenue = totalRevenue,
                    isLoading = false
                )
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(isLoading = false)
            }
        }
    }
}
