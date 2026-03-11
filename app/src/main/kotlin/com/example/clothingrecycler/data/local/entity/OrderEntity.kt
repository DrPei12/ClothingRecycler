package com.example.clothingrecycler.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "orders")
data class OrderEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val orderNumber: String,          // 订单号，如 "RK20260211001" (入库) 或 "CK20260211001" (出库)
    val type: String,                 // "INBOUND" 或 "OUTBOUND"
    val totalWeight: Double,          // 总重量（公斤）
    val totalAmount: Double,          // 总金额
    val categoryCount: Int,           // 涉及类别数量
    val itemCount: Int,               // 记录条目数量
    val timestamp: Long = System.currentTimeMillis(),
    val note: String = "",             // 备注
    val customerId: Long? = null       // 关联客户ID
)
