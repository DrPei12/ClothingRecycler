package com.example.clothingrecycler.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "customers")
data class CustomerEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val name: String,
    val phone: String = "",
    val email: String = "",
    val address: String = "",
    val note: String = "",
    val hasInboundOrders: Boolean = false,  // 是否有入库订单
    val hasOutboundOrders: Boolean = false, // 是否有出库订单
    val createdAt: Long = System.currentTimeMillis(),
    val updatedAt: Long = System.currentTimeMillis()
)
