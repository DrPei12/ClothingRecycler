package com.example.clothingrecycler.domain.usecase

import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.model.Order
import com.example.clothingrecycler.domain.model.OrderType
import com.example.clothingrecycler.domain.repository.OrderRepository
import com.example.clothingrecycler.domain.repository.OrderStatistics
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject

class GetAllOrdersUseCase @Inject constructor(
    private val repository: OrderRepository
) {
    operator fun invoke(): Flow<List<Order>> {
        return repository.getAllOrders()
    }
}

class GetOrderByIdUseCase @Inject constructor(
    private val repository: OrderRepository
) {
    suspend operator fun invoke(id: Long): Order? {
        return repository.getOrderById(id)
    }
}

class CreateOrderUseCase @Inject constructor(
    private val repository: OrderRepository
) {
    suspend operator fun invoke(
        type: OrderType,
        items: List<OrderItem>,
        note: String = "",
        customerId: Long? = null,
        timestamp: Long = System.currentTimeMillis()
    ): Long {
        val totalWeight = items.sumOf { it.weight }
        val totalAmount = items.sumOf { it.weight * it.unitPrice }
        val categoryCount = items.map { it.categoryId }.distinct().size

        // 生成订单号: RK/CK + 时间戳 + 序号
        val prefix = if (type == OrderType.INBOUND) "RK" else "CK"
        val count = repository.getOrderCount() + 1
        val orderNumber = "$prefix${timestamp}$count"

        val order = Order(
            orderNumber = orderNumber,
            type = type,
            totalWeight = totalWeight,
            totalAmount = totalAmount,
            categoryCount = categoryCount,
            itemCount = items.size,
            timestamp = timestamp,
            note = note,
            customerId = customerId
        )

        return repository.insertOrder(order)
    }
}

data class OrderItem(
    val categoryId: Long,
    val categoryName: String,
    val weight: Double,
    val unitPrice: Double
)

// ========== 统计用例 ==========

class GetStatisticsUseCase @Inject constructor(
    private val repository: OrderRepository
) {
    operator fun invoke(startTime: Long, endTime: Long): Flow<OrderStatistics> {
        return repository.getStatistics(startTime, endTime)
    }
}

// ========== 筛选用例 ==========

class GetFilteredOrdersUseCase @Inject constructor(
    private val repository: OrderRepository
) {
    operator fun invoke(
        startTime: Long,
        endTime: Long,
        type: OrderType? = null,
        customerId: Long? = null,
        minAmount: Double? = null,
        maxAmount: Double? = null
    ): Flow<List<Order>> {
        return repository.getFilteredOrders(startTime, endTime, type, customerId, minAmount, maxAmount)
    }
}

class SearchOrdersUseCase @Inject constructor(
    private val repository: OrderRepository
) {
    operator fun invoke(keyword: String): Flow<List<Order>> {
        return repository.searchOrders(keyword)
    }
}
