package com.example.clothingrecycler.domain.usecase

import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.repository.CategoryRepository
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject

class GetAllCategoriesUseCase @Inject constructor(
    private val repository: CategoryRepository
) {
    operator fun invoke(): Flow<List<Category>> {
        return repository.getAllCategories()
    }
}

class AddCategoryUseCase @Inject constructor(
    private val repository: CategoryRepository
) {
    suspend operator fun invoke(category: Category): Long {
        return repository.insertCategory(category)
    }
}

class UpdateCategoryUseCase @Inject constructor(
    private val repository: CategoryRepository
) {
    suspend operator fun invoke(category: Category) {
        repository.updateCategory(category)
    }
}

class DeleteCategoryUseCase @Inject constructor(
    private val repository: CategoryRepository
) {
    suspend operator fun invoke(id: Long) {
        repository.deleteCategory(id)
    }
}

class GetTotalEstimatedValueUseCase @Inject constructor(
    private val repository: CategoryRepository
) {
    operator fun invoke(): Flow<Double> {
        return repository.getTotalEstimatedValue()
    }
}
