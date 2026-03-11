package com.example.clothingrecycler.data.repository

import com.example.clothingrecycler.data.local.dao.CategoryDao
import com.example.clothingrecycler.data.local.dao.OutboundRecordDao
import com.example.clothingrecycler.data.local.entity.OutboundRecordEntity
import com.example.clothingrecycler.domain.model.OutboundRecord
import com.example.clothingrecycler.domain.model.WeightUnit
import com.example.clothingrecycler.domain.repository.OutboundRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class OutboundRepositoryImpl @Inject constructor(
    private val outboundRecordDao: OutboundRecordDao,
    private val categoryDao: CategoryDao
) : OutboundRepository {

    companion object {
        private const val JIN_TO_KG = 0.5 // 1斤 = 0.5公斤
    }

    override fun getAllRecords(): Flow<List<OutboundRecord>> {
        return outboundRecordDao.getAllRecords().map { entities ->
            entities.map { entity ->
                val category = categoryDao.getCategoryById(entity.categoryId)
                entity.toDomain(category?.name ?: "未知类别")
            }
        }
    }

    override suspend fun insertRecord(record: OutboundRecord): Long {
        val entity = record.toEntity()
        val id = outboundRecordDao.insertRecord(entity)
        // 更新库存
        updateStock(record.categoryId, record.weight, record.unitType)
        return id
    }

    override suspend fun insertRecords(records: List<OutboundRecord>) {
        records.forEach { insertRecord(it) }
    }

    /**
     * 根据单位类型更新库存（出库减少）
     */
    private suspend fun updateStock(categoryId: Long, weight: Double, unitType: WeightUnit) {
        val category = categoryDao.getCategoryById(categoryId) ?: return
        val categoryUnitType = try {
            WeightUnit.valueOf(category.unitType)
        } catch (e: Exception) {
            WeightUnit.KILOGRAM
        }

        // 只有单位类型匹配时才扣减库存
        when {
            unitType == WeightUnit.KILOGRAM && categoryUnitType != WeightUnit.PIECE -> {
                categoryDao.decreaseStock(categoryId, weight)
            }
            unitType == WeightUnit.JIN && categoryUnitType != WeightUnit.PIECE -> {
                categoryDao.decreaseStockWithUnit(
                    categoryId,
                    weight * JIN_TO_KG, // 斤转公斤
                    weight, // 斤
                    0
                )
            }
            unitType == WeightUnit.PIECE && categoryUnitType == WeightUnit.PIECE -> {
                categoryDao.decreaseStockWithUnit(
                    categoryId,
                    0.0,
                    0.0,
                    weight.toInt()
                )
            }
        }
    }

    override fun getTotalRevenue(): Flow<Double> {
        return outboundRecordDao.getTotalRevenue().map { it ?: 0.0 }
    }

    private fun OutboundRecordEntity.toDomain(categoryName: String): OutboundRecord {
        return OutboundRecord(
            id = id,
            categoryId = categoryId,
            categoryName = categoryName,
            weight = weight,
            unitPrice = unitPrice,
            totalRevenue = totalRevenue,
            unitType = try { WeightUnit.valueOf(unitType) } catch (e: Exception) { WeightUnit.KILOGRAM },
            timestamp = timestamp
        )
    }

    private fun OutboundRecord.toEntity(): OutboundRecordEntity {
        return OutboundRecordEntity(
            id = id,
            categoryId = categoryId,
            weight = weight,
            unitPrice = unitPrice,
            totalRevenue = totalRevenue,
            unitType = unitType.name,
            timestamp = timestamp
        )
    }
}
