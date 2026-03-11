package com.example.clothingrecycler.domain.repository

import com.example.clothingrecycler.domain.model.Category
import com.example.clothingrecycler.domain.model.Customer
import com.example.clothingrecycler.domain.model.InboundRecord
import com.example.clothingrecycler.domain.model.Order
import com.example.clothingrecycler.domain.model.OrderType
import com.example.clothingrecycler.domain.model.OutboundRecord
import com.example.clothingrecycler.domain.model.StockCategory
import kotlinx.coroutines.flow.Flow

interface CategoryRepository {
    fun getAllCategories(): Flow<List<Category>>
    suspend fun getCategoryById(id: Long): Category?
    suspend fun insertCategory(category: Category): Long
    suspend fun updateCategory(category: Category)
    suspend fun deleteCategory(id: Long)
    suspend fun increaseStock(categoryId: Long, weight: Double)
    suspend fun decreaseStock(categoryId: Long, weight: Double)
    fun getTotalEstimatedValue(): Flow<Double>
}

interface InboundRepository {
    fun getAllRecords(): Flow<List<InboundRecord>>
    suspend fun insertRecord(record: InboundRecord): Long
    suspend fun insertRecords(records: List<InboundRecord>)
    fun getTotalCost(): Flow<Double>
    suspend fun getStockCategories(): List<StockCategory>
}

interface OutboundRepository {
    fun getAllRecords(): Flow<List<OutboundRecord>>
    suspend fun insertRecord(record: OutboundRecord): Long
    suspend fun insertRecords(records: List<OutboundRecord>)
    fun getTotalRevenue(): Flow<Double>
}

interface OrderRepository {
    fun getAllOrders(): Flow<List<Order>>
    suspend fun getOrderById(id: Long): Order?
    suspend fun insertOrder(order: Order): Long
    suspend fun deleteOrder(id: Long)
    suspend fun getOrderCount(): Int
    fun getOrdersByCustomerId(customerId: Long): Flow<List<Order>>
    suspend fun updateOrderCustomerId(orderId: Long, customerId: Long?)
    // ========== 统计与筛选 ==========
    fun getStatistics(startTime: Long, endTime: Long): Flow<OrderStatistics>
    fun getFilteredOrders(
        startTime: Long,
        endTime: Long,
        type: OrderType?,
        customerId: Long?,
        minAmount: Double?,
        maxAmount: Double?
    ): Flow<List<Order>>
    fun searchOrders(keyword: String): Flow<List<Order>>
}

data class OrderStatistics(
    val inboundWeight: Double,
    val inboundAmount: Double,
    val outboundWeight: Double,
    val outboundAmount: Double,
    val orderCount: Int
) {
    val netProfit: Double get() = outboundAmount - inboundAmount
    val totalWeight: Double get() = inboundWeight + outboundWeight
}

interface CustomerRepository {
    fun getAllCustomers(): Flow<List<Customer>>
    fun getInboundCustomers(): Flow<List<Customer>>
    fun getOutboundCustomers(): Flow<List<Customer>>
    fun searchCustomers(query: String): Flow<List<Customer>>
    suspend fun getCustomerById(id: Long): Customer?
    fun getCustomerByIdFlow(id: Long): Flow<Customer?>
    suspend fun insertCustomer(customer: Customer): Long
    suspend fun updateCustomer(customer: Customer)
    suspend fun deleteCustomer(id: Long)
    suspend fun updateCustomerInboundStatus(customerId: Long, hasInbound: Boolean)
    suspend fun updateCustomerOutboundStatus(customerId: Long, hasOutbound: Boolean)
    suspend fun getCustomerCount(): Int
}

/**
 * 客户-类别价格仓库
 * 管理客户对各类别的最后交易价格（记忆功能）
 */
interface CustomerCategoryPriceRepository {
    /**
     * 获取某客户对某类别的最后交易价格
     */
    suspend fun getPrice(customerId: Long, categoryId: Long): Double?

    /**
     * 更新客户-类别价格
     */
    suspend fun updatePrice(customerId: Long, categoryId: Long, price: Double)

    /**
     * 获取客户所有类别的价格
     */
    fun getPricesByCustomer(customerId: Long): Flow<Map<Long, Double>>
}
