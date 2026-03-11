package com.example.clothingrecycler.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Update
import com.example.clothingrecycler.data.local.entity.CustomerEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface CustomerDao {
    @Query("SELECT * FROM customers ORDER BY name ASC")
    fun getAllCustomers(): Flow<List<CustomerEntity>>

    @Query("SELECT * FROM customers WHERE hasInboundOrders = 1 ORDER BY name ASC")
    fun getInboundCustomers(): Flow<List<CustomerEntity>>

    @Query("SELECT * FROM customers WHERE hasOutboundOrders = 1 ORDER BY name ASC")
    fun getOutboundCustomers(): Flow<List<CustomerEntity>>

    @Query("SELECT * FROM customers WHERE name LIKE '%' || :query || '%' ORDER BY name ASC")
    fun searchCustomers(query: String): Flow<List<CustomerEntity>>

    @Query("SELECT * FROM customers WHERE id = :id")
    suspend fun getCustomerById(id: Long): CustomerEntity?

    @Query("SELECT * FROM customers WHERE id = :id")
    fun getCustomerByIdFlow(id: Long): Flow<CustomerEntity?>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertCustomer(customer: CustomerEntity): Long

    @Update
    suspend fun updateCustomer(customer: CustomerEntity)

    @Query("DELETE FROM customers WHERE id = :id")
    suspend fun deleteCustomer(id: Long)

    @Query("UPDATE customers SET hasInboundOrders = :hasInbound, updatedAt = :timestamp WHERE id = :customerId")
    suspend fun updateInboundStatus(customerId: Long, hasInbound: Boolean, timestamp: Long = System.currentTimeMillis())

    @Query("UPDATE customers SET hasOutboundOrders = :hasOutbound, updatedAt = :timestamp WHERE id = :customerId")
    suspend fun updateOutboundStatus(customerId: Long, hasOutbound: Boolean, timestamp: Long = System.currentTimeMillis())

    @Query("SELECT COUNT(*) FROM customers")
    suspend fun getCustomerCount(): Int
}
