package com.example.clothingrecycler.domain.usecase

import com.example.clothingrecycler.domain.model.Customer
import com.example.clothingrecycler.domain.model.Order
import com.example.clothingrecycler.domain.repository.CustomerRepository
import com.example.clothingrecycler.domain.repository.OrderRepository
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject

class GetAllCustomersUseCase @Inject constructor(
    private val customerRepository: CustomerRepository
) {
    operator fun invoke(): Flow<List<Customer>> = customerRepository.getAllCustomers()
}

class GetInboundCustomersUseCase @Inject constructor(
    private val customerRepository: CustomerRepository
) {
    operator fun invoke(): Flow<List<Customer>> = customerRepository.getInboundCustomers()
}

class GetOutboundCustomersUseCase @Inject constructor(
    private val customerRepository: CustomerRepository
) {
    operator fun invoke(): Flow<List<Customer>> = customerRepository.getOutboundCustomers()
}

class SearchCustomersUseCase @Inject constructor(
    private val customerRepository: CustomerRepository
) {
    operator fun invoke(query: String): Flow<List<Customer>> = customerRepository.searchCustomers(query)
}

class GetCustomerByIdUseCase @Inject constructor(
    private val customerRepository: CustomerRepository
) {
    suspend operator fun invoke(id: Long): Customer? = customerRepository.getCustomerById(id)
}

class GetCustomerOrdersUseCase @Inject constructor(
    private val orderRepository: OrderRepository
) {
    operator fun invoke(customerId: Long): Flow<List<Order>> = orderRepository.getOrdersByCustomerId(customerId)
}

class AddCustomerUseCase @Inject constructor(
    private val customerRepository: CustomerRepository
) {
    suspend operator fun invoke(
        name: String,
        phone: String = "",
        email: String = "",
        address: String = "",
        note: String = ""
    ): Long {
        val customer = Customer(
            name = name.trim(),
            phone = phone.trim(),
            email = email.trim(),
            address = address.trim(),
            note = note.trim()
        )
        return customerRepository.insertCustomer(customer)
    }
}

class UpdateCustomerUseCase @Inject constructor(
    private val customerRepository: CustomerRepository
) {
    suspend operator fun invoke(customer: Customer) {
        customerRepository.updateCustomer(customer.copy(updatedAt = System.currentTimeMillis()))
    }
}

class DeleteCustomerUseCase @Inject constructor(
    private val customerRepository: CustomerRepository
) {
    suspend operator fun invoke(id: Long) {
        customerRepository.deleteCustomer(id)
    }
}

class LinkOrderToCustomerUseCase @Inject constructor(
    private val orderRepository: OrderRepository,
    private val customerRepository: CustomerRepository
) {
    suspend operator fun invoke(orderId: Long, customerId: Long) {
        // 更新订单的客户ID
        orderRepository.updateOrderCustomerId(orderId, customerId)

        // 获取订单信息以判断是入库还是出库
        val order = orderRepository.getOrderById(orderId) ?: return

        // 更新客户的订单状态
        when (order.type) {
            com.example.clothingrecycler.domain.model.OrderType.INBOUND -> {
                customerRepository.updateCustomerInboundStatus(customerId, true)
            }
            com.example.clothingrecycler.domain.model.OrderType.OUTBOUND -> {
                customerRepository.updateCustomerOutboundStatus(customerId, true)
            }
        }
    }
}
