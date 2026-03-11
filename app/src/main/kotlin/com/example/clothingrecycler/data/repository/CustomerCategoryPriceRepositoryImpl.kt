package com.example.clothingrecycler.data.repository

import com.example.clothingrecycler.data.local.dao.CustomerCategoryPriceDao
import com.example.clothingrecycler.data.local.entity.CustomerCategoryPriceEntity
import com.example.clothingrecycler.domain.repository.CustomerCategoryPriceRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class CustomerCategoryPriceRepositoryImpl @Inject constructor(
    private val customerCategoryPriceDao: CustomerCategoryPriceDao
) : CustomerCategoryPriceRepository {

    override suspend fun getPrice(customerId: Long, categoryId: Long): Double? {
        return customerCategoryPriceDao.getPrice(customerId, categoryId)?.price
    }

    override suspend fun updatePrice(customerId: Long, categoryId: Long, price: Double) {
        customerCategoryPriceDao.insertOrUpdatePrice(
            CustomerCategoryPriceEntity(
                customerId = customerId,
                categoryId = categoryId,
                price = price
            )
        )
    }

    override fun getPricesByCustomer(customerId: Long): Flow<Map<Long, Double>> {
        return customerCategoryPriceDao.getPricesByCustomer(customerId).map { entities ->
            entities.associate { it.categoryId to it.price }
        }
    }
}
