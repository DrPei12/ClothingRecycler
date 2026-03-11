package com.example.clothingrecycler.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.example.clothingrecycler.data.local.entity.OutboundRecordEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface OutboundRecordDao {
    @Query("SELECT * FROM outbound_records ORDER BY timestamp DESC")
    fun getAllRecords(): Flow<List<OutboundRecordEntity>>

    @Query("SELECT * FROM outbound_records WHERE id = :id")
    suspend fun getRecordById(id: Long): OutboundRecordEntity?

    @Query("SELECT * FROM outbound_records WHERE timestamp BETWEEN :startTime AND :endTime ORDER BY timestamp DESC")
    fun getRecordsByTimeRange(startTime: Long, endTime: Long): Flow<List<OutboundRecordEntity>>

    @Query("SELECT * FROM outbound_records WHERE timestamp BETWEEN :startTime AND :endTime ORDER BY timestamp DESC")
    suspend fun getRecordsByTimeRangeSync(startTime: Long, endTime: Long): List<OutboundRecordEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertRecord(record: OutboundRecordEntity): Long

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertRecords(records: List<OutboundRecordEntity>)

    @Query("DELETE FROM outbound_records WHERE id = :id")
    suspend fun deleteRecord(id: Long)

    @Query("SELECT SUM(totalRevenue) FROM outbound_records WHERE timestamp BETWEEN :startTime AND :endTime")
    suspend fun getTotalRevenueByTimeRange(startTime: Long, endTime: Long): Double?

    @Query("SELECT SUM(totalRevenue) FROM outbound_records")
    fun getTotalRevenue(): Flow<Double?>
}
