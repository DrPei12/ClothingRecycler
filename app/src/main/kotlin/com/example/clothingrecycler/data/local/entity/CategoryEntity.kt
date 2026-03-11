package com.example.clothingrecycler.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "categories")
data class CategoryEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val name: String,
    val buyPrice: Double,       // 收购价：元（每计量单位）
    val sellPrice: Double,      // 卖价：元/公斤
    val stock: Double = 0.0,    // 当前库存：公斤
    val stockInJin: Double = 0.0,    // 当前库存：斤
    val stockInPieces: Int = 0,      // 当前库存：件
    val unitType: String = "KILOGRAM", // 计量单位: KILOGRAM, JIN, PIECE
    val createdAt: Long = System.currentTimeMillis(),
    val updatedAt: Long = System.currentTimeMillis()
)
