package com.example.clothingrecycler.domain.usecase

import com.example.clothingrecycler.domain.model.InboundRecord
import com.example.clothingrecycler.domain.repository.InboundRepository
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject

class GetInboundRecordsUseCase @Inject constructor(
    private val repository: InboundRepository
) {
    operator fun invoke(): Flow<List<InboundRecord>> {
        return repository.getAllRecords()
    }
}

class AddInboundRecordUseCase @Inject constructor(
    private val repository: InboundRepository
) {
    suspend operator fun invoke(record: InboundRecord): Long {
        return repository.insertRecord(record)
    }
}

class AddInboundRecordsUseCase @Inject constructor(
    private val repository: InboundRepository
) {
    suspend operator fun invoke(records: List<InboundRecord>) {
        repository.insertRecords(records)
    }
}

class GetTotalInboundCostUseCase @Inject constructor(
    private val repository: InboundRepository
) {
    operator fun invoke(): Flow<Double> {
        return repository.getTotalCost()
    }
}
