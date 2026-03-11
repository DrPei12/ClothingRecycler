package com.example.clothingrecycler.ui.screens.stock

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

data class StockUiState(
    val categories: List<StockCategory> = emptyList(),
    val totalCost: Double = 0.0,
    val isLoading: Boolean = false
)

@HiltViewModel
class StockViewModel @Inject constructor(
    private val inboundRepository: InboundRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(StockUiState(isLoading = true))
    val uiState: StateFlow<StockUiState> = _uiState.asStateFlow()

    init {
        loadStockData()
    }

    fun loadStockData() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true)

            try {
                val stockCategories = inboundRepository.getStockCategories()
                val totalCost = stockCategories.sumOf { category ->
                    when (category.unitType) {
                        WeightUnit.KILOGRAM -> category.stock * category.averageBuyPrice
                        WeightUnit.JIN -> category.stockInJin * category.averageBuyPrice
                        WeightUnit.PIECE -> category.stockInPieces * category.averageBuyPrice
                    }
                }

                _uiState.value = StockUiState(
                    categories = stockCategories,
                    totalCost = totalCost,
                    isLoading = false
                )
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(isLoading = false)
            }
        }
    }
}
