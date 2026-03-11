package com.example.clothingrecycler.data.local.database

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import com.example.clothingrecycler.data.local.dao.CategoryDao
import com.example.clothingrecycler.data.local.dao.CustomerCategoryPriceDao
import com.example.clothingrecycler.data.local.dao.CustomerDao
import com.example.clothingrecycler.data.local.dao.InboundRecordDao
import com.example.clothingrecycler.data.local.dao.OrderDao
import com.example.clothingrecycler.data.local.dao.OutboundRecordDao
import com.example.clothingrecycler.data.local.entity.CategoryEntity
import com.example.clothingrecycler.data.local.entity.CustomerCategoryPriceEntity
import com.example.clothingrecycler.data.local.entity.CustomerEntity
import com.example.clothingrecycler.data.local.entity.InboundRecordEntity
import com.example.clothingrecycler.data.local.entity.OrderEntity
import com.example.clothingrecycler.data.local.entity.OutboundRecordEntity

val MIGRATION_1_2 = object : Migration(1, 2) {
    override fun migrate(database: SupportSQLiteDatabase) {
        // 创建订单表
        database.execSQL("""
            CREATE TABLE IF NOT EXISTS `orders` (
                `id` INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                `orderNumber` TEXT NOT NULL,
                `type` TEXT NOT NULL,
                `totalWeight` REAL NOT NULL,
                `totalAmount` REAL NOT NULL,
                `categoryCount` INTEGER NOT NULL,
                `itemCount` INTEGER NOT NULL,
                `timestamp` INTEGER NOT NULL,
                `note` TEXT NOT NULL
            )
        """.trimIndent())
    }
}

val MIGRATION_2_3 = object : Migration(2, 3) {
    override fun migrate(database: SupportSQLiteDatabase) {
        // 创建客户表
        database.execSQL("""
            CREATE TABLE IF NOT EXISTS `customers` (
                `id` INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                `name` TEXT NOT NULL,
                `phone` TEXT NOT NULL DEFAULT '',
                `email` TEXT NOT NULL DEFAULT '',
                `address` TEXT NOT NULL DEFAULT '',
                `note` TEXT NOT NULL DEFAULT '',
                `hasInboundOrders` INTEGER NOT NULL DEFAULT 0,
                `hasOutboundOrders` INTEGER NOT NULL DEFAULT 0,
                `createdAt` INTEGER NOT NULL,
                `updatedAt` INTEGER NOT NULL
            )
        """.trimIndent())

        // 为 orders 表添加 customer_id 列
        database.execSQL("""
            ALTER TABLE orders ADD COLUMN customerId INTEGER DEFAULT NULL
        """.trimIndent())
    }
}

val MIGRATION_3_4 = object : Migration(3, 4) {
    override fun migrate(database: SupportSQLiteDatabase) {
        // 创建客户-类别价格表
        database.execSQL("""
            CREATE TABLE IF NOT EXISTS `customer_category_prices` (
                `customerId` INTEGER NOT NULL,
                `categoryId` INTEGER NOT NULL,
                `price` REAL NOT NULL,
                `updatedAt` INTEGER NOT NULL,
                PRIMARY KEY (`customerId`, `categoryId`)
            )
        """.trimIndent())
    }
}

val MIGRATION_4_5 = object : Migration(4, 5) {
    override fun migrate(database: SupportSQLiteDatabase) {
        // 为 categories 表添加 unitType, stockInJin, stockInPieces
        database.execSQL("""
            ALTER TABLE categories ADD COLUMN stockInJin REAL DEFAULT 0.0
        """.trimIndent())
        database.execSQL("""
            ALTER TABLE categories ADD COLUMN stockInPieces INTEGER DEFAULT 0
        """.trimIndent())
        database.execSQL("""
            ALTER TABLE categories ADD COLUMN unitType TEXT DEFAULT 'KILOGRAM'
        """.trimIndent())

        // 为 inbound_records 添加 unitType
        database.execSQL("""
            ALTER TABLE inbound_records ADD COLUMN unitType TEXT DEFAULT 'KILOGRAM'
        """.trimIndent())

        // 为 outbound_records 添加 unitType
        database.execSQL("""
            ALTER TABLE outbound_records ADD COLUMN unitType TEXT DEFAULT 'KILOGRAM'
        """.trimIndent())
    }
}

