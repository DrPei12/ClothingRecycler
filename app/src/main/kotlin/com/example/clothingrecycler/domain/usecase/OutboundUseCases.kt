package com.example.clothingrecycler.domain.usecase

import com.example.clothingrecycler.domain.model.OutboundRecord
import com.example.clothingrecycler.domain.repository.OutboundRepository
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject

class GetOutboundRecordsUseCase @Inject constructor(
    private val repository: OutboundRepository
) {
    operator fun invoke(): Flow<List<OutboundRecord>> {
        return repository.getAllRecords()
    }
}

class AddOutboundRecordUseCase @Inject constructor(
    private val repository: OutboundRepository
) {
    suspend operator fun invoke(record: OutboundRecord): Long {
        return repository.insertRecord(record)
    }
}

class AddOutboundRecordsUseCase @Inject constructor(
    private val repository: OutboundRepository
) {
    suspend operator fun invoke(records: List<OutboundRecord>) {
        repository.insertRecords(records)
    }
}

class GetTotalOutboundRevenueUseCase @Inject constructor(
    private val repository: OutboundRepository
) {
    operator fun invoke(): Flow<Double> {
        return repository.getTotalRevenue()
    }
}
