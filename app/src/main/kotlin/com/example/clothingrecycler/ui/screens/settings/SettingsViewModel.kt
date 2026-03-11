package com.example.clothingrecycler.ui.screens.settings

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.preferencesDataStore
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.usecase.AddCategoryUseCase
import com.example.clothingrecycler.domain.usecase.DeleteCategoryUseCase
import com.example.clothingrecycler.domain.usecase.GetAllCategoriesUseCase
import com.example.clothingrecycler.domain.usecase.UpdateCategoryUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import javax.inject.Inject

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "settings")

data class SettingsUiState(
    val categories: List<Category> = emptyList(),
    val useKilogram: Boolean = true,
    val isLoading: Boolean = true,
    val error: String? = null
)

@HiltViewModel
class SettingsViewModel @Inject constructor(
    @ApplicationContext private val context: Context,
    private val getAllCategoriesUseCase: GetAllCategoriesUseCase,
    private val addCategoryUseCase: AddCategoryUseCase,
    private val updateCategoryUseCase: UpdateCategoryUseCase,
    private val deleteCategoryUseCase: DeleteCategoryUseCase
) : ViewModel() {
    
    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()
    
    companion object {
        private val USE_KILOGRAM = booleanPreferencesKey("use_kilogram")
    }
    
    init {
        loadSettings()
        loadCategories()
    }
    
    private fun loadSettings() {
        viewModelScope.launch {
            try {
                val useKilogram = context.dataStore.data.first()[USE_KILOGRAM] ?: true
                _uiState.value = _uiState.value.copy(useKilogram = useKilogram)
            } catch (e: Exception) {
                // ignore
            }
        }
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
    
    fun setUseKilogram(useKilogram: Boolean) {
        viewModelScope.launch {
            context.dataStore.edit { preferences ->
                preferences[USE_KILOGRAM] = useKilogram
            }
            _uiState.value = _uiState.value.copy(useKilogram = useKilogram)
        }
    }
    
    fun addCategory(category: Category) {
        viewModelScope.launch {
            addCategoryUseCase(category)
        }
    }
    
    fun updateCategory(category: Category) {
        viewModelScope.launch {
            updateCategoryUseCase(category)
        }
    }
    
    fun deleteCategory(id: Long) {
        viewModelScope.launch {
            deleteCategoryUseCase(id)
        }
    }
}