// 修复迁移4->5中列的notNull属性问题
val MIGRATION_5_6 = object : Migration(5, 6) {
    override fun migrate(database: SupportSQLiteDatabase) {
        // 先删除旧索引，避免迁移后冲突
        database.execSQL("DROP INDEX IF EXISTS index_inbound_records_categoryId")
        database.execSQL("DROP INDEX IF EXISTS index_outbound_records_categoryId")

        // categories 表：重建表以确保正确的列属性
        database.execSQL("""
            CREATE TABLE _categories_new (
                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                name TEXT NOT NULL,
                buyPrice REAL NOT NULL,
                sellPrice REAL NOT NULL,
                stock REAL NOT NULL DEFAULT 0.0,
                stockInJin REAL NOT NULL DEFAULT 0.0,
                stockInPieces INTEGER NOT NULL DEFAULT 0,
                unitType TEXT NOT NULL DEFAULT 'KILOGRAM',
                createdAt INTEGER NOT NULL,
                updatedAt INTEGER NOT NULL
            )
        """.trimIndent())
        database.execSQL("""
            INSERT INTO _categories_new (id, name, buyPrice, sellPrice, stock, stockInJin, stockInPieces, unitType, createdAt, updatedAt)
            SELECT id, name, buyPrice, sellPrice, stock, COALESCE(stockInJin, 0.0), COALESCE(stockInPieces, 0), COALESCE(unitType, 'KILOGRAM'), createdAt, updatedAt FROM categories
        """.trimIndent())
        database.execSQL("DROP TABLE categories")
        database.execSQL("ALTER TABLE _categories_new RENAME TO categories")

        // inbound_records 表：重建表以确保正确的列属性和约束
        database.execSQL("""
            CREATE TABLE _inbound_records_new (
                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                categoryId INTEGER NOT NULL,
                weight REAL NOT NULL,
                unitPrice REAL NOT NULL,
                totalCost REAL NOT NULL,
                unitType TEXT NOT NULL DEFAULT 'KILOGRAM',
                timestamp INTEGER NOT NULL,
                FOREIGN KEY(categoryId) REFERENCES categories(id) ON DELETE CASCADE
            )
        """.trimIndent())
        database.execSQL("""
            INSERT INTO _inbound_records_new (id, categoryId, weight, unitPrice, totalCost, unitType, timestamp)
            SELECT id, categoryId, weight, unitPrice, totalCost, COALESCE(unitType, 'KILOGRAM'), timestamp FROM inbound_records
        """.trimIndent())
        database.execSQL("DROP TABLE inbound_records")
        database.execSQL("ALTER TABLE _inbound_records_new RENAME TO inbound_records")
        // RENAME 后创建索引
        database.execSQL("""
            CREATE INDEX index_inbound_records_categoryId ON inbound_records(categoryId)
        """.trimIndent())

        // outbound_records 表：重建表以确保正确的列属性和约束
        database.execSQL("""
            CREATE TABLE _outbound_records_new (
                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                categoryId INTEGER NOT NULL,
                weight REAL NOT NULL,
                unitPrice REAL NOT NULL,
                totalRevenue REAL NOT NULL,
                unitType TEXT NOT NULL DEFAULT 'KILOGRAM',
                timestamp INTEGER NOT NULL,
                FOREIGN KEY(categoryId) REFERENCES categories(id) ON DELETE CASCADE
            )
        """.trimIndent())
        database.execSQL("""
            INSERT INTO _outbound_records_new (id, categoryId, weight, unitPrice, totalRevenue, unitType, timestamp)
            SELECT id, categoryId, weight, unitPrice, totalRevenue, COALESCE(unitType, 'KILOGRAM'), timestamp FROM outbound_records
        """.trimIndent())
        database.execSQL("DROP TABLE outbound_records")
        database.execSQL("ALTER TABLE _outbound_records_new RENAME TO outbound_records")
        // RENAME 后创建索引
        database.execSQL("""
            CREATE INDEX index_outbound_records_categoryId ON outbound_records(categoryId)
        """.trimIndent())
    }
}

@Database(
    entities = [
        CategoryEntity::class,
        InboundRecordEntity::class,
        OutboundRecordEntity::class,
        OrderEntity::class,
        CustomerEntity::class,
        CustomerCategoryPriceEntity::class
    ],
    version = 6,
    exportSchema = false
)
abstract class ClothingRecyclerDatabase : RoomDatabase() {

    abstract fun categoryDao(): CategoryDao
    abstract fun inboundRecordDao(): InboundRecordDao
    abstract fun outboundRecordDao(): OutboundRecordDao
    abstract fun orderDao(): OrderDao
    abstract fun customerDao(): CustomerDao
    abstract fun customerCategoryPriceDao(): CustomerCategoryPriceDao

    companion object {
        @Volatile
        private var INSTANCE: ClothingRecyclerDatabase? = null

        fun getInstance(context: Context): ClothingRecyclerDatabase {
            return INSTANCE ?: synchronized(this) {
                val instance = Room.databaseBuilder(
                    context.applicationContext,
                    ClothingRecyclerDatabase::class.java,
                    "clothing_recycler_db"
                )
                    .addMigrations(MIGRATION_1_2, MIGRATION_2_3, MIGRATION_3_4, MIGRATION_4_5, MIGRATION_5_6)
                    .build()
                INSTANCE = instance
                instance
            }
        }
    }
}
