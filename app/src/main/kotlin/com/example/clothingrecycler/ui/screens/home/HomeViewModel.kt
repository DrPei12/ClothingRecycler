package com.example.clothingrecycler.ui.screens.home

import androidx.lifecycle.ViewModel
import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.usecase.GetAllCategoriesUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import javax.inject.Inject

data class HomeUiState(
    val categories: List<Category> = emptyList(),
    val totalEstimatedValue: Double = 0.0,
    val isLoading: Boolean = false
)

@HiltViewModel
class HomeViewModel @Inject constructor(
    private val getAllCategoriesUseCase: GetAllCategoriesUseCase
) : ViewModel() {
    
    private val _uiState = MutableStateFlow(HomeUiState())
    val uiState: StateFlow<HomeUiState> = _uiState
    
    fun setCategories(categories: List<Category>) {
        val totalValue = categories.sumOf { it.stock * it.sellPrice }
        _uiState.value = HomeUiState(
            categories = categories,
            totalEstimatedValue = totalValue,
            isLoading = false
        )
    }
}
