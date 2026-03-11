package com.example.clothingrecycler.di

import android.content.Context
import androidx.room.Room
import com.example.clothingrecycler.data.local.dao.CategoryDao
import com.example.clothingrecycler.data.local.dao.CustomerCategoryPriceDao
import com.example.clothingrecycler.data.local.dao.CustomerDao
import com.example.clothingrecycler.data.local.dao.InboundRecordDao
import com.example.clothingrecycler.data.local.dao.OrderDao
import com.example.clothingrecycler.data.local.dao.OutboundRecordDao
import com.example.clothingrecycler.data.local.database.ClothingRecyclerDatabase
import com.example.clothingrecycler.data.repository.CategoryRepositoryImpl
import com.example.clothingrecycler.data.repository.CustomerCategoryPriceRepositoryImpl
import com.example.clothingrecycler.data.repository.CustomerRepositoryImpl
import com.example.clothingrecycler.data.repository.InboundRepositoryImpl
import com.example.clothingrecycler.data.repository.OrderRepositoryImpl
import com.example.clothingrecycler.data.repository.OutboundRepositoryImpl
import com.example.clothingrecycler.domain.repository.CategoryRepository
import com.example.clothingrecycler.domain.repository.CustomerCategoryPriceRepository
import com.example.clothingrecycler.domain.repository.CustomerRepository
import com.example.clothingrecycler.domain.repository.InboundRepository
import com.example.clothingrecycler.domain.repository.OrderRepository
import com.example.clothingrecycler.domain.repository.OutboundRepository
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context
    ): ClothingRecyclerDatabase {
        return Room.databaseBuilder(
            context,
            ClothingRecyclerDatabase::class.java,
            "clothing_recycler_db"
        ).build()
    }

    @Provides
    @Singleton
    fun provideCategoryDao(database: ClothingRecyclerDatabase): CategoryDao {
        return database.categoryDao()
    }

    @Provides
    @Singleton
    fun provideInboundRecordDao(database: ClothingRecyclerDatabase): InboundRecordDao {
        return database.inboundRecordDao()
    }

    @Provides
    @Singleton
    fun provideOutboundRecordDao(database: ClothingRecyclerDatabase): OutboundRecordDao {
        return database.outboundRecordDao()
    }

    @Provides
    @Singleton
    fun provideOrderDao(database: ClothingRecyclerDatabase): OrderDao {
        return database.orderDao()
    }

    @Provides
    @Singleton
    fun provideCustomerDao(database: ClothingRecyclerDatabase): CustomerDao {
        return database.customerDao()
    }

    @Provides
    @Singleton
    fun provideCustomerCategoryPriceDao(database: ClothingRecyclerDatabase): CustomerCategoryPriceDao {
        return database.customerCategoryPriceDao()
    }
}

@Module
@InstallIn(SingletonComponent::class)
abstract class RepositoryModule {

    @Binds
    @Singleton
    abstract fun bindCategoryRepository(
        impl: CategoryRepositoryImpl
    ): CategoryRepository

    @Binds
    @Singleton
    abstract fun bindCustomerRepository(
        impl: CustomerRepositoryImpl
    ): CustomerRepository

    @Binds
    @Singleton
    abstract fun bindInboundRepository(
        impl: InboundRepositoryImpl
    ): InboundRepository

    @Binds
    @Singleton
    abstract fun bindOutboundRepository(
        impl: OutboundRepositoryImpl
    ): OutboundRepository

    @Binds
    @Singleton
    abstract fun bindOrderRepository(
        impl: OrderRepositoryImpl
    ): OrderRepository

    @Binds
    @Singleton
    abstract fun bindCustomerCategoryPriceRepository(
        impl: CustomerCategoryPriceRepositoryImpl
    ): CustomerCategoryPriceRepository
}
