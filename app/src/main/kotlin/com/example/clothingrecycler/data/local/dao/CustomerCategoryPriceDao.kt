package com.example.clothingrecycler.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.example.clothingrecycler.data.local.entity.CustomerCategoryPriceEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface CustomerCategoryPriceDao {

    @Query("SELECT * FROM customer_category_prices WHERE customerId = :customerId AND categoryId = :categoryId")
    suspend fun getPrice(customerId: Long, categoryId: Long): CustomerCategoryPriceEntity?

    @Query("SELECT * FROM customer_category_prices WHERE customerId = :customerId")
    fun getPricesByCustomer(customerId: Long): Flow<List<CustomerCategoryPriceEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertOrUpdatePrice(price: CustomerCategoryPriceEntity)

    @Query("DELETE FROM customer_category_prices WHERE customerId = :customerId AND categoryId = :categoryId")
    suspend fun deletePrice(customerId: Long, categoryId: Long)
}
