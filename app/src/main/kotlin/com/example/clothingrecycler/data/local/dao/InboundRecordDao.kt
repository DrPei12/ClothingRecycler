package com.example.clothingrecycler.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.example.clothingrecycler.data.local.entity.InboundRecordEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface InboundRecordDao {
    @Query("SELECT * FROM inbound_records ORDER BY timestamp DESC")
    fun getAllRecords(): Flow<List<InboundRecordEntity>>

    @Query("SELECT * FROM inbound_records WHERE id = :id")
    suspend fun getRecordById(id: Long): InboundRecordEntity?

    @Query("SELECT * FROM inbound_records WHERE timestamp BETWEEN :startTime AND :endTime ORDER BY timestamp DESC")
    fun getRecordsByTimeRange(startTime: Long, endTime: Long): Flow<List<InboundRecordEntity>>

    @Query("SELECT * FROM inbound_records WHERE timestamp BETWEEN :startTime AND :endTime ORDER BY timestamp DESC")
    suspend fun getRecordsByTimeRangeSync(startTime: Long, endTime: Long): List<InboundRecordEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertRecord(record: InboundRecordEntity): Long

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertRecords(records: List<InboundRecordEntity>)

    @Query("DELETE FROM inbound_records WHERE id = :id")
    suspend fun deleteRecord(id: Long)

    @Query("SELECT SUM(totalCost) FROM inbound_records WHERE timestamp BETWEEN :startTime AND :endTime")
    suspend fun getTotalCostByTimeRange(startTime: Long, endTime: Long): Double?

    @Query("SELECT SUM(totalCost) FROM inbound_records")
    fun getTotalCost(): Flow<Double?>

    // ========== 平均收购价查询 ==========

    @Query("""
        SELECT
            categoryId,
            SUM(totalCost) as totalCost,
            SUM(weight) as totalWeight
        FROM inbound_records
        WHERE categoryId = :categoryId
        GROUP BY categoryId
    """)
    suspend fun getCategoryInboundStats(categoryId: Long): CategoryInboundStats?

    @Query("SELECT * FROM inbound_records WHERE categoryId = :categoryId")
    fun getRecordsByCategory(categoryId: Long): Flow<List<InboundRecordEntity>>
}

data class CategoryInboundStats(
    val categoryId: Long,
    val totalCost: Double,
    val totalWeight: Double
) {
    val averagePrice: Double
        get() = if (totalWeight > 0) totalCost / totalWeight else 0.0
}
