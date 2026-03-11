package com.example.clothingrecycler.data.repository

import com.example.clothingrecycler.data.local.dao.CustomerDao
import com.example.clothingrecycler.data.local.entity.CustomerEntity
import com.example.clothingrecycler.domain.model.Customer
import com.example.clothingrecycler.domain.repository.CustomerRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class CustomerRepositoryImpl @Inject constructor(
    private val customerDao: CustomerDao
) : CustomerRepository {

    override fun getAllCustomers(): Flow<List<Customer>> {
        return customerDao.getAllCustomers().map { entities ->
            entities.map { it.toDomain() }
        }
    }

    override fun getInboundCustomers(): Flow<List<Customer>> {
        return customerDao.getInboundCustomers().map { entities ->
            entities.map { it.toDomain() }
        }
    }

    override fun getOutboundCustomers(): Flow<List<Customer>> {
        return customerDao.getOutboundCustomers().map { entities ->
            entities.map { it.toDomain() }
        }
    }

    override fun searchCustomers(query: String): Flow<List<Customer>> {
        return customerDao.searchCustomers(query).map { entities ->
            entities.map { it.toDomain() }
        }
    }

    override suspend fun getCustomerById(id: Long): Customer? {
        return customerDao.getCustomerById(id)?.toDomain()
    }

    override fun getCustomerByIdFlow(id: Long): Flow<Customer?> {
        return customerDao.getCustomerByIdFlow(id).map { it?.toDomain() }
    }

    override suspend fun insertCustomer(customer: Customer): Long {
        return customerDao.insertCustomer(customer.toEntity())
    }

    override suspend fun updateCustomer(customer: Customer) {
        customerDao.updateCustomer(customer.toEntity())
    }

    override suspend fun deleteCustomer(id: Long) {
        customerDao.deleteCustomer(id)
    }

    override suspend fun updateCustomerInboundStatus(customerId: Long, hasInbound: Boolean) {
        customerDao.updateInboundStatus(customerId, hasInbound)
    }

    override suspend fun updateCustomerOutboundStatus(customerId: Long, hasOutbound: Boolean) {
        customerDao.updateOutboundStatus(customerId, hasOutbound)
    }

    override suspend fun getCustomerCount(): Int {
        return customerDao.getCustomerCount()
    }

    private fun CustomerEntity.toDomain(): Customer {
        return Customer(
            id = id,
            name = name,
            phone = phone,
            email = email,
            address = address,
            note = note,
            hasInboundOrders = hasInboundOrders,
            hasOutboundOrders = hasOutboundOrders,
            createdAt = createdAt,
            updatedAt = updatedAt
        )
    }

    private fun Customer.toEntity(): CustomerEntity {
        return CustomerEntity(
            id = id,
            name = name,
            phone = phone,
            email = email,
            address = address,
            note = note,
            hasInboundOrders = hasInboundOrders,
            hasOutboundOrders = hasOutboundOrders,
            createdAt = createdAt,
            updatedAt = updatedAt
        )
    }
}
