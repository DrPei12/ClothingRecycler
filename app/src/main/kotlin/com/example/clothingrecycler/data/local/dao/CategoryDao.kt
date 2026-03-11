package com.example.clothingrecycler.data.local.dao

import androidx.room.Dao
import androidx.room.Delete
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Update
import com.example.clothingrecycler.data.local.entity.CategoryEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface CategoryDao {
    @Query("SELECT * FROM categories ORDER BY name ASC")
    fun getAllCategories(): Flow<List<CategoryEntity>>

    @Query("SELECT * FROM categories WHERE id = :id")
    suspend fun getCategoryById(id: Long): CategoryEntity?

    @Query("SELECT * FROM categories WHERE name = :name")
    suspend fun getCategoryByName(name: String): CategoryEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertCategory(category: CategoryEntity): Long

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertCategories(categories: List<CategoryEntity>)

    @Update
    suspend fun updateCategory(category: CategoryEntity)

    @Delete
    suspend fun deleteCategory(category: CategoryEntity)

    @Query("DELETE FROM categories WHERE id = :id")
    suspend fun deleteCategoryById(id: Long)

    @Query("UPDATE categories SET stock = stock + :weight, updatedAt = :timestamp WHERE id = :categoryId")
    suspend fun increaseStock(categoryId: Long, weight: Double, timestamp: Long = System.currentTimeMillis())

    @Query("""
        UPDATE categories SET
            stock = stock + :kgWeight,
            stockInJin = stockInJin + :jinWeight,
            stockInPieces = stockInPieces + :pieces,
            updatedAt = :timestamp
        WHERE id = :categoryId
    """)
    suspend fun increaseStockWithUnit(
        categoryId: Long,
        kgWeight: Double,
        jinWeight: Double,
        pieces: Int,
        timestamp: Long = System.currentTimeMillis()
    )

    @Query("""
        UPDATE categories SET
            stock = stock - :kgWeight,
            stockInJin = stockInJin - :jinWeight,
            stockInPieces = stockInPieces - :pieces,
            updatedAt = :timestamp
        WHERE id = :categoryId
    """)
    suspend fun decreaseStockWithUnit(
        categoryId: Long,
        kgWeight: Double,
        jinWeight: Double,
        pieces: Int,
        timestamp: Long = System.currentTimeMillis()
    )

    @Query("UPDATE categories SET stock = stock - :weight, updatedAt = :timestamp WHERE id = :categoryId")
    suspend fun decreaseStock(categoryId: Long, weight: Double, timestamp: Long = System.currentTimeMillis())

    @Query("SELECT SUM(stock * sellPrice) FROM categories")
    fun getTotalEstimatedValue(): Flow<Double?>
}
