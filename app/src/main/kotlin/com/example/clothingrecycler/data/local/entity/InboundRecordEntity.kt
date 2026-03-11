package com.example.clothingrecycler.data.local.entity

import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "inbound_records",
    foreignKeys = [
        ForeignKey(
            entity = CategoryEntity::class,
            parentColumns = ["id"],
            childColumns = ["categoryId"],
            onDelete = ForeignKey.CASCADE
        )
    ],
    indices = [Index("categoryId")]
)
data class InboundRecordEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val categoryId: Long,
    val weight: Double,         // 数量（按单位类型）
    val unitPrice: Double,      // 当时的收购价（每公斤）
    val totalCost: Double,      // 总支出
    val unitType: String = "KILOGRAM",  // 入库时的单位: KILOGRAM, JIN, PIECE
    val timestamp: Long = System.currentTimeMillis()
)
