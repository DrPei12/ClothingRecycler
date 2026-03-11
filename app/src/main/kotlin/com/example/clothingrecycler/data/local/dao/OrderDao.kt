package com.example.clothingrecycler.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.example.clothingrecycler.data.local.entity.OrderEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface OrderDao {
    @Query("SELECT * FROM orders ORDER BY timestamp DESC")
    fun getAllOrders(): Flow<List<OrderEntity>>

    @Query("SELECT * FROM orders WHERE id = :id")
    suspend fun getOrderById(id: Long): OrderEntity?

    @Query("SELECT * FROM orders WHERE type = :type ORDER BY timestamp DESC")
    fun getOrdersByType(type: String): Flow<List<OrderEntity>>

    @Query("SELECT * FROM orders WHERE customerId = :customerId ORDER BY timestamp DESC")
    fun getOrdersByCustomerId(customerId: Long): Flow<List<OrderEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertOrder(order: OrderEntity): Long

    @Query("DELETE FROM orders WHERE id = :id")
    suspend fun deleteOrder(id: Long)

    @Query("UPDATE orders SET customerId = :customerId WHERE id = :orderId")
    suspend fun updateCustomerId(orderId: Long, customerId: Long?)

    @Query("SELECT COUNT(*) FROM orders")
    suspend fun getOrderCount(): Int

    @Query("SELECT COUNT(*) FROM orders WHERE customerId = :customerId AND type = :type")
    suspend fun getOrderCountByCustomerAndType(customerId: Long, type: String): Int

    // ========== 统计查询 ==========

    @Query("""
        SELECT
            COALESCE(SUM(CASE WHEN type = 'INBOUND' THEN totalWeight ELSE 0 END), 0) as inboundWeight,
            COALESCE(SUM(CASE WHEN type = 'INBOUND' THEN totalAmount ELSE 0 END), 0) as inboundAmount,
            COALESCE(SUM(CASE WHEN type = 'OUTBOUND' THEN totalWeight ELSE 0 END), 0) as outboundWeight,
            COALESCE(SUM(CASE WHEN type = 'OUTBOUND' THEN totalAmount ELSE 0 END), 0) as outboundAmount,
            COUNT(*) as orderCount
        FROM orders
        WHERE timestamp BETWEEN :startTime AND :endTime
    """)
    suspend fun getStatistics(startTime: Long, endTime: Long): OrderStatistics

    @Query("""
        SELECT
            COALESCE(SUM(CASE WHEN type = 'INBOUND' THEN totalWeight ELSE 0 END), 0) as inboundWeight,
            COALESCE(SUM(CASE WHEN type = 'INBOUND' THEN totalAmount ELSE 0 END), 0) as inboundAmount,
            COALESCE(SUM(CASE WHEN type = 'OUTBOUND' THEN totalWeight ELSE 0 END), 0) as outboundWeight,
            COALESCE(SUM(CASE WHEN type = 'OUTBOUND' THEN totalAmount ELSE 0 END), 0) as outboundAmount,
            COUNT(*) as orderCount
        FROM orders
        WHERE timestamp BETWEEN :startTime AND :endTime
    """)
    fun getStatisticsFlow(startTime: Long, endTime: Long): Flow<OrderStatistics>

    // ========== 筛选查询 ==========

    @Query("""
        SELECT * FROM orders
        WHERE timestamp BETWEEN :startTime AND :endTime
        AND (:type IS NULL OR type = :type)
        AND (:customerId IS NULL OR customerId = :customerId)
        AND (:minAmount IS NULL OR totalAmount >= :minAmount)
        AND (:maxAmount IS NULL OR totalAmount <= :maxAmount)
        ORDER BY timestamp DESC
    """)
    fun getFilteredOrders(
        startTime: Long,
        endTime: Long,
        type: String?,
        customerId: Long?,
        minAmount: Double?,
        maxAmount: Double?
    ): Flow<List<OrderEntity>>

    @Query("""
        SELECT * FROM orders
        WHERE orderNumber LIKE '%' || :keyword || '%'
           OR id IN (
               SELECT customerId FROM customers WHERE name LIKE '%' || :keyword || '%'
           )
        ORDER BY timestamp DESC
    """)
    fun searchOrders(keyword: String): Flow<List<OrderEntity>>
}

data class OrderStatistics(
    val inboundWeight: Double,
    val inboundAmount: Double,
    val outboundWeight: Double,
    val outboundAmount: Double,
    val orderCount: Int
)
