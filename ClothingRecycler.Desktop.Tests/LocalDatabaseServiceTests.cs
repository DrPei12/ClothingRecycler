using Microsoft.Data.Sqlite;

using ClothingRecycler.Desktop.Models;
using ClothingRecycler.Desktop.Services;

using System.Globalization;

namespace ClothingRecycler.Desktop.Tests;

public sealed class LocalDatabaseServiceTests
{
    [Fact]
    public async Task InitializeAsync_UpgradesLegacyDatabaseToLatestSchemaVersion()
    {
        await using var scope = new TestScope();
        await scope.CreateLegacyVersionTwoDatabaseAsync();

        await scope.Service.InitializeAsync();

        Assert.Equal(3, scope.Service.CurrentSchemaVersion);

        await using var connection = new SqliteConnection($"Data Source={AppDataPaths.DatabasePath}");
        await connection.OpenAsync();

        var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(await versionCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        var indexCommand = connection.CreateCommand();
        indexCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'index' AND name = 'idx_orders_timestamp';
            """;
        var indexExists = Convert.ToInt32(await indexCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        Assert.Equal(3, version);
        Assert.Equal(1, indexExists);
    }

    [Fact]
    public async Task DeleteOrderAsync_RestoresStockAndRebuildsCustomerState()
    {
        await using var scope = new TestScope();
        await scope.InitializeAsync();

        var categoryId = await scope.CreateCategoryAsync("夏装", WeightUnit.Kilogram, buyPrice: 5, sellPrice: 12);

        await scope.Service.AddInboundRecordAsync(categoryId, quantity: 10, unitPrice: 5, customerName: "Alice");
        await scope.Service.AddOutboundRecordAsync(categoryId, quantity: 4, unitPrice: 12, customerName: "Alice");

        var outboundOrder = (await scope.Service.GetRecentOrdersAsync("outbound", 10)).Single();

        await scope.Service.DeleteOrderAsync(outboundOrder.Id);

        var category = (await scope.Service.GetCategoriesAsync()).Single(item => item.Id == categoryId);
        var alice = (await scope.Service.GetCustomersAsync()).Single(customer => customer.Name == "Alice");
        var memories = await scope.Service.GetCustomerPriceMemoriesAsync(alice.Id);

        Assert.Equal(10, category.Stock, 3);
        Assert.True(alice.HasInboundOrders);
        Assert.False(alice.HasOutboundOrders);
        Assert.Single(memories);
        Assert.Equal(categoryId, memories[0].CategoryId);
        Assert.Equal(5, memories[0].Price, 3);
    }

    [Fact]
    public async Task UpdateOrderAsync_RebuildsStockAndCustomerPriceMemory()
    {
        await using var scope = new TestScope();
        await scope.InitializeAsync();

        var topsId = await scope.CreateCategoryAsync("上衣", WeightUnit.Kilogram, buyPrice: 4, sellPrice: 10);
        var pantsId = await scope.CreateCategoryAsync("长裤", WeightUnit.Kilogram, buyPrice: 6, sellPrice: 18);

        await scope.Service.AddInboundRecordAsync(topsId, quantity: 10, unitPrice: 4, customerName: null);
        await scope.Service.AddInboundRecordAsync(pantsId, quantity: 5, unitPrice: 6, customerName: null);
        await scope.Service.AddOutboundRecordAsync(topsId, quantity: 4, unitPrice: 10, customerName: "Alice");

        var outboundOrder = (await scope.Service.GetRecentOrdersAsync("outbound", 10)).Single();
        var order = await scope.Service.GetOrderEditModelAsync(outboundOrder.Id);

        var updatedOrder = new OrderEditModel
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            Type = order.Type,
            CustomerId = order.CustomerId,
            CustomerName = "Bob",
            CategoryId = pantsId,
            Quantity = 3,
            UnitPrice = 20,
            UnitType = WeightUnit.Kilogram,
            Timestamp = order.Timestamp
        };

        await scope.Service.UpdateOrderAsync(updatedOrder);

        var categories = await scope.Service.GetCategoriesAsync();
        var tops = categories.Single(item => item.Id == topsId);
        var pants = categories.Single(item => item.Id == pantsId);
        var customers = await scope.Service.GetCustomersAsync();
        var alice = customers.Single(customer => customer.Name == "Alice");
        var bob = customers.Single(customer => customer.Name == "Bob");
        var bobMemories = await scope.Service.GetCustomerPriceMemoriesAsync(bob.Id);
        var aliceMemories = await scope.Service.GetCustomerPriceMemoriesAsync(alice.Id);

        Assert.Equal(10, tops.Stock, 3);
        Assert.Equal(2, pants.Stock, 3);
        Assert.False(alice.HasInboundOrders);
        Assert.False(alice.HasOutboundOrders);
        Assert.True(bob.HasOutboundOrders);
        Assert.Single(bobMemories);
        Assert.Equal(pantsId, bobMemories[0].CategoryId);
        Assert.Equal(20, bobMemories[0].Price, 3);
        Assert.Empty(aliceMemories);
    }

    [Fact]
    public async Task RestoreDatabaseAsync_RestoresPreviousCategoryState()
    {
        await using var scope = new TestScope();
        await scope.InitializeAsync();

        var categoryId = await scope.CreateCategoryAsync("外套", WeightUnit.Piece, buyPrice: 30, sellPrice: 80);
        var backupPath = await scope.Service.CreateBackupAsync();

        await scope.Service.SaveCategoryAsync(new CategoryModel
        {
            Id = categoryId,
            Name = "外套",
            BuyPrice = 45,
            SellPrice = 95,
            Stock = 0,
            StockInJin = 0,
            StockInPieces = 0,
            UnitType = WeightUnit.Piece,
            IsArchived = true
        });

        var restorePoint = await scope.Service.RestoreDatabaseAsync(backupPath);
        await scope.Service.InitializeAsync();

        var category = (await scope.Service.GetCategoriesAsync()).Single(item => item.Id == categoryId);

        Assert.False(string.IsNullOrWhiteSpace(restorePoint));
        Assert.Equal(30, category.BuyPrice, 3);
        Assert.Equal(80, category.SellPrice, 3);
        Assert.False(category.IsArchived);
    }

    [Fact]
    public async Task RunConsistencyCheckAsync_ReturnsHealthyReport_ForConsistentData()
    {
        await using var scope = new TestScope();
        await scope.InitializeAsync();

        var categoryId = await scope.CreateCategoryAsync("羽绒服", WeightUnit.Piece, buyPrice: 35, sellPrice: 120);

        await scope.Service.AddInboundRecordAsync(categoryId, quantity: 6, unitPrice: 35, customerName: "上游A");
        await scope.Service.AddOutboundRecordAsync(categoryId, quantity: 2, unitPrice: 120, customerName: "门店B");

        var report = await scope.Service.RunConsistencyCheckAsync();

        Assert.True(report.IsHealthy);
        Assert.Equal(0, report.IssueCount);
        Assert.All(report.CheckItems, item => Assert.Equal("正常", item.StatusText));
    }

    [Fact]
    public async Task RunConsistencyCheckAsync_FindsCustomerFlagAndPriceMemoryMismatch()
    {
        await using var scope = new TestScope();
        await scope.InitializeAsync();

        var categoryId = await scope.CreateCategoryAsync("卫衣", WeightUnit.Kilogram, buyPrice: 8, sellPrice: 18);
        await scope.Service.AddInboundRecordAsync(categoryId, quantity: 10, unitPrice: 8, customerName: "Alice");

        var customer = (await scope.Service.GetCustomersAsync()).Single(item => item.Name == "Alice");

        await using (var connection = new SqliteConnection($"Data Source={AppDataPaths.DatabasePath}"))
        {
            await connection.OpenAsync();

            var updateCustomerCommand = connection.CreateCommand();
            updateCustomerCommand.CommandText =
                """
                UPDATE customers
                SET has_inbound_orders = 0
                WHERE id = $customerId;
                """;
            updateCustomerCommand.Parameters.AddWithValue("$customerId", customer.Id);
            await updateCustomerCommand.ExecuteNonQueryAsync();

            var duplicateMemoryCommand = connection.CreateCommand();
            duplicateMemoryCommand.CommandText =
                """
                INSERT INTO customer_category_prices (customer_id, category_id, price, updated_at)
                VALUES ($customerId, $categoryId, $price, $updatedAt);
                """;
            duplicateMemoryCommand.Parameters.AddWithValue("$customerId", customer.Id);
            duplicateMemoryCommand.Parameters.AddWithValue("$categoryId", categoryId);
            duplicateMemoryCommand.Parameters.AddWithValue("$price", 99);
            duplicateMemoryCommand.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.AddHours(-3).ToUnixTimeMilliseconds());
            await duplicateMemoryCommand.ExecuteNonQueryAsync();
        }

        var report = await scope.Service.RunConsistencyCheckAsync();

        Assert.False(report.IsHealthy);
        Assert.Contains(report.Issues, issue => issue.CheckName == "客户使用标记");
        Assert.Contains(report.Issues, issue => issue.CheckName == "客户价格记忆");
    }

    [Fact]
    public async Task RunConsistencyCheckAsync_FindsOrderIntegrityMismatch()
    {
        await using var scope = new TestScope();
        await scope.InitializeAsync();

        var categoryId = await scope.CreateCategoryAsync("牛仔裤", WeightUnit.Kilogram, buyPrice: 12, sellPrice: 28);
        await scope.Service.AddInboundRecordAsync(categoryId, quantity: 5, unitPrice: 12, customerName: null);

        var order = (await scope.Service.GetRecentOrdersAsync("inbound", 10)).Single();

        await using (var connection = new SqliteConnection($"Data Source={AppDataPaths.DatabasePath}"))
        {
            await connection.OpenAsync();

            var tamperOrderCommand = connection.CreateCommand();
            tamperOrderCommand.CommandText =
                """
                UPDATE orders
                SET
                    total_weight = total_weight + 3,
                    total_amount = total_amount + 20
                WHERE id = $orderId;
                """;
            tamperOrderCommand.Parameters.AddWithValue("$orderId", order.Id);
            await tamperOrderCommand.ExecuteNonQueryAsync();
        }

        var report = await scope.Service.RunConsistencyCheckAsync();

        Assert.False(report.IsHealthy);
        Assert.True(report.Issues.Count(issue => issue.CheckName == "订单明细关系") >= 2);
        Assert.NotEqual("正常", report.CheckItems.Single(item => item.Name == "订单明细关系").StatusText);
    }

    [Fact]
    public async Task RepairRepairableConsistencyIssuesAsync_RebuildsCustomerDerivedData()
    {
        await using var scope = new TestScope();
        await scope.InitializeAsync();

        var categoryId = await scope.CreateCategoryAsync("毛衣", WeightUnit.Kilogram, buyPrice: 9, sellPrice: 22);
        await scope.Service.AddInboundRecordAsync(categoryId, quantity: 12, unitPrice: 9, customerName: "Alice");

        var customer = (await scope.Service.GetCustomersAsync()).Single(item => item.Name == "Alice");

        await using (var connection = new SqliteConnection($"Data Source={AppDataPaths.DatabasePath}"))
        {
            await connection.OpenAsync();

            var updateCustomerCommand = connection.CreateCommand();
            updateCustomerCommand.CommandText =
                """
                UPDATE customers
                SET
                    has_inbound_orders = 0,
                    has_outbound_orders = 1
                WHERE id = $customerId;
                """;
            updateCustomerCommand.Parameters.AddWithValue("$customerId", customer.Id);
            await updateCustomerCommand.ExecuteNonQueryAsync();

            var duplicateMemoryCommand = connection.CreateCommand();
            duplicateMemoryCommand.CommandText =
                """
                INSERT INTO customer_category_prices (customer_id, category_id, price, updated_at)
                VALUES ($customerId, $categoryId, $price, $updatedAt);
                """;
            duplicateMemoryCommand.Parameters.AddWithValue("$customerId", customer.Id);
            duplicateMemoryCommand.Parameters.AddWithValue("$categoryId", categoryId);
            duplicateMemoryCommand.Parameters.AddWithValue("$price", 88);
            duplicateMemoryCommand.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.AddHours(-2).ToUnixTimeMilliseconds());
            await duplicateMemoryCommand.ExecuteNonQueryAsync();
        }

        var beforeReport = await scope.Service.RunConsistencyCheckAsync();
        Assert.Contains(beforeReport.Issues, issue => issue.CheckName == "客户使用标记");
        Assert.Contains(beforeReport.Issues, issue => issue.CheckName == "客户价格记忆");

        var repairResult = await scope.Service.RepairRepairableConsistencyIssuesAsync();

        var afterReport = repairResult.Report;
        var memories = await scope.Service.GetCustomerPriceMemoriesAsync(customer.Id);
        var repairedCustomer = (await scope.Service.GetCustomersAsync()).Single(item => item.Id == customer.Id);

        Assert.StartsWith(AppDataPaths.BackupDirectory, repairResult.RestorePointPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("consistency-repair-point-", Path.GetFileName(repairResult.RestorePointPath), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(repairResult.RestorePointPath));
        Assert.Equal(1, repairResult.ProcessedCustomerCount);
        Assert.DoesNotContain(afterReport.Issues, issue => issue.CheckName == "客户使用标记");
        Assert.DoesNotContain(afterReport.Issues, issue => issue.CheckName == "客户价格记忆");
        Assert.True(repairedCustomer.HasInboundOrders);
        Assert.False(repairedCustomer.HasOutboundOrders);
        Assert.Single(memories);
        Assert.Equal(9, memories[0].Price, 3);
    }

    private sealed class TestScope : IAsyncDisposable
    {
        private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "ClothingRecyclerTests", Guid.NewGuid().ToString("N"));

        public TestScope()
        {
            AppDataPaths.RootDirectoryOverride = _rootPath;
            Logger = new AppLogger();
            Service = new LocalDatabaseService(Logger);
        }

        public AppLogger Logger { get; }

        public LocalDatabaseService Service { get; }

        public async Task InitializeAsync()
        {
            await Logger.InitializeAsync();
            await Service.InitializeAsync();
        }

        public async Task<long> CreateCategoryAsync(string name, WeightUnit unitType, double buyPrice, double sellPrice)
        {
            await Service.SaveCategoryAsync(new CategoryModel
            {
                Name = name,
                BuyPrice = buyPrice,
                SellPrice = sellPrice,
                UnitType = unitType,
                IsArchived = false
            });

            var categories = await Service.GetCategoriesAsync();
            return categories.Single(category => category.Name == name).Id;
        }

        public async Task CreateLegacyVersionTwoDatabaseAsync()
        {
            Directory.CreateDirectory(_rootPath);

            await using var connection = new SqliteConnection($"Data Source={AppDataPaths.DatabasePath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE categories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    buy_price REAL NOT NULL,
                    sell_price REAL NOT NULL,
                    stock REAL NOT NULL DEFAULT 0,
                    stock_in_jin REAL NOT NULL DEFAULT 0,
                    stock_in_pieces INTEGER NOT NULL DEFAULT 0,
                    unit_type TEXT NOT NULL,
                    created_at INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL,
                    is_archived INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE customers (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    phone TEXT NOT NULL DEFAULT '',
                    email TEXT NOT NULL DEFAULT '',
                    address TEXT NOT NULL DEFAULT '',
                    note TEXT NOT NULL DEFAULT '',
                    has_inbound_orders INTEGER NOT NULL DEFAULT 0,
                    has_outbound_orders INTEGER NOT NULL DEFAULT 0,
                    created_at INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL
                );

                CREATE TABLE customer_category_prices (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    customer_id INTEGER NOT NULL,
                    category_id INTEGER NOT NULL,
                    price REAL NOT NULL,
                    updated_at INTEGER NOT NULL
                );

                CREATE TABLE orders (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    order_number TEXT NOT NULL,
                    type TEXT NOT NULL,
                    total_weight REAL NOT NULL,
                    total_amount REAL NOT NULL,
                    category_count INTEGER NOT NULL,
                    item_count INTEGER NOT NULL,
                    customer_id INTEGER NULL,
                    note TEXT NOT NULL DEFAULT '',
                    timestamp INTEGER NOT NULL
                );

                CREATE TABLE inbound_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    order_id INTEGER NULL,
                    category_id INTEGER NOT NULL,
                    weight REAL NOT NULL,
                    unit_price REAL NOT NULL,
                    total_cost REAL NOT NULL,
                    unit_type TEXT NOT NULL,
                    timestamp INTEGER NOT NULL
                );

                CREATE TABLE outbound_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    order_id INTEGER NULL,
                    category_id INTEGER NOT NULL,
                    weight REAL NOT NULL,
                    unit_price REAL NOT NULL,
                    total_revenue REAL NOT NULL,
                    unit_type TEXT NOT NULL,
                    timestamp INTEGER NOT NULL
                );

                CREATE TABLE stock_adjustments (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    category_id INTEGER NOT NULL,
                    previous_quantity REAL NOT NULL,
                    adjusted_quantity REAL NOT NULL,
                    delta_quantity REAL NOT NULL,
                    unit_type TEXT NOT NULL,
                    reason TEXT NOT NULL DEFAULT '',
                    timestamp INTEGER NOT NULL
                );

                CREATE TABLE stock_audits (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    category_id INTEGER NOT NULL,
                    system_quantity REAL NOT NULL,
                    actual_quantity REAL NOT NULL,
                    delta_quantity REAL NOT NULL,
                    unit_type TEXT NOT NULL,
                    note TEXT NOT NULL DEFAULT '',
                    timestamp INTEGER NOT NULL
                );
                """;

            await command.ExecuteNonQueryAsync();
        }

        public ValueTask DisposeAsync()
        {
            AppDataPaths.RootDirectoryOverride = null;

            try
            {
                if (Directory.Exists(_rootPath))
                {
                    Directory.Delete(_rootPath, recursive: true);
                }
            }
            catch
            {
                // 测试清理失败不影响断言结果。
            }

            return ValueTask.CompletedTask;
        }
    }
}
