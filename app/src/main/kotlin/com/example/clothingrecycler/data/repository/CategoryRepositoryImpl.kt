package com.example.clothingrecycler.data.repository

import com.example.clothingrecycler.data.local.dao.CategoryDao
import com.example.clothingrecycler.data.local.entity.CategoryEntity
import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.model.WeightUnit
import com.example.clothingrecycler.domain.repository.CategoryRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class CategoryRepositoryImpl @Inject constructor(
    private val categoryDao: CategoryDao
) : CategoryRepository {

    override fun getAllCategories(): Flow<List<Category>> {
        return categoryDao.getAllCategories().map { entities ->
            entities.map { it.toDomain() }
        }
    }

    override suspend fun getCategoryById(id: Long): Category? {
        return categoryDao.getCategoryById(id)?.toDomain()
    }

    override suspend fun insertCategory(category: Category): Long {
        return categoryDao.insertCategory(category.toEntity())
    }

    override suspend fun updateCategory(category: Category) {
        categoryDao.updateCategory(category.toEntity())
    }

    override suspend fun deleteCategory(id: Long) {
        categoryDao.deleteCategoryById(id)
    }

    override suspend fun increaseStock(categoryId: Long, weight: Double) {
        categoryDao.increaseStock(categoryId, weight)
    }

    override suspend fun decreaseStock(categoryId: Long, weight: Double) {
        categoryDao.decreaseStock(categoryId, weight)
    }

    override fun getTotalEstimatedValue(): Flow<Double> {
        return categoryDao.getTotalEstimatedValue().map { it ?: 0.0 }
    }

    private fun CategoryEntity.toDomain(): Category {
        return Category(
            id = id,
            name = name,
            buyPrice = buyPrice,
            sellPrice = sellPrice,
            stock = stock,
            stockInJin = stockInJin,
            stockInPieces = stockInPieces,
            unitType = try { WeightUnit.valueOf(unitType) } catch (e: Exception) { WeightUnit.KILOGRAM }
        )
    }

    private fun Category.toEntity(): CategoryEntity {
        return CategoryEntity(
            id = id,
            name = name,
            buyPrice = buyPrice,
            sellPrice = sellPrice,
            stock = stock,
            stockInJin = stockInJin,
            stockInPieces = stockInPieces,
            unitType = unitType.name,
            updatedAt = System.currentTimeMillis()
        )
    }
}
