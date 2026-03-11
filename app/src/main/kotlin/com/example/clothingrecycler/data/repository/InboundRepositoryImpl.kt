package com.example.clothingrecycler.data.repository

import com.example.clothingrecycler.data.local.dao.CategoryDao
import com.example.clothingrecycler.data.local.dao.InboundRecordDao
import com.example.clothingrecycler.data.local.entity.InboundRecordEntity
import com.example.clothingrecycler.domain.model.InboundRecord
import com.example.clothingrecycler.domain.model.StockCategory
import com.example.clothingrecycler.domain.model.WeightUnit
import com.example.clothingrecycler.domain.repository.InboundRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class InboundRepositoryImpl @Inject constructor(
    private val inboundRecordDao: InboundRecordDao,
    private val categoryDao: CategoryDao
) : InboundRepository {

    companion object {
        private const val JIN_TO_KG = 0.5 // 1斤 = 0.5公斤
    }

    override fun getAllRecords(): Flow<List<InboundRecord>> {
        return inboundRecordDao.getAllRecords().map { entities ->
            entities.map { entity ->
                val category = categoryDao.getCategoryById(entity.categoryId)
                entity.toDomain(category?.name ?: "未知类别")
            }
        }
    }

    override suspend fun insertRecord(record: InboundRecord): Long {
        val entity = record.toEntity()
        val id = inboundRecordDao.insertRecord(entity)
        // 更新库存
        updateStock(record.categoryId, record.weight, record.unitType)
        return id
    }

    override suspend fun insertRecords(records: List<InboundRecord>) {
        records.forEach { insertRecord(it) }
    }

    /**
     * 根据单位类型更新库存
     */
    private suspend fun updateStock(categoryId: Long, weight: Double, unitType: WeightUnit) {
        val (kg, jin, pieces) = when (unitType) {
            WeightUnit.KILOGRAM -> Triple(weight, weight * 2, 0)
            WeightUnit.JIN -> Triple(weight * JIN_TO_KG, weight, 0)
            WeightUnit.PIECE -> Triple(0.0, 0.0, weight.toInt())
        }
        categoryDao.increaseStockWithUnit(categoryId, kg, jin, pieces)
    }

    override fun getTotalCost(): Flow<Double> {
        return inboundRecordDao.getTotalCost().map { it ?: 0.0 }
    }

    override suspend fun getStockCategories(): List<StockCategory> {
        val categories = categoryDao.getAllCategories().first()
        return categories.map { category ->
            val unitType = try {
                WeightUnit.valueOf(category.unitType)
            } catch (e: Exception) {
                WeightUnit.KILOGRAM
            }

            val stats = inboundRecordDao.getCategoryInboundStats(category.id)
            // 如果没有入库记录，使用类别设置里的收购价；否则使用加权平均价
            val averageBuyPrice = stats?.averagePrice?.takeIf { it > 0 } ?: category.buyPrice
            
            StockCategory(
                id = category.id,
                name = category.name,
                stock = category.stock,
                stockInJin = category.stockInJin,
                stockInPieces = category.stockInPieces,
                sellPrice = category.sellPrice,
                averageBuyPrice = averageBuyPrice,
                unitType = unitType
            )
        }
    }

    private fun InboundRecordEntity.toDomain(categoryName: String): InboundRecord {
        val unitType = try {
            WeightUnit.valueOf(unitType)
        } catch (e: Exception) {
            WeightUnit.KILOGRAM
        }
        return InboundRecord(
            id = id,
            categoryId = categoryId,
            categoryName = categoryName,
            weight = weight,
            unitPrice = unitPrice,
            totalCost = totalCost,
            unitType = unitType,
            timestamp = timestamp
        )
    }

    private fun InboundRecord.toEntity(): InboundRecordEntity {
        return InboundRecordEntity(
            id = id,
            categoryId = categoryId,
            weight = weight,
            unitPrice = unitPrice,
            totalCost = totalCost,
            unitType = unitType.name,
            timestamp = timestamp
        )
    }
}
