package com.example.clothingrecycler.domain.model

/**
 * 重量单位枚举
 */
enum class WeightUnit {
    KILOGRAM,  // 公斤 (kg)
    JIN,       // 斤 (0.5 kg)
    PIECE      // 件 (个数)
}

data class Category(
    val id: Long = 0,
    val name: String,
    val buyPrice: Double,       // 收购价：元（每计量单位）
    val sellPrice: Double,     // 卖价：元（每公斤）
    val stock: Double = 0.0,    // 当前库存（公斤）
    val stockInJin: Double = 0.0,  // 当前库存（斤）
    val stockInPieces: Int = 0,    // 当前库存（件）
    val unitType: WeightUnit = WeightUnit.KILOGRAM  // 计量单位
)

// 库存类别详情（包含平均收购价）
data class StockCategory(
    val id: Long = 0,
    val name: String,
    val stock: Double,           // 库存公斤数
    val stockInJin: Double = 0.0,
    val stockInPieces: Int = 0,
    val sellPrice: Double,       // 卖价
    val averageBuyPrice: Double = 0.0,  // 平均收购价（加权平均）
    val unitType: WeightUnit = WeightUnit.KILOGRAM
) {
    /**
     * 获取显示的库存数量和单位
     */
    fun getDisplayStock(): Pair<Double, String> {
        return when (unitType) {
            WeightUnit.KILOGRAM -> stock to "kg"
            WeightUnit.JIN -> stockInJin to "斤"
            WeightUnit.PIECE -> stockInPieces.toDouble() to "件"
        }
    }
}

data class InboundRecord(
    val id: Long = 0,
    val categoryId: Long,
    val categoryName: String = "",
    val weight: Double,
    val unitPrice: Double,
    val totalCost: Double,
    val unitType: WeightUnit = WeightUnit.KILOGRAM,  // 入库时的单位
    val timestamp: Long = System.currentTimeMillis()
)

data class OutboundRecord(
    val id: Long = 0,
    val categoryId: Long,
    val categoryName: String = "",
    val weight: Double,
    val unitPrice: Double,
    val totalRevenue: Double,
    val unitType: WeightUnit = WeightUnit.KILOGRAM,  // 出库时的单位
    val timestamp: Long = System.currentTimeMillis()
)

data class Order(
    val id: Long = 0,
    val orderNumber: String,
    val type: OrderType,
    val totalWeight: Double,
    val totalAmount: Double,
    val categoryCount: Int,
    val itemCount: Int,
    val timestamp: Long = System.currentTimeMillis(),
    val note: String = "",
    val customerId: Long? = null
)

enum class OrderType {
    INBOUND,  // 入库
    OUTBOUND  // 出库
}

data class Customer(
    val id: Long = 0,
    val name: String,
    val phone: String = "",
    val email: String = "",
    val address: String = "",
    val note: String = "",
    val hasInboundOrders: Boolean = false,
    val hasOutboundOrders: Boolean = false,
    val createdAt: Long = System.currentTimeMillis(),
    val updatedAt: Long = System.currentTimeMillis()
)
