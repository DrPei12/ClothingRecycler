package com.example.clothingrecycler.data.repository

import com.example.clothingrecycler.data.local.dao.OrderDao
import com.example.clothingrecycler.data.local.entity.OrderEntity
import com.example.clothingrecycler.domain.model.Order
import com.example.clothingrecycler.domain.model.OrderType
import com.example.clothingrecycler.domain.repository.OrderRepository
import com.example.clothingrecycler.domain.repository.OrderStatistics
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class OrderRepositoryImpl @Inject constructor(
    private val orderDao: OrderDao
) : OrderRepository {

    override fun getAllOrders(): Flow<List<Order>> {
        return orderDao.getAllOrders().map { entities ->
            entities.map { it.toDomain() }
        }
    }

    override suspend fun getOrderById(id: Long): Order? {
        return orderDao.getOrderById(id)?.toDomain()
    }

    override suspend fun insertOrder(order: Order): Long {
        return orderDao.insertOrder(order.toEntity())
    }

    override suspend fun deleteOrder(id: Long) {
        orderDao.deleteOrder(id)
    }

    override suspend fun getOrderCount(): Int {
        return orderDao.getOrderCount()
    }

    override fun getOrdersByCustomerId(customerId: Long): Flow<List<Order>> {
        return orderDao.getOrdersByCustomerId(customerId).map { entities ->
            entities.map { it.toDomain() }
        }
    }

    override suspend fun updateOrderCustomerId(orderId: Long, customerId: Long?) {
        orderDao.updateCustomerId(orderId, customerId)
    }

    // ========== 统计与筛选实现 ==========

    override fun getStatistics(startTime: Long, endTime: Long): Flow<OrderStatistics> {
        return orderDao.getStatisticsFlow(startTime, endTime).map { stats ->
            OrderStatistics(
                inboundWeight = stats.inboundWeight,
                inboundAmount = stats.inboundAmount,
                outboundWeight = stats.outboundWeight,
                outboundAmount = stats.outboundAmount,
                orderCount = stats.orderCount
            )
        }
    }

    override fun getFilteredOrders(
        startTime: Long,
        endTime: Long,
        type: OrderType?,
        customerId: Long?,
        minAmount: Double?,
        maxAmount: Double?
    ): Flow<List<Order>> {
        return orderDao.getFilteredOrders(
            startTime = startTime,
            endTime = endTime,
            type = type?.name,
            customerId = customerId,
            minAmount = minAmount,
            maxAmount = maxAmount
        ).map { entities ->
            entities.map { it.toDomain() }
        }
    }

    override fun searchOrders(keyword: String): Flow<List<Order>> {
        return orderDao.searchOrders(keyword).map { entities ->
            entities.map { it.toDomain() }
        }
    }

    private fun OrderEntity.toDomain(): Order {
        return Order(
            id = id,
            orderNumber = orderNumber,
            type = if (type == "INBOUND") OrderType.INBOUND else OrderType.OUTBOUND,
            totalWeight = totalWeight,
            totalAmount = totalAmount,
            categoryCount = categoryCount,
            itemCount = itemCount,
            timestamp = timestamp,
            note = note,
            customerId = customerId
        )
    }

    private fun Order.toEntity(): OrderEntity {
        return OrderEntity(
            id = id,
            orderNumber = orderNumber,
            type = type.name,
            totalWeight = totalWeight,
            totalAmount = totalAmount,
            categoryCount = categoryCount,
            itemCount = itemCount,
            timestamp = timestamp,
            note = note,
            customerId = customerId
        )
    }
}
