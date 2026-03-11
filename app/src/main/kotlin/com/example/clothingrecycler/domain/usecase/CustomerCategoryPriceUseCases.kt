package com.example.clothingrecycler.domain.usecase

import com.example.clothingrecycler.domain.repository.CustomerCategoryPriceRepository
import javax.inject.Inject

/**
 * 获取客户-类别价格用例
 */
class GetCustomerCategoryPriceUseCase @Inject constructor(
    private val repository: CustomerCategoryPriceRepository
) {
    suspend operator fun invoke(customerId: Long, categoryId: Long): Double? {
        return repository.getPrice(customerId, categoryId)
    }
}

/**
 * 更新客户-类别价格用例
 */
class UpdateCustomerCategoryPriceUseCase @Inject constructor(
    private val repository: CustomerCategoryPriceRepository
) {
    suspend operator fun invoke(customerId: Long, categoryId: Long, price: Double) {
        repository.updatePrice(customerId, categoryId, price)
    }
}
