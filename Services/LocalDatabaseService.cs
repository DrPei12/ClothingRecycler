using System.Text;

using Microsoft.Data.Sqlite;

namespace ClothingRecycler.Desktop.Services
{
    public sealed class LocalDatabaseService
    {
        private const int BaseSchemaVersion = 1;
        private const int CategoryArchiveSchemaVersion = 2;
        private const int PerformanceIndexesSchemaVersion = 3;
        private const int LatestSchemaVersion = PerformanceIndexesSchemaVersion;

        private readonly AppLogger _logger;

        public LocalDatabaseService(AppLogger logger)
        {
            _logger = logger;
        }

        public string DatabasePath => AppDataPaths.DatabasePath;

        public string BackupDirectory => AppDataPaths.BackupDirectory;

        public string ExportDirectory => AppDataPaths.ExportDirectory;

        public int CurrentSchemaVersion { get; private set; } = LatestSchemaVersion;

        public async Task InitializeAsync()
        {
            AppDataPaths.EnsureDirectories();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var currentVersion = await GetCurrentSchemaVersionAsync(connection);
            if (currentVersion > LatestSchemaVersion)
            {
                throw new InvalidOperationException($"当前数据库架构版本 v{currentVersion} 高于应用支持的 v{LatestSchemaVersion}，请升级应用。");
            }

            if (currentVersion == 0)
            {
                await _logger.LogInfoAsync($"检测到空数据库，准备初始化到架构版本 v{LatestSchemaVersion}。");
            }
            else if (currentVersion < LatestSchemaVersion)
            {
                await _logger.LogInfoAsync($"检测到数据库架构版本 v{currentVersion}，准备升级到 v{LatestSchemaVersion}。");
            }

            for (var targetVersion = currentVersion + 1; targetVersion <= LatestSchemaVersion; targetVersion++)
            {
                await ApplyMigrationAsync(connection, targetVersion);
                await SetSchemaVersionAsync(connection, targetVersion);
                await _logger.LogInfoAsync($"已完成数据库迁移 v{targetVersion}：{GetMigrationName(targetVersion)}。");
            }

            CurrentSchemaVersion = currentVersion == 0 || currentVersion < LatestSchemaVersion
                ? LatestSchemaVersion
                : currentVersion;

            if (currentVersion == LatestSchemaVersion)
            {
                await _logger.LogInfoAsync($"数据库架构已是最新版本 v{CurrentSchemaVersion}。");
            }
        }

        public async Task<string> CreateBackupAsync()
        {
            AppDataPaths.EnsureDirectories();
            return await CreateBackupSnapshotAsync("clothingrecycler-backup");
        }

        public async Task<string?> RestoreDatabaseAsync(string sourcePath)
        {
            AppDataPaths.EnsureDirectories();

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new InvalidOperationException("\u672A\u627E\u5230\u8981\u6062\u590D\u7684\u5907\u4EFD\u6587\u4EF6\u3002");
            }

            var sourceFullPath = Path.GetFullPath(sourcePath);
            var databaseFullPath = Path.GetFullPath(DatabasePath);
            if (string.Equals(sourceFullPath, databaseFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("\u9009\u62E9\u7684\u6587\u4EF6\u5C31\u662F\u5F53\u524D\u6B63\u5728\u4F7F\u7528\u7684\u6570\u636E\u5E93\u3002");
            }

            string? restorePointPath = null;
            if (File.Exists(DatabasePath))
            {
                restorePointPath = Path.Combine(
                    BackupDirectory,
                    $"restore-point-{DateTime.Now:yyyyMMdd-HHmmss}.db");
                await Task.Run(() => File.Copy(DatabasePath, restorePointPath, overwrite: true));
            }

            await Task.Run(() => File.Copy(sourceFullPath, DatabasePath, overwrite: true));
            return restorePointPath;
        }

        public async Task<string> ExportBusinessDataAsync()
        {
            AppDataPaths.EnsureDirectories();

            var exportPath = Path.Combine(
                ExportDirectory,
                $"business-export-{DateTime.Now:yyyyMMdd-HHmmss}");

            Directory.CreateDirectory(exportPath);

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await ExportQueryToCsvAsync(
                connection,
                Path.Combine(exportPath, "categories.csv"),
                """
                SELECT
                    id AS category_id,
                    name AS category_name,
                    unit_type,
                    buy_price,
                    sell_price,
                    stock,
                    stock_in_jin,
                    stock_in_pieces,
                    is_archived,
                    datetime(created_at / 1000, 'unixepoch', 'localtime') AS created_at_local,
                    datetime(updated_at / 1000, 'unixepoch', 'localtime') AS updated_at_local
                FROM categories
                ORDER BY is_archived ASC, name COLLATE NOCASE;
                """);

            await ExportQueryToCsvAsync(
                connection,
                Path.Combine(exportPath, "customers.csv"),
                """
                SELECT
                    id AS customer_id,
                    name AS customer_name,
                    phone,
                    email,
                    address,
                    note,
                    has_inbound_orders,
                    has_outbound_orders,
                    datetime(created_at / 1000, 'unixepoch', 'localtime') AS created_at_local,
                    datetime(updated_at / 1000, 'unixepoch', 'localtime') AS updated_at_local
                FROM customers
                ORDER BY name COLLATE NOCASE;
                """);

            await ExportQueryToCsvAsync(
                connection,
                Path.Combine(exportPath, "customer_price_memory.csv"),
                """
                SELECT
                    ccp.id AS price_memory_id,
                    ccp.customer_id,
                    c.name AS customer_name,
                    ccp.category_id,
                    cg.name AS category_name,
                    ccp.price,
                    datetime(ccp.updated_at / 1000, 'unixepoch', 'localtime') AS updated_at_local
                FROM customer_category_prices ccp
                INNER JOIN customers c ON c.id = ccp.customer_id
                INNER JOIN categories cg ON cg.id = ccp.category_id
                ORDER BY c.name COLLATE NOCASE, cg.name COLLATE NOCASE;
                """);

            await ExportQueryToCsvAsync(
                connection,
                Path.Combine(exportPath, "orders.csv"),
                """
                SELECT
                    o.id AS order_id,
                    o.order_number,
                    o.type,
                    CASE
                        WHEN o.type = 'outbound' THEN '出库'
                        ELSE '入库'
                    END AS type_text,
                    IFNULL(c.name, '') AS customer_name,
                    o.total_weight,
                    o.total_amount,
                    o.category_count,
                    o.item_count,
                    o.note,
                    datetime(o.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local
                FROM orders o
                LEFT JOIN customers c ON c.id = o.customer_id
                ORDER BY o.timestamp DESC;
                """);

            await ExportQueryToCsvAsync(
                connection,
                Path.Combine(exportPath, "inbound_records.csv"),
                """
                SELECT
                    ir.id AS inbound_record_id,
                    ir.order_id,
                    IFNULL(o.order_number, '') AS order_number,
                    ir.category_id,
                    c.name AS category_name,
                    ir.weight,
                    ir.unit_price,
                    ir.total_cost,
                    ir.unit_type,
                    datetime(ir.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local
                FROM inbound_records ir
                INNER JOIN categories c ON c.id = ir.category_id
                LEFT JOIN orders o ON o.id = ir.order_id
                ORDER BY ir.timestamp DESC;
                """);

            await ExportQueryToCsvAsync(
                connection,
                Path.Combine(exportPath, "outbound_records.csv"),
                """
                SELECT
                    orr.id AS outbound_record_id,
                    orr.order_id,
                    IFNULL(o.order_number, '') AS order_number,
                    orr.category_id,
                    c.name AS category_name,
                    orr.weight,
                    orr.unit_price,
                    orr.total_revenue,
                    orr.unit_type,
                    datetime(orr.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local
                FROM outbound_records orr
                INNER JOIN categories c ON c.id = orr.category_id
                LEFT JOIN orders o ON o.id = orr.order_id
                ORDER BY orr.timestamp DESC;
                """);

            await ExportQueryToCsvAsync(
                connection,
                Path.Combine(exportPath, "stock_adjustments.csv"),
                """
                SELECT
                    sa.id AS adjustment_id,
                    sa.category_id,
                    c.name AS category_name,
                    sa.previous_quantity,
                    sa.adjusted_quantity,
                    sa.delta_quantity,
                    sa.unit_type,
                    sa.reason,
                    datetime(sa.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local
                FROM stock_adjustments sa
                INNER JOIN categories c ON c.id = sa.category_id
                ORDER BY sa.timestamp DESC;
                """);

            await ExportQueryToCsvAsync(
                connection,
                Path.Combine(exportPath, "stock_audits.csv"),
                """
                SELECT
                    sa.id AS audit_id,
                    sa.category_id,
                    c.name AS category_name,
                    sa.system_quantity,
                    sa.actual_quantity,
                    sa.delta_quantity,
                    sa.unit_type,
                    sa.note,
                    datetime(sa.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local
                FROM stock_audits sa
                INNER JOIN categories c ON c.id = sa.category_id
                ORDER BY sa.timestamp DESC;
                """);

            return exportPath;
        }

        public async Task<ImportPreviewModel> PreviewCategoriesImportAsync(string sourcePath)
        {
            var (preview, _) = await BuildCategoryImportPreviewAsync(sourcePath);
            return preview;
        }

        public async Task<string> ImportCategoriesAsync(string sourcePath)
        {
            var (preview, plans) = await BuildCategoryImportPreviewAsync(sourcePath);
            if (!preview.CanImport)
            {
                throw CreateImportPreviewException(preview);
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            foreach (var plan in plans)
            {
                if (plan.IsUpdate)
                {
                    var updateCommand = connection.CreateCommand();
                    updateCommand.Transaction = transaction;
                    updateCommand.CommandText =
                        """
                        UPDATE categories
                        SET
                            buy_price = $buyPrice,
                            sell_price = $sellPrice,
                            is_archived = $isArchived,
                            updated_at = $updatedAt
                        WHERE id = $id;
                        """;
                    updateCommand.Parameters.AddWithValue("$id", plan.ExistingId!.Value);
                    updateCommand.Parameters.AddWithValue("$buyPrice", plan.BuyPrice);
                    updateCommand.Parameters.AddWithValue("$sellPrice", plan.SellPrice);
                    updateCommand.Parameters.AddWithValue("$isArchived", plan.IsArchived ? 1 : 0);
                    updateCommand.Parameters.AddWithValue("$updatedAt", now);
                    await updateCommand.ExecuteNonQueryAsync();
                }
                else
                {
                    var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText =
                        """
                        INSERT INTO categories (
                            name,
                            buy_price,
                            sell_price,
                            stock,
                            stock_in_jin,
                            stock_in_pieces,
                            unit_type,
                            created_at,
                            updated_at,
                            is_archived
                        )
                        VALUES (
                            $name,
                            $buyPrice,
                            $sellPrice,
                            0,
                            0,
                            0,
                            $unitType,
                            $createdAt,
                            $updatedAt,
                            $isArchived
                        );
                        """;
                    insertCommand.Parameters.AddWithValue("$name", plan.Name);
                    insertCommand.Parameters.AddWithValue("$buyPrice", plan.BuyPrice);
                    insertCommand.Parameters.AddWithValue("$sellPrice", plan.SellPrice);
                    insertCommand.Parameters.AddWithValue("$unitType", plan.UnitType.ToString());
                    insertCommand.Parameters.AddWithValue("$createdAt", now);
                    insertCommand.Parameters.AddWithValue("$updatedAt", now);
                    insertCommand.Parameters.AddWithValue("$isArchived", plan.IsArchived ? 1 : 0);
                    await insertCommand.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            return $"分类导入完成：新增 {preview.InsertCount} 条，更新 {preview.UpdateCount} 条。";
        }

        public async Task<ImportPreviewModel> PreviewCustomersImportAsync(string sourcePath)
        {
            var (preview, _) = await BuildCustomerImportPreviewAsync(sourcePath);
            return preview;
        }

        public async Task<string> ImportCustomersAsync(string sourcePath)
        {
            var (preview, plans) = await BuildCustomerImportPreviewAsync(sourcePath);
            if (!preview.CanImport)
            {
                throw CreateImportPreviewException(preview);
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            foreach (var plan in plans)
            {
                if (plan.IsUpdate)
                {
                    var updateCommand = connection.CreateCommand();
                    updateCommand.Transaction = transaction;
                    updateCommand.CommandText =
                        """
                        UPDATE customers
                        SET
                            phone = $phone,
                            email = $email,
                            address = $address,
                            note = $note,
                            has_inbound_orders = $hasInboundOrders,
                            has_outbound_orders = $hasOutboundOrders,
                            updated_at = $updatedAt
                        WHERE id = $id;
                        """;
                    updateCommand.Parameters.AddWithValue("$id", plan.ExistingId!.Value);
                    updateCommand.Parameters.AddWithValue("$phone", plan.Phone);
                    updateCommand.Parameters.AddWithValue("$email", plan.Email);
                    updateCommand.Parameters.AddWithValue("$address", plan.Address);
                    updateCommand.Parameters.AddWithValue("$note", plan.Note);
                    updateCommand.Parameters.AddWithValue("$hasInboundOrders", plan.HasInboundOrders ? 1 : 0);
                    updateCommand.Parameters.AddWithValue("$hasOutboundOrders", plan.HasOutboundOrders ? 1 : 0);
                    updateCommand.Parameters.AddWithValue("$updatedAt", now);
                    await updateCommand.ExecuteNonQueryAsync();
                }
                else
                {
                    var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText =
                        """
                        INSERT INTO customers (
                            name,
                            phone,
                            email,
                            address,
                            note,
                            has_inbound_orders,
                            has_outbound_orders,
                            created_at,
                            updated_at
                        )
                        VALUES (
                            $name,
                            $phone,
                            $email,
                            $address,
                            $note,
                            $hasInboundOrders,
                            $hasOutboundOrders,
                            $createdAt,
                            $updatedAt
                        );
                        """;
                    insertCommand.Parameters.AddWithValue("$name", plan.Name);
                    insertCommand.Parameters.AddWithValue("$phone", plan.Phone);
                    insertCommand.Parameters.AddWithValue("$email", plan.Email);
                    insertCommand.Parameters.AddWithValue("$address", plan.Address);
                    insertCommand.Parameters.AddWithValue("$note", plan.Note);
                    insertCommand.Parameters.AddWithValue("$hasInboundOrders", plan.HasInboundOrders ? 1 : 0);
                    insertCommand.Parameters.AddWithValue("$hasOutboundOrders", plan.HasOutboundOrders ? 1 : 0);
                    insertCommand.Parameters.AddWithValue("$createdAt", now);
                    insertCommand.Parameters.AddWithValue("$updatedAt", now);
                    await insertCommand.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            return $"客户导入完成：新增 {preview.InsertCount} 条，更新 {preview.UpdateCount} 条。";
        }

        public async Task<IReadOnlyList<CategoryModel>> GetCategoriesAsync(bool includeArchived = true)
        {
            var categories = new List<CategoryModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                includeArchived
                    ? """
                      SELECT id, name, buy_price, sell_price, stock, stock_in_jin, stock_in_pieces, unit_type, is_archived, created_at, updated_at
                      FROM categories
                      ORDER BY is_archived ASC, name COLLATE NOCASE;
                      """
                    : """
                      SELECT id, name, buy_price, sell_price, stock, stock_in_jin, stock_in_pieces, unit_type, is_archived, created_at, updated_at
                      FROM categories
                      WHERE is_archived = 0
                      ORDER BY name COLLATE NOCASE;
                      """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new CategoryModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    BuyPrice = reader.GetDouble(2),
                    SellPrice = reader.GetDouble(3),
                    Stock = reader.GetDouble(4),
                    StockInJin = reader.GetDouble(5),
                    StockInPieces = reader.GetInt32(6),
                    UnitType = ParseUnit(reader.GetString(7)),
                    IsArchived = reader.GetInt64(8) == 1,
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
                    UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))
                });
            }

            return categories;
        }

        public async Task<IReadOnlyList<CategoryModel>> GetActiveCategoriesAsync() =>
            await GetCategoriesAsync(includeArchived: false);

        public async Task<IReadOnlyList<CategoryManagementItemModel>> GetCategoryManagementItemsAsync()
        {
            var items = new List<CategoryManagementItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    c.id,
                    c.name,
                    c.buy_price,
                    c.sell_price,
                    c.stock,
                    c.stock_in_jin,
                    c.stock_in_pieces,
                    c.unit_type,
                    c.is_archived,
                    c.created_at,
                    c.updated_at,
                    (SELECT COUNT(*) FROM inbound_records WHERE category_id = c.id) AS inbound_record_count,
                    (SELECT COUNT(*) FROM outbound_records WHERE category_id = c.id) AS outbound_record_count,
                    IFNULL((SELECT SUM(weight) FROM inbound_records WHERE category_id = c.id), 0) AS inbound_quantity,
                    IFNULL((SELECT SUM(weight) FROM outbound_records WHERE category_id = c.id), 0) AS outbound_quantity,
                    (
                        SELECT MAX(timestamp)
                        FROM (
                            SELECT timestamp FROM inbound_records WHERE category_id = c.id
                            UNION ALL
                            SELECT timestamp FROM outbound_records WHERE category_id = c.id
                        )
                    ) AS last_activity_at
                FROM categories c
                ORDER BY c.is_archived ASC, last_activity_at DESC, c.name COLLATE NOCASE;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var category = new CategoryModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    BuyPrice = reader.GetDouble(2),
                    SellPrice = reader.GetDouble(3),
                    Stock = reader.GetDouble(4),
                    StockInJin = reader.GetDouble(5),
                    StockInPieces = reader.GetInt32(6),
                    UnitType = ParseUnit(reader.GetString(7)),
                    IsArchived = reader.GetInt64(8) == 1,
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
                    UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))
                };

