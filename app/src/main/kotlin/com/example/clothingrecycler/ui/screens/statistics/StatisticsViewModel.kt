package com.example.clothingrecycler.ui.screens.statistics

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.clothingrecycler.domain.repository.OrderStatistics
import com.example.clothingrecycler.domain.usecase.GetStatisticsUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.util.Calendar
import javax.inject.Inject

enum class StatisticsPeriod {
    TODAY,
    THIS_WEEK,
    THIS_MONTH,
    THIS_YEAR,
    CUSTOM
}

data class StatisticsUiState(
    val period: StatisticsPeriod = StatisticsPeriod.TODAY,
    val statistics: OrderStatistics = OrderStatistics(
        inboundWeight = 0.0,
        inboundAmount = 0.0,
        outboundWeight = 0.0,
        outboundAmount = 0.0,
        orderCount = 0
    ),
    val startDate: Long = System.currentTimeMillis(),
    val endDate: Long = System.currentTimeMillis(),
    val isLoading: Boolean = false
)

@HiltViewModel
class StatisticsViewModel @Inject constructor(
    private val getStatisticsUseCase: GetStatisticsUseCase
) : ViewModel() {

    private val _uiState = MutableStateFlow(StatisticsUiState())
    val uiState: StateFlow<StatisticsUiState> = _uiState.asStateFlow()

    init {
        loadStatistics(StatisticsPeriod.TODAY)
    }

    fun setPeriod(period: StatisticsPeriod) {
        _uiState.value = _uiState.value.copy(period = period)
        loadStatistics(period)
    }

    fun setCustomDateRange(startDate: Long, endDate: Long) {
        _uiState.value = _uiState.value.copy(
            period = StatisticsPeriod.CUSTOM,
            startDate = startDate,
            endDate = endDate
        )
        loadCustomStatistics(startDate, endDate)
    }

    private fun loadStatistics(period: StatisticsPeriod) {
        val (startTime, endTime) = getDateRange(period)
        loadCustomStatistics(startTime, endTime)
    }

    private fun loadCustomStatistics(startTime: Long, endTime: Long) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true)
            getStatisticsUseCase(startTime, endTime).collect { stats ->
                _uiState.value = _uiState.value.copy(
                    statistics = stats,
                    isLoading = false,
                    startDate = startTime,
                    endDate = endTime
                )
            }
        }
    }

    private fun getDateRange(period: StatisticsPeriod): Pair<Long, Long> {
        val calendar = Calendar.getInstance()
        val endTime = calendar.timeInMillis

        when (period) {
            StatisticsPeriod.TODAY -> {
                calendar.set(Calendar.HOUR_OF_DAY, 0)
                calendar.set(Calendar.MINUTE, 0)
                calendar.set(Calendar.SECOND, 0)
                calendar.set(Calendar.MILLISECOND, 0)
            }
            StatisticsPeriod.THIS_WEEK -> {
                calendar.set(Calendar.DAY_OF_WEEK, calendar.firstDayOfWeek)
                calendar.set(Calendar.HOUR_OF_DAY, 0)
                calendar.set(Calendar.MINUTE, 0)
                calendar.set(Calendar.SECOND, 0)
                calendar.set(Calendar.MILLISECOND, 0)
            }
            StatisticsPeriod.THIS_MONTH -> {
                calendar.set(Calendar.DAY_OF_MONTH, 1)
                calendar.set(Calendar.HOUR_OF_DAY, 0)
                calendar.set(Calendar.MINUTE, 0)
                calendar.set(Calendar.SECOND, 0)
                calendar.set(Calendar.MILLISECOND, 0)
            }
            StatisticsPeriod.THIS_YEAR -> {
                calendar.set(Calendar.DAY_OF_YEAR, 1)
                calendar.set(Calendar.HOUR_OF_DAY, 0)
                calendar.set(Calendar.MINUTE, 0)
                calendar.set(Calendar.SECOND, 0)
                calendar.set(Calendar.MILLISECOND, 0)
            }
            StatisticsPeriod.CUSTOM -> {
                // 使用当前状态的自定义日期范围
                return Pair(_uiState.value.startDate, _uiState.value.endDate)
            }
        }
        return Pair(calendar.timeInMillis, endTime)
    }
}
