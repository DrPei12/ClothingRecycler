package com.example.clothingrecycler.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

/**
 * 客户-类别价格关联表
 * 用于存储每个客户对每个类别的最后交易价格（记忆功能）
 */
@Entity(
    tableName = "customer_category_prices",
    primaryKeys = ["customerId", "categoryId"]
)
data class CustomerCategoryPriceEntity(
    val customerId: Long,
    val categoryId: Long,
    val price: Double,           // 最后交易价格
    val updatedAt: Long = System.currentTimeMillis()
)