                items.Add(new CategoryManagementItemModel
                {
                    Category = category,
                    InboundRecordCount = reader.GetInt32(11),
                    OutboundRecordCount = reader.GetInt32(12),
                    TotalInboundQuantity = reader.GetDouble(13),
                    TotalOutboundQuantity = reader.GetDouble(14),
                    LastActivityAt = reader.IsDBNull(15)
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(15))
                });
            }

            return items;
        }

        public async Task<IReadOnlyList<StockCategoryItemModel>> GetStockCategoryItemsAsync()
        {
            var items = new List<StockCategoryItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    c.id,
                    c.name,
                    c.buy_price,
                    c.sell_price,
                    c.stock,
                    c.stock_in_jin,
                    c.stock_in_pieces,
                    c.unit_type,
                    c.is_archived,
                    c.created_at,
                    c.updated_at,
                    (SELECT COUNT(*) FROM inbound_records WHERE category_id = c.id) AS inbound_record_count,
                    (SELECT COUNT(*) FROM outbound_records WHERE category_id = c.id) AS outbound_record_count,
                    (SELECT MAX(timestamp) FROM inbound_records WHERE category_id = c.id) AS last_inbound_at,
                    (SELECT MAX(timestamp) FROM outbound_records WHERE category_id = c.id) AS last_outbound_at
                FROM categories c
                ORDER BY c.is_archived ASC, c.updated_at DESC, c.name COLLATE NOCASE;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var category = new CategoryModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    BuyPrice = reader.GetDouble(2),
                    SellPrice = reader.GetDouble(3),
                    Stock = reader.GetDouble(4),
                    StockInJin = reader.GetDouble(5),
                    StockInPieces = reader.GetInt32(6),
                    UnitType = ParseUnit(reader.GetString(7)),
                    IsArchived = reader.GetInt64(8) == 1,
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
                    UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))
                };

                items.Add(new StockCategoryItemModel
                {
                    Category = category,
                    InboundRecordCount = reader.GetInt32(11),
                    OutboundRecordCount = reader.GetInt32(12),
                    LastInboundAt = reader.IsDBNull(13)
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(13)),
                    LastOutboundAt = reader.IsDBNull(14)
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(14))
                });
            }

            return items;
        }

        public async Task<IReadOnlyList<StockAdjustmentRecordModel>> GetRecentStockAdjustmentsAsync(int count, long? categoryId = null)
        {
            var items = new List<StockAdjustmentRecordModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                categoryId.HasValue
                    ? """
                      SELECT
                          sa.id,
                          sa.category_id,
                          c.name,
                          sa.previous_quantity,
                          sa.adjusted_quantity,
                          sa.delta_quantity,
                          sa.unit_type,
                          sa.reason,
                          sa.timestamp
                      FROM stock_adjustments sa
                      INNER JOIN categories c ON c.id = sa.category_id
                      WHERE sa.category_id = $categoryId
                      ORDER BY sa.timestamp DESC
                      LIMIT $count;
                      """
                    : """
                      SELECT
                          sa.id,
                          sa.category_id,
                          c.name,
                          sa.previous_quantity,
                          sa.adjusted_quantity,
                          sa.delta_quantity,
                          sa.unit_type,
                          sa.reason,
                          sa.timestamp
                      FROM stock_adjustments sa
                      INNER JOIN categories c ON c.id = sa.category_id
                      ORDER BY sa.timestamp DESC
                      LIMIT $count;
                      """;

            if (categoryId.HasValue)
            {
                command.Parameters.AddWithValue("$categoryId", categoryId.Value);
            }

            command.Parameters.AddWithValue("$count", count);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new StockAdjustmentRecordModel
                {
                    Id = reader.GetInt64(0),
                    CategoryId = reader.GetInt64(1),
                    CategoryName = reader.GetString(2),
                    PreviousQuantity = reader.GetDouble(3),
                    AdjustedQuantity = reader.GetDouble(4),
                    DeltaQuantity = reader.GetDouble(5),
                    UnitType = ParseUnit(reader.GetString(6)),
                    Reason = reader.GetString(7),
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8))
                });
            }

            return items;
        }

        public async Task<IReadOnlyList<StockAuditRecordModel>> GetRecentStockAuditsAsync(int count, long? categoryId = null)
        {
            var items = new List<StockAuditRecordModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                categoryId.HasValue
                    ? """
                      SELECT
                          sa.id,
                          sa.category_id,
                          c.name,
                          sa.system_quantity,
                          sa.actual_quantity,
                          sa.delta_quantity,
                          sa.unit_type,
                          sa.note,
                          sa.timestamp
                      FROM stock_audits sa
                      INNER JOIN categories c ON c.id = sa.category_id
                      WHERE sa.category_id = $categoryId
                      ORDER BY sa.timestamp DESC
                      LIMIT $count;
                      """
                    : """
                      SELECT
                          sa.id,
                          sa.category_id,
                          c.name,
                          sa.system_quantity,
                          sa.actual_quantity,
                          sa.delta_quantity,
                          sa.unit_type,
                          sa.note,
                          sa.timestamp
                      FROM stock_audits sa
                      INNER JOIN categories c ON c.id = sa.category_id
                      ORDER BY sa.timestamp DESC
                      LIMIT $count;
                      """;

            if (categoryId.HasValue)
            {
                command.Parameters.AddWithValue("$categoryId", categoryId.Value);
            }

            command.Parameters.AddWithValue("$count", count);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new StockAuditRecordModel
                {
                    Id = reader.GetInt64(0),
                    CategoryId = reader.GetInt64(1),
                    CategoryName = reader.GetString(2),
                    SystemQuantity = reader.GetDouble(3),
                    ActualQuantity = reader.GetDouble(4),
                    DeltaQuantity = reader.GetDouble(5),
                    UnitType = ParseUnit(reader.GetString(6)),
                    Note = reader.GetString(7),
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8))
                });
            }

            return items;
        }

        public async Task<IReadOnlyList<CustomerModel>> GetCustomersAsync()
        {
            var customers = new List<CustomerModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    id,
                    name,
                    phone,
                    email,
                    address,
                    note,
                    has_inbound_orders,
                    has_outbound_orders,
                    created_at,
                    updated_at
                FROM customers
                ORDER BY name COLLATE NOCASE;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                customers.Add(new CustomerModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Phone = reader.GetString(2),
                    Email = reader.GetString(3),
                    Address = reader.GetString(4),
                    Note = reader.GetString(5),
                    HasInboundOrders = reader.GetInt64(6) == 1,
                    HasOutboundOrders = reader.GetInt64(7) == 1,
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
                    UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9))
                });
            }

            return customers;
        }

        public async Task<CustomerModel?> GetCustomerAsync(long customerId)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    id,
                    name,
                    phone,
                    email,
                    address,
                    note,
                    has_inbound_orders,
                    has_outbound_orders,
                    created_at,
                    updated_at
                FROM customers
                WHERE id = $id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$id", customerId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new CustomerModel
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Phone = reader.GetString(2),
                Email = reader.GetString(3),
                Address = reader.GetString(4),
                Note = reader.GetString(5),
                HasInboundOrders = reader.GetInt64(6) == 1,
                HasOutboundOrders = reader.GetInt64(7) == 1,
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
                UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9))
            };
        }

        public async Task<IReadOnlyList<CustomerModel>> GetInboundCustomersAsync() =>
            await GetCustomersByUsageAsync("has_inbound_orders");

        public async Task<IReadOnlyList<CustomerModel>> GetOutboundCustomersAsync() =>
            await GetCustomersByUsageAsync("has_outbound_orders");

        public async Task<IReadOnlyList<CustomerCategoryPriceModel>> GetCustomerCategoryPricesAsync()
        {
            var memories = new List<CustomerCategoryPriceModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT customer_id, category_id, price, updated_at
                FROM customer_category_prices
                ORDER BY updated_at DESC;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                memories.Add(new CustomerCategoryPriceModel
                {
                    CustomerId = reader.GetInt64(0),
                    CategoryId = reader.GetInt64(1),
                    Price = reader.GetDouble(2),
                    UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3))
                });
            }

            return memories;
        }

        public async Task<IReadOnlyList<InboundRecordModel>> GetRecentInboundRecordsAsync(int count)
        {
            var records = new List<InboundRecordModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    ir.id,
                    ir.category_id,
                    c.name,
                    IFNULL(cu.name, ''),
                    ir.weight,
                    ir.unit_price,
                    ir.total_cost,
                    ir.unit_type,
                    ir.timestamp
                FROM inbound_records ir
                INNER JOIN categories c ON c.id = ir.category_id
                LEFT JOIN orders o ON o.id = ir.order_id
                LEFT JOIN customers cu ON cu.id = o.customer_id
                ORDER BY ir.timestamp DESC
                LIMIT $count;
                """;
            command.Parameters.AddWithValue("$count", count);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(new InboundRecordModel
                {
                    Id = reader.GetInt64(0),
                    CategoryId = reader.GetInt64(1),
                    CategoryName = reader.GetString(2),
                    CustomerName = reader.GetString(3),
                    Quantity = reader.GetDouble(4),
                    UnitPrice = reader.GetDouble(5),
                    TotalCost = reader.GetDouble(6),
                    UnitType = ParseUnit(reader.GetString(7)),
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8))
                });
            }

            return records;
        }

        public async Task<IReadOnlyList<OutboundRecordModel>> GetRecentOutboundRecordsAsync(int count)
        {
            var records = new List<OutboundRecordModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    orr.id,
                    orr.category_id,
                    c.name,
                    IFNULL(cu.name, ''),
                    orr.weight,
                    orr.unit_price,
                    orr.total_revenue,
                    orr.unit_type,
                    orr.timestamp
                FROM outbound_records orr
                INNER JOIN categories c ON c.id = orr.category_id
                LEFT JOIN orders o ON o.id = orr.order_id
                LEFT JOIN customers cu ON cu.id = o.customer_id
                ORDER BY orr.timestamp DESC
                LIMIT $count;
                """;
            command.Parameters.AddWithValue("$count", count);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(new OutboundRecordModel
                {
                    Id = reader.GetInt64(0),
                    CategoryId = reader.GetInt64(1),
                    CategoryName = reader.GetString(2),
                    CustomerName = reader.GetString(3),
                    Quantity = reader.GetDouble(4),
                    UnitPrice = reader.GetDouble(5),
                    TotalRevenue = reader.GetDouble(6),
                    UnitType = ParseUnit(reader.GetString(7)),
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8))
                });
            }

            return records;
        }

        public async Task<IReadOnlyList<OrderListItemModel>> GetRecentOrdersAsync(string? type, int count)
        {
            var orders = new List<OrderListItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    o.id,
                    o.order_number,
                    o.type,
                    IFNULL(c.name, ''),
                    o.total_weight,
                    o.total_amount,
                    o.category_count,
                    o.item_count,
                    o.timestamp
                FROM orders o
                LEFT JOIN customers c ON c.id = o.customer_id
                WHERE ($type IS NULL OR o.type = $type)
                ORDER BY o.timestamp DESC
                LIMIT $count;
                """;
            command.Parameters.AddWithValue("$count", count);

            var typeParameter = command.CreateParameter();
            typeParameter.ParameterName = "$type";
            typeParameter.Value = string.IsNullOrWhiteSpace(type) ? DBNull.Value : type;
            command.Parameters.Add(typeParameter);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                orders.Add(new OrderListItemModel
                {
                    Id = reader.GetInt64(0),
                    OrderNumber = reader.GetString(1),
                    Type = reader.GetString(2),
                    CustomerName = reader.GetString(3),
                    TotalWeight = reader.GetDouble(4),
                    TotalAmount = reader.GetDouble(5),
                    CategoryCount = reader.GetInt32(6),
                    ItemCount = reader.GetInt32(7),
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8))
                });
            }

            return orders;
        }

        public async Task<OrderOverview> GetOrderOverviewAsync()
        {
            var todayRange = GetTodayRange();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    COUNT(*) AS total_count,
                    SUM(CASE WHEN timestamp >= $start AND timestamp < $end THEN 1 ELSE 0 END) AS today_count,
                    SUM(CASE WHEN type = 'inbound' THEN 1 ELSE 0 END) AS inbound_count,
                    SUM(CASE WHEN type = 'outbound' THEN 1 ELSE 0 END) AS outbound_count
                FROM orders;
                """;
            command.Parameters.AddWithValue("$start", todayRange.Start);
            command.Parameters.AddWithValue("$end", todayRange.End);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new OrderOverview
                {
                    TotalOrderCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    TodayOrderCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    InboundOrderCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    OutboundOrderCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                };
            }

            return new OrderOverview();
        }

        public async Task<CustomerOverview> GetCustomerOverviewAsync()
        {
            var todayRange = GetTodayRange();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    COUNT(*) AS total_count,
                    SUM(CASE WHEN has_inbound_orders = 1 THEN 1 ELSE 0 END) AS inbound_count,
                    SUM(CASE WHEN has_outbound_orders = 1 THEN 1 ELSE 0 END) AS outbound_count,
                    (
                        SELECT COUNT(DISTINCT customer_id)
                        FROM orders
                        WHERE customer_id IS NOT NULL
                            AND timestamp >= $start
                            AND timestamp < $end
                    ) AS active_today_count
                FROM customers;
                """;
            command.Parameters.AddWithValue("$start", todayRange.Start);
            command.Parameters.AddWithValue("$end", todayRange.End);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CustomerOverview
                {
                    TotalCustomerCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    InboundCustomerCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    OutboundCustomerCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    ActiveTodayCustomerCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                };
            }

            return new CustomerOverview();
        }

        public async Task<AnalyticsOverview> GetAnalyticsOverviewAsync()
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    (SELECT IFNULL(SUM(total_cost), 0) FROM inbound_records) AS total_inbound_amount,
                    (SELECT IFNULL(SUM(total_revenue), 0) FROM outbound_records) AS total_outbound_amount,
                    (SELECT COUNT(DISTINCT customer_id) FROM orders WHERE customer_id IS NOT NULL) AS active_customer_count,
                    (SELECT COUNT(*) FROM orders) AS total_order_count;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var inboundAmount = reader.IsDBNull(0) ? 0 : reader.GetDouble(0);
                var outboundAmount = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);

                return new AnalyticsOverview
                {
                    TotalInboundAmount = inboundAmount,
                    TotalOutboundAmount = outboundAmount,
                    EstimatedMargin = Math.Round(outboundAmount - inboundAmount, 2),
                    ActiveCustomerCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    TotalOrderCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                };
            }

            return new AnalyticsOverview();
        }

        public async Task<IReadOnlyList<DailyAnalyticsItemModel>> GetDailyAnalyticsAsync(int days)
        {
            var items = new Dictionary<DateOnly, DailyAnalyticsItemModel>();
            var today = DateTimeOffset.Now.Date;

            for (var offset = days - 1; offset >= 0; offset--)
            {
                var date = today.AddDays(-offset);
                items[DateOnly.FromDateTime(date)] = new DailyAnalyticsItemModel
                {
                    Date = date,
                    InboundAmount = 0,
                    OutboundAmount = 0
                };
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await FillDailyAnalyticsAsync(connection, "inbound_records", "total_cost", days, items, isInbound: true);
            await FillDailyAnalyticsAsync(connection, "outbound_records", "total_revenue", days, items, isInbound: false);

            return items.Values.OrderByDescending(item => item.Date).ToList();
        }

        public async Task<IReadOnlyList<CategoryAnalyticsItemModel>> GetTopInboundCategoriesAnalyticsAsync(int count) =>
            await GetTopCategoryAnalyticsAsync("inbound_records", "total_cost", count);

        public async Task<IReadOnlyList<CategoryAnalyticsItemModel>> GetTopOutboundCategoriesAnalyticsAsync(int count) =>
            await GetTopCategoryAnalyticsAsync("outbound_records", "total_revenue", count);

        public async Task<IReadOnlyList<CustomerAnalyticsItemModel>> GetTopCustomersAnalyticsAsync(int count)
        {
            var customers = new List<CustomerAnalyticsItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    c.id,
                    c.name,
                    COUNT(o.id) AS order_count,
                    IFNULL(SUM(o.total_amount), 0) AS total_amount,
                    MAX(o.timestamp) AS last_transaction_at
                FROM customers c
                INNER JOIN orders o ON o.customer_id = c.id
                GROUP BY c.id, c.name
                ORDER BY total_amount DESC, order_count DESC, c.name COLLATE NOCASE
                LIMIT $count;
                """;
            command.Parameters.AddWithValue("$count", count);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                customers.Add(new CustomerAnalyticsItemModel
                {
                    CustomerId = reader.GetInt64(0),
                    CustomerName = reader.GetString(1),
                    OrderCount = reader.GetInt32(2),
                    TotalAmount = reader.GetDouble(3),
                    LastTransactionAt = reader.IsDBNull(4)
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))
                });
            }

            return customers;
        }

        public async Task<IReadOnlyList<CustomerListItemModel>> GetCustomerListItemsAsync()
        {
            var customers = new List<CustomerListItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    c.id,
                    c.name,
                    c.has_inbound_orders,
                    c.has_outbound_orders,
                    COUNT(o.id) AS total_orders,
                    SUM(CASE WHEN o.type = 'inbound' THEN 1 ELSE 0 END) AS inbound_orders,
                    SUM(CASE WHEN o.type = 'outbound' THEN 1 ELSE 0 END) AS outbound_orders,
                    MAX(o.timestamp) AS last_transaction_at
                FROM customers c
                LEFT JOIN orders o ON o.customer_id = c.id
                GROUP BY c.id, c.name, c.has_inbound_orders, c.has_outbound_orders
                ORDER BY last_transaction_at DESC, c.name COLLATE NOCASE;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                customers.Add(new CustomerListItemModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    HasInboundOrders = reader.GetInt64(2) == 1,
                    HasOutboundOrders = reader.GetInt64(3) == 1,
                    TotalOrderCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    InboundOrderCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    OutboundOrderCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    LastTransactionAt = reader.IsDBNull(7)
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7))
                });
            }

            return customers;
        }

        public async Task<CustomerDetailSummary> GetCustomerDetailSummaryAsync(long customerId)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    COUNT(*) AS total_orders,
                    SUM(CASE WHEN type = 'inbound' THEN 1 ELSE 0 END) AS inbound_orders,
                    SUM(CASE WHEN type = 'outbound' THEN 1 ELSE 0 END) AS outbound_orders,
                    SUM(CASE WHEN type = 'inbound' THEN total_amount ELSE 0 END) AS inbound_amount,
                    SUM(CASE WHEN type = 'outbound' THEN total_amount ELSE 0 END) AS outbound_amount,
                    MAX(timestamp) AS last_transaction_at
                FROM orders
                WHERE customer_id = $customerId;
                """;
            command.Parameters.AddWithValue("$customerId", customerId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CustomerDetailSummary
                {
                    TotalOrderCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    InboundOrderCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    OutboundOrderCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    InboundAmount = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    OutboundAmount = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                    LastTransactionAt = reader.IsDBNull(5)
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5))
                };
            }

            return new CustomerDetailSummary();
        }

        public async Task<IReadOnlyList<OrderListItemModel>> GetCustomerRecentOrdersAsync(long customerId, int count)
        {
            var orders = new List<OrderListItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    o.id,
                    o.order_number,
                    o.type,
                    IFNULL(c.name, ''),
                    o.total_weight,
                    o.total_amount,
                    o.category_count,
                    o.item_count,
                    o.timestamp
                FROM orders o
                LEFT JOIN customers c ON c.id = o.customer_id
                WHERE o.customer_id = $customerId
                ORDER BY o.timestamp DESC
                LIMIT $count;
                """;
            command.Parameters.AddWithValue("$customerId", customerId);
            command.Parameters.AddWithValue("$count", count);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                orders.Add(new OrderListItemModel
                {
                    Id = reader.GetInt64(0),
                    OrderNumber = reader.GetString(1),
                    Type = reader.GetString(2),
                    CustomerName = reader.GetString(3),
                    TotalWeight = reader.GetDouble(4),
                    TotalAmount = reader.GetDouble(5),
                    CategoryCount = reader.GetInt32(6),
                    ItemCount = reader.GetInt32(7),
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8))
                });
            }

            return orders;
        }

        public async Task<IReadOnlyList<OrderDetailItemModel>> GetOrderDetailItemsAsync(long orderId)
        {
            var items = new List<OrderDetailItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT category_id, category_name, quantity, unit_price, line_amount, unit_type
                FROM (
                    SELECT
                        ir.category_id AS category_id,
                        c.name AS category_name,
                        ir.weight AS quantity,
                        ir.unit_price AS unit_price,
                        ir.total_cost AS line_amount,
                        ir.unit_type AS unit_type
                    FROM inbound_records ir
                    INNER JOIN categories c ON c.id = ir.category_id
                    WHERE ir.order_id = $orderId

                    UNION ALL

                    SELECT
                        orr.category_id AS category_id,
                        c.name AS category_name,
                        orr.weight AS quantity,
                        orr.unit_price AS unit_price,
                        orr.total_revenue AS line_amount,
                        orr.unit_type AS unit_type
                    FROM outbound_records orr
                    INNER JOIN categories c ON c.id = orr.category_id
                    WHERE orr.order_id = $orderId
                )
                ORDER BY category_name COLLATE NOCASE;
                """;
            command.Parameters.AddWithValue("$orderId", orderId);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new OrderDetailItemModel
                {
                    CategoryId = reader.GetInt64(0),
                    CategoryName = reader.GetString(1),
                    Quantity = reader.GetDouble(2),
                    UnitPrice = reader.GetDouble(3),
                    LineAmount = reader.GetDouble(4),
                    UnitType = ParseUnit(reader.GetString(5))
                });
            }

            return items;
        }

        public async Task<OrderEditModel> GetOrderEditModelAsync(long orderId)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    o.id,
                    o.order_number,
                    o.type,
                    o.customer_id,
                    IFNULL(c.name, ''),
                    d.category_id,
                    d.quantity,
                    d.unit_price,
                    d.unit_type,
                    o.timestamp
                FROM orders o
                LEFT JOIN customers c ON c.id = o.customer_id
                INNER JOIN (
                    SELECT
                        ir.order_id,
                        ir.category_id,
                        ir.weight AS quantity,
                        ir.unit_price,
                        ir.unit_type
                    FROM inbound_records ir

                    UNION ALL

                    SELECT
                        orr.order_id,
                        orr.category_id,
                        orr.weight AS quantity,
                        orr.unit_price,
                        orr.unit_type
                    FROM outbound_records orr
                ) d ON d.order_id = o.id
                WHERE o.id = $orderId;
                """;
            command.Parameters.AddWithValue("$orderId", orderId);

            var records = new List<OrderEditModel>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(new OrderEditModel
                {
                    OrderId = reader.GetInt64(0),
                    OrderNumber = reader.GetString(1),
                    Type = reader.GetString(2),
                    CustomerId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    CustomerName = reader.GetString(4),
                    CategoryId = reader.GetInt64(5),
                    Quantity = reader.GetDouble(6),
                    UnitPrice = reader.GetDouble(7),
                    UnitType = ParseUnit(reader.GetString(8)),
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9))
                });
            }

            if (records.Count == 0)
            {
                throw new InvalidOperationException("\u672A\u627E\u5230\u8981\u7F16\u8F91\u7684\u8BA2\u5355\u3002");
            }

            if (records.Count > 1)
            {
                throw new InvalidOperationException("\u5F53\u524D\u53EA\u652F\u6301\u7F16\u8F91\u5355\u6761\u660E\u7EC6\u7684\u8BA2\u5355\u3002");
            }

            return records[0];
        }

        public async Task UpdateOrderAsync(OrderEditModel order)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var originalOrder = await GetOrderEditModelAsync(connection, transaction, order.OrderId);
            var newCategory = await GetCategoryAsync(connection, transaction, order.CategoryId);
            var normalizedQuantity = NormalizeQuantity(order.Quantity, newCategory.UnitType);

            if (normalizedQuantity <= 0)
            {
                throw new InvalidOperationException("\u8BA2\u5355\u6570\u91CF\u5FC5\u987B\u5927\u4E8E 0\u3002");
            }

            if (order.UnitPrice <= 0)
            {
                throw new InvalidOperationException("\u8BA2\u5355\u5355\u4EF7\u5FC5\u987B\u5927\u4E8E 0\u3002");
            }

            if (originalOrder.IsOutbound)
            {
                var availableStock = GetAvailableStock(
                    newCategory.Stock,
                    newCategory.StockInJin,
                    newCategory.StockInPieces,
                    newCategory.UnitType);

                if (newCategory.Id == originalOrder.CategoryId)
                {
                    availableStock += originalOrder.Quantity;
                }

                if (normalizedQuantity > availableStock + 0.0001)
                {
                    throw new InvalidOperationException("\u4FEE\u6539\u540E\u5E93\u5B58\u4E0D\u8DB3\u3002");
                }
            }

            var customerName = order.CustomerName.Trim();
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long? customerId = null;

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                customerId = await GetOrCreateCustomerAsync(
                    connection,
                    transaction,
                    customerName,
                    now,
                    markInbound: !originalOrder.IsOutbound,
                    markOutbound: originalOrder.IsOutbound);
            }

            var deleteInboundCommand = connection.CreateCommand();
            deleteInboundCommand.Transaction = transaction;
            deleteInboundCommand.CommandText = "DELETE FROM inbound_records WHERE order_id = $orderId;";
            deleteInboundCommand.Parameters.AddWithValue("$orderId", order.OrderId);
            await deleteInboundCommand.ExecuteNonQueryAsync();

            var deleteOutboundCommand = connection.CreateCommand();
            deleteOutboundCommand.Transaction = transaction;
            deleteOutboundCommand.CommandText = "DELETE FROM outbound_records WHERE order_id = $orderId;";
            deleteOutboundCommand.Parameters.AddWithValue("$orderId", order.OrderId);
            await deleteOutboundCommand.ExecuteNonQueryAsync();

            var totalAmount = Math.Round(normalizedQuantity * order.UnitPrice, 2);

            var updateOrderCommand = connection.CreateCommand();
            updateOrderCommand.Transaction = transaction;
            updateOrderCommand.CommandText =
                """
                UPDATE orders
                SET
                    total_weight = $totalWeight,
                    total_amount = $totalAmount,
                    category_count = 1,
                    item_count = 1,
                    customer_id = $customerId
                WHERE id = $orderId;
                """;
            updateOrderCommand.Parameters.AddWithValue("$orderId", order.OrderId);
            updateOrderCommand.Parameters.AddWithValue("$totalWeight", normalizedQuantity);
            updateOrderCommand.Parameters.AddWithValue("$totalAmount", totalAmount);
            var customerParameter = updateOrderCommand.CreateParameter();
            customerParameter.ParameterName = "$customerId";
            customerParameter.Value = customerId.HasValue ? customerId.Value : DBNull.Value;
            updateOrderCommand.Parameters.Add(customerParameter);
            await updateOrderCommand.ExecuteNonQueryAsync();

            var insertRecordCommand = connection.CreateCommand();
            insertRecordCommand.Transaction = transaction;

            if (originalOrder.IsOutbound)
            {
                insertRecordCommand.CommandText =
                    """
                    INSERT INTO outbound_records (
                        order_id, category_id, weight, unit_price, total_revenue, unit_type, timestamp
                    )
                    VALUES (
                        $orderId, $categoryId, $weight, $unitPrice, $totalAmount, $unitType, $timestamp
                    );
                    """;
            }
            else
            {
                insertRecordCommand.CommandText =
                    """
                    INSERT INTO inbound_records (
                        order_id, category_id, weight, unit_price, total_cost, unit_type, timestamp
                    )
                    VALUES (
                        $orderId, $categoryId, $weight, $unitPrice, $totalAmount, $unitType, $timestamp
                    );
                    """;
            }

            insertRecordCommand.Parameters.AddWithValue("$orderId", order.OrderId);
            insertRecordCommand.Parameters.AddWithValue("$categoryId", newCategory.Id);
            insertRecordCommand.Parameters.AddWithValue("$weight", normalizedQuantity);
            insertRecordCommand.Parameters.AddWithValue("$unitPrice", order.UnitPrice);
            insertRecordCommand.Parameters.AddWithValue("$totalAmount", totalAmount);
            insertRecordCommand.Parameters.AddWithValue("$unitType", newCategory.UnitType.ToString());
            insertRecordCommand.Parameters.AddWithValue("$timestamp", order.Timestamp.ToUnixTimeMilliseconds());
            await insertRecordCommand.ExecuteNonQueryAsync();

            await RebuildAllCategoryDataAsync(connection, transaction);

            var affectedCustomerIds = new HashSet<long>();
            if (originalOrder.CustomerId.HasValue)
            {
                affectedCustomerIds.Add(originalOrder.CustomerId.Value);
            }

            if (customerId.HasValue)
            {
                affectedCustomerIds.Add(customerId.Value);
            }

            foreach (var affectedCustomerId in affectedCustomerIds)
            {
                await UpdateCustomerUsageFlagsAsync(connection, transaction, affectedCustomerId);
                await RebuildCustomerPriceMemoriesAsync(connection, transaction, affectedCustomerId);
            }

            await transaction.CommitAsync();
        }

        public async Task<IReadOnlyList<CustomerPriceMemoryItemModel>> GetCustomerPriceMemoriesAsync(long customerId)
        {
            var memories = new List<CustomerPriceMemoryItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    p.category_id,
                    c.name,
                    p.price,
                    p.updated_at
                FROM customer_category_prices p
                INNER JOIN categories c ON c.id = p.category_id
                WHERE p.customer_id = $customerId
                ORDER BY p.updated_at DESC, c.name COLLATE NOCASE;
                """;
            command.Parameters.AddWithValue("$customerId", customerId);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                memories.Add(new CustomerPriceMemoryItemModel
                {
                    CategoryId = reader.GetInt64(0),
                    CategoryName = reader.GetString(1),
                    Price = reader.GetDouble(2),
                    UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3))
                });
            }

            return memories;
        }

        public async Task<long> SaveCustomerAsync(CustomerModel customer)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var normalizedName = customer.Name.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidOperationException("\u5BA2\u6237\u540D\u79F0\u4E0D\u80FD\u4E3A\u7A7A\u3002");
            }

            await EnsureCustomerNameAvailableAsync(connection, transaction, customer.Id, normalizedName);

            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long customerId;

            if (customer.Id == 0)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO customers (
                        name, phone, email, address, note, has_inbound_orders, has_outbound_orders, created_at, updated_at
                    )
                    VALUES (
                        $name, $phone, $email, $address, $note, 0, 0, $createdAt, $updatedAt
                    );
                    """;
                command.Parameters.AddWithValue("$name", normalizedName);
                command.Parameters.AddWithValue("$phone", customer.Phone.Trim());
                command.Parameters.AddWithValue("$email", customer.Email.Trim());
                command.Parameters.AddWithValue("$address", customer.Address.Trim());
                command.Parameters.AddWithValue("$note", customer.Note.Trim());
                command.Parameters.AddWithValue("$createdAt", now);
                command.Parameters.AddWithValue("$updatedAt", now);
                await command.ExecuteNonQueryAsync();

                customerId = await GetLastInsertRowIdAsync(connection, transaction);
            }
            else
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    UPDATE customers
                    SET
                        name = $name,
                        phone = $phone,
                        email = $email,
                        address = $address,
                        note = $note,
                        updated_at = $updatedAt
                    WHERE id = $id;
                    """;
                command.Parameters.AddWithValue("$id", customer.Id);
                command.Parameters.AddWithValue("$name", normalizedName);
                command.Parameters.AddWithValue("$phone", customer.Phone.Trim());
                command.Parameters.AddWithValue("$email", customer.Email.Trim());
                command.Parameters.AddWithValue("$address", customer.Address.Trim());
                command.Parameters.AddWithValue("$note", customer.Note.Trim());
                command.Parameters.AddWithValue("$updatedAt", now);
                await command.ExecuteNonQueryAsync();

                customerId = customer.Id;
            }

            await transaction.CommitAsync();
            return customerId;
        }

        public async Task DeleteOrderAsync(long orderId)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var orderCommand = connection.CreateCommand();
            orderCommand.Transaction = transaction;
            orderCommand.CommandText =
                """
                SELECT customer_id
                FROM orders
                WHERE id = $orderId
                LIMIT 1;
                """;
            orderCommand.Parameters.AddWithValue("$orderId", orderId);

            var customerIdValue = await orderCommand.ExecuteScalarAsync();
            if (customerIdValue is null)
            {
                throw new InvalidOperationException("\u672A\u627E\u5230\u8981\u5220\u9664\u7684\u8BA2\u5355\u3002");
            }

            var deleteInboundCommand = connection.CreateCommand();
            deleteInboundCommand.Transaction = transaction;
            deleteInboundCommand.CommandText = "DELETE FROM inbound_records WHERE order_id = $orderId;";
            deleteInboundCommand.Parameters.AddWithValue("$orderId", orderId);
            await deleteInboundCommand.ExecuteNonQueryAsync();

            var deleteOutboundCommand = connection.CreateCommand();
            deleteOutboundCommand.Transaction = transaction;
            deleteOutboundCommand.CommandText = "DELETE FROM outbound_records WHERE order_id = $orderId;";
            deleteOutboundCommand.Parameters.AddWithValue("$orderId", orderId);
            await deleteOutboundCommand.ExecuteNonQueryAsync();

            var deleteOrderCommand = connection.CreateCommand();
            deleteOrderCommand.Transaction = transaction;
            deleteOrderCommand.CommandText = "DELETE FROM orders WHERE id = $orderId;";
            deleteOrderCommand.Parameters.AddWithValue("$orderId", orderId);
            await deleteOrderCommand.ExecuteNonQueryAsync();

            await RebuildAllCategoryDataAsync(connection, transaction);

            if (customerIdValue is not DBNull)
            {
                var customerId = Convert.ToInt64(customerIdValue, CultureInfo.InvariantCulture);
                await UpdateCustomerUsageFlagsAsync(connection, transaction, customerId);
                await RebuildCustomerPriceMemoriesAsync(connection, transaction, customerId);
            }

            await transaction.CommitAsync();
        }

        public async Task SaveCategoryAsync(CategoryModel category)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var command = connection.CreateCommand();

            if (category.Id == 0)
            {
                command.CommandText =
                    """
                    INSERT INTO categories (
                        name, buy_price, sell_price, stock, stock_in_jin, stock_in_pieces, unit_type, is_archived, created_at, updated_at
                    )
                    VALUES (
                        $name, $buyPrice, $sellPrice, $stock, $stockInJin, $stockInPieces, $unitType, $isArchived, $createdAt, $updatedAt
                    );
                    """;
                command.Parameters.AddWithValue("$createdAt", now);
            }
            else
            {
                command.CommandText =
                    """
                    UPDATE categories
                    SET
                        name = $name,
                        buy_price = $buyPrice,
                        sell_price = $sellPrice,
                        stock = $stock,
                        stock_in_jin = $stockInJin,
                        stock_in_pieces = $stockInPieces,
                        unit_type = $unitType,
                        is_archived = $isArchived,
                        updated_at = $updatedAt
                    WHERE id = $id;
                    """;
                command.Parameters.AddWithValue("$id", category.Id);
            }

            command.Parameters.AddWithValue("$name", category.Name.Trim());
            command.Parameters.AddWithValue("$buyPrice", category.BuyPrice);
            command.Parameters.AddWithValue("$sellPrice", category.SellPrice);
            command.Parameters.AddWithValue("$stock", category.Stock);
            command.Parameters.AddWithValue("$stockInJin", category.StockInJin);
            command.Parameters.AddWithValue("$stockInPieces", category.StockInPieces);
            command.Parameters.AddWithValue("$unitType", category.UnitType.ToString());
            command.Parameters.AddWithValue("$isArchived", category.IsArchived ? 1 : 0);
            command.Parameters.AddWithValue("$updatedAt", now);

            await command.ExecuteNonQueryAsync();
        }

        public async Task SetCategoryArchivedAsync(long categoryId, bool isArchived)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE categories
                SET
                    is_archived = $isArchived,
                    updated_at = $updatedAt
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", categoryId);
            command.Parameters.AddWithValue("$isArchived", isArchived ? 1 : 0);
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync();
        }

        public async Task AdjustCategoryStockAsync(long categoryId, double adjustedQuantity, string? reason)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var category = await GetCategoryAsync(connection, transaction, categoryId);
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            await ApplyStockMutationAsync(connection, transaction, category, adjustedQuantity, reason, now, allowSameQuantity: false);

            await transaction.CommitAsync();
        }

        public async Task CreateStockAuditAsync(long categoryId, double actualQuantity, string? note)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var category = await GetCategoryAsync(connection, transaction, categoryId);
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var noteText = note?.Trim() ?? string.Empty;
            var mutation = await ApplyStockMutationAsync(
                connection,
                transaction,
                category,
                actualQuantity,
                string.IsNullOrWhiteSpace(noteText) ? "\u76D8\u70B9\u6821\u6B63" : $"\u76D8\u70B9\u6821\u6B63\uFF1A{noteText}",
                now,
                allowSameQuantity: true);

            var auditCommand = connection.CreateCommand();
            auditCommand.Transaction = transaction;
            auditCommand.CommandText =
                """
                INSERT INTO stock_audits (
                    category_id,
                    system_quantity,
                    actual_quantity,
                    delta_quantity,
                    unit_type,
                    note,
                    timestamp
                )
                VALUES (
                    $categoryId,
                    $systemQuantity,
                    $actualQuantity,
                    $deltaQuantity,
                    $unitType,
                    $note,
                    $timestamp
                );
                """;
            auditCommand.Parameters.AddWithValue("$categoryId", categoryId);
            auditCommand.Parameters.AddWithValue("$systemQuantity", mutation.PreviousQuantity);
            auditCommand.Parameters.AddWithValue("$actualQuantity", mutation.AdjustedQuantity);
            auditCommand.Parameters.AddWithValue("$deltaQuantity", mutation.DeltaQuantity);
            auditCommand.Parameters.AddWithValue("$unitType", category.UnitType.ToString());
            auditCommand.Parameters.AddWithValue("$note", noteText);
            auditCommand.Parameters.AddWithValue("$timestamp", now);
            await auditCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }

        public async Task AddInboundRecordAsync(long categoryId, double quantity, double unitPrice, string? customerName)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var categoryCommand = connection.CreateCommand();
            categoryCommand.Transaction = transaction;
            categoryCommand.CommandText =
                """
                SELECT stock, stock_in_jin, stock_in_pieces, unit_type
                FROM categories
                WHERE id = $id;
                """;
            categoryCommand.Parameters.AddWithValue("$id", categoryId);

            double stock;
            double stockInJin;
            int stockInPieces;
            WeightUnit unitType;

            await using (var reader = await categoryCommand.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    throw new InvalidOperationException("\u672A\u627E\u5230\u8981\u5165\u5E93\u7684\u5206\u7C7B\u3002");
                }

                stock = reader.GetDouble(0);
                stockInJin = reader.GetDouble(1);
                stockInPieces = reader.GetInt32(2);
                unitType = ParseUnit(reader.GetString(3));
            }

            var normalizedQuantity = unitType == WeightUnit.Piece
                ? Math.Round(quantity)
                : Math.Round(quantity, 2);

            var totalCost = Math.Round(normalizedQuantity * unitPrice, 2);
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var normalizedCustomerName = customerName?.Trim() ?? string.Empty;

            switch (unitType)
            {
                case WeightUnit.Kilogram:
                    stock += normalizedQuantity;
                    break;
                case WeightUnit.Jin:
                    stockInJin += normalizedQuantity;
                    break;
                case WeightUnit.Piece:
                    stockInPieces += (int)Math.Round(normalizedQuantity);
                    break;
            }

            var updateCategoryCommand = connection.CreateCommand();
            updateCategoryCommand.Transaction = transaction;
            updateCategoryCommand.CommandText =
                """
                UPDATE categories
                SET
                    buy_price = $buyPrice,
                    stock = $stock,
                    stock_in_jin = $stockInJin,
                    stock_in_pieces = $stockInPieces,
                    updated_at = $updatedAt
                WHERE id = $id;
                """;
            updateCategoryCommand.Parameters.AddWithValue("$id", categoryId);
            updateCategoryCommand.Parameters.AddWithValue("$buyPrice", unitPrice);
            updateCategoryCommand.Parameters.AddWithValue("$stock", stock);
            updateCategoryCommand.Parameters.AddWithValue("$stockInJin", stockInJin);
            updateCategoryCommand.Parameters.AddWithValue("$stockInPieces", stockInPieces);
            updateCategoryCommand.Parameters.AddWithValue("$updatedAt", now);
            await updateCategoryCommand.ExecuteNonQueryAsync();

            long? customerId = null;
            if (!string.IsNullOrWhiteSpace(normalizedCustomerName))
            {
                customerId = await GetOrCreateCustomerAsync(connection, transaction, normalizedCustomerName, now, markInbound: true, markOutbound: false);
                await UpsertCustomerCategoryPriceAsync(connection, transaction, customerId.Value, categoryId, unitPrice, now);
            }

            var insertRecordCommand = connection.CreateCommand();
            insertRecordCommand.Transaction = transaction;
            insertRecordCommand.CommandText =
                """
                INSERT INTO inbound_records (
                    order_id, category_id, weight, unit_price, total_cost, unit_type, timestamp
                )
                VALUES (
                    $orderId, $categoryId, $weight, $unitPrice, $totalCost, $unitType, $timestamp
                );
                """;
            var orderId = await InsertOrderAsync(connection, transaction, "inbound", customerId, normalizedQuantity, totalCost, unitType, now);
            insertRecordCommand.Parameters.AddWithValue("$orderId", orderId);
            insertRecordCommand.Parameters.AddWithValue("$categoryId", categoryId);
            insertRecordCommand.Parameters.AddWithValue("$weight", normalizedQuantity);
            insertRecordCommand.Parameters.AddWithValue("$unitPrice", unitPrice);
            insertRecordCommand.Parameters.AddWithValue("$totalCost", totalCost);
            insertRecordCommand.Parameters.AddWithValue("$unitType", unitType.ToString());
            insertRecordCommand.Parameters.AddWithValue("$timestamp", now);
            await insertRecordCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }

        public async Task AddOutboundRecordAsync(long categoryId, double quantity, double unitPrice, string? customerName)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var categoryCommand = connection.CreateCommand();
            categoryCommand.Transaction = transaction;
            categoryCommand.CommandText =
                """
                SELECT stock, stock_in_jin, stock_in_pieces, unit_type
                FROM categories
                WHERE id = $id;
                """;
            categoryCommand.Parameters.AddWithValue("$id", categoryId);

            double stock;
            double stockInJin;
            int stockInPieces;
            WeightUnit unitType;

            await using (var reader = await categoryCommand.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    throw new InvalidOperationException("\u672A\u627E\u5230\u8981\u51FA\u5E93\u7684\u5206\u7C7B\u3002");
                }

                stock = reader.GetDouble(0);
                stockInJin = reader.GetDouble(1);
                stockInPieces = reader.GetInt32(2);
                unitType = ParseUnit(reader.GetString(3));
            }

            var normalizedQuantity = NormalizeQuantity(quantity, unitType);
            if (normalizedQuantity <= 0)
            {
                throw new InvalidOperationException("\u51FA\u5E93\u6570\u91CF\u5FC5\u987B\u5927\u4E8E 0\u3002");
            }

            var availableStock = GetAvailableStock(stock, stockInJin, stockInPieces, unitType);
            if (normalizedQuantity > availableStock + 0.0001)
            {
                throw new InvalidOperationException("\u51FA\u5E93\u5931\u8D25\uff0c\u5F53\u524D\u5E93\u5B58\u4E0D\u8DB3\u3002");
            }

            var totalRevenue = Math.Round(normalizedQuantity * unitPrice, 2);
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var normalizedCustomerName = customerName?.Trim() ?? string.Empty;

            DeductStock(normalizedQuantity, unitType, ref stock, ref stockInJin, ref stockInPieces);

            var updateCategoryCommand = connection.CreateCommand();
            updateCategoryCommand.Transaction = transaction;
            updateCategoryCommand.CommandText =
                """
                UPDATE categories
                SET
                    sell_price = $sellPrice,
                    stock = $stock,
                    stock_in_jin = $stockInJin,
                    stock_in_pieces = $stockInPieces,
                    updated_at = $updatedAt
                WHERE id = $id;
                """;
            updateCategoryCommand.Parameters.AddWithValue("$id", categoryId);
            updateCategoryCommand.Parameters.AddWithValue("$sellPrice", unitPrice);
            updateCategoryCommand.Parameters.AddWithValue("$stock", stock);
            updateCategoryCommand.Parameters.AddWithValue("$stockInJin", stockInJin);
            updateCategoryCommand.Parameters.AddWithValue("$stockInPieces", stockInPieces);
            updateCategoryCommand.Parameters.AddWithValue("$updatedAt", now);
            await updateCategoryCommand.ExecuteNonQueryAsync();

            long? customerId = null;
            if (!string.IsNullOrWhiteSpace(normalizedCustomerName))
            {
                customerId = await GetOrCreateCustomerAsync(connection, transaction, normalizedCustomerName, now, markInbound: false, markOutbound: true);
                await UpsertCustomerCategoryPriceAsync(connection, transaction, customerId.Value, categoryId, unitPrice, now);
            }

            var orderId = await InsertOrderAsync(connection, transaction, "outbound", customerId, normalizedQuantity, totalRevenue, unitType, now);

            var insertRecordCommand = connection.CreateCommand();
            insertRecordCommand.Transaction = transaction;
            insertRecordCommand.CommandText =
                """
                INSERT INTO outbound_records (
                    order_id, category_id, weight, unit_price, total_revenue, unit_type, timestamp
                )
                VALUES (
                    $orderId, $categoryId, $weight, $unitPrice, $totalRevenue, $unitType, $timestamp
                );
                """;
            insertRecordCommand.Parameters.AddWithValue("$orderId", orderId);
            insertRecordCommand.Parameters.AddWithValue("$categoryId", categoryId);
            insertRecordCommand.Parameters.AddWithValue("$weight", normalizedQuantity);
            insertRecordCommand.Parameters.AddWithValue("$unitPrice", unitPrice);
            insertRecordCommand.Parameters.AddWithValue("$totalRevenue", totalRevenue);
            insertRecordCommand.Parameters.AddWithValue("$unitType", unitType.ToString());
            insertRecordCommand.Parameters.AddWithValue("$timestamp", now);
            await insertRecordCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }

        public async Task DeleteCategoryAsync(long categoryId)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var guardCommand = connection.CreateCommand();
            guardCommand.CommandText =
                """
                SELECT
                    stock,
                    stock_in_jin,
                    stock_in_pieces,
                    (
                        SELECT COUNT(*)
                        FROM inbound_records
                        WHERE category_id = $id
                    ) + (
                        SELECT COUNT(*)
                        FROM outbound_records
                        WHERE category_id = $id
                    ) + (
                        SELECT COUNT(*)
                        FROM stock_adjustments
                        WHERE category_id = $id
                    ) + (
                        SELECT COUNT(*)
                        FROM stock_audits
                        WHERE category_id = $id
                    ) AS history_count
                FROM categories
                WHERE id = $id
                LIMIT 1;
                """;
            guardCommand.Parameters.AddWithValue("$id", categoryId);

            await using (var reader = await guardCommand.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    throw new InvalidOperationException("\u672A\u627E\u5230\u8981\u5220\u9664\u7684\u5206\u7C7B\u3002");
                }

                var hasStock = reader.GetDouble(0) > 0.0001 || reader.GetDouble(1) > 0.0001 || reader.GetInt32(2) > 0;
                var historyCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                if (hasStock || historyCount > 0)
                {
                    throw new InvalidOperationException("\u5F53\u524D\u5206\u7C7B\u8FD8\u6709\u5E93\u5B58\u6216\u5386\u53F2\u6D41\u6C34\uff0C\u4E0D\u80FD\u76F4\u63A5\u5220\u9664\uff0C\u8BF7\u6539\u7528\u201C\u5F52\u6863\u201D\u3002");
                }
            }

            var deletePriceMemoryCommand = connection.CreateCommand();
            deletePriceMemoryCommand.CommandText = "DELETE FROM customer_category_prices WHERE category_id = $id;";
            deletePriceMemoryCommand.Parameters.AddWithValue("$id", categoryId);
            await deletePriceMemoryCommand.ExecuteNonQueryAsync();

            var deleteAdjustmentCommand = connection.CreateCommand();
            deleteAdjustmentCommand.CommandText = "DELETE FROM stock_adjustments WHERE category_id = $id;";
            deleteAdjustmentCommand.Parameters.AddWithValue("$id", categoryId);
            await deleteAdjustmentCommand.ExecuteNonQueryAsync();

            var deleteAuditCommand = connection.CreateCommand();
            deleteAuditCommand.CommandText = "DELETE FROM stock_audits WHERE category_id = $id;";
            deleteAuditCommand.Parameters.AddWithValue("$id", categoryId);
            await deleteAuditCommand.ExecuteNonQueryAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM categories WHERE id = $id;";
            command.Parameters.AddWithValue("$id", categoryId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            var categories = await GetCategoriesAsync();
            var todayRange = GetTodayRange();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var todayInboundAmount = await GetAmountSumAsync(connection, "inbound_records", "total_cost", todayRange.Start, todayRange.End);
            var todayOutboundAmount = await GetAmountSumAsync(connection, "outbound_records", "total_revenue", todayRange.Start, todayRange.End);

            return new DashboardSummary
            {
                CategoryCount = categories.Count,
                LowStockCount = categories.Count(category => category.IsLowStock),
                TotalInventoryCost = Math.Round(categories.Sum(category => category.InventoryCost), 2),
                ForecastRevenue = Math.Round(categories.Sum(category => category.ForecastRevenue), 2),
                TodayInboundAmount = todayInboundAmount,
                TodayOutboundAmount = todayOutboundAmount
            };
        }

        public async Task<ConsistencyCheckReportModel> RunConsistencyCheckAsync()
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var issues = new List<ConsistencyCheckIssueModel>();
            var checkItems = new List<ConsistencyCheckItemModel>();

            var duplicateMasterDataIssues = await CollectDuplicateMasterDataIssuesAsync(connection);
            issues.AddRange(duplicateMasterDataIssues);
            checkItems.Add(CreateCheckItem(
                "主数据唯一性",
                duplicateMasterDataIssues.Count,
                "检查分类和客户是否存在同名记录，避免按名称匹配时出现歧义。"));

            var orderIntegrityIssues = await CollectOrderIntegrityIssuesAsync(connection);
            issues.AddRange(orderIntegrityIssues);
            checkItems.Add(CreateCheckItem(
                "订单明细关系",
                orderIntegrityIssues.Count,
                "检查订单类型、明细条数、数量、金额和件数统计是否互相匹配。"));

            var customerUsageIssues = await CollectCustomerUsageFlagIssuesAsync(connection);
            issues.AddRange(customerUsageIssues);
            checkItems.Add(CreateCheckItem(
                "客户使用标记",
                customerUsageIssues.Count,
                "检查客户的入库/出库标记是否和实际订单历史一致。"));

            var customerPriceMemoryIssues = await CollectCustomerPriceMemoryIssuesAsync(connection);
            issues.AddRange(customerPriceMemoryIssues);
            checkItems.Add(CreateCheckItem(
                "客户价格记忆",
                customerPriceMemoryIssues.Count,
                "检查价格记忆是否重复、缺失、孤立或没有跟上最近一笔交易。"));

            var categoryUnitIssues = await CollectCategoryUnitConsistencyIssuesAsync(connection);
            issues.AddRange(categoryUnitIssues);
            checkItems.Add(CreateCheckItem(
                "分类单位一致性",
                categoryUnitIssues.Count,
                "检查分类当前单位是否和历史入库/出库记录里的单位一致。"));

            var stockFieldIssues = await CollectCategoryStockFieldIssuesAsync(connection);
            issues.AddRange(stockFieldIssues);
            checkItems.Add(CreateCheckItem(
                "库存字段合法性",
                stockFieldIssues.Count,
                "检查库存字段是否出现负数，或在非当前单位列里残留数量。"));

            var report = new ConsistencyCheckReportModel
            {
                GeneratedAt = DateTimeOffset.Now,
                CategoryCount = await GetTableCountAsync(connection, "categories"),
                CustomerCount = await GetTableCountAsync(connection, "customers"),
                OrderCount = await GetTableCountAsync(connection, "orders"),
                CheckItems = checkItems,
                Issues = issues
            };

            await _logger.LogInfoAsync(
                report.IsHealthy
                    ? "账本一致性检查完成，未发现问题。"
                    : $"账本一致性检查完成，发现 {report.IssueCount} 项问题。");

            return report;
        }

        public async Task<ConsistencyRepairResultModel> RepairRepairableConsistencyIssuesAsync()
        {
            var restorePointPath = await CreateBackupSnapshotAsync("consistency-repair-point");

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            var customerIds = new List<long>();

            var customerCommand = connection.CreateCommand();
            customerCommand.Transaction = transaction;
            customerCommand.CommandText =
                """
                SELECT id
                FROM customers
                ORDER BY id;
                """;

            await using (var reader = await customerCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    customerIds.Add(reader.GetInt64(0));
                }
            }

            var clearPriceMemoryCommand = connection.CreateCommand();
            clearPriceMemoryCommand.Transaction = transaction;
            clearPriceMemoryCommand.CommandText = "DELETE FROM customer_category_prices;";
            await clearPriceMemoryCommand.ExecuteNonQueryAsync();

            foreach (var customerId in customerIds)
            {
                await UpdateCustomerUsageFlagsAsync(connection, transaction, customerId);
                await RebuildCustomerPriceMemoriesAsync(connection, transaction, customerId);
            }

            await transaction.CommitAsync();

            var report = await RunConsistencyCheckAsync();

            await _logger.LogInfoAsync(
                report.IsHealthy
                    ? $"已执行账本自动修复，重建 {customerIds.Count} 位客户的使用标记和价格记忆。恢复点：{restorePointPath}。复查未发现剩余一致性问题。"
                    : $"已执行账本自动修复，重建 {customerIds.Count} 位客户的使用标记和价格记忆。恢复点：{restorePointPath}。复查后仍有 {report.IssueCount} 项问题。");

            return new ConsistencyRepairResultModel
            {
                RestorePointPath = restorePointPath,
                ProcessedCustomerCount = customerIds.Count,
                Report = report
            };
        }

        public async Task<IReadOnlyList<CategoryModel>> GetTopForecastCategoriesAsync(int count)
        {
            var categories = await GetCategoriesAsync();
            return categories
                .OrderByDescending(category => category.ForecastRevenue)
                .ThenBy(category => category.Name)
                .Take(count)
                .ToList();
        }

        private SqliteConnection CreateConnection() => new($"Data Source={DatabasePath}");

        private async Task<string> CreateBackupSnapshotAsync(string prefix)
        {
            AppDataPaths.EnsureDirectories();

            if (!File.Exists(DatabasePath))
            {
                throw new InvalidOperationException("当前还没有可备份的数据库文件。");
            }

            var backupPath = Path.Combine(
                BackupDirectory,
                $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmssfff}.db");

            await Task.Run(() => File.Copy(DatabasePath, backupPath, overwrite: true));
            return backupPath;
        }

        private static async Task<int> GetTableCountAsync(SqliteConnection connection, string tableName)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private static ConsistencyCheckItemModel CreateCheckItem(string name, int issueCount, string detailText) =>
            new()
            {
                Name = name,
                StatusText = issueCount == 0 ? "正常" : $"{issueCount} 项",
                DetailText = detailText
            };

        private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectDuplicateMasterDataIssuesAsync(SqliteConnection connection)
        {
            var issues = new List<ConsistencyCheckIssueModel>();

            var categoryCommand = connection.CreateCommand();
            categoryCommand.CommandText =
                """
                SELECT LOWER(name) AS key_name, GROUP_CONCAT(name, ' / '), COUNT(*)
                FROM categories
                GROUP BY LOWER(name)
                HAVING COUNT(*) > 1;
                """;

            await using (var reader = await categoryCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    issues.Add(new ConsistencyCheckIssueModel
                    {
                        CheckName = "主数据唯一性",
                        SeverityText = "警告",
                        Title = "分类名称重复",
                        DetailText = $"以下分类名称会按同一个键匹配：{reader.GetString(1)}，共 {reader.GetInt32(2)} 条。"
                    });
                }
            }

            var customerCommand = connection.CreateCommand();
            customerCommand.CommandText =
                """
                SELECT LOWER(name) AS key_name, GROUP_CONCAT(name, ' / '), COUNT(*)
                FROM customers
                GROUP BY LOWER(name)
                HAVING COUNT(*) > 1;
                """;

            await using (var reader = await customerCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    issues.Add(new ConsistencyCheckIssueModel
                    {
                        CheckName = "主数据唯一性",
                        SeverityText = "警告",
                        Title = "客户名称重复",
                        DetailText = $"以下客户名称会按同一个键匹配：{reader.GetString(1)}，共 {reader.GetInt32(2)} 条。"
                    });
                }
            }

            return issues;
        }

        private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectOrderIntegrityIssuesAsync(SqliteConnection connection)
        {
            var issues = new List<ConsistencyCheckIssueModel>();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                WITH order_metrics AS (
                    SELECT
                        o.id,
                        o.order_number,
                        o.type,
                        o.total_weight,
                        o.total_amount,
                        o.category_count,
                        o.item_count,
                        IFNULL((SELECT COUNT(*) FROM inbound_records WHERE order_id = o.id), 0) AS inbound_count,
                        IFNULL((SELECT COUNT(*) FROM outbound_records WHERE order_id = o.id), 0) AS outbound_count,
                        IFNULL((SELECT SUM(weight) FROM inbound_records WHERE order_id = o.id), 0)
                            + IFNULL((SELECT SUM(weight) FROM outbound_records WHERE order_id = o.id), 0) AS actual_weight,
                        IFNULL((SELECT SUM(total_cost) FROM inbound_records WHERE order_id = o.id), 0)
                            + IFNULL((SELECT SUM(total_revenue) FROM outbound_records WHERE order_id = o.id), 0) AS actual_amount,
                        (
                            SELECT unit_type
                            FROM (
                                SELECT order_id, unit_type, id FROM inbound_records
                                UNION ALL
                                SELECT order_id, unit_type, id FROM outbound_records
                            ) details
                            WHERE details.order_id = o.id
                            ORDER BY id DESC
                            LIMIT 1
                        ) AS detail_unit_type
                    FROM orders o
                )
                SELECT
                    id,
                    order_number,
                    type,
                    total_weight,
                    total_amount,
                    category_count,
                    item_count,
                    inbound_count,
                    outbound_count,
                    actual_weight,
                    actual_amount,
                    detail_unit_type
                FROM order_metrics;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var orderNumber = reader.GetString(1);
                var type = reader.GetString(2);
                var totalWeight = reader.GetDouble(3);
                var totalAmount = reader.GetDouble(4);
                var categoryCount = reader.GetInt32(5);
                var itemCount = reader.GetInt32(6);
                var inboundCount = reader.GetInt32(7);
                var outboundCount = reader.GetInt32(8);
                var actualWeight = reader.GetDouble(9);
                var actualAmount = reader.GetDouble(10);
                var detailUnitType = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);

                if (string.Equals(type, "inbound", StringComparison.OrdinalIgnoreCase))
                {
                    if (inboundCount != 1 || outboundCount != 0)
                    {
                        issues.Add(CreateOrderIssue(orderNumber, "入库订单明细关系异常", $"预期 1 条入库明细、0 条出库明细，实际为 {inboundCount} / {outboundCount}。"));
                    }
                }
                else if (string.Equals(type, "outbound", StringComparison.OrdinalIgnoreCase))
                {
                    if (outboundCount != 1 || inboundCount != 0)
                    {
                        issues.Add(CreateOrderIssue(orderNumber, "出库订单明细关系异常", $"预期 1 条出库明细、0 条入库明细，实际为 {outboundCount} / {inboundCount}。"));
                    }
                }
                else
                {
                    issues.Add(CreateOrderIssue(orderNumber, "订单类型异常", $"检测到未识别的订单类型：{type}。"));
                }

                if (Math.Abs(totalWeight - actualWeight) > 0.0001)
                {
                    issues.Add(CreateOrderIssue(orderNumber, "订单数量汇总不一致", $"订单总数量为 {totalWeight:0.##}，但明细汇总为 {actualWeight:0.##}。"));
                }

                if (Math.Abs(totalAmount - actualAmount) > 0.0001)
                {
                    issues.Add(CreateOrderIssue(orderNumber, "订单金额汇总不一致", $"订单总金额为 ¥{totalAmount:0.##}，但明细汇总为 ¥{actualAmount:0.##}。"));
                }

                if (categoryCount != 1)
                {
                    issues.Add(CreateOrderIssue(orderNumber, "订单分类数异常", $"当前桌面版按单明细订单运行，但 category_count = {categoryCount}。"));
                }

                if (!string.IsNullOrWhiteSpace(detailUnitType))
                {
                    var expectedItemCount = string.Equals(detailUnitType, WeightUnit.Piece.ToString(), StringComparison.OrdinalIgnoreCase)
                        ? (int)Math.Round(actualWeight)
                        : 1;
                    if (itemCount != expectedItemCount)
                    {
                        issues.Add(CreateOrderIssue(orderNumber, "订单件数异常", $"明细单位为 {detailUnitType}，预期 item_count = {expectedItemCount}，实际为 {itemCount}。"));
                    }
                }
            }

            return issues;
        }

        private static ConsistencyCheckIssueModel CreateOrderIssue(string orderNumber, string title, string detailText) =>
            new()
            {
                CheckName = "订单明细关系",
                SeverityText = "错误",
                Title = $"{title}：{orderNumber}",
                DetailText = detailText
            };

        private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectCustomerUsageFlagIssuesAsync(SqliteConnection connection)
        {
            var issues = new List<ConsistencyCheckIssueModel>();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    c.name,
                    c.has_inbound_orders,
                    c.has_outbound_orders,
                    IFNULL(SUM(CASE WHEN o.type = 'inbound' THEN 1 ELSE 0 END), 0) AS actual_inbound_count,
                    IFNULL(SUM(CASE WHEN o.type = 'outbound' THEN 1 ELSE 0 END), 0) AS actual_outbound_count
                FROM customers c
                LEFT JOIN orders o ON o.customer_id = c.id
                GROUP BY c.id, c.name, c.has_inbound_orders, c.has_outbound_orders
                ORDER BY c.name COLLATE NOCASE;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var customerName = reader.GetString(0);
                var hasInboundOrders = reader.GetInt64(1) == 1;
                var hasOutboundOrders = reader.GetInt64(2) == 1;
                var actualInboundOrders = reader.GetInt64(3) > 0;
                var actualOutboundOrders = reader.GetInt64(4) > 0;

                if (hasInboundOrders != actualInboundOrders || hasOutboundOrders != actualOutboundOrders)
                {
                    issues.Add(new ConsistencyCheckIssueModel
                    {
                        CheckName = "客户使用标记",
                        SeverityText = "警告",
                        Title = $"客户标记不一致：{customerName}",
                        DetailText = $"当前标记为 入库={FormatBooleanLabel(hasInboundOrders)} / 出库={FormatBooleanLabel(hasOutboundOrders)}，实际订单为 入库={FormatBooleanLabel(actualInboundOrders)} / 出库={FormatBooleanLabel(actualOutboundOrders)}。"
                    });
                }
            }

            return issues;
        }

        private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectCustomerPriceMemoryIssuesAsync(SqliteConnection connection)
        {
            var issues = new List<ConsistencyCheckIssueModel>();

            var duplicateCommand = connection.CreateCommand();
            duplicateCommand.CommandText =
                """
                SELECT
                    IFNULL(c.name, '缺失客户'),
                    IFNULL(cg.name, '缺失分类'),
                    COUNT(*)
                FROM customer_category_prices p
                LEFT JOIN customers c ON c.id = p.customer_id
                LEFT JOIN categories cg ON cg.id = p.category_id
                GROUP BY p.customer_id, p.category_id
                HAVING COUNT(*) > 1;
                """;

            await using (var reader = await duplicateCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    issues.Add(new ConsistencyCheckIssueModel
                    {
                        CheckName = "客户价格记忆",
                        SeverityText = "警告",
                        Title = $"价格记忆重复：{reader.GetString(0)} / {reader.GetString(1)}",
                        DetailText = $"同一个客户和分类存在 {reader.GetInt32(2)} 条价格记忆记录。"
                    });
                }
            }

            var staleCommand = connection.CreateCommand();
            staleCommand.CommandText =
                """
                WITH latest_price AS (
                    SELECT
                        customer_id,
                        category_id,
                        price,
                        updated_at
                    FROM (
                        SELECT
                            o.customer_id,
                            d.category_id,
                            d.price,
                            d.updated_at,
                            ROW_NUMBER() OVER (
                                PARTITION BY o.customer_id, d.category_id
                                ORDER BY d.updated_at DESC, d.record_id DESC
                            ) AS rn
                        FROM orders o
                        INNER JOIN (
                            SELECT order_id, category_id, unit_price AS price, timestamp AS updated_at, id AS record_id
                            FROM inbound_records
                            UNION ALL
                            SELECT order_id, category_id, unit_price AS price, timestamp AS updated_at, id AS record_id
                            FROM outbound_records
                        ) d ON d.order_id = o.id
                        WHERE o.customer_id IS NOT NULL
                    )
                    WHERE rn = 1
                )
                SELECT
                    IFNULL(c.name, '缺失客户') AS customer_name,
                    IFNULL(cg.name, '缺失分类') AS category_name,
                    p.id,
                    p.price,
                    p.updated_at,
                    lp.price AS latest_price,
                    lp.updated_at AS latest_updated_at
                FROM customer_category_prices p
                LEFT JOIN customers c ON c.id = p.customer_id
                LEFT JOIN categories cg ON cg.id = p.category_id
                LEFT JOIN latest_price lp ON lp.customer_id = p.customer_id AND lp.category_id = p.category_id
                WHERE c.id IS NULL
                    OR cg.id IS NULL
                    OR lp.customer_id IS NULL
                    OR ABS(p.price - lp.price) > 0.0001
                    OR p.updated_at != lp.updated_at
                ORDER BY customer_name COLLATE NOCASE, category_name COLLATE NOCASE;
                """;

            await using (var reader = await staleCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var customerName = reader.GetString(0);
                    var categoryName = reader.GetString(1);
                    var latestPrice = reader.IsDBNull(5) ? (double?)null : reader.GetDouble(5);
                    var latestUpdatedAt = reader.IsDBNull(6)
                        ? (DateTimeOffset?)null
                        : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6));

                    var detailText = latestPrice is null
                        ? "当前价格记忆找不到对应的最近成交记录，可能已经孤立。"
                        : $"当前记忆为 ¥{reader.GetDouble(3):0.##}，最近成交价为 ¥{latestPrice.Value:0.##}，最近成交时间为 {latestUpdatedAt!.Value.ToLocalTime():yyyy-MM-dd HH:mm}。";

                    issues.Add(new ConsistencyCheckIssueModel
                    {
                        CheckName = "客户价格记忆",
                        SeverityText = "警告",
                        Title = $"价格记忆需要重建：{customerName} / {categoryName}",
                        DetailText = detailText
                    });
                }
            }

            return issues;
        }

        private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectCategoryUnitConsistencyIssuesAsync(SqliteConnection connection)
        {
            var issues = new List<ConsistencyCheckIssueModel>();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    c.name,
                    c.unit_type,
                    r.record_unit_type,
                    r.record_type,
                    r.record_count
                FROM categories c
                INNER JOIN (
                    SELECT category_id, unit_type AS record_unit_type, '入库' AS record_type, COUNT(*) AS record_count
                    FROM inbound_records
                    GROUP BY category_id, unit_type

                    UNION ALL

                    SELECT category_id, unit_type AS record_unit_type, '出库' AS record_type, COUNT(*) AS record_count
                    FROM outbound_records
                    GROUP BY category_id, unit_type
                ) r ON r.category_id = c.id
                WHERE r.record_unit_type != c.unit_type
                ORDER BY c.name COLLATE NOCASE;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                issues.Add(new ConsistencyCheckIssueModel
                {
                    CheckName = "分类单位一致性",
                    SeverityText = "警告",
                    Title = $"分类单位不一致：{reader.GetString(0)}",
                    DetailText = $"当前分类单位为 {reader.GetString(1)}，但存在 {reader.GetString(3)} 记录使用 {reader.GetString(2)}，共 {reader.GetInt32(4)} 条。"
                });
            }

            return issues;
        }

        private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectCategoryStockFieldIssuesAsync(SqliteConnection connection)
        {
            var issues = new List<ConsistencyCheckIssueModel>();

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    name,
                    unit_type,
                    stock,
                    stock_in_jin,
                    stock_in_pieces
                FROM categories
                ORDER BY name COLLATE NOCASE;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var unitType = ParseUnit(reader.GetString(1));
                var stock = reader.GetDouble(2);
                var stockInJin = reader.GetDouble(3);
                var stockInPieces = reader.GetInt32(4);

                if (stock < -0.0001 || stockInJin < -0.0001 || stockInPieces < 0)
                {
                    issues.Add(new ConsistencyCheckIssueModel
                    {
                        CheckName = "库存字段合法性",
                        SeverityText = "错误",
                        Title = $"库存出现负数：{name}",
                        DetailText = $"当前库存字段为 kg={stock:0.##}，斤={stockInJin:0.##}，件={stockInPieces}。"
                    });
                    continue;
                }

                var hasInactiveValue = unitType switch
                {
                    WeightUnit.Kilogram => Math.Abs(stockInJin) > 0.0001 || stockInPieces != 0,
                    WeightUnit.Jin => Math.Abs(stock) > 0.0001 || stockInPieces != 0,
                    WeightUnit.Piece => Math.Abs(stock) > 0.0001 || Math.Abs(stockInJin) > 0.0001,
                    _ => false
                };

                if (hasInactiveValue)
                {
                    issues.Add(new ConsistencyCheckIssueModel
                    {
                        CheckName = "库存字段合法性",
                        SeverityText = "警告",
                        Title = $"非当前单位字段残留数量：{name}",
                        DetailText = $"当前分类单位为 {FormatUnitLabel(unitType)}，但其他库存列仍有值：kg={stock:0.##}，斤={stockInJin:0.##}，件={stockInPieces}。"
                    });
                }
            }

            return issues;
        }

        private static string FormatBooleanLabel(bool value) => value ? "是" : "否";

        private async Task<int> GetCurrentSchemaVersionAsync(SqliteConnection connection)
        {
            var version = await GetPragmaUserVersionAsync(connection);
            if (version > 0)
            {
                return version;
            }

            if (!await HasAnyBusinessTablesAsync(connection))
            {
                return 0;
            }

            if (!await HasAllBaseTablesAsync(connection))
            {
                await _logger.LogWarningAsync("数据库未记录架构版本，且基础表不完整，将按 v0 重新补齐基础结构。");
                return 0;
            }

            var inferredVersion = await InferLegacySchemaVersionAsync(connection);
            await SetSchemaVersionAsync(connection, inferredVersion);
            await _logger.LogWarningAsync($"数据库未记录架构版本，已按现有结构推断为 v{inferredVersion}。");
            return inferredVersion;
        }

        private static async Task<int> GetPragmaUserVersionAsync(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private static async Task SetSchemaVersionAsync(SqliteConnection connection, int version)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {version};";
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<bool> HasAnyBusinessTablesAsync(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                    AND name IN (
                        'categories',
                        'customers',
                        'customer_category_prices',
                        'orders',
                        'inbound_records',
                        'outbound_records',
                        'stock_adjustments',
                        'stock_audits'
                    );
                """;

            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
        }

        private static async Task<bool> HasAllBaseTablesAsync(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                    AND name IN (
                        'categories',
                        'customers',
                        'customer_category_prices',
                        'orders',
                        'inbound_records',
                        'outbound_records',
                        'stock_adjustments',
                        'stock_audits'
                    );
                """;

            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 8;
        }

        private static async Task<int> InferLegacySchemaVersionAsync(SqliteConnection connection)
        {
            if (await ColumnExistsAsync(connection, transaction: null, "categories", "is_archived"))
            {
                return CategoryArchiveSchemaVersion;
            }

            return BaseSchemaVersion;
        }

        private static async Task ApplyMigrationAsync(SqliteConnection connection, int targetVersion)
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            switch (targetVersion)
            {
                case BaseSchemaVersion:
                    await ApplyBaseSchemaMigrationAsync(connection, transaction);
                    break;
                case CategoryArchiveSchemaVersion:
                    await ApplyCategoryArchiveMigrationAsync(connection, transaction);
                    break;
                case PerformanceIndexesSchemaVersion:
                    await ApplyPerformanceIndexesMigrationAsync(connection, transaction);
                    break;
                default:
                    throw new InvalidOperationException($"未定义的数据库迁移版本：v{targetVersion}");
            }

            await transaction.CommitAsync();
        }

        private static async Task ApplyBaseSchemaMigrationAsync(SqliteConnection connection, SqliteTransaction transaction)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                """
                CREATE TABLE IF NOT EXISTS categories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    buy_price REAL NOT NULL,
                    sell_price REAL NOT NULL,
                    stock REAL NOT NULL DEFAULT 0,
                    stock_in_jin REAL NOT NULL DEFAULT 0,
                    stock_in_pieces INTEGER NOT NULL DEFAULT 0,
                    unit_type TEXT NOT NULL,
                    created_at INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS customers (
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

                CREATE TABLE IF NOT EXISTS customer_category_prices (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    customer_id INTEGER NOT NULL,
                    category_id INTEGER NOT NULL,
                    price REAL NOT NULL,
                    updated_at INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS orders (
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

                CREATE TABLE IF NOT EXISTS inbound_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    order_id INTEGER NULL,
                    category_id INTEGER NOT NULL,
                    weight REAL NOT NULL,
                    unit_price REAL NOT NULL,
                    total_cost REAL NOT NULL,
                    unit_type TEXT NOT NULL,
                    timestamp INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS outbound_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    order_id INTEGER NULL,
                    category_id INTEGER NOT NULL,
                    weight REAL NOT NULL,
                    unit_price REAL NOT NULL,
                    total_revenue REAL NOT NULL,
                    unit_type TEXT NOT NULL,
                    timestamp INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS stock_adjustments (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    category_id INTEGER NOT NULL,
                    previous_quantity REAL NOT NULL,
                    adjusted_quantity REAL NOT NULL,
                    delta_quantity REAL NOT NULL,
                    unit_type TEXT NOT NULL,
                    reason TEXT NOT NULL DEFAULT '',
                    timestamp INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS stock_audits (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    category_id INTEGER NOT NULL,
                    system_quantity REAL NOT NULL,
                    actual_quantity REAL NOT NULL,
                    delta_quantity REAL NOT NULL,
                    unit_type TEXT NOT NULL,
                    note TEXT NOT NULL DEFAULT '',
                    timestamp INTEGER NOT NULL
                );
                """);
        }

        private static async Task ApplyCategoryArchiveMigrationAsync(SqliteConnection connection, SqliteTransaction transaction)
        {
            if (await ColumnExistsAsync(connection, transaction, "categories", "is_archived"))
            {
                return;
            }

            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "ALTER TABLE categories ADD COLUMN is_archived INTEGER NOT NULL DEFAULT 0;");
        }

        private static async Task ApplyPerformanceIndexesMigrationAsync(SqliteConnection connection, SqliteTransaction transaction)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                """
                CREATE INDEX IF NOT EXISTS idx_categories_name ON categories(name COLLATE NOCASE);
                CREATE INDEX IF NOT EXISTS idx_customers_name ON customers(name COLLATE NOCASE);
                CREATE INDEX IF NOT EXISTS idx_customer_category_prices_lookup ON customer_category_prices(customer_id, category_id);
                CREATE INDEX IF NOT EXISTS idx_orders_timestamp ON orders(timestamp DESC);
                CREATE INDEX IF NOT EXISTS idx_orders_customer_id ON orders(customer_id);
                CREATE INDEX IF NOT EXISTS idx_inbound_records_order_id ON inbound_records(order_id);
                CREATE INDEX IF NOT EXISTS idx_inbound_records_category_id ON inbound_records(category_id);
                CREATE INDEX IF NOT EXISTS idx_outbound_records_order_id ON outbound_records(order_id);
                CREATE INDEX IF NOT EXISTS idx_outbound_records_category_id ON outbound_records(category_id);
                CREATE INDEX IF NOT EXISTS idx_stock_adjustments_category_id ON stock_adjustments(category_id);
                CREATE INDEX IF NOT EXISTS idx_stock_adjustments_timestamp ON stock_adjustments(timestamp DESC);
                CREATE INDEX IF NOT EXISTS idx_stock_audits_category_id ON stock_audits(category_id);
                CREATE INDEX IF NOT EXISTS idx_stock_audits_timestamp ON stock_audits(timestamp DESC);
                """);
        }

        private static async Task ExecuteNonQueryAsync(SqliteConnection connection, SqliteTransaction transaction, string commandText)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<bool> ColumnExistsAsync(
            SqliteConnection connection,
            SqliteTransaction? transaction,
            string tableName,
            string columnName)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                $"""
                SELECT COUNT(*)
                FROM pragma_table_info('{tableName}')
                WHERE name = $columnName;
                """;
            command.Parameters.AddWithValue("$columnName", columnName);

            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
        }

        private static string GetMigrationName(int version) =>
            version switch
            {
                BaseSchemaVersion => "创建基础业务表",
                CategoryArchiveSchemaVersion => "补充分级归档字段",
                PerformanceIndexesSchemaVersion => "创建查询性能索引",
                _ => "未知迁移"
            };

        private static async Task<StockMutationSnapshot> ApplyStockMutationAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            CategoryModel category,
            double adjustedQuantity,
            string? reason,
            long timestamp,
            bool allowSameQuantity)
        {
            var normalizedQuantity = NormalizeQuantity(adjustedQuantity, category.UnitType);
            if (normalizedQuantity < 0)
            {
                throw new InvalidOperationException("\u8C03\u6574\u540E\u5E93\u5B58\u4E0D\u80FD\u4E3A\u8D1F\u6570\u3002");
            }

            var previousQuantity = GetAvailableStock(
                category.Stock,
                category.StockInJin,
                category.StockInPieces,
                category.UnitType);
            var deltaQuantity = Math.Round(normalizedQuantity - previousQuantity, 2);

            if (!allowSameQuantity && Math.Abs(deltaQuantity) < 0.0001)
            {
                throw new InvalidOperationException("\u8C03\u6574\u540E\u5E93\u5B58\u4E0E\u5F53\u524D\u4E00\u81F4\u3002");
            }

            var updatedStock = category.Stock;
            var updatedStockInJin = category.StockInJin;
            var updatedStockInPieces = category.StockInPieces;

            ApplyStockQuantity(
                normalizedQuantity,
                category.UnitType,
                ref updatedStock,
                ref updatedStockInJin,
                ref updatedStockInPieces);

            var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE categories
                SET
                    stock = $stock,
                    stock_in_jin = $stockInJin,
                    stock_in_pieces = $stockInPieces,
                    updated_at = $updatedAt
                WHERE id = $id;
                """;
            updateCommand.Parameters.AddWithValue("$id", category.Id);
            updateCommand.Parameters.AddWithValue("$stock", updatedStock);
            updateCommand.Parameters.AddWithValue("$stockInJin", updatedStockInJin);
            updateCommand.Parameters.AddWithValue("$stockInPieces", updatedStockInPieces);
            updateCommand.Parameters.AddWithValue("$updatedAt", timestamp);
            await updateCommand.ExecuteNonQueryAsync();

            if (Math.Abs(deltaQuantity) >= 0.0001)
            {
                var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText =
                    """
                    INSERT INTO stock_adjustments (
                        category_id,
                        previous_quantity,
                        adjusted_quantity,
                        delta_quantity,
                        unit_type,
                        reason,
                        timestamp
                    )
                    VALUES (
                        $categoryId,
                        $previousQuantity,
                        $adjustedQuantity,
                        $deltaQuantity,
                        $unitType,
                        $reason,
                        $timestamp
                    );
                    """;
                insertCommand.Parameters.AddWithValue("$categoryId", category.Id);
                insertCommand.Parameters.AddWithValue("$previousQuantity", previousQuantity);
                insertCommand.Parameters.AddWithValue("$adjustedQuantity", normalizedQuantity);
                insertCommand.Parameters.AddWithValue("$deltaQuantity", deltaQuantity);
                insertCommand.Parameters.AddWithValue("$unitType", category.UnitType.ToString());
                insertCommand.Parameters.AddWithValue("$reason", reason?.Trim() ?? string.Empty);
                insertCommand.Parameters.AddWithValue("$timestamp", timestamp);
                await insertCommand.ExecuteNonQueryAsync();
            }

            return new StockMutationSnapshot(previousQuantity, normalizedQuantity, deltaQuantity);
        }

        private static double NormalizeQuantity(double quantity, WeightUnit unitType)
        {
            var safeQuantity = Math.Max(0, quantity);
            return unitType == WeightUnit.Piece
                ? Math.Round(safeQuantity)
                : Math.Round(safeQuantity, 2);
        }

        private static double GetAvailableStock(double stock, double stockInJin, int stockInPieces, WeightUnit unitType) =>
            unitType switch
            {
                WeightUnit.Kilogram => stock,
                WeightUnit.Jin => stockInJin,
                WeightUnit.Piece => stockInPieces,
                _ => stock
            };

        private static void ApplyStockQuantity(double quantity, WeightUnit unitType, ref double stock, ref double stockInJin, ref int stockInPieces)
        {
            switch (unitType)
            {
                case WeightUnit.Kilogram:
                    stock = quantity;
                    break;
                case WeightUnit.Jin:
                    stockInJin = quantity;
                    break;
                case WeightUnit.Piece:
                    stockInPieces = (int)Math.Round(quantity);
                    break;
            }
        }

        private static void DeductStock(double quantity, WeightUnit unitType, ref double stock, ref double stockInJin, ref int stockInPieces)
        {
            switch (unitType)
            {
                case WeightUnit.Kilogram:
                    stock -= quantity;
                    break;
                case WeightUnit.Jin:
                    stockInJin -= quantity;
                    break;
                case WeightUnit.Piece:
                    stockInPieces -= (int)Math.Round(quantity);
                    break;
            }
        }

        private async Task<IReadOnlyList<CustomerModel>> GetCustomersByUsageAsync(string usageColumn)
        {
            var customers = new List<CustomerModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT
                    id,
                    name,
                    phone,
                    email,
                    address,
                    note,
                    has_inbound_orders,
                    has_outbound_orders,
                    created_at,
                    updated_at
                FROM customers
                WHERE {usageColumn} = 1
                ORDER BY name COLLATE NOCASE;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                customers.Add(new CustomerModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Phone = reader.GetString(2),
                    Email = reader.GetString(3),
                    Address = reader.GetString(4),
                    Note = reader.GetString(5),
                    HasInboundOrders = reader.GetInt64(6) == 1,
                    HasOutboundOrders = reader.GetInt64(7) == 1,
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
                    UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9))
                });
            }

            return customers;
        }

        private static async Task FillDailyAnalyticsAsync(
            SqliteConnection connection,
            string table,
            string amountColumn,
            int days,
            IDictionary<DateOnly, DailyAnalyticsItemModel> items,
            bool isInbound)
        {
            var startDate = DateTimeOffset.Now.Date.AddDays(-(days - 1));
            var startTimestamp = new DateTimeOffset(startDate, DateTimeOffset.Now.Offset).ToUnixTimeMilliseconds();

            var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT
                    strftime('%Y-%m-%d', datetime(timestamp / 1000, 'unixepoch', 'localtime')) AS day_key,
                    IFNULL(SUM({amountColumn}), 0) AS amount
                FROM {table}
                WHERE timestamp >= $start
                GROUP BY day_key
                ORDER BY day_key ASC;
                """;
            command.Parameters.AddWithValue("$start", startTimestamp);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var dayKey = reader.GetString(0);
                if (!DateOnly.TryParseExact(
                        dayKey,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dateKey)
                    || !items.TryGetValue(dateKey, out var current))
                {
                    continue;
                }

                var amount = reader.GetDouble(1);
                items[dateKey] = new DailyAnalyticsItemModel
                {
                    Date = current.Date,
                    InboundAmount = isInbound ? amount : current.InboundAmount,
                    OutboundAmount = isInbound ? current.OutboundAmount : amount
                };
            }
        }

        private async Task<IReadOnlyList<CategoryAnalyticsItemModel>> GetTopCategoryAnalyticsAsync(string table, string amountColumn, int count)
        {
            var categories = new List<CategoryAnalyticsItemModel>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT
                    c.name,
                    IFNULL(SUM(r.weight), 0) AS quantity,
                    r.unit_type,
                    IFNULL(SUM(r.{amountColumn}), 0) AS amount
                FROM {table} r
                INNER JOIN categories c ON c.id = r.category_id
                GROUP BY r.category_id, c.name, r.unit_type
                ORDER BY amount DESC, c.name COLLATE NOCASE
                LIMIT $count;
                """;
            command.Parameters.AddWithValue("$count", count);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new CategoryAnalyticsItemModel
                {
                    CategoryName = reader.GetString(0),
                    Quantity = reader.GetDouble(1),
                    UnitType = ParseUnit(reader.GetString(2)),
                    Amount = reader.GetDouble(3)
                });
            }

            return categories;
        }

        private static async Task EnsureCustomerNameAvailableAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long customerId,
            string customerName)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT id
                FROM customers
                WHERE LOWER(name) = LOWER($name)
                    AND id != $id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$name", customerName);
            command.Parameters.AddWithValue("$id", customerId);

            var duplicateId = await command.ExecuteScalarAsync();
            if (duplicateId is not null)
            {
                throw new InvalidOperationException("\u5DF2\u5B58\u5728\u540C\u540D\u5BA2\u6237\uff0c\u8BF7\u76F4\u63A5\u7F16\u8F91\u539F\u6709\u5BA2\u6237\u3002");
            }
        }

        private static async Task UpdateCustomerUsageFlagsAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long customerId)
        {
            var statsCommand = connection.CreateCommand();
            statsCommand.Transaction = transaction;
            statsCommand.CommandText =
                """
                SELECT
                    SUM(CASE WHEN type = 'inbound' THEN 1 ELSE 0 END) AS inbound_count,
                    SUM(CASE WHEN type = 'outbound' THEN 1 ELSE 0 END) AS outbound_count
                FROM orders
                WHERE customer_id = $customerId;
                """;
            statsCommand.Parameters.AddWithValue("$customerId", customerId);

            var hasInboundOrders = false;
            var hasOutboundOrders = false;

            await using (var reader = await statsCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    hasInboundOrders = !reader.IsDBNull(0) && reader.GetInt64(0) > 0;
                    hasOutboundOrders = !reader.IsDBNull(1) && reader.GetInt64(1) > 0;
                }
            }

            var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE customers
                SET
                    has_inbound_orders = $hasInboundOrders,
                    has_outbound_orders = $hasOutboundOrders,
                    updated_at = $updatedAt
                WHERE id = $customerId;
                """;
            updateCommand.Parameters.AddWithValue("$customerId", customerId);
            updateCommand.Parameters.AddWithValue("$hasInboundOrders", hasInboundOrders ? 1 : 0);
            updateCommand.Parameters.AddWithValue("$hasOutboundOrders", hasOutboundOrders ? 1 : 0);
            updateCommand.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToUnixTimeMilliseconds());
            await updateCommand.ExecuteNonQueryAsync();
        }

        private static async Task RebuildCustomerPriceMemoriesAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long customerId)
        {
            var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM customer_category_prices WHERE customer_id = $customerId;";
            deleteCommand.Parameters.AddWithValue("$customerId", customerId);
            await deleteCommand.ExecuteNonQueryAsync();

            var latestPrices = new Dictionary<long, (double Price, long UpdatedAt)>();

            var queryCommand = connection.CreateCommand();
            queryCommand.Transaction = transaction;
            queryCommand.CommandText =
                """
                SELECT category_id, price, updated_at
                FROM (
                    SELECT ir.category_id, ir.unit_price AS price, ir.timestamp AS updated_at, ir.id AS record_id
                    FROM inbound_records ir
                    INNER JOIN orders o ON o.id = ir.order_id
                    WHERE o.customer_id = $customerId

                    UNION ALL

                    SELECT orr.category_id, orr.unit_price AS price, orr.timestamp AS updated_at, orr.id AS record_id
                    FROM outbound_records orr
                    INNER JOIN orders o ON o.id = orr.order_id
                    WHERE o.customer_id = $customerId
                )
                ORDER BY updated_at DESC, record_id DESC;
                """;
            queryCommand.Parameters.AddWithValue("$customerId", customerId);

            await using (var reader = await queryCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var categoryId = reader.GetInt64(0);
                    if (latestPrices.ContainsKey(categoryId))
                    {
                        continue;
                    }

                    latestPrices[categoryId] = (reader.GetDouble(1), reader.GetInt64(2));
                }
            }

            foreach (var pair in latestPrices)
            {
                var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText =
                    """
                    INSERT INTO customer_category_prices (
                        customer_id, category_id, price, updated_at
                    )
                    VALUES (
                        $customerId, $categoryId, $price, $updatedAt
                    );
                    """;
                insertCommand.Parameters.AddWithValue("$customerId", customerId);
                insertCommand.Parameters.AddWithValue("$categoryId", pair.Key);
                insertCommand.Parameters.AddWithValue("$price", pair.Value.Price);
                insertCommand.Parameters.AddWithValue("$updatedAt", pair.Value.UpdatedAt);
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        private static async Task RebuildAllCategoryDataAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT
                    c.id,
                    c.buy_price,
                    c.sell_price,
                    IFNULL((SELECT SUM(weight) FROM inbound_records WHERE category_id = c.id AND unit_type = 'Kilogram'), 0),
                    IFNULL((SELECT SUM(weight) FROM inbound_records WHERE category_id = c.id AND unit_type = 'Jin'), 0),
                    IFNULL((SELECT SUM(weight) FROM inbound_records WHERE category_id = c.id AND unit_type = 'Piece'), 0),
                    IFNULL((SELECT SUM(weight) FROM outbound_records WHERE category_id = c.id AND unit_type = 'Kilogram'), 0),
                    IFNULL((SELECT SUM(weight) FROM outbound_records WHERE category_id = c.id AND unit_type = 'Jin'), 0),
                    IFNULL((SELECT SUM(weight) FROM outbound_records WHERE category_id = c.id AND unit_type = 'Piece'), 0),
                    (SELECT unit_price FROM inbound_records WHERE category_id = c.id ORDER BY timestamp DESC, id DESC LIMIT 1),
                    (SELECT unit_price FROM outbound_records WHERE category_id = c.id ORDER BY timestamp DESC, id DESC LIMIT 1)
                FROM categories c;
                """;

            var snapshots = new List<(long CategoryId, double BuyPrice, double SellPrice, double Stock, double StockInJin, int StockInPieces)>();

            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var inboundKilogram = reader.GetDouble(3);
                    var inboundJin = reader.GetDouble(4);
                    var inboundPieces = reader.GetDouble(5);
                    var outboundKilogram = reader.GetDouble(6);
                    var outboundJin = reader.GetDouble(7);
                    var outboundPieces = reader.GetDouble(8);

                    snapshots.Add((
                        reader.GetInt64(0),
                        reader.IsDBNull(9) ? reader.GetDouble(1) : reader.GetDouble(9),
                        reader.IsDBNull(10) ? reader.GetDouble(2) : reader.GetDouble(10),
                        Math.Max(0, Math.Round(inboundKilogram - outboundKilogram, 2)),
                        Math.Max(0, Math.Round(inboundJin - outboundJin, 2)),
                        Math.Max(0, (int)Math.Round(inboundPieces - outboundPieces))));
                }
            }

            var updatedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            foreach (var snapshot in snapshots)
            {
                var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText =
                    """
                    UPDATE categories
                    SET
                        buy_price = $buyPrice,
                        sell_price = $sellPrice,
                        stock = $stock,
                        stock_in_jin = $stockInJin,
                        stock_in_pieces = $stockInPieces,
                        updated_at = $updatedAt
                    WHERE id = $id;
                    """;
                updateCommand.Parameters.AddWithValue("$id", snapshot.CategoryId);
                updateCommand.Parameters.AddWithValue("$buyPrice", snapshot.BuyPrice);
                updateCommand.Parameters.AddWithValue("$sellPrice", snapshot.SellPrice);
                updateCommand.Parameters.AddWithValue("$stock", snapshot.Stock);
                updateCommand.Parameters.AddWithValue("$stockInJin", snapshot.StockInJin);
                updateCommand.Parameters.AddWithValue("$stockInPieces", snapshot.StockInPieces);
                updateCommand.Parameters.AddWithValue("$updatedAt", updatedAt);
                await updateCommand.ExecuteNonQueryAsync();
            }
        }

        private readonly record struct StockMutationSnapshot(double PreviousQuantity, double AdjustedQuantity, double DeltaQuantity);

        private readonly record struct CategoryImportPlan(
            long? ExistingId,
            string Name,
            double BuyPrice,
            double SellPrice,
            WeightUnit UnitType,
            bool IsArchived,
            bool IsUpdate);

        private readonly record struct CustomerImportPlan(
            long? ExistingId,
            string Name,
            string Phone,
            string Email,
            string Address,
            string Note,
            bool HasInboundOrders,
            bool HasOutboundOrders,
            bool IsUpdate);

        private readonly record struct CsvRawRow(int LineNumber, List<string> Values);

        private readonly record struct CsvRowData(int LineNumber, IReadOnlyDictionary<string, string> Values);

        private static async Task<CategoryModel> GetCategoryAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long categoryId)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT id, name, buy_price, sell_price, stock, stock_in_jin, stock_in_pieces, unit_type, is_archived, created_at, updated_at
                FROM categories
                WHERE id = $id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$id", categoryId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("\u672A\u627E\u5230\u5BF9\u5E94\u7684\u5206\u7C7B\u3002");
            }

            return new CategoryModel
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                BuyPrice = reader.GetDouble(2),
                SellPrice = reader.GetDouble(3),
                Stock = reader.GetDouble(4),
                StockInJin = reader.GetDouble(5),
                StockInPieces = reader.GetInt32(6),
                UnitType = ParseUnit(reader.GetString(7)),
                IsArchived = reader.GetInt64(8) == 1,
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
                UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))
            };
        }

        private static async Task<OrderEditModel> GetOrderEditModelAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long orderId)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT
                    o.id,
                    o.order_number,
                    o.type,
                    o.customer_id,
                    IFNULL(c.name, ''),
                    d.category_id,
                    d.quantity,
                    d.unit_price,
                    d.unit_type,
                    o.timestamp
                FROM orders o
                LEFT JOIN customers c ON c.id = o.customer_id
                INNER JOIN (
                    SELECT
                        ir.order_id,
                        ir.category_id,
                        ir.weight AS quantity,
                        ir.unit_price,
                        ir.unit_type
                    FROM inbound_records ir

                    UNION ALL

                    SELECT
                        orr.order_id,
                        orr.category_id,
                        orr.weight AS quantity,
                        orr.unit_price,
                        orr.unit_type
                    FROM outbound_records orr
                ) d ON d.order_id = o.id
                WHERE o.id = $orderId;
                """;
            command.Parameters.AddWithValue("$orderId", orderId);

            var records = new List<OrderEditModel>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(new OrderEditModel
                {
                    OrderId = reader.GetInt64(0),
                    OrderNumber = reader.GetString(1),
                    Type = reader.GetString(2),
                    CustomerId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    CustomerName = reader.GetString(4),
                    CategoryId = reader.GetInt64(5),
                    Quantity = reader.GetDouble(6),
                    UnitPrice = reader.GetDouble(7),
                    UnitType = ParseUnit(reader.GetString(8)),
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9))
                });
            }

            if (records.Count == 0)
            {
                throw new InvalidOperationException("\u672A\u627E\u5230\u8981\u7F16\u8F91\u7684\u8BA2\u5355\u3002");
            }

            if (records.Count > 1)
            {
                throw new InvalidOperationException("\u5F53\u524D\u53EA\u652F\u6301\u7F16\u8F91\u5355\u6761\u660E\u7EC6\u7684\u8BA2\u5355\u3002");
            }

            return records[0];
        }

        private static async Task<long> GetOrCreateCustomerAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string customerName,
            long now,
            bool markInbound,
            bool markOutbound)
        {
            var selectCommand = connection.CreateCommand();
            selectCommand.Transaction = transaction;
            selectCommand.CommandText =
                """
                SELECT id
                FROM customers
                WHERE LOWER(name) = LOWER($name)
                LIMIT 1;
                """;
            selectCommand.Parameters.AddWithValue("$name", customerName);

            var existingCustomerId = await selectCommand.ExecuteScalarAsync();
            if (existingCustomerId is not null && existingCustomerId != DBNull.Value)
            {
                var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText =
                    """
                    UPDATE customers
                    SET
                        has_inbound_orders = CASE WHEN $markInbound = 1 THEN 1 ELSE has_inbound_orders END,
                        has_outbound_orders = CASE WHEN $markOutbound = 1 THEN 1 ELSE has_outbound_orders END,
                        updated_at = $updatedAt
                    WHERE id = $id;
                    """;
                updateCommand.Parameters.AddWithValue("$id", Convert.ToInt64(existingCustomerId, CultureInfo.InvariantCulture));
                updateCommand.Parameters.AddWithValue("$markInbound", markInbound ? 1 : 0);
                updateCommand.Parameters.AddWithValue("$markOutbound", markOutbound ? 1 : 0);
                updateCommand.Parameters.AddWithValue("$updatedAt", now);
                await updateCommand.ExecuteNonQueryAsync();

                return Convert.ToInt64(existingCustomerId, CultureInfo.InvariantCulture);
            }

            var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO customers (
                    name, phone, email, address, note, has_inbound_orders, has_outbound_orders, created_at, updated_at
                )
                VALUES (
                    $name, '', '', '', '', $hasInboundOrders, $hasOutboundOrders, $createdAt, $updatedAt
                );
                """;
            insertCommand.Parameters.AddWithValue("$name", customerName);
            insertCommand.Parameters.AddWithValue("$hasInboundOrders", markInbound ? 1 : 0);
            insertCommand.Parameters.AddWithValue("$hasOutboundOrders", markOutbound ? 1 : 0);
            insertCommand.Parameters.AddWithValue("$createdAt", now);
            insertCommand.Parameters.AddWithValue("$updatedAt", now);
            await insertCommand.ExecuteNonQueryAsync();

            return await GetLastInsertRowIdAsync(connection, transaction);
        }

        private static async Task UpsertCustomerCategoryPriceAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long customerId,
            long categoryId,
            double price,
            long now)
        {
            var selectCommand = connection.CreateCommand();
            selectCommand.Transaction = transaction;
            selectCommand.CommandText =
                """
                SELECT id
                FROM customer_category_prices
                WHERE customer_id = $customerId AND category_id = $categoryId
                ORDER BY updated_at DESC
                LIMIT 1;
                """;
            selectCommand.Parameters.AddWithValue("$customerId", customerId);
            selectCommand.Parameters.AddWithValue("$categoryId", categoryId);

            var existingId = await selectCommand.ExecuteScalarAsync();
            if (existingId is not null && existingId != DBNull.Value)
            {
                var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText =
                    """
                    UPDATE customer_category_prices
                    SET
                        price = $price,
                        updated_at = $updatedAt
                    WHERE id = $id;
                    """;
                updateCommand.Parameters.AddWithValue("$id", Convert.ToInt64(existingId, CultureInfo.InvariantCulture));
                updateCommand.Parameters.AddWithValue("$price", price);
                updateCommand.Parameters.AddWithValue("$updatedAt", now);
                await updateCommand.ExecuteNonQueryAsync();
                return;
            }

            var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO customer_category_prices (
                    customer_id, category_id, price, updated_at
                )
                VALUES (
                    $customerId, $categoryId, $price, $updatedAt
                );
                """;
            insertCommand.Parameters.AddWithValue("$customerId", customerId);
            insertCommand.Parameters.AddWithValue("$categoryId", categoryId);
            insertCommand.Parameters.AddWithValue("$price", price);
            insertCommand.Parameters.AddWithValue("$updatedAt", now);
            await insertCommand.ExecuteNonQueryAsync();
        }

        private static async Task<long> InsertOrderAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string type,
            long? customerId,
            double quantity,
            double totalRevenue,
            WeightUnit unitType,
            long now)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO orders (
                    order_number, type, total_weight, total_amount, category_count, item_count, customer_id, note, timestamp
                )
                VALUES (
                    $orderNumber, $type, $totalWeight, $totalAmount, 1, $itemCount, $customerId, '', $timestamp
                );
                """;
            command.Parameters.AddWithValue("$orderNumber", CreateOrderNumber(type, now));
            command.Parameters.AddWithValue("$type", type);
            command.Parameters.AddWithValue("$totalWeight", quantity);
            command.Parameters.AddWithValue("$totalAmount", totalRevenue);
            command.Parameters.AddWithValue("$itemCount", unitType == WeightUnit.Piece ? (int)Math.Round(quantity) : 1);
            command.Parameters.AddWithValue("$timestamp", now);

            var customerParameter = command.CreateParameter();
            customerParameter.ParameterName = "$customerId";
            customerParameter.Value = customerId.HasValue ? customerId.Value : DBNull.Value;
            command.Parameters.Add(customerParameter);

            await command.ExecuteNonQueryAsync();
            return await GetLastInsertRowIdAsync(connection, transaction);
        }

        private static string CreateOrderNumber(string type, long now)
        {
            var prefix = string.Equals(type, "outbound", StringComparison.OrdinalIgnoreCase) ? "OUT" : "IN";
            return $"{prefix}-{DateTimeOffset.FromUnixTimeMilliseconds(now):yyyyMMddHHmmssfff}";
        }

        private static async Task<long> GetLastInsertRowIdAsync(SqliteConnection connection, SqliteTransaction transaction)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT last_insert_rowid();";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        private static async Task ExportQueryToCsvAsync(SqliteConnection connection, string filePath, string query)
        {
            var command = connection.CreateCommand();
            command.CommandText = query;

            await using var reader = await command.ExecuteReaderAsync();
            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var headers = Enumerable.Range(0, reader.FieldCount)
                .Select(index => EscapeCsv(reader.GetName(index)));
            await writer.WriteLineAsync(string.Join(",", headers));

            while (await reader.ReadAsync())
            {
                var values = new string[reader.FieldCount];
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    values[index] = EscapeCsv(
                        reader.IsDBNull(index)
                            ? string.Empty
                            : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty);
                }

                await writer.WriteLineAsync(string.Join(",", values));
            }
        }

        private static string EscapeCsv(string value)
        {
            var normalized = value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);

            if (normalized.Contains(',', StringComparison.Ordinal) ||
                normalized.Contains('"', StringComparison.Ordinal) ||
                normalized.Contains('\n', StringComparison.Ordinal))
            {
                return $"\"{normalized.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            }

            return normalized;
        }

        private async Task<(ImportPreviewModel Preview, List<CategoryImportPlan> Plans)> BuildCategoryImportPreviewAsync(string sourcePath)
        {
            ValidateCsvSourcePath(sourcePath);

            var rows = await ReadCsvRowsAsync(sourcePath);

            var existingCategories = new Dictionary<string, (long Id, WeightUnit UnitType)>(StringComparer.OrdinalIgnoreCase);
            await using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT id, name, unit_type FROM categories;";

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingCategories[reader.GetString(1).Trim()] = (reader.GetInt64(0), ParseUnit(reader.GetString(2)));
                }
            }

            var issues = new List<ImportPreviewIssueModel>();
            var previewRows = new List<ImportPreviewRowModel>();
            var plans = new List<CategoryImportPlan>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var lineNumber = row.LineNumber;
                try
                {
                    var name = GetCsvValue(row.Values, "category_name", "name", "分类名称", "分类")?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = "缺少分类名称。" });
                        continue;
                    }

                    if (!seenNames.Add(name))
                    {
                        issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = $"分类“{name}”在同一个 CSV 中重复出现。" });
                        continue;
                    }

                    var unitType = ParseCsvUnit(GetCsvValue(row.Values, "unit_type", "unit", "单位类型", "单位"));
                    var buyPrice = ParseCsvDouble(GetCsvValue(row.Values, "buy_price", "收购价", "采购价"), "收购价");
                    var sellPrice = ParseCsvDouble(GetCsvValue(row.Values, "sell_price", "卖价", "销售价"), "卖价");
                    var isArchived = ParseCsvBool(GetCsvValue(row.Values, "is_archived", "archived", "已归档", "归档"), defaultValue: false);

                    if (buyPrice <= 0)
                    {
                        issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = "收购价必须大于 0。" });
                        continue;
                    }

                    if (sellPrice <= 0)
                    {
                        issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = "卖价必须大于 0。" });
                        continue;
                    }

                    var isUpdate = existingCategories.TryGetValue(name, out var existingCategory);
                    if (isUpdate && existingCategory.UnitType != unitType)
                    {
                        issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = $"分类“{name}”的单位与现有数据不一致，无法自动合并。" });
                        continue;
                    }

                    previewRows.Add(new ImportPreviewRowModel
                    {
                        LineNumber = lineNumber,
                        Name = name,
                        ActionText = isUpdate ? "更新" : "新增",
                        DetailText = $"单位：{FormatUnitLabel(unitType)}，收购价：¥{buyPrice:0.##}，卖价：¥{sellPrice:0.##}{(isArchived ? "，状态：已归档" : string.Empty)}"
                    });

                    plans.Add(new CategoryImportPlan(
                        ExistingId: isUpdate ? existingCategory.Id : null,
                        Name: name,
                        BuyPrice: buyPrice,
                        SellPrice: sellPrice,
                        UnitType: unitType,
                        IsArchived: isArchived,
                        IsUpdate: isUpdate));
                }
                catch (Exception ex)
                {
                    issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = ex.Message });
                }
            }

            return (
                new ImportPreviewModel
                {
                    EntityDisplayName = "分类",
                    SourcePath = sourcePath,
                    TotalRowCount = rows.Count,
                    ReadyToImportCount = plans.Count,
                    InsertCount = plans.Count(plan => !plan.IsUpdate),
                    UpdateCount = plans.Count(plan => plan.IsUpdate),
                    SkippedCount = 0,
                    Rows = previewRows,
                    Issues = issues
                },
                plans);
        }

        private async Task<(ImportPreviewModel Preview, List<CustomerImportPlan> Plans)> BuildCustomerImportPreviewAsync(string sourcePath)
        {
            ValidateCsvSourcePath(sourcePath);

            var rows = await ReadCsvRowsAsync(sourcePath);

            var existingCustomers = new Dictionary<string, (long Id, bool HasInboundOrders, bool HasOutboundOrders)>(StringComparer.OrdinalIgnoreCase);
            await using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT id, name, has_inbound_orders, has_outbound_orders FROM customers;";

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingCustomers[reader.GetString(1).Trim()] = (reader.GetInt64(0), reader.GetInt64(2) == 1, reader.GetInt64(3) == 1);
                }
            }

            var issues = new List<ImportPreviewIssueModel>();
            var previewRows = new List<ImportPreviewRowModel>();
            var plans = new List<CustomerImportPlan>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var lineNumber = row.LineNumber;
                try
                {
                    var name = GetCsvValue(row.Values, "customer_name", "name", "客户名称", "客户")?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = "缺少客户名称。" });
                        continue;
                    }

                    if (!seenNames.Add(name))
                    {
                        issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = $"客户“{name}”在同一个 CSV 中重复出现。" });
                        continue;
                    }

                    var phone = GetCsvValue(row.Values, "phone", "mobile", "电话", "手机号")?.Trim() ?? string.Empty;
                    var email = GetCsvValue(row.Values, "email", "邮箱")?.Trim() ?? string.Empty;
                    var address = GetCsvValue(row.Values, "address", "地址")?.Trim() ?? string.Empty;
                    var note = GetCsvValue(row.Values, "note", "备注")?.Trim() ?? string.Empty;
                    var hasInboundOrders = ParseCsvBool(GetCsvValue(row.Values, "has_inbound_orders", "入库客户"), defaultValue: false);
                    var hasOutboundOrders = ParseCsvBool(GetCsvValue(row.Values, "has_outbound_orders", "出库客户"), defaultValue: false);

                    var isUpdate = existingCustomers.TryGetValue(name, out var existingCustomer);
                    var mergedInbound = isUpdate ? existingCustomer.HasInboundOrders || hasInboundOrders : hasInboundOrders;
                    var mergedOutbound = isUpdate ? existingCustomer.HasOutboundOrders || hasOutboundOrders : hasOutboundOrders;

                    previewRows.Add(new ImportPreviewRowModel
                    {
                        LineNumber = lineNumber,
                        Name = name,
                        ActionText = isUpdate ? "更新" : "新增",
                        DetailText = $"电话：{DisplayOrPlaceholder(phone)}，邮箱：{DisplayOrPlaceholder(email)}，标签：{BuildCustomerUsageText(mergedInbound, mergedOutbound)}"
                    });

                    plans.Add(new CustomerImportPlan(
                        ExistingId: isUpdate ? existingCustomer.Id : null,
                        Name: name,
                        Phone: phone,
                        Email: email,
                        Address: address,
                        Note: note,
                        HasInboundOrders: mergedInbound,
                        HasOutboundOrders: mergedOutbound,
                        IsUpdate: isUpdate));
                }
                catch (Exception ex)
                {
                    issues.Add(new ImportPreviewIssueModel { LineNumber = lineNumber, Message = ex.Message });
                }
            }

            return (
                new ImportPreviewModel
                {
                    EntityDisplayName = "客户",
                    SourcePath = sourcePath,
                    TotalRowCount = rows.Count,
                    ReadyToImportCount = plans.Count,
                    InsertCount = plans.Count(plan => !plan.IsUpdate),
                    UpdateCount = plans.Count(plan => plan.IsUpdate),
                    SkippedCount = 0,
                    Rows = previewRows,
                    Issues = issues
                },
                plans);
        }

        private static InvalidOperationException CreateImportPreviewException(ImportPreviewModel preview)
        {
            if (preview.IssueCount == 0)
            {
                return new InvalidOperationException("当前没有可导入的有效记录。");
            }

            var issueSummary = string.Join(
                Environment.NewLine,
                preview.Issues
                    .Take(3)
                    .Select(issue => $"{issue.LineText}：{issue.Message}"));

            if (preview.IssueCount > 3)
            {
                issueSummary += $"{Environment.NewLine}以及另外 {preview.IssueCount - 3} 个问题。";
            }

            return new InvalidOperationException($"导入预览未通过，请先修正以下问题：{Environment.NewLine}{issueSummary}");
        }

        private static void ValidateCsvSourcePath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new InvalidOperationException("未找到要导入的 CSV 文件。");
            }

            if (!string.Equals(Path.GetExtension(sourcePath), ".csv", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("当前只支持导入 CSV 文件。");
            }
        }

        private static async Task<IReadOnlyList<CsvRowData>> ReadCsvRowsAsync(string sourcePath)
        {
            var content = await File.ReadAllTextAsync(sourcePath, Encoding.UTF8);
            var rows = ParseCsvContent(content);
            if (rows.Count <= 1)
            {
                return [];
            }

            var headers = rows[0]
                .Values
                .Select(value => NormalizeCsvHeader(value))
                .ToList();

            var items = new List<CsvRowData>();
            foreach (var row in rows.Skip(1))
            {
                if (row.Values.All(value => string.IsNullOrWhiteSpace(value)))
                {
                    continue;
                }

                var item = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < headers.Count; index++)
                {
                    if (string.IsNullOrWhiteSpace(headers[index]))
                    {
                        continue;
                    }

                    item[headers[index]] = index < row.Values.Count ? row.Values[index].Trim() : string.Empty;
                }

                items.Add(new CsvRowData(row.LineNumber, item));
            }

            return items;
        }

        private static List<CsvRawRow> ParseCsvContent(string content)
        {
            var rows = new List<CsvRawRow>();
            var currentRow = new List<string>();
            var fieldBuilder = new StringBuilder();
            var inQuotes = false;
            var lineNumber = 1;

            for (var index = 0; index < content.Length; index++)
            {
                var current = content[index];

                if (inQuotes)
                {
                    if (current == '"')
                    {
                        if (index + 1 < content.Length && content[index + 1] == '"')
                        {
                            fieldBuilder.Append('"');
                            index++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        fieldBuilder.Append(current);
                    }

                    continue;
                }

                switch (current)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        currentRow.Add(fieldBuilder.ToString());
                        fieldBuilder.Clear();
                        break;
                    case '\r':
                        currentRow.Add(fieldBuilder.ToString());
                        fieldBuilder.Clear();
                        rows.Add(new CsvRawRow(lineNumber, currentRow));
                        currentRow = [];
                        if (index + 1 < content.Length && content[index + 1] == '\n')
                        {
                            index++;
                        }
                        lineNumber++;
                        break;
                    case '\n':
                        currentRow.Add(fieldBuilder.ToString());
                        fieldBuilder.Clear();
                        rows.Add(new CsvRawRow(lineNumber, currentRow));
                        currentRow = [];
                        lineNumber++;
                        break;
                    default:
                        fieldBuilder.Append(current);
                        break;
                }
            }

            currentRow.Add(fieldBuilder.ToString());
            rows.Add(new CsvRawRow(lineNumber, currentRow));

            while (rows.Count > 0 && rows[^1].Values.All(value => string.IsNullOrWhiteSpace(value)))
            {
                rows.RemoveAt(rows.Count - 1);
            }

            return rows;
        }

        private static string NormalizeCsvHeader(string header) =>
            header
                .Trim()
                .TrimStart('\uFEFF')
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();

        private static string? GetCsvValue(IReadOnlyDictionary<string, string> row, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var normalized = NormalizeCsvHeader(candidate);
                if (row.TryGetValue(normalized, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static double ParseCsvDouble(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"导入文件缺少“{fieldName}”字段的值。");
            }

            if (double.TryParse(value, CultureInfo.InvariantCulture, out var invariantValue))
            {
                return invariantValue;
            }

            if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var currentCultureValue))
            {
                return currentCultureValue;
            }

            throw new InvalidOperationException($"“{fieldName}”包含无法识别的数字：{value}");
        }

        private static bool ParseCsvBool(string? value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "1" or "true" or "yes" or "y" or "是" or "已归档" => true,
                "0" or "false" or "no" or "n" or "否" or "未归档" => false,
                _ => throw new InvalidOperationException($"无法识别的布尔值：{value}")
            };
        }

        private static WeightUnit ParseCsvUnit(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return WeightUnit.Kilogram;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "kilogram" or "kg" or "公斤" => WeightUnit.Kilogram,
                "jin" or "斤" => WeightUnit.Jin,
                "piece" or "pieces" or "件" => WeightUnit.Piece,
                _ => ParseUnit(value)
            };
        }

        private static string FormatUnitLabel(WeightUnit unitType) =>
            unitType switch
            {
                WeightUnit.Kilogram => "kg",
                WeightUnit.Jin => "斤",
                WeightUnit.Piece => "件",
                _ => "kg"
            };

        private static string DisplayOrPlaceholder(string value) =>
            string.IsNullOrWhiteSpace(value) ? "未填写" : value;

        private static string BuildCustomerUsageText(bool hasInboundOrders, bool hasOutboundOrders)
        {
            if (hasInboundOrders && hasOutboundOrders)
            {
                return "入库 / 出库";
            }

            if (hasInboundOrders)
            {
                return "入库";
            }

            if (hasOutboundOrders)
            {
                return "出库";
            }

            return "未标记";
        }

        private static async Task<double> GetAmountSumAsync(SqliteConnection connection, string table, string columnName, long start, long end)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT IFNULL(SUM({columnName}), 0)
                FROM {table}
                WHERE timestamp >= $start AND timestamp < $end;
                """;
            command.Parameters.AddWithValue("$start", start);
            command.Parameters.AddWithValue("$end", end);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToDouble(result, CultureInfo.InvariantCulture);
        }

        private static (long Start, long End) GetTodayRange()
        {
            var now = DateTimeOffset.Now;
            var start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
            var end = start.AddDays(1);
            return (start.ToUnixTimeMilliseconds(), end.ToUnixTimeMilliseconds());
        }

        private static WeightUnit ParseUnit(string value) =>
            Enum.TryParse<WeightUnit>(value, true, out var unit) ? unit : WeightUnit.Kilogram;
    }
}
