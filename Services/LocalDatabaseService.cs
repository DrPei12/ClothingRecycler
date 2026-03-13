using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClothingRecycler.Desktop.Models;
using Microsoft.Data.Sqlite;

namespace ClothingRecycler.Desktop.Services;

public sealed class LocalDatabaseService
{
	private readonly record struct StockMutationSnapshot(double PreviousQuantity, double AdjustedQuantity, double DeltaQuantity);

	private readonly record struct CategoryImportPlan(long? ExistingId, string Name, double BuyPrice, double SellPrice, WeightUnit UnitType, bool IsArchived, bool IsUpdate);

	private readonly record struct CustomerImportPlan(long? ExistingId, string Name, string Phone, string Email, string Address, string Note, bool HasInboundOrders, bool HasOutboundOrders, bool IsUpdate);

	private readonly record struct CsvRawRow(int LineNumber, List<string> Values);

	private readonly record struct CsvRowData(int LineNumber, IReadOnlyDictionary<string, string> Values);

	private const int BaseSchemaVersion = 1;

	private const int CategoryArchiveSchemaVersion = 2;

	private const int PerformanceIndexesSchemaVersion = 3;

	private const int LatestSchemaVersion = 3;

	private readonly AppLogger _logger;

	public string DatabasePath => AppDataPaths.DatabasePath;

	public string BackupDirectory => AppDataPaths.BackupDirectory;

	public string ExportDirectory => AppDataPaths.ExportDirectory;

	public int CurrentSchemaVersion { get; private set; } = 3;

	public LocalDatabaseService(AppLogger logger)
	{
		_logger = logger;
	}

	public async Task InitializeAsync()
	{
		AppDataPaths.EnsureDirectories();
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		int currentVersion = await GetCurrentSchemaVersionAsync(connection);
		if (currentVersion > 3)
		{
            throw new InvalidOperationException($"Database schema v{currentVersion} is newer than supported v3.");
		}
		if (currentVersion == 0)
		{
            await _logger.LogInfoAsync($"Detected empty database. Initializing schema v3.");
		}
		else if (currentVersion < 3)
		{
            await _logger.LogInfoAsync($"Upgrading database schema from v{currentVersion} to v3.");
		}
		for (int targetVersion = currentVersion + 1; targetVersion <= 3; targetVersion++)
		{
			await ApplyMigrationAsync(connection, targetVersion);
			await SetSchemaVersionAsync(connection, targetVersion);
            await _logger.LogInfoAsync($"Completed database migration v{targetVersion}: {GetMigrationName(targetVersion)}.");
		}
		CurrentSchemaVersion = ((currentVersion == 0 || currentVersion < 3) ? 3 : currentVersion);
		if (currentVersion == 3)
		{
            await _logger.LogInfoAsync($"Database schema already at latest version v{CurrentSchemaVersion}.");
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
            throw new InvalidOperationException("Backup file to restore was not found.");
		}
		string sourceFullPath = Path.GetFullPath(sourcePath);
		string fullPath = Path.GetFullPath(DatabasePath);
		if (string.Equals(sourceFullPath, fullPath, StringComparison.OrdinalIgnoreCase))
		{
            throw new InvalidOperationException("Selected file is the current database in use.");
		}
		string restorePointPath = null;
		if (File.Exists(DatabasePath))
		{
			restorePointPath = Path.Combine(BackupDirectory, $"restore-point-{DateTime.Now:yyyyMMdd-HHmmss}.db");
			await Task.Run(delegate
			{
				File.Copy(DatabasePath, restorePointPath, overwrite: true);
			});
		}
		await Task.Run(delegate
		{
			File.Copy(sourceFullPath, DatabasePath, overwrite: true);
		});
		return restorePointPath;
	}

	public async Task<string> ExportBusinessDataAsync()
	{
		AppDataPaths.EnsureDirectories();
		string exportPath = Path.Combine(ExportDirectory, $"business-export-{DateTime.Now:yyyyMMdd-HHmmss}");
		Directory.CreateDirectory(exportPath);
		string result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			await ExportQueryToCsvAsync(connection, Path.Combine(exportPath, "categories.csv"), "SELECT\n    id AS category_id,\n    name AS category_name,\n    unit_type,\n    buy_price,\n    sell_price,\n    stock,\n    stock_in_jin,\n    stock_in_pieces,\n    is_archived,\n    datetime(created_at / 1000, 'unixepoch', 'localtime') AS created_at_local,\n    datetime(updated_at / 1000, 'unixepoch', 'localtime') AS updated_at_local\nFROM categories\nORDER BY is_archived ASC, name COLLATE NOCASE;");
			await ExportQueryToCsvAsync(connection, Path.Combine(exportPath, "customers.csv"), "SELECT\n    id AS customer_id,\n    name AS customer_name,\n    phone,\n    email,\n    address,\n    note,\n    has_inbound_orders,\n    has_outbound_orders,\n    datetime(created_at / 1000, 'unixepoch', 'localtime') AS created_at_local,\n    datetime(updated_at / 1000, 'unixepoch', 'localtime') AS updated_at_local\nFROM customers\nORDER BY name COLLATE NOCASE;");
			await ExportQueryToCsvAsync(connection, Path.Combine(exportPath, "customer_price_memory.csv"), "SELECT\n    ccp.id AS price_memory_id,\n    ccp.customer_id,\n    c.name AS customer_name,\n    ccp.category_id,\n    cg.name AS category_name,\n    ccp.price,\n    datetime(ccp.updated_at / 1000, 'unixepoch', 'localtime') AS updated_at_local\nFROM customer_category_prices ccp\nINNER JOIN customers c ON c.id = ccp.customer_id\nINNER JOIN categories cg ON cg.id = ccp.category_id\nORDER BY c.name COLLATE NOCASE, cg.name COLLATE NOCASE;");
			await ExportQueryToCsvAsync(connection, Path.Combine(exportPath, "orders.csv"), "SELECT\n    o.id AS order_id,\n    o.order_number,\n    o.type,\n    CASE\n        WHEN o.type = 'outbound' THEN '鍑哄簱'\n        ELSE '鍏ュ簱'\n    END AS type_text,\n    IFNULL(c.name, '') AS customer_name,\n    o.total_weight,\n    o.total_amount,\n    o.category_count,\n    o.item_count,\n    o.note,\n    datetime(o.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local\nFROM orders o\nLEFT JOIN customers c ON c.id = o.customer_id\nORDER BY o.timestamp DESC;");
			await ExportQueryToCsvAsync(connection, Path.Combine(exportPath, "inbound_records.csv"), "SELECT\n    ir.id AS inbound_record_id,\n    ir.order_id,\n    IFNULL(o.order_number, '') AS order_number,\n    ir.category_id,\n    c.name AS category_name,\n    ir.weight,\n    ir.unit_price,\n    ir.total_cost,\n    ir.unit_type,\n    datetime(ir.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local\nFROM inbound_records ir\nINNER JOIN categories c ON c.id = ir.category_id\nLEFT JOIN orders o ON o.id = ir.order_id\nORDER BY ir.timestamp DESC;");
			await ExportQueryToCsvAsync(connection, Path.Combine(exportPath, "outbound_records.csv"), "SELECT\n    orr.id AS outbound_record_id,\n    orr.order_id,\n    IFNULL(o.order_number, '') AS order_number,\n    orr.category_id,\n    c.name AS category_name,\n    orr.weight,\n    orr.unit_price,\n    orr.total_revenue,\n    orr.unit_type,\n    datetime(orr.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local\nFROM outbound_records orr\nINNER JOIN categories c ON c.id = orr.category_id\nLEFT JOIN orders o ON o.id = orr.order_id\nORDER BY orr.timestamp DESC;");
			await ExportQueryToCsvAsync(connection, Path.Combine(exportPath, "stock_adjustments.csv"), "SELECT\n    sa.id AS adjustment_id,\n    sa.category_id,\n    c.name AS category_name,\n    sa.previous_quantity,\n    sa.adjusted_quantity,\n    sa.delta_quantity,\n    sa.unit_type,\n    sa.reason,\n    datetime(sa.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local\nFROM stock_adjustments sa\nINNER JOIN categories c ON c.id = sa.category_id\nORDER BY sa.timestamp DESC;");
			await ExportQueryToCsvAsync(connection, Path.Combine(exportPath, "stock_audits.csv"), "SELECT\n    sa.id AS audit_id,\n    sa.category_id,\n    c.name AS category_name,\n    sa.system_quantity,\n    sa.actual_quantity,\n    sa.delta_quantity,\n    sa.unit_type,\n    sa.note,\n    datetime(sa.timestamp / 1000, 'unixepoch', 'localtime') AS timestamp_local\nFROM stock_audits sa\nINNER JOIN categories c ON c.id = sa.category_id\nORDER BY sa.timestamp DESC;");
			result = exportPath;
		}
		return result;
	}

	public async Task<ImportPreviewModel> PreviewCategoriesImportAsync(string sourcePath)
	{
		return (await BuildCategoryImportPreviewAsync(sourcePath)).Item1;
	}

	public async Task<string> ImportCategoriesAsync(string sourcePath)
	{
		var (preview, plans) = await BuildCategoryImportPreviewAsync(sourcePath);
		if (!preview.CanImport)
		{
			throw CreateImportPreviewException(preview);
		}
		string result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			string text;
			await using (SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync()))
			{
				long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
				foreach (CategoryImportPlan item in plans)
				{
					if (item.IsUpdate)
					{
						SqliteCommand sqliteCommand = connection.CreateCommand();
						sqliteCommand.Transaction = transaction;
						sqliteCommand.CommandText = "UPDATE categories\nSET\n    buy_price = $buyPrice,\n    sell_price = $sellPrice,\n    is_archived = $isArchived,\n    updated_at = $updatedAt\nWHERE id = $id;";
						sqliteCommand.Parameters.AddWithValue("$id", item.ExistingId.Value);
						sqliteCommand.Parameters.AddWithValue("$buyPrice", item.BuyPrice);
						sqliteCommand.Parameters.AddWithValue("$sellPrice", item.SellPrice);
						sqliteCommand.Parameters.AddWithValue("$isArchived", item.IsArchived ? 1 : 0);
						sqliteCommand.Parameters.AddWithValue("$updatedAt", now);
						await sqliteCommand.ExecuteNonQueryAsync();
					}
					else
					{
						SqliteCommand sqliteCommand2 = connection.CreateCommand();
						sqliteCommand2.Transaction = transaction;
						sqliteCommand2.CommandText = "INSERT INTO categories (\n    name,\n    buy_price,\n    sell_price,\n    stock,\n    stock_in_jin,\n    stock_in_pieces,\n    unit_type,\n    created_at,\n    updated_at,\n    is_archived\n)\nVALUES (\n    $name,\n    $buyPrice,\n    $sellPrice,\n    0,\n    0,\n    0,\n    $unitType,\n    $createdAt,\n    $updatedAt,\n    $isArchived\n);";
						sqliteCommand2.Parameters.AddWithValue("$name", item.Name);
						sqliteCommand2.Parameters.AddWithValue("$buyPrice", item.BuyPrice);
						sqliteCommand2.Parameters.AddWithValue("$sellPrice", item.SellPrice);
						sqliteCommand2.Parameters.AddWithValue("$unitType", item.UnitType.ToString());
						sqliteCommand2.Parameters.AddWithValue("$createdAt", now);
						sqliteCommand2.Parameters.AddWithValue("$updatedAt", now);
						sqliteCommand2.Parameters.AddWithValue("$isArchived", item.IsArchived ? 1 : 0);
						await sqliteCommand2.ExecuteNonQueryAsync();
					}
				}
				await transaction.CommitAsync();
                text = $"Category import completed: inserted {preview.InsertCount}, updated {preview.UpdateCount}.";
			}
			result = text;
		}
		return result;
	}

	public async Task<ImportPreviewModel> PreviewCustomersImportAsync(string sourcePath)
	{
		return (await BuildCustomerImportPreviewAsync(sourcePath)).Item1;
	}

	public async Task<string> ImportCustomersAsync(string sourcePath)
	{
		var (preview, plans) = await BuildCustomerImportPreviewAsync(sourcePath);
		if (!preview.CanImport)
		{
			throw CreateImportPreviewException(preview);
		}
		string result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			string text;
			await using (SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync()))
			{
				long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
				foreach (CustomerImportPlan item in plans)
				{
					if (item.IsUpdate)
					{
						SqliteCommand sqliteCommand = connection.CreateCommand();
						sqliteCommand.Transaction = transaction;
						sqliteCommand.CommandText = "UPDATE customers\nSET\n    phone = $phone,\n    email = $email,\n    address = $address,\n    note = $note,\n    has_inbound_orders = $hasInboundOrders,\n    has_outbound_orders = $hasOutboundOrders,\n    updated_at = $updatedAt\nWHERE id = $id;";
						sqliteCommand.Parameters.AddWithValue("$id", item.ExistingId.Value);
						sqliteCommand.Parameters.AddWithValue("$phone", item.Phone);
						sqliteCommand.Parameters.AddWithValue("$email", item.Email);
						sqliteCommand.Parameters.AddWithValue("$address", item.Address);
						sqliteCommand.Parameters.AddWithValue("$note", item.Note);
						sqliteCommand.Parameters.AddWithValue("$hasInboundOrders", item.HasInboundOrders ? 1 : 0);
						sqliteCommand.Parameters.AddWithValue("$hasOutboundOrders", item.HasOutboundOrders ? 1 : 0);
						sqliteCommand.Parameters.AddWithValue("$updatedAt", now);
						await sqliteCommand.ExecuteNonQueryAsync();
					}
					else
					{
						SqliteCommand sqliteCommand2 = connection.CreateCommand();
						sqliteCommand2.Transaction = transaction;
						sqliteCommand2.CommandText = "INSERT INTO customers (\n    name,\n    phone,\n    email,\n    address,\n    note,\n    has_inbound_orders,\n    has_outbound_orders,\n    created_at,\n    updated_at\n)\nVALUES (\n    $name,\n    $phone,\n    $email,\n    $address,\n    $note,\n    $hasInboundOrders,\n    $hasOutboundOrders,\n    $createdAt,\n    $updatedAt\n);";
						sqliteCommand2.Parameters.AddWithValue("$name", item.Name);
						sqliteCommand2.Parameters.AddWithValue("$phone", item.Phone);
						sqliteCommand2.Parameters.AddWithValue("$email", item.Email);
						sqliteCommand2.Parameters.AddWithValue("$address", item.Address);
						sqliteCommand2.Parameters.AddWithValue("$note", item.Note);
						sqliteCommand2.Parameters.AddWithValue("$hasInboundOrders", item.HasInboundOrders ? 1 : 0);
						sqliteCommand2.Parameters.AddWithValue("$hasOutboundOrders", item.HasOutboundOrders ? 1 : 0);
						sqliteCommand2.Parameters.AddWithValue("$createdAt", now);
						sqliteCommand2.Parameters.AddWithValue("$updatedAt", now);
						await sqliteCommand2.ExecuteNonQueryAsync();
					}
				}
				await transaction.CommitAsync();
                text = $"Customer import completed: inserted {preview.InsertCount}, updated {preview.UpdateCount}.";
			}
			result = text;
		}
		return result;
	}

	public async Task<IReadOnlyList<CategoryModel>> GetCategoriesAsync(bool includeArchived = true)
	{
		List<CategoryModel> categories = new List<CategoryModel>();
		IReadOnlyList<CategoryModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = (includeArchived ? "SELECT id, name, buy_price, sell_price, stock, stock_in_jin, stock_in_pieces, unit_type, is_archived, created_at, updated_at\nFROM categories\nORDER BY is_archived ASC, name COLLATE NOCASE;" : "SELECT id, name, buy_price, sell_price, stock, stock_in_jin, stock_in_pieces, unit_type, is_archived, created_at, updated_at\nFROM categories\nWHERE is_archived = 0\nORDER BY name COLLATE NOCASE;");
			IReadOnlyList<CategoryModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
						IsArchived = (reader.GetInt64(8) == 1),
						CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
						UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))
					});
				}
				readOnlyList = categories;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<CategoryModel>> GetActiveCategoriesAsync()
	{
		return await GetCategoriesAsync(includeArchived: false);
	}

	public async Task<IReadOnlyList<CategoryManagementItemModel>> GetCategoryManagementItemsAsync()
	{
		List<CategoryManagementItemModel> items = new List<CategoryManagementItemModel>();
		IReadOnlyList<CategoryManagementItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    c.id,\n    c.name,\n    c.buy_price,\n    c.sell_price,\n    c.stock,\n    c.stock_in_jin,\n    c.stock_in_pieces,\n    c.unit_type,\n    c.is_archived,\n    c.created_at,\n    c.updated_at,\n    (SELECT COUNT(*) FROM inbound_records WHERE category_id = c.id) AS inbound_record_count,\n    (SELECT COUNT(*) FROM outbound_records WHERE category_id = c.id) AS outbound_record_count,\n    IFNULL((SELECT SUM(weight) FROM inbound_records WHERE category_id = c.id), 0) AS inbound_quantity,\n    IFNULL((SELECT SUM(weight) FROM outbound_records WHERE category_id = c.id), 0) AS outbound_quantity,\n    (\n        SELECT MAX(timestamp)\n        FROM (\n            SELECT timestamp FROM inbound_records WHERE category_id = c.id\n            UNION ALL\n            SELECT timestamp FROM outbound_records WHERE category_id = c.id\n        )\n    ) AS last_activity_at\nFROM categories c\nORDER BY c.is_archived ASC, last_activity_at DESC, c.name COLLATE NOCASE;";
			IReadOnlyList<CategoryManagementItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				while (await reader.ReadAsync())
				{
					CategoryModel category = new CategoryModel
					{
						Id = reader.GetInt64(0),
						Name = reader.GetString(1),
						BuyPrice = reader.GetDouble(2),
						SellPrice = reader.GetDouble(3),
						Stock = reader.GetDouble(4),
						StockInJin = reader.GetDouble(5),
						StockInPieces = reader.GetInt32(6),
						UnitType = ParseUnit(reader.GetString(7)),
						IsArchived = (reader.GetInt64(8) == 1),
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
						LastActivityAt = (reader.IsDBNull(15) ? ((DateTimeOffset?)null) : new DateTimeOffset?(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(15))))
					});
				}
				readOnlyList = items;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<StockCategoryItemModel>> GetStockCategoryItemsAsync()
	{
		List<StockCategoryItemModel> items = new List<StockCategoryItemModel>();
		IReadOnlyList<StockCategoryItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    c.id,\n    c.name,\n    c.buy_price,\n    c.sell_price,\n    c.stock,\n    c.stock_in_jin,\n    c.stock_in_pieces,\n    c.unit_type,\n    c.is_archived,\n    c.created_at,\n    c.updated_at,\n    (SELECT COUNT(*) FROM inbound_records WHERE category_id = c.id) AS inbound_record_count,\n    (SELECT COUNT(*) FROM outbound_records WHERE category_id = c.id) AS outbound_record_count,\n    (SELECT MAX(timestamp) FROM inbound_records WHERE category_id = c.id) AS last_inbound_at,\n    (SELECT MAX(timestamp) FROM outbound_records WHERE category_id = c.id) AS last_outbound_at\nFROM categories c\nORDER BY c.is_archived ASC, c.updated_at DESC, c.name COLLATE NOCASE;";
			IReadOnlyList<StockCategoryItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				while (await reader.ReadAsync())
				{
					CategoryModel category = new CategoryModel
					{
						Id = reader.GetInt64(0),
						Name = reader.GetString(1),
						BuyPrice = reader.GetDouble(2),
						SellPrice = reader.GetDouble(3),
						Stock = reader.GetDouble(4),
						StockInJin = reader.GetDouble(5),
						StockInPieces = reader.GetInt32(6),
						UnitType = ParseUnit(reader.GetString(7)),
						IsArchived = (reader.GetInt64(8) == 1),
						CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
						UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))
					};
					items.Add(new StockCategoryItemModel
					{
						Category = category,
						InboundRecordCount = reader.GetInt32(11),
						OutboundRecordCount = reader.GetInt32(12),
						LastInboundAt = (reader.IsDBNull(13) ? ((DateTimeOffset?)null) : new DateTimeOffset?(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(13)))),
						LastOutboundAt = (reader.IsDBNull(14) ? ((DateTimeOffset?)null) : new DateTimeOffset?(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(14))))
					});
				}
				readOnlyList = items;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<StockAdjustmentRecordModel>> GetRecentStockAdjustmentsAsync(int count, long? categoryId = null)
	{
		List<StockAdjustmentRecordModel> items = new List<StockAdjustmentRecordModel>();
		IReadOnlyList<StockAdjustmentRecordModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = (categoryId.HasValue ? "SELECT\n    sa.id,\n    sa.category_id,\n    c.name,\n    sa.previous_quantity,\n    sa.adjusted_quantity,\n    sa.delta_quantity,\n    sa.unit_type,\n    sa.reason,\n    sa.timestamp\nFROM stock_adjustments sa\nINNER JOIN categories c ON c.id = sa.category_id\nWHERE sa.category_id = $categoryId\nORDER BY sa.timestamp DESC\nLIMIT $count;" : "SELECT\n    sa.id,\n    sa.category_id,\n    c.name,\n    sa.previous_quantity,\n    sa.adjusted_quantity,\n    sa.delta_quantity,\n    sa.unit_type,\n    sa.reason,\n    sa.timestamp\nFROM stock_adjustments sa\nINNER JOIN categories c ON c.id = sa.category_id\nORDER BY sa.timestamp DESC\nLIMIT $count;");
			if (categoryId.HasValue)
			{
				sqliteCommand.Parameters.AddWithValue("$categoryId", categoryId.Value);
			}
			sqliteCommand.Parameters.AddWithValue("$count", count);
			IReadOnlyList<StockAdjustmentRecordModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = items;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<StockAuditRecordModel>> GetRecentStockAuditsAsync(int count, long? categoryId = null)
	{
		List<StockAuditRecordModel> items = new List<StockAuditRecordModel>();
		IReadOnlyList<StockAuditRecordModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = (categoryId.HasValue ? "SELECT\n    sa.id,\n    sa.category_id,\n    c.name,\n    sa.system_quantity,\n    sa.actual_quantity,\n    sa.delta_quantity,\n    sa.unit_type,\n    sa.note,\n    sa.timestamp\nFROM stock_audits sa\nINNER JOIN categories c ON c.id = sa.category_id\nWHERE sa.category_id = $categoryId\nORDER BY sa.timestamp DESC\nLIMIT $count;" : "SELECT\n    sa.id,\n    sa.category_id,\n    c.name,\n    sa.system_quantity,\n    sa.actual_quantity,\n    sa.delta_quantity,\n    sa.unit_type,\n    sa.note,\n    sa.timestamp\nFROM stock_audits sa\nINNER JOIN categories c ON c.id = sa.category_id\nORDER BY sa.timestamp DESC\nLIMIT $count;");
			if (categoryId.HasValue)
			{
				sqliteCommand.Parameters.AddWithValue("$categoryId", categoryId.Value);
			}
			sqliteCommand.Parameters.AddWithValue("$count", count);
			IReadOnlyList<StockAuditRecordModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = items;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<CustomerModel>> GetCustomersAsync()
	{
		List<CustomerModel> customers = new List<CustomerModel>();
		IReadOnlyList<CustomerModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    id,\n    name,\n    phone,\n    email,\n    address,\n    note,\n    has_inbound_orders,\n    has_outbound_orders,\n    created_at,\n    updated_at\nFROM customers\nORDER BY name COLLATE NOCASE;";
			IReadOnlyList<CustomerModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
						HasInboundOrders = (reader.GetInt64(6) == 1),
						HasOutboundOrders = (reader.GetInt64(7) == 1),
						CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
						UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9))
					});
				}
				readOnlyList = customers;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<CustomerModel?> GetCustomerAsync(long customerId)
	{
		CustomerModel result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    id,\n    name,\n    phone,\n    email,\n    address,\n    note,\n    has_inbound_orders,\n    has_outbound_orders,\n    created_at,\n    updated_at\nFROM customers\nWHERE id = $id\nLIMIT 1;";
			sqliteCommand.Parameters.AddWithValue("$id", customerId);
			CustomerModel customerModel;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				customerModel = ((await reader.ReadAsync()) ? new CustomerModel
				{
					Id = reader.GetInt64(0),
					Name = reader.GetString(1),
					Phone = reader.GetString(2),
					Email = reader.GetString(3),
					Address = reader.GetString(4),
					Note = reader.GetString(5),
					HasInboundOrders = (reader.GetInt64(6) == 1),
					HasOutboundOrders = (reader.GetInt64(7) == 1),
					CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
					UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9))
				} : null);
			}
			result = customerModel;
		}
		return result;
	}

	public async Task<IReadOnlyList<CustomerModel>> GetInboundCustomersAsync()
	{
		return await GetCustomersByUsageAsync("has_inbound_orders");
	}

	public async Task<IReadOnlyList<CustomerModel>> GetOutboundCustomersAsync()
	{
		return await GetCustomersByUsageAsync("has_outbound_orders");
	}

	public async Task<IReadOnlyList<CustomerCategoryPriceModel>> GetCustomerCategoryPricesAsync()
	{
		List<CustomerCategoryPriceModel> memories = new List<CustomerCategoryPriceModel>();
		IReadOnlyList<CustomerCategoryPriceModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT customer_id, category_id, price, updated_at\nFROM customer_category_prices\nORDER BY updated_at DESC;";
			IReadOnlyList<CustomerCategoryPriceModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = memories;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<InboundRecordModel>> GetRecentInboundRecordsAsync(int count)
	{
		List<InboundRecordModel> records = new List<InboundRecordModel>();
		IReadOnlyList<InboundRecordModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    ir.id,\n    ir.category_id,\n    c.name,\n    IFNULL(cu.name, ''),\n    ir.weight,\n    ir.unit_price,\n    ir.total_cost,\n    ir.unit_type,\n    ir.timestamp\nFROM inbound_records ir\nINNER JOIN categories c ON c.id = ir.category_id\nLEFT JOIN orders o ON o.id = ir.order_id\nLEFT JOIN customers cu ON cu.id = o.customer_id\nORDER BY ir.timestamp DESC\nLIMIT $count;";
			sqliteCommand.Parameters.AddWithValue("$count", count);
			IReadOnlyList<InboundRecordModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = records;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<OutboundRecordModel>> GetRecentOutboundRecordsAsync(int count)
	{
		List<OutboundRecordModel> records = new List<OutboundRecordModel>();
		IReadOnlyList<OutboundRecordModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    orr.id,\n    orr.category_id,\n    c.name,\n    IFNULL(cu.name, ''),\n    orr.weight,\n    orr.unit_price,\n    orr.total_revenue,\n    orr.unit_type,\n    orr.timestamp\nFROM outbound_records orr\nINNER JOIN categories c ON c.id = orr.category_id\nLEFT JOIN orders o ON o.id = orr.order_id\nLEFT JOIN customers cu ON cu.id = o.customer_id\nORDER BY orr.timestamp DESC\nLIMIT $count;";
			sqliteCommand.Parameters.AddWithValue("$count", count);
			IReadOnlyList<OutboundRecordModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = records;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<OrderListItemModel>> GetRecentOrdersAsync(string? type, int count)
	{
		List<OrderListItemModel> orders = new List<OrderListItemModel>();
		IReadOnlyList<OrderListItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    o.id,\n    o.order_number,\n    o.type,\n    IFNULL(c.name, ''),\n    o.total_weight,\n    o.total_amount,\n    o.category_count,\n    o.item_count,\n    o.timestamp\nFROM orders o\nLEFT JOIN customers c ON c.id = o.customer_id\nWHERE ($type IS NULL OR o.type = $type)\nORDER BY o.timestamp DESC\nLIMIT $count;";
			sqliteCommand.Parameters.AddWithValue("$count", count);
			SqliteParameter sqliteParameter = sqliteCommand.CreateParameter();
			sqliteParameter.ParameterName = "$type";
			sqliteParameter.Value = (string.IsNullOrWhiteSpace(type) ? ((IConvertible)DBNull.Value) : ((IConvertible)type));
			sqliteCommand.Parameters.Add(sqliteParameter);
			IReadOnlyList<OrderListItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = orders;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<OrderOverview> GetOrderOverviewAsync()
	{
		(long Start, long End) todayRange = GetTodayRange();
		OrderOverview result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    COUNT(*) AS total_count,\n    SUM(CASE WHEN timestamp >= $start AND timestamp < $end THEN 1 ELSE 0 END) AS today_count,\n    SUM(CASE WHEN type = 'inbound' THEN 1 ELSE 0 END) AS inbound_count,\n    SUM(CASE WHEN type = 'outbound' THEN 1 ELSE 0 END) AS outbound_count\nFROM orders;";
			sqliteCommand.Parameters.AddWithValue("$start", todayRange.Start);
			sqliteCommand.Parameters.AddWithValue("$end", todayRange.End);
			OrderOverview orderOverview;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				orderOverview = ((!(await reader.ReadAsync())) ? new OrderOverview() : new OrderOverview
				{
					TotalOrderCount = ((!reader.IsDBNull(0)) ? reader.GetInt32(0) : 0),
					TodayOrderCount = ((!reader.IsDBNull(1)) ? reader.GetInt32(1) : 0),
					InboundOrderCount = ((!reader.IsDBNull(2)) ? reader.GetInt32(2) : 0),
					OutboundOrderCount = ((!reader.IsDBNull(3)) ? reader.GetInt32(3) : 0)
				});
			}
			result = orderOverview;
		}
		return result;
	}

	public async Task<CustomerOverview> GetCustomerOverviewAsync()
	{
		(long Start, long End) todayRange = GetTodayRange();
		CustomerOverview result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    COUNT(*) AS total_count,\n    SUM(CASE WHEN has_inbound_orders = 1 THEN 1 ELSE 0 END) AS inbound_count,\n    SUM(CASE WHEN has_outbound_orders = 1 THEN 1 ELSE 0 END) AS outbound_count,\n    (\n        SELECT COUNT(DISTINCT customer_id)\n        FROM orders\n        WHERE customer_id IS NOT NULL\n            AND timestamp >= $start\n            AND timestamp < $end\n    ) AS active_today_count\nFROM customers;";
			sqliteCommand.Parameters.AddWithValue("$start", todayRange.Start);
			sqliteCommand.Parameters.AddWithValue("$end", todayRange.End);
			CustomerOverview customerOverview;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				customerOverview = ((!(await reader.ReadAsync())) ? new CustomerOverview() : new CustomerOverview
				{
					TotalCustomerCount = ((!reader.IsDBNull(0)) ? reader.GetInt32(0) : 0),
					InboundCustomerCount = ((!reader.IsDBNull(1)) ? reader.GetInt32(1) : 0),
					OutboundCustomerCount = ((!reader.IsDBNull(2)) ? reader.GetInt32(2) : 0),
					ActiveTodayCustomerCount = ((!reader.IsDBNull(3)) ? reader.GetInt32(3) : 0)
				});
			}
			result = customerOverview;
		}
		return result;
	}

	public async Task<AnalyticsOverview> GetAnalyticsOverviewAsync()
	{
		AnalyticsOverview result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    (SELECT IFNULL(SUM(total_cost), 0) FROM inbound_records) AS total_inbound_amount,\n    (SELECT IFNULL(SUM(total_revenue), 0) FROM outbound_records) AS total_outbound_amount,\n    (SELECT COUNT(DISTINCT customer_id) FROM orders WHERE customer_id IS NOT NULL) AS active_customer_count,\n    (SELECT COUNT(*) FROM orders) AS total_order_count;";
			AnalyticsOverview analyticsOverview;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				if (await reader.ReadAsync())
				{
					double num = (reader.IsDBNull(0) ? 0.0 : reader.GetDouble(0));
					double num2 = (reader.IsDBNull(1) ? 0.0 : reader.GetDouble(1));
					analyticsOverview = new AnalyticsOverview
					{
						TotalInboundAmount = num,
						TotalOutboundAmount = num2,
						EstimatedMargin = Math.Round(num2 - num, 2),
						ActiveCustomerCount = ((!reader.IsDBNull(2)) ? reader.GetInt32(2) : 0),
						TotalOrderCount = ((!reader.IsDBNull(3)) ? reader.GetInt32(3) : 0)
					};
				}
				else
				{
					analyticsOverview = new AnalyticsOverview();
				}
			}
			result = analyticsOverview;
		}
		return result;
	}

	public async Task<IReadOnlyList<DailyAnalyticsItemModel>> GetDailyAnalyticsAsync(int days)
	{
		Dictionary<DateOnly, DailyAnalyticsItemModel> items = new Dictionary<DateOnly, DailyAnalyticsItemModel>();
		DateTime date = DateTimeOffset.Now.Date;
		for (int num = days - 1; num >= 0; num--)
		{
			DateTime dateTime = date.AddDays(-num);
			items[DateOnly.FromDateTime(dateTime)] = new DailyAnalyticsItemModel
			{
				Date = dateTime,
				InboundAmount = 0.0,
				OutboundAmount = 0.0
			};
		}
		IReadOnlyList<DailyAnalyticsItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			await FillDailyAnalyticsAsync(connection, "inbound_records", "total_cost", days, items, isInbound: true);
			await FillDailyAnalyticsAsync(connection, "outbound_records", "total_revenue", days, items, isInbound: false);
			result = items.Values.OrderByDescending((DailyAnalyticsItemModel item) => item.Date).ToList();
		}
		return result;
	}

	public async Task<IReadOnlyList<CategoryAnalyticsItemModel>> GetTopInboundCategoriesAnalyticsAsync(int count)
	{
		return await GetTopCategoryAnalyticsAsync("inbound_records", "total_cost", count);
	}

	public async Task<IReadOnlyList<CategoryAnalyticsItemModel>> GetTopOutboundCategoriesAnalyticsAsync(int count)
	{
		return await GetTopCategoryAnalyticsAsync("outbound_records", "total_revenue", count);
	}

	public async Task<IReadOnlyList<CustomerAnalyticsItemModel>> GetTopCustomersAnalyticsAsync(int count)
	{
		List<CustomerAnalyticsItemModel> customers = new List<CustomerAnalyticsItemModel>();
		IReadOnlyList<CustomerAnalyticsItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    c.id,\n    c.name,\n    COUNT(o.id) AS order_count,\n    IFNULL(SUM(o.total_amount), 0) AS total_amount,\n    MAX(o.timestamp) AS last_transaction_at\nFROM customers c\nINNER JOIN orders o ON o.customer_id = c.id\nGROUP BY c.id, c.name\nORDER BY total_amount DESC, order_count DESC, c.name COLLATE NOCASE\nLIMIT $count;";
			sqliteCommand.Parameters.AddWithValue("$count", count);
			IReadOnlyList<CustomerAnalyticsItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				while (await reader.ReadAsync())
				{
					customers.Add(new CustomerAnalyticsItemModel
					{
						CustomerId = reader.GetInt64(0),
						CustomerName = reader.GetString(1),
						OrderCount = reader.GetInt32(2),
						TotalAmount = reader.GetDouble(3),
						LastTransactionAt = (reader.IsDBNull(4) ? ((DateTimeOffset?)null) : new DateTimeOffset?(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))))
					});
				}
				readOnlyList = customers;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<CustomerListItemModel>> GetCustomerListItemsAsync()
	{
		List<CustomerListItemModel> customers = new List<CustomerListItemModel>();
		IReadOnlyList<CustomerListItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    c.id,\n    c.name,\n    c.has_inbound_orders,\n    c.has_outbound_orders,\n    COUNT(o.id) AS total_orders,\n    SUM(CASE WHEN o.type = 'inbound' THEN 1 ELSE 0 END) AS inbound_orders,\n    SUM(CASE WHEN o.type = 'outbound' THEN 1 ELSE 0 END) AS outbound_orders,\n    MAX(o.timestamp) AS last_transaction_at\nFROM customers c\nLEFT JOIN orders o ON o.customer_id = c.id\nGROUP BY c.id, c.name, c.has_inbound_orders, c.has_outbound_orders\nORDER BY last_transaction_at DESC, c.name COLLATE NOCASE;";
			IReadOnlyList<CustomerListItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				while (await reader.ReadAsync())
				{
					customers.Add(new CustomerListItemModel
					{
						Id = reader.GetInt64(0),
						Name = reader.GetString(1),
						HasInboundOrders = (reader.GetInt64(2) == 1),
						HasOutboundOrders = (reader.GetInt64(3) == 1),
						TotalOrderCount = ((!reader.IsDBNull(4)) ? reader.GetInt32(4) : 0),
						InboundOrderCount = ((!reader.IsDBNull(5)) ? reader.GetInt32(5) : 0),
						OutboundOrderCount = ((!reader.IsDBNull(6)) ? reader.GetInt32(6) : 0),
						LastTransactionAt = (reader.IsDBNull(7) ? ((DateTimeOffset?)null) : new DateTimeOffset?(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7))))
					});
				}
				readOnlyList = customers;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<CustomerDetailSummary> GetCustomerDetailSummaryAsync(long customerId)
	{
		CustomerDetailSummary result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    COUNT(*) AS total_orders,\n    SUM(CASE WHEN type = 'inbound' THEN 1 ELSE 0 END) AS inbound_orders,\n    SUM(CASE WHEN type = 'outbound' THEN 1 ELSE 0 END) AS outbound_orders,\n    SUM(CASE WHEN type = 'inbound' THEN total_amount ELSE 0 END) AS inbound_amount,\n    SUM(CASE WHEN type = 'outbound' THEN total_amount ELSE 0 END) AS outbound_amount,\n    MAX(timestamp) AS last_transaction_at\nFROM orders\nWHERE customer_id = $customerId;";
			sqliteCommand.Parameters.AddWithValue("$customerId", customerId);
			CustomerDetailSummary customerDetailSummary;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				customerDetailSummary = ((!(await reader.ReadAsync())) ? new CustomerDetailSummary() : new CustomerDetailSummary
				{
					TotalOrderCount = ((!reader.IsDBNull(0)) ? reader.GetInt32(0) : 0),
					InboundOrderCount = ((!reader.IsDBNull(1)) ? reader.GetInt32(1) : 0),
					OutboundOrderCount = ((!reader.IsDBNull(2)) ? reader.GetInt32(2) : 0),
					InboundAmount = (reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3)),
					OutboundAmount = (reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4)),
					LastTransactionAt = (reader.IsDBNull(5) ? ((DateTimeOffset?)null) : new DateTimeOffset?(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5))))
				});
			}
			result = customerDetailSummary;
		}
		return result;
	}

	public async Task<IReadOnlyList<OrderListItemModel>> GetCustomerRecentOrdersAsync(long customerId, int count)
	{
		List<OrderListItemModel> orders = new List<OrderListItemModel>();
		IReadOnlyList<OrderListItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    o.id,\n    o.order_number,\n    o.type,\n    IFNULL(c.name, ''),\n    o.total_weight,\n    o.total_amount,\n    o.category_count,\n    o.item_count,\n    o.timestamp\nFROM orders o\nLEFT JOIN customers c ON c.id = o.customer_id\nWHERE o.customer_id = $customerId\nORDER BY o.timestamp DESC\nLIMIT $count;";
			sqliteCommand.Parameters.AddWithValue("$customerId", customerId);
			sqliteCommand.Parameters.AddWithValue("$count", count);
			IReadOnlyList<OrderListItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = orders;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<IReadOnlyList<OrderDetailItemModel>> GetOrderDetailItemsAsync(long orderId)
	{
		List<OrderDetailItemModel> items = new List<OrderDetailItemModel>();
		IReadOnlyList<OrderDetailItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT category_id, category_name, quantity, unit_price, line_amount, unit_type\nFROM (\n    SELECT\n        ir.category_id AS category_id,\n        c.name AS category_name,\n        ir.weight AS quantity,\n        ir.unit_price AS unit_price,\n        ir.total_cost AS line_amount,\n        ir.unit_type AS unit_type\n    FROM inbound_records ir\n    INNER JOIN categories c ON c.id = ir.category_id\n    WHERE ir.order_id = $orderId\n\n    UNION ALL\n\n    SELECT\n        orr.category_id AS category_id,\n        c.name AS category_name,\n        orr.weight AS quantity,\n        orr.unit_price AS unit_price,\n        orr.total_revenue AS line_amount,\n        orr.unit_type AS unit_type\n    FROM outbound_records orr\n    INNER JOIN categories c ON c.id = orr.category_id\n    WHERE orr.order_id = $orderId\n)\nORDER BY category_name COLLATE NOCASE;";
			sqliteCommand.Parameters.AddWithValue("$orderId", orderId);
			IReadOnlyList<OrderDetailItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = items;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<OrderEditModel> GetOrderEditModelAsync(long orderId)
	{
		OrderEditModel result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    o.id,\n    o.order_number,\n    o.type,\n    o.customer_id,\n    IFNULL(c.name, ''),\n    d.category_id,\n    d.quantity,\n    d.unit_price,\n    d.unit_type,\n    o.timestamp\nFROM orders o\nLEFT JOIN customers c ON c.id = o.customer_id\nINNER JOIN (\n    SELECT\n        ir.order_id,\n        ir.category_id,\n        ir.weight AS quantity,\n        ir.unit_price,\n        ir.unit_type\n    FROM inbound_records ir\n\n    UNION ALL\n\n    SELECT\n        orr.order_id,\n        orr.category_id,\n        orr.weight AS quantity,\n        orr.unit_price,\n        orr.unit_type\n    FROM outbound_records orr\n) d ON d.order_id = o.id\nWHERE o.id = $orderId;";
			sqliteCommand.Parameters.AddWithValue("$orderId", orderId);
			List<OrderEditModel> records = new List<OrderEditModel>();
			OrderEditModel orderEditModel;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
				while (await reader.ReadAsync())
				{
					records.Add(new OrderEditModel
					{
						OrderId = reader.GetInt64(0),
						OrderNumber = reader.GetString(1),
						Type = reader.GetString(2),
						CustomerId = (reader.IsDBNull(3) ? ((long?)null) : new long?(reader.GetInt64(3))),
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
                    throw new InvalidOperationException("Order to edit was not found.");
				}
				if (records.Count > 1)
				{
                    throw new InvalidOperationException("Only single-detail orders can be edited.");
				}
				orderEditModel = records[0];
			}
			result = orderEditModel;
		}
		return result;
	}

	public async Task UpdateOrderAsync(OrderEditModel order)
	{
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		await using SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync());
		OrderEditModel originalOrder = await GetOrderEditModelAsync(connection, transaction, order.OrderId);
		CategoryModel newCategory = await GetCategoryAsync(connection, transaction, order.CategoryId);
		double normalizedQuantity = NormalizeQuantity(order.Quantity, newCategory.UnitType);
		if (normalizedQuantity <= 0.0)
		{
            throw new InvalidOperationException("Order quantity must be greater than 0.");
		}
		if (order.UnitPrice <= 0.0)
		{
            throw new InvalidOperationException("Order unit price must be greater than 0.");
		}
		if (originalOrder.IsOutbound)
		{
			double num = GetAvailableStock(newCategory.Stock, newCategory.StockInJin, newCategory.StockInPieces, newCategory.UnitType);
			if (newCategory.Id == originalOrder.CategoryId)
			{
				num += originalOrder.Quantity;
			}
			if (normalizedQuantity > num + 0.0001)
			{
                throw new InvalidOperationException("Insufficient stock after modification.");
			}
		}
		string text = order.CustomerName.Trim();
		long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		long? customerId = null;
		if (!string.IsNullOrWhiteSpace(text))
		{
			customerId = await GetOrCreateCustomerAsync(connection, transaction, text, now, !originalOrder.IsOutbound, originalOrder.IsOutbound);
		}
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "DELETE FROM inbound_records WHERE order_id = $orderId;";
		sqliteCommand.Parameters.AddWithValue("$orderId", order.OrderId);
		await sqliteCommand.ExecuteNonQueryAsync();
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.Transaction = transaction;
		sqliteCommand2.CommandText = "DELETE FROM outbound_records WHERE order_id = $orderId;";
		sqliteCommand2.Parameters.AddWithValue("$orderId", order.OrderId);
		await sqliteCommand2.ExecuteNonQueryAsync();
		double totalAmount = Math.Round(normalizedQuantity * order.UnitPrice, 2);
		SqliteCommand sqliteCommand3 = connection.CreateCommand();
		sqliteCommand3.Transaction = transaction;
		sqliteCommand3.CommandText = "UPDATE orders\nSET\n    total_weight = $totalWeight,\n    total_amount = $totalAmount,\n    category_count = 1,\n    item_count = 1,\n    customer_id = $customerId\nWHERE id = $orderId;";
		sqliteCommand3.Parameters.AddWithValue("$orderId", order.OrderId);
		sqliteCommand3.Parameters.AddWithValue("$totalWeight", normalizedQuantity);
		sqliteCommand3.Parameters.AddWithValue("$totalAmount", totalAmount);
		SqliteParameter sqliteParameter = sqliteCommand3.CreateParameter();
		sqliteParameter.ParameterName = "$customerId";
		sqliteParameter.Value = (customerId.HasValue ? ((object)customerId.Value) : DBNull.Value);
		sqliteCommand3.Parameters.Add(sqliteParameter);
		await sqliteCommand3.ExecuteNonQueryAsync();
		SqliteCommand sqliteCommand4 = connection.CreateCommand();
		sqliteCommand4.Transaction = transaction;
		if (originalOrder.IsOutbound)
		{
			sqliteCommand4.CommandText = "INSERT INTO outbound_records (\n    order_id, category_id, weight, unit_price, total_revenue, unit_type, timestamp\n)\nVALUES (\n    $orderId, $categoryId, $weight, $unitPrice, $totalAmount, $unitType, $timestamp\n);";
		}
		else
		{
			sqliteCommand4.CommandText = "INSERT INTO inbound_records (\n    order_id, category_id, weight, unit_price, total_cost, unit_type, timestamp\n)\nVALUES (\n    $orderId, $categoryId, $weight, $unitPrice, $totalAmount, $unitType, $timestamp\n);";
		}
		sqliteCommand4.Parameters.AddWithValue("$orderId", order.OrderId);
		sqliteCommand4.Parameters.AddWithValue("$categoryId", newCategory.Id);
		sqliteCommand4.Parameters.AddWithValue("$weight", normalizedQuantity);
		sqliteCommand4.Parameters.AddWithValue("$unitPrice", order.UnitPrice);
		sqliteCommand4.Parameters.AddWithValue("$totalAmount", totalAmount);
		sqliteCommand4.Parameters.AddWithValue("$unitType", newCategory.UnitType.ToString());
		sqliteCommand4.Parameters.AddWithValue("$timestamp", order.Timestamp.ToUnixTimeMilliseconds());
		await sqliteCommand4.ExecuteNonQueryAsync();
		await RebuildAllCategoryDataAsync(connection, transaction);
		HashSet<long> hashSet = new HashSet<long>();
		if (originalOrder.CustomerId.HasValue)
		{
			hashSet.Add(originalOrder.CustomerId.Value);
		}
		if (customerId.HasValue)
		{
			hashSet.Add(customerId.Value);
		}
		foreach (long affectedCustomerId in hashSet)
		{
			await UpdateCustomerUsageFlagsAsync(connection, transaction, affectedCustomerId);
			await RebuildCustomerPriceMemoriesAsync(connection, transaction, affectedCustomerId);
		}
		await transaction.CommitAsync();
	}

	public async Task<IReadOnlyList<CustomerPriceMemoryItemModel>> GetCustomerPriceMemoriesAsync(long customerId)
	{
		List<CustomerPriceMemoryItemModel> memories = new List<CustomerPriceMemoryItemModel>();
		IReadOnlyList<CustomerPriceMemoryItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    p.category_id,\n    c.name,\n    p.price,\n    p.updated_at\nFROM customer_category_prices p\nINNER JOIN categories c ON c.id = p.category_id\nWHERE p.customer_id = $customerId\nORDER BY p.updated_at DESC, c.name COLLATE NOCASE;";
			sqliteCommand.Parameters.AddWithValue("$customerId", customerId);
			IReadOnlyList<CustomerPriceMemoryItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = memories;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<long> SaveCustomerAsync(CustomerModel customer)
	{
		long result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			long num2;
			await using (SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync()))
			{
				string normalizedName = customer.Name.Trim();
				if (string.IsNullOrWhiteSpace(normalizedName))
				{
                    throw new InvalidOperationException("Customer name cannot be empty.");
				}
				await EnsureCustomerNameAvailableAsync(connection, transaction, customer.Id, normalizedName);
				long num = DateTimeOffset.Now.ToUnixTimeMilliseconds();
				long customerId;
				if (customer.Id == 0L)
				{
					SqliteCommand sqliteCommand = connection.CreateCommand();
					sqliteCommand.Transaction = transaction;
					sqliteCommand.CommandText = "INSERT INTO customers (\n    name, phone, email, address, note, has_inbound_orders, has_outbound_orders, created_at, updated_at\n)\nVALUES (\n    $name, $phone, $email, $address, $note, 0, 0, $createdAt, $updatedAt\n);";
					sqliteCommand.Parameters.AddWithValue("$name", normalizedName);
					sqliteCommand.Parameters.AddWithValue("$phone", customer.Phone.Trim());
					sqliteCommand.Parameters.AddWithValue("$email", customer.Email.Trim());
					sqliteCommand.Parameters.AddWithValue("$address", customer.Address.Trim());
					sqliteCommand.Parameters.AddWithValue("$note", customer.Note.Trim());
					sqliteCommand.Parameters.AddWithValue("$createdAt", num);
					sqliteCommand.Parameters.AddWithValue("$updatedAt", num);
					await sqliteCommand.ExecuteNonQueryAsync();
					customerId = await GetLastInsertRowIdAsync(connection, transaction);
				}
				else
				{
					SqliteCommand sqliteCommand2 = connection.CreateCommand();
					sqliteCommand2.Transaction = transaction;
					sqliteCommand2.CommandText = "UPDATE customers\nSET\n    name = $name,\n    phone = $phone,\n    email = $email,\n    address = $address,\n    note = $note,\n    updated_at = $updatedAt\nWHERE id = $id;";
					sqliteCommand2.Parameters.AddWithValue("$id", customer.Id);
					sqliteCommand2.Parameters.AddWithValue("$name", normalizedName);
					sqliteCommand2.Parameters.AddWithValue("$phone", customer.Phone.Trim());
					sqliteCommand2.Parameters.AddWithValue("$email", customer.Email.Trim());
					sqliteCommand2.Parameters.AddWithValue("$address", customer.Address.Trim());
					sqliteCommand2.Parameters.AddWithValue("$note", customer.Note.Trim());
					sqliteCommand2.Parameters.AddWithValue("$updatedAt", num);
					await sqliteCommand2.ExecuteNonQueryAsync();
					customerId = customer.Id;
				}
				await transaction.CommitAsync();
				num2 = customerId;
			}
			result = num2;
		}
		return result;
	}

	public async Task DeleteOrderAsync(long orderId)
	{
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		await using SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync());
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT customer_id\nFROM orders\nWHERE id = $orderId\nLIMIT 1;";
		sqliteCommand.Parameters.AddWithValue("$orderId", orderId);
		object customerIdValue = await sqliteCommand.ExecuteScalarAsync();
		if (customerIdValue == null)
		{
            throw new InvalidOperationException("Order to delete was not found.");
		}
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.Transaction = transaction;
		sqliteCommand2.CommandText = "DELETE FROM inbound_records WHERE order_id = $orderId;";
		sqliteCommand2.Parameters.AddWithValue("$orderId", orderId);
		await sqliteCommand2.ExecuteNonQueryAsync();
		SqliteCommand sqliteCommand3 = connection.CreateCommand();
		sqliteCommand3.Transaction = transaction;
		sqliteCommand3.CommandText = "DELETE FROM outbound_records WHERE order_id = $orderId;";
		sqliteCommand3.Parameters.AddWithValue("$orderId", orderId);
		await sqliteCommand3.ExecuteNonQueryAsync();
		SqliteCommand sqliteCommand4 = connection.CreateCommand();
		sqliteCommand4.Transaction = transaction;
		sqliteCommand4.CommandText = "DELETE FROM orders WHERE id = $orderId;";
		sqliteCommand4.Parameters.AddWithValue("$orderId", orderId);
		await sqliteCommand4.ExecuteNonQueryAsync();
		await RebuildAllCategoryDataAsync(connection, transaction);
		if (!(customerIdValue is DBNull))
		{
			long customerId = Convert.ToInt64(customerIdValue, CultureInfo.InvariantCulture);
			await UpdateCustomerUsageFlagsAsync(connection, transaction, customerId);
			await RebuildCustomerPriceMemoriesAsync(connection, transaction, customerId);
		}
		await transaction.CommitAsync();
	}

	public async Task SaveCategoryAsync(CategoryModel category)
	{
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		long num = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		if (category.Id == 0L)
		{
			sqliteCommand.CommandText = "INSERT INTO categories (\n    name, buy_price, sell_price, stock, stock_in_jin, stock_in_pieces, unit_type, is_archived, created_at, updated_at\n)\nVALUES (\n    $name, $buyPrice, $sellPrice, $stock, $stockInJin, $stockInPieces, $unitType, $isArchived, $createdAt, $updatedAt\n);";
			sqliteCommand.Parameters.AddWithValue("$createdAt", num);
		}
		else
		{
			sqliteCommand.CommandText = "UPDATE categories\nSET\n    name = $name,\n    buy_price = $buyPrice,\n    sell_price = $sellPrice,\n    stock = $stock,\n    stock_in_jin = $stockInJin,\n    stock_in_pieces = $stockInPieces,\n    unit_type = $unitType,\n    is_archived = $isArchived,\n    updated_at = $updatedAt\nWHERE id = $id;";
			sqliteCommand.Parameters.AddWithValue("$id", category.Id);
		}
		sqliteCommand.Parameters.AddWithValue("$name", category.Name.Trim());
		sqliteCommand.Parameters.AddWithValue("$buyPrice", category.BuyPrice);
		sqliteCommand.Parameters.AddWithValue("$sellPrice", category.SellPrice);
		sqliteCommand.Parameters.AddWithValue("$stock", category.Stock);
		sqliteCommand.Parameters.AddWithValue("$stockInJin", category.StockInJin);
		sqliteCommand.Parameters.AddWithValue("$stockInPieces", category.StockInPieces);
		sqliteCommand.Parameters.AddWithValue("$unitType", category.UnitType.ToString());
		sqliteCommand.Parameters.AddWithValue("$isArchived", category.IsArchived ? 1 : 0);
		sqliteCommand.Parameters.AddWithValue("$updatedAt", num);
		await sqliteCommand.ExecuteNonQueryAsync();
	}

	public async Task SetCategoryArchivedAsync(long categoryId, bool isArchived)
	{
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "UPDATE categories\nSET\n    is_archived = $isArchived,\n    updated_at = $updatedAt\nWHERE id = $id;";
		sqliteCommand.Parameters.AddWithValue("$id", categoryId);
		sqliteCommand.Parameters.AddWithValue("$isArchived", isArchived ? 1 : 0);
		sqliteCommand.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToUnixTimeMilliseconds());
		await sqliteCommand.ExecuteNonQueryAsync();
	}

	public async Task AdjustCategoryStockAsync(long categoryId, double adjustedQuantity, string? reason)
	{
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		await using SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync());
		CategoryModel category = await GetCategoryAsync(connection, transaction, categoryId);
		long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		await ApplyStockMutationAsync(connection, transaction, category, adjustedQuantity, reason, timestamp, allowSameQuantity: false);
		await transaction.CommitAsync();
	}

	public async Task CreateStockAuditAsync(long categoryId, double actualQuantity, string? note)
	{
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		await using SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync());
		CategoryModel category = await GetCategoryAsync(connection, transaction, categoryId);
		long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		string noteText = note?.Trim() ?? string.Empty;
        StockMutationSnapshot stockMutationSnapshot = await ApplyStockMutationAsync(connection, transaction, category, actualQuantity, string.IsNullOrWhiteSpace(noteText) ? "Audit adjustment" : ("Audit adjustment: " + noteText), now, allowSameQuantity: true);
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "INSERT INTO stock_audits (\n    category_id,\n    system_quantity,\n    actual_quantity,\n    delta_quantity,\n    unit_type,\n    note,\n    timestamp\n)\nVALUES (\n    $categoryId,\n    $systemQuantity,\n    $actualQuantity,\n    $deltaQuantity,\n    $unitType,\n    $note,\n    $timestamp\n);";
		sqliteCommand.Parameters.AddWithValue("$categoryId", categoryId);
		sqliteCommand.Parameters.AddWithValue("$systemQuantity", stockMutationSnapshot.PreviousQuantity);
		sqliteCommand.Parameters.AddWithValue("$actualQuantity", stockMutationSnapshot.AdjustedQuantity);
		sqliteCommand.Parameters.AddWithValue("$deltaQuantity", stockMutationSnapshot.DeltaQuantity);
		sqliteCommand.Parameters.AddWithValue("$unitType", category.UnitType.ToString());
		sqliteCommand.Parameters.AddWithValue("$note", noteText);
		sqliteCommand.Parameters.AddWithValue("$timestamp", now);
		await sqliteCommand.ExecuteNonQueryAsync();
		await transaction.CommitAsync();
	}

	public async Task AddInboundRecordAsync(long categoryId, double quantity, double unitPrice, string? customerName)
	{
		await AddInboundOrderAsync(customerName,
		[
			new InboundOrderLineInputModel
			{
				CategoryId = categoryId,
				Quantity = quantity,
				UnitPrice = unitPrice
			}
		]);
	}

	public async Task<InboundOrderConfirmationModel> AddInboundOrderAsync(string? customerName, IReadOnlyList<InboundOrderLineInputModel> lines)
	{
		ArgumentNullException.ThrowIfNull(lines);
		List<InboundOrderLineInputModel> list = lines.Where((InboundOrderLineInputModel line) => line.Quantity > 0.0).ToList();
		if (list.Count == 0)
		{
            throw new InvalidOperationException("Please fill at least one inbound quantity.");
		}
		if (list.Any((InboundOrderLineInputModel line) => line.UnitPrice <= 0.0))
		{
            throw new InvalidOperationException("All filled inbound lines must have a unit price greater than 0.");
		}

		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		await using SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync());
		long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		string normalizedCustomerName = customerName?.Trim() ?? string.Empty;

		long? customerId = null;
		if (!string.IsNullOrWhiteSpace(normalizedCustomerName))
		{
			customerId = await GetOrCreateCustomerAsync(connection, transaction, normalizedCustomerName, now, markInbound: true, markOutbound: false);
		}

		List<OrderDetailItemModel> detailItems = new List<OrderDetailItemModel>(list.Count);
		foreach (InboundOrderLineInputModel item in list)
		{
			SqliteCommand categoryCommand = connection.CreateCommand();
			categoryCommand.Transaction = transaction;
			categoryCommand.CommandText =
				"""
				SELECT name, stock, stock_in_jin, stock_in_pieces, unit_type
				FROM categories
				WHERE id = $id;
				""";
			categoryCommand.Parameters.AddWithValue("$id", item.CategoryId);

			string categoryName;
			double stock;
			double stockInJin;
			int stockInPieces;
			WeightUnit unitType;
			await using (SqliteDataReader reader = await categoryCommand.ExecuteReaderAsync())
			{
				if (!(await reader.ReadAsync()))
				{
                    throw new InvalidOperationException("Inbound category was not found.");
				}

				categoryName = reader.GetString(0);
				stock = reader.GetDouble(1);
				stockInJin = reader.GetDouble(2);
				stockInPieces = reader.GetInt32(3);
				unitType = ParseUnit(reader.GetString(4));
			}

			double normalizedQuantity = NormalizeQuantity(item.Quantity, unitType);
			if (normalizedQuantity <= 0.0)
			{
				continue;
			}

			double normalizedUnitPrice = Math.Round(item.UnitPrice, 2);
			double totalCost = Math.Round(normalizedQuantity * normalizedUnitPrice, 2);
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

			SqliteCommand updateCategoryCommand = connection.CreateCommand();
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
			updateCategoryCommand.Parameters.AddWithValue("$id", item.CategoryId);
			updateCategoryCommand.Parameters.AddWithValue("$buyPrice", normalizedUnitPrice);
			updateCategoryCommand.Parameters.AddWithValue("$stock", stock);
			updateCategoryCommand.Parameters.AddWithValue("$stockInJin", stockInJin);
			updateCategoryCommand.Parameters.AddWithValue("$stockInPieces", stockInPieces);
			updateCategoryCommand.Parameters.AddWithValue("$updatedAt", now);
			await updateCategoryCommand.ExecuteNonQueryAsync();

			if (customerId.HasValue)
			{
				await UpsertCustomerCategoryPriceAsync(connection, transaction, customerId.Value, item.CategoryId, normalizedUnitPrice, now);
			}

			detailItems.Add(new OrderDetailItemModel
			{
				CategoryId = item.CategoryId,
				CategoryName = categoryName,
				Quantity = normalizedQuantity,
				UnitType = unitType,
				UnitPrice = normalizedUnitPrice,
				LineAmount = totalCost
			});
		}

		if (detailItems.Count == 0)
		{
            throw new InvalidOperationException("Please fill at least one valid inbound line.");
		}

		string orderNumber = CreateOrderNumber("inbound", now, normalizedCustomerName);
		double totalQuantity = Math.Round(detailItems.Sum((OrderDetailItemModel item) => item.Quantity), 2);
		double totalAmount = Math.Round(detailItems.Sum((OrderDetailItemModel item) => item.LineAmount), 2);
		int categoryCount = detailItems.Select((OrderDetailItemModel item) => item.CategoryId).Distinct().Count();
		int itemCount = CalculateOrderItemCount(detailItems);
		long orderId = await InsertOrderAsync(connection, transaction, orderNumber, "inbound", customerId, totalQuantity, totalAmount, categoryCount, itemCount, now);

		foreach (OrderDetailItemModel detailItem in detailItems)
		{
			SqliteCommand insertRecordCommand = connection.CreateCommand();
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
			insertRecordCommand.Parameters.AddWithValue("$orderId", orderId);
			insertRecordCommand.Parameters.AddWithValue("$categoryId", detailItem.CategoryId);
			insertRecordCommand.Parameters.AddWithValue("$weight", detailItem.Quantity);
			insertRecordCommand.Parameters.AddWithValue("$unitPrice", detailItem.UnitPrice);
			insertRecordCommand.Parameters.AddWithValue("$totalCost", detailItem.LineAmount);
			insertRecordCommand.Parameters.AddWithValue("$unitType", detailItem.UnitType.ToString());
			insertRecordCommand.Parameters.AddWithValue("$timestamp", now);
			await insertRecordCommand.ExecuteNonQueryAsync();
		}

		await transaction.CommitAsync();
		return new InboundOrderConfirmationModel
		{
			OrderId = orderId,
			OrderNumber = orderNumber,
			CustomerName = normalizedCustomerName,
			Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(now),
			CategoryCount = categoryCount,
			ItemCount = itemCount,
			TotalAmount = totalAmount,
			Items = detailItems.OrderBy((OrderDetailItemModel item) => item.CategoryName, StringComparer.CurrentCultureIgnoreCase).ToList()
		};
	}
public async Task<OutboundOrderConfirmationModel> AddOutboundRecordAsync(long categoryId, double quantity, double unitPrice, string? customerName)
	{
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		await using SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync());
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT name, stock, stock_in_jin, stock_in_pieces, unit_type\nFROM categories\nWHERE id = $id;";
		sqliteCommand.Parameters.AddWithValue("$id", categoryId);
		string categoryName;
		double stock;
		double stockInJin;
		int stockInPieces;
		WeightUnit unitType;
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			if (!(await reader.ReadAsync()))
			{
                throw new InvalidOperationException("Outbound category was not found.");
			}
			categoryName = reader.GetString(0);
			stock = reader.GetDouble(1);
			stockInJin = reader.GetDouble(2);
			stockInPieces = reader.GetInt32(3);
			unitType = ParseUnit(reader.GetString(4));
		}
		double normalizedQuantity = NormalizeQuantity(quantity, unitType);
		if (normalizedQuantity <= 0.0)
		{
            throw new InvalidOperationException("Outbound quantity must be greater than 0.");
		}
		double availableStock = GetAvailableStock(stock, stockInJin, stockInPieces, unitType);
		if (normalizedQuantity > availableStock + 0.0001)
		{
            throw new InvalidOperationException("Outbound failed because stock is insufficient.");
		}
		double normalizedUnitPrice = Math.Round(unitPrice, 2);
		double totalRevenue = Math.Round(normalizedQuantity * normalizedUnitPrice, 2);
		long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		string normalizedCustomerName = customerName?.Trim() ?? string.Empty;
		DeductStock(normalizedQuantity, unitType, ref stock, ref stockInJin, ref stockInPieces);
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.Transaction = transaction;
		sqliteCommand2.CommandText = "UPDATE categories\nSET\n    sell_price = $sellPrice,\n    stock = $stock,\n    stock_in_jin = $stockInJin,\n    stock_in_pieces = $stockInPieces,\n    updated_at = $updatedAt\nWHERE id = $id;";
		sqliteCommand2.Parameters.AddWithValue("$id", categoryId);
		sqliteCommand2.Parameters.AddWithValue("$sellPrice", normalizedUnitPrice);
		sqliteCommand2.Parameters.AddWithValue("$stock", stock);
		sqliteCommand2.Parameters.AddWithValue("$stockInJin", stockInJin);
		sqliteCommand2.Parameters.AddWithValue("$stockInPieces", stockInPieces);
		sqliteCommand2.Parameters.AddWithValue("$updatedAt", now);
		await sqliteCommand2.ExecuteNonQueryAsync();
		long? customerId = null;
		if (!string.IsNullOrWhiteSpace(normalizedCustomerName))
		{
			customerId = await GetOrCreateCustomerAsync(connection, transaction, normalizedCustomerName, now, markInbound: false, markOutbound: true);
			await UpsertCustomerCategoryPriceAsync(connection, transaction, customerId.Value, categoryId, normalizedUnitPrice, now);
		}
		string orderNumber = CreateOrderNumber("outbound", now, normalizedCustomerName);
		long num = await InsertOrderAsync(connection, transaction, orderNumber, "outbound", customerId, normalizedQuantity, totalRevenue, 1, ((unitType == WeightUnit.Piece) ? ((int)Math.Round(normalizedQuantity)) : 1), now);
		SqliteCommand sqliteCommand3 = connection.CreateCommand();
		sqliteCommand3.Transaction = transaction;
		sqliteCommand3.CommandText = "INSERT INTO outbound_records (\n    order_id, category_id, weight, unit_price, total_revenue, unit_type, timestamp\n)\nVALUES (\n    $orderId, $categoryId, $weight, $unitPrice, $totalRevenue, $unitType, $timestamp\n);";
		sqliteCommand3.Parameters.AddWithValue("$orderId", num);
		sqliteCommand3.Parameters.AddWithValue("$categoryId", categoryId);
		sqliteCommand3.Parameters.AddWithValue("$weight", normalizedQuantity);
		sqliteCommand3.Parameters.AddWithValue("$unitPrice", normalizedUnitPrice);
		sqliteCommand3.Parameters.AddWithValue("$totalRevenue", totalRevenue);
		sqliteCommand3.Parameters.AddWithValue("$unitType", unitType.ToString());
		sqliteCommand3.Parameters.AddWithValue("$timestamp", now);
		await sqliteCommand3.ExecuteNonQueryAsync();
		await transaction.CommitAsync();

		return new OutboundOrderConfirmationModel
		{
			OrderId = num,
			OrderNumber = orderNumber,
			CustomerName = normalizedCustomerName,
			Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(now),
			CategoryCount = 1,
			ItemCount = unitType == WeightUnit.Piece ? (int)Math.Round(normalizedQuantity) : 1,
			TotalAmount = totalRevenue,
			Items =
			[
				new OrderDetailItemModel
				{
					CategoryId = categoryId,
					CategoryName = categoryName,
					Quantity = normalizedQuantity,
					UnitType = unitType,
					UnitPrice = normalizedUnitPrice,
					LineAmount = totalRevenue
				}
			]
		};
	}

	public async Task DeleteCategoryAsync(long categoryId)
	{
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT\n    stock,\n    stock_in_jin,\n    stock_in_pieces,\n    (\n        SELECT COUNT(*)\n        FROM inbound_records\n        WHERE category_id = $id\n    ) + (\n        SELECT COUNT(*)\n        FROM outbound_records\n        WHERE category_id = $id\n    ) + (\n        SELECT COUNT(*)\n        FROM stock_adjustments\n        WHERE category_id = $id\n    ) + (\n        SELECT COUNT(*)\n        FROM stock_audits\n        WHERE category_id = $id\n    ) AS history_count\nFROM categories\nWHERE id = $id\nLIMIT 1;";
		sqliteCommand.Parameters.AddWithValue("$id", categoryId);
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			if (!(await reader.ReadAsync()))
			{
                throw new InvalidOperationException("Category to delete was not found.");
			}
			bool num = reader.GetDouble(0) > 0.0001 || reader.GetDouble(1) > 0.0001 || reader.GetInt32(2) > 0;
			int num2 = ((!reader.IsDBNull(3)) ? reader.GetInt32(3) : 0);
			if (num || num2 > 0)
			{
                throw new InvalidOperationException("This category still has stock or history and cannot be deleted directly. Archive it instead.");
			}
		}
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.CommandText = "DELETE FROM customer_category_prices WHERE category_id = $id;";
		sqliteCommand2.Parameters.AddWithValue("$id", categoryId);
		await sqliteCommand2.ExecuteNonQueryAsync();
		SqliteCommand sqliteCommand3 = connection.CreateCommand();
		sqliteCommand3.CommandText = "DELETE FROM stock_adjustments WHERE category_id = $id;";
		sqliteCommand3.Parameters.AddWithValue("$id", categoryId);
		await sqliteCommand3.ExecuteNonQueryAsync();
		SqliteCommand sqliteCommand4 = connection.CreateCommand();
		sqliteCommand4.CommandText = "DELETE FROM stock_audits WHERE category_id = $id;";
		sqliteCommand4.Parameters.AddWithValue("$id", categoryId);
		await sqliteCommand4.ExecuteNonQueryAsync();
		SqliteCommand sqliteCommand5 = connection.CreateCommand();
		sqliteCommand5.CommandText = "DELETE FROM categories WHERE id = $id;";
		sqliteCommand5.Parameters.AddWithValue("$id", categoryId);
		await sqliteCommand5.ExecuteNonQueryAsync();
	}

	public async Task<DashboardSummary> GetDashboardSummaryAsync()
	{
		IReadOnlyList<CategoryModel> categories = await GetCategoriesAsync();
		(long Start, long End) todayRange = GetTodayRange();
		DashboardSummary result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			double todayInboundAmount = await GetAmountSumAsync(connection, "inbound_records", "total_cost", todayRange.Start, todayRange.End);
			double todayOutboundAmount = await GetAmountSumAsync(connection, "outbound_records", "total_revenue", todayRange.Start, todayRange.End);
			result = new DashboardSummary
			{
				CategoryCount = categories.Count,
				LowStockCount = categories.Count((CategoryModel category) => category.IsLowStock),
				TotalInventoryCost = Math.Round(categories.Sum((CategoryModel category) => category.InventoryCost), 2),
				ForecastRevenue = Math.Round(categories.Sum((CategoryModel category) => category.ForecastRevenue), 2),
				TodayInboundAmount = todayInboundAmount,
				TodayOutboundAmount = todayOutboundAmount
			};
		}
		return result;
	}

	public async Task<ConsistencyCheckReportModel> RunConsistencyCheckAsync()
	{
		ConsistencyCheckReportModel result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			List<ConsistencyCheckIssueModel> issues = new List<ConsistencyCheckIssueModel>();
			List<ConsistencyCheckItemModel> checkItems = new List<ConsistencyCheckItemModel>();
			IReadOnlyList<ConsistencyCheckIssueModel> readOnlyList = await CollectDuplicateMasterDataIssuesAsync(connection);
			issues.AddRange(readOnlyList);
			checkItems.Add(CreateCheckItem("主数据唯一性", readOnlyList.Count, "检查分类和客户是否存在重名记录，避免按名称匹配时出现歧义。"));
			IReadOnlyList<ConsistencyCheckIssueModel> readOnlyList2 = await CollectOrderIntegrityIssuesAsync(connection);
			issues.AddRange(readOnlyList2);
			checkItems.Add(CreateCheckItem("订单明细关系", readOnlyList2.Count, "检查订单类型、明细数量、总数量、总金额和件数汇总是否一致。"));
			IReadOnlyList<ConsistencyCheckIssueModel> readOnlyList3 = await CollectCustomerUsageFlagIssuesAsync(connection);
			issues.AddRange(readOnlyList3);
			checkItems.Add(CreateCheckItem("客户使用标记", readOnlyList3.Count, "检查入库客户和出库客户标记是否与实际订单历史一致。"));
			IReadOnlyList<ConsistencyCheckIssueModel> readOnlyList4 = await CollectCustomerPriceMemoryIssuesAsync(connection);
			issues.AddRange(readOnlyList4);
			checkItems.Add(CreateCheckItem("客户价格记忆", readOnlyList4.Count, "检查客户价格记忆是否存在重复、缺失、孤立或过期。"));
			IReadOnlyList<ConsistencyCheckIssueModel> readOnlyList5 = await CollectCategoryUnitConsistencyIssuesAsync(connection);
			issues.AddRange(readOnlyList5);
			checkItems.Add(CreateCheckItem("分类单位一致性", readOnlyList5.Count, "检查分类当前单位是否和历史入库、出库记录中的单位一致。"));
			IReadOnlyList<ConsistencyCheckIssueModel> readOnlyList6 = await CollectCategoryStockFieldIssuesAsync(connection);
			issues.AddRange(readOnlyList6);
			checkItems.Add(CreateCheckItem("库存字段合法性", readOnlyList6.Count, "检查库存字段是否出现负数，或在非当前单位字段里残留数量。"));
			ConsistencyCheckReportModel report = new ConsistencyCheckReportModel
			{
				GeneratedAt = DateTimeOffset.Now,
				CategoryCount = await GetTableCountAsync(connection, "categories"),
				CustomerCount = await GetTableCountAsync(connection, "customers"),
				OrderCount = await GetTableCountAsync(connection, "orders"),
				CheckItems = checkItems,
				Issues = issues
			};
			await _logger.LogInfoAsync(report.IsHealthy
				? "账本一致性检查完成，未发现问题。"
				: $"账本一致性检查完成，发现 {report.IssueCount} 个问题。");
			result = report;
		}
		return result;
	}

	public async Task<ConsistencyRepairResultModel> RepairRepairableConsistencyIssuesAsync()
	{
		string restorePointPath = await CreateBackupSnapshotAsync("consistency-repair-point");
		ConsistencyRepairResultModel result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			ConsistencyRepairResultModel consistencyRepairResultModel;
			await using (SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync()))
			{
				List<long> customerIds = new List<long>();
				SqliteCommand sqliteCommand = connection.CreateCommand();
				sqliteCommand.Transaction = transaction;
				sqliteCommand.CommandText = "SELECT id\nFROM customers\nORDER BY id;";
				await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
				{
					while (await reader.ReadAsync())
					{
						customerIds.Add(reader.GetInt64(0));
					}
				}
				SqliteCommand sqliteCommand2 = connection.CreateCommand();
				sqliteCommand2.Transaction = transaction;
				sqliteCommand2.CommandText = "DELETE FROM customer_category_prices;";
				await sqliteCommand2.ExecuteNonQueryAsync();
				foreach (long customerId in customerIds)
				{
					await UpdateCustomerUsageFlagsAsync(connection, transaction, customerId);
					await RebuildCustomerPriceMemoriesAsync(connection, transaction, customerId);
				}
				await transaction.CommitAsync();
				ConsistencyCheckReportModel report = await RunConsistencyCheckAsync();
				await _logger.LogInfoAsync(report.IsHealthy
					? $"已执行账本自动修复，重建 {customerIds.Count} 位客户的使用标记和价格记忆。恢复点：{restorePointPath}。复查未发现剩余问题。"
					: $"已执行账本自动修复，重建 {customerIds.Count} 位客户的使用标记和价格记忆。恢复点：{restorePointPath}。复查后仍有 {report.IssueCount} 个问题。");
				consistencyRepairResultModel = new ConsistencyRepairResultModel
				{
					RestorePointPath = restorePointPath,
					ProcessedCustomerCount = customerIds.Count,
					Report = report
				};
			}
			result = consistencyRepairResultModel;
		}
		return result;
	}

	public async Task<IReadOnlyList<CategoryModel>> GetTopForecastCategoriesAsync(int count)
	{
		return (from category in await GetCategoriesAsync()
			orderby category.ForecastRevenue descending, category.Name
			select category).Take(count).ToList();
	}

	private SqliteConnection CreateConnection()
	{
		return new SqliteConnection("Data Source=" + DatabasePath);
	}

	private async Task<string> CreateBackupSnapshotAsync(string prefix)
	{
		AppDataPaths.EnsureDirectories();
		if (!File.Exists(DatabasePath))
		{
            throw new InvalidOperationException("There is no database file to back up.");
		}
		string backupPath = Path.Combine(BackupDirectory, $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmssfff}.db");
		await Task.Run(delegate
		{
			File.Copy(DatabasePath, backupPath, overwrite: true);
		});
		return backupPath;
	}

	private static async Task<int> GetTableCountAsync(SqliteConnection connection, string tableName)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT COUNT(*) FROM " + tableName + ";";
		return Convert.ToInt32(await sqliteCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
	}

	private static ConsistencyCheckItemModel CreateCheckItem(string name, int issueCount, string detailText)
	{
		return new ConsistencyCheckItemModel
		{
			Name = name,
			StatusText = ((issueCount == 0) ? "正常" : $"{issueCount} 项问题"),
			DetailText = detailText
		};
	}

	private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectDuplicateMasterDataIssuesAsync(SqliteConnection connection)
	{
		List<ConsistencyCheckIssueModel> issues = new List<ConsistencyCheckIssueModel>();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT LOWER(name) AS key_name, GROUP_CONCAT(name, ' / '), COUNT(*)\nFROM categories\nGROUP BY LOWER(name)\nHAVING COUNT(*) > 1;";
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				issues.Add(new ConsistencyCheckIssueModel
				{
					CheckName = "主数据唯一性",
					SeverityText = "警告",
					Title = "分类名称重复",
					DetailText = $"以下分类名称归一化后重复：{reader.GetString(1)}。共 {reader.GetInt32(2)} 条。"
				});
			}
		}
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.CommandText = "SELECT LOWER(name) AS key_name, GROUP_CONCAT(name, ' / '), COUNT(*)\nFROM customers\nGROUP BY LOWER(name)\nHAVING COUNT(*) > 1;";
		await using (SqliteDataReader reader = await sqliteCommand2.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				issues.Add(new ConsistencyCheckIssueModel
				{
					CheckName = "主数据唯一性",
					SeverityText = "警告",
					Title = "客户名称重复",
					DetailText = $"以下客户名称归一化后重复：{reader.GetString(1)}。共 {reader.GetInt32(2)} 条。"
				});
			}
		}
		return issues;
	}

	private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectOrderIntegrityIssuesAsync(SqliteConnection connection)
	{
		List<ConsistencyCheckIssueModel> list = new List<ConsistencyCheckIssueModel>();
		SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			WITH order_details AS (
			    SELECT
			        ir.order_id,
			        ir.category_id,
			        ir.weight AS quantity,
			        ir.total_cost AS line_amount,
			        ir.unit_type,
			        'inbound' AS detail_type
			    FROM inbound_records ir

			    UNION ALL

			    SELECT
			        orr.order_id,
			        orr.category_id,
			        orr.weight AS quantity,
			        orr.total_revenue AS line_amount,
			        orr.unit_type,
			        'outbound' AS detail_type
			    FROM outbound_records orr
			),
			order_metrics AS (
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
			        IFNULL((SELECT COUNT(*) FROM order_details WHERE order_id = o.id), 0) AS detail_count,
			        IFNULL((SELECT SUM(quantity) FROM order_details WHERE order_id = o.id), 0) AS actual_weight,
			        IFNULL((SELECT SUM(line_amount) FROM order_details WHERE order_id = o.id), 0) AS actual_amount,
			        IFNULL((
			            SELECT SUM(
			                CASE
			                    WHEN unit_type = 'Piece' THEN CAST(ROUND(quantity) AS INTEGER)
			                    ELSE 1
			                END
			            )
			            FROM order_details
			            WHERE order_id = o.id
			        ), 0) AS actual_item_count
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
			    detail_count,
			    actual_weight,
			    actual_amount,
			    actual_item_count
			FROM order_metrics;
			""";
		await using SqliteDataReader reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			string orderNumber = reader.GetString(1);
			string type = reader.GetString(2);
			double totalWeight = reader.GetDouble(3);
			double totalAmount = reader.GetDouble(4);
			int categoryCount = reader.GetInt32(5);
			int itemCount = reader.GetInt32(6);
			int inboundCount = reader.GetInt32(7);
			int outboundCount = reader.GetInt32(8);
			int detailCount = reader.GetInt32(9);
			double actualWeight = reader.GetDouble(10);
			double actualAmount = reader.GetDouble(11);
			int actualItemCount = reader.GetInt32(12);
			if (string.Equals(type, "inbound", StringComparison.OrdinalIgnoreCase))
			{
				if (inboundCount < 1 || outboundCount != 0)
				{
                    list.Add(CreateOrderIssue(orderNumber, "入库订单明细关系不一致", $"预期至少有 1 条入库明细且 0 条出库明细，实际为 {inboundCount} / {outboundCount}。"));
				}
			}
			else if (string.Equals(type, "outbound", StringComparison.OrdinalIgnoreCase))
			{
				if (outboundCount != 1 || inboundCount != 0)
				{
                    list.Add(CreateOrderIssue(orderNumber, "出库订单明细关系不一致", $"预期有 1 条出库明细且 0 条入库明细，实际为 {outboundCount} / {inboundCount}。"));
				}
			}
			else
			{
                list.Add(CreateOrderIssue(orderNumber, "订单类型未知", $"检测到不支持的订单类型：{type}。"));
			}
			if (Math.Abs(totalWeight - actualWeight) > 0.0001)
			{
				list.Add(CreateOrderIssue(orderNumber, "订单数量汇总不一致", $"订单总数量为 {totalWeight:0.##}，但明细汇总为 {actualWeight:0.##}。"));
			}
			if (Math.Abs(totalAmount - actualAmount) > 0.0001)
			{
				list.Add(CreateOrderIssue(orderNumber, "订单金额汇总不一致", $"订单总金额为 ¥{totalAmount:0.##}，但明细汇总为 ¥{actualAmount:0.##}。"));
			}
			if (categoryCount != detailCount)
			{
				list.Add(CreateOrderIssue(orderNumber, "订单分类数不一致", $"订单 category_count = {categoryCount}，但实际明细条数为 {detailCount}。"));
			}
			if (itemCount != actualItemCount)
			{
				list.Add(CreateOrderIssue(orderNumber, "订单件数不一致", $"订单 item_count = {itemCount}，但根据明细计算为 {actualItemCount}。"));
			}
		}
		return list;
	}

	private static ConsistencyCheckIssueModel CreateOrderIssue(string orderNumber, string title, string detailText)
	{
		return new ConsistencyCheckIssueModel
		{
			CheckName = "订单明细关系",
			SeverityText = "错误",
			Title = title + ": " + orderNumber,
			DetailText = detailText
		};
	}
private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectCustomerUsageFlagIssuesAsync(SqliteConnection connection)
	{
		List<ConsistencyCheckIssueModel> issues = new List<ConsistencyCheckIssueModel>();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT\n    c.name,\n    c.has_inbound_orders,\n    c.has_outbound_orders,\n    IFNULL(SUM(CASE WHEN o.type = 'inbound' THEN 1 ELSE 0 END), 0) AS actual_inbound_count,\n    IFNULL(SUM(CASE WHEN o.type = 'outbound' THEN 1 ELSE 0 END), 0) AS actual_outbound_count\nFROM customers c\nLEFT JOIN orders o ON o.customer_id = c.id\nGROUP BY c.id, c.name, c.has_inbound_orders, c.has_outbound_orders\nORDER BY c.name COLLATE NOCASE;";
		IReadOnlyList<ConsistencyCheckIssueModel> result;
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				string text = reader.GetString(0);
				bool flag = reader.GetInt64(1) == 1;
				bool flag2 = reader.GetInt64(2) == 1;
				bool flag3 = reader.GetInt64(3) > 0;
				bool flag4 = reader.GetInt64(4) > 0;
				if (flag != flag3 || flag2 != flag4)
				{
					issues.Add(new ConsistencyCheckIssueModel
					{
						CheckName = "客户使用标记",
						SeverityText = "警告",
						Title = "客户标记不一致：" + text,
						DetailText = $"当前标记：入库={FormatBooleanLabel(flag)} / 出库={FormatBooleanLabel(flag2)}；实际订单：入库={FormatBooleanLabel(flag3)} / 出库={FormatBooleanLabel(flag4)}。"
					});
				}
			}
			result = issues;
		}
		return result;
	}

	private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectCustomerPriceMemoryIssuesAsync(SqliteConnection connection)
	{
		List<ConsistencyCheckIssueModel> issues = new List<ConsistencyCheckIssueModel>();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT\n    IFNULL(c.name, '缺失客户'),\n    IFNULL(cg.name, '缺失分类'),\n    COUNT(*)\nFROM customer_category_prices p\nLEFT JOIN customers c ON c.id = p.customer_id\nLEFT JOIN categories cg ON cg.id = p.category_id\nGROUP BY p.customer_id, p.category_id\nHAVING COUNT(*) > 1;";
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				issues.Add(new ConsistencyCheckIssueModel
				{
					CheckName = "客户价格记忆",
					SeverityText = "警告",
					Title = "价格记忆重复：" + reader.GetString(0) + " / " + reader.GetString(1),
					DetailText = $"同一客户与分类存在 {reader.GetInt32(2)} 条价格记忆。"
				});
			}
		}
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.CommandText = "WITH latest_price AS (\n    SELECT\n        customer_id,\n        category_id,\n        price,\n        updated_at\n    FROM (\n        SELECT\n            o.customer_id,\n            d.category_id,\n            d.price,\n            d.updated_at,\n            ROW_NUMBER() OVER (\n                PARTITION BY o.customer_id, d.category_id\n                ORDER BY d.updated_at DESC, d.record_id DESC\n            ) AS rn\n        FROM orders o\n        INNER JOIN (\n            SELECT order_id, category_id, unit_price AS price, timestamp AS updated_at, id AS record_id\n            FROM inbound_records\n            UNION ALL\n            SELECT order_id, category_id, unit_price AS price, timestamp AS updated_at, id AS record_id\n            FROM outbound_records\n        ) d ON d.order_id = o.id\n        WHERE o.customer_id IS NOT NULL\n    )\n    WHERE rn = 1\n)\nSELECT\n    IFNULL(c.name, '缺失客户') AS customer_name,\n    IFNULL(cg.name, '缺失分类') AS category_name,\n    p.id,\n    p.price,\n    p.updated_at,\n    lp.price AS latest_price,\n    lp.updated_at AS latest_updated_at\nFROM customer_category_prices p\nLEFT JOIN customers c ON c.id = p.customer_id\nLEFT JOIN categories cg ON cg.id = p.category_id\nLEFT JOIN latest_price lp ON lp.customer_id = p.customer_id AND lp.category_id = p.category_id\nWHERE c.id IS NULL\n    OR cg.id IS NULL\n    OR lp.customer_id IS NULL\n    OR ABS(p.price - lp.price) > 0.0001\n    OR p.updated_at != lp.updated_at\nORDER BY customer_name COLLATE NOCASE, category_name COLLATE NOCASE;";
		await using (SqliteDataReader reader = await sqliteCommand2.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				string text = reader.GetString(0);
				string text2 = reader.GetString(1);
				double? num = (reader.IsDBNull(5) ? ((double?)null) : new double?(reader.GetDouble(5)));
				DateTimeOffset? dateTimeOffset = (reader.IsDBNull(6) ? ((DateTimeOffset?)null) : new DateTimeOffset?(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6))));
				string detailText = !num.HasValue
					? "当前价格记忆找不到对应的最近成交记录，可能已经孤立。"
					: $"当前记忆为 ¥{reader.GetDouble(3):0.##}，最近成交价为 ¥{num.Value:0.##}，最近成交时间为 {dateTimeOffset.Value.ToLocalTime():yyyy-MM-dd HH:mm}。";
				issues.Add(new ConsistencyCheckIssueModel
				{
					CheckName = "客户价格记忆",
					SeverityText = "警告",
					Title = "价格记忆需要重建：" + text + " / " + text2,
					DetailText = detailText
				});
			}
		}
		return issues;
	}

	private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectCategoryUnitConsistencyIssuesAsync(SqliteConnection connection)
	{
		List<ConsistencyCheckIssueModel> issues = new List<ConsistencyCheckIssueModel>();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT\n    c.name,\n    c.unit_type,\n    r.record_unit_type,\n    r.record_type,\n    r.record_count\nFROM categories c\nINNER JOIN (\n    SELECT category_id, unit_type AS record_unit_type, '入库' AS record_type, COUNT(*) AS record_count\n    FROM inbound_records\n    GROUP BY category_id, unit_type\n\n    UNION ALL\n\n    SELECT category_id, unit_type AS record_unit_type, '出库' AS record_type, COUNT(*) AS record_count\n    FROM outbound_records\n    GROUP BY category_id, unit_type\n) r ON r.category_id = c.id\nWHERE r.record_unit_type != c.unit_type\nORDER BY c.name COLLATE NOCASE;";
		IReadOnlyList<ConsistencyCheckIssueModel> result;
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				issues.Add(new ConsistencyCheckIssueModel
				{
					CheckName = "分类单位一致性",
					SeverityText = "警告",
					Title = "分类单位不一致：" + reader.GetString(0),
					DetailText = $"当前分类单位为 {reader.GetString(1)}，但 {reader.GetString(3)} 记录使用了 {reader.GetString(2)}。共 {reader.GetInt32(4)} 条。"
				});
			}
			result = issues;
		}
		return result;
	}

	private static async Task<IReadOnlyList<ConsistencyCheckIssueModel>> CollectCategoryStockFieldIssuesAsync(SqliteConnection connection)
	{
		List<ConsistencyCheckIssueModel> issues = new List<ConsistencyCheckIssueModel>();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT\n    name,\n    unit_type,\n    stock,\n    stock_in_jin,\n    stock_in_pieces\nFROM categories\nORDER BY name COLLATE NOCASE;";
		IReadOnlyList<ConsistencyCheckIssueModel> result;
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				string text = reader.GetString(0);
				WeightUnit weightUnit = ParseUnit(reader.GetString(1));
				double num = reader.GetDouble(2);
				double num2 = reader.GetDouble(3);
				int @int = reader.GetInt32(4);
				if (num < -0.0001 || num2 < -0.0001 || @int < 0)
				{
					issues.Add(new ConsistencyCheckIssueModel
					{
						CheckName = "库存字段合法性",
						SeverityText = "错误",
						Title = "库存出现负数：" + text,
						DetailText = $"当前库存字段为：千克={num:0.##}，斤={num2:0.##}，件数={@int}。"
					});
				}
				else if (weightUnit switch
				{
					WeightUnit.Kilogram => Math.Abs(num2) > 0.0001 || @int != 0, 
					WeightUnit.Jin => Math.Abs(num) > 0.0001 || @int != 0, 
					WeightUnit.Piece => Math.Abs(num) > 0.0001 || Math.Abs(num2) > 0.0001, 
					_ => false, 
				})
				{
					issues.Add(new ConsistencyCheckIssueModel
					{
						CheckName = "库存字段合法性",
						SeverityText = "警告",
						Title = "非当前单位字段残留数量：" + text,
						DetailText = $"当前分类单位为 {FormatUnitLabel(weightUnit)}，但其他库存列仍有值：千克={num:0.##}，斤={num2:0.##}，件数={@int}。"
					});
				}
			}
			result = issues;
		}
		return result;
	}

	private static string FormatBooleanLabel(bool value)
	{
		if (!value)
		{
			return "否";
		}
		return "是";
	}

	private async Task<int> GetCurrentSchemaVersionAsync(SqliteConnection connection)
	{
		int num = await GetPragmaUserVersionAsync(connection);
		if (num > 0)
		{
			return num;
		}
		if (!(await HasAnyBusinessTablesAsync(connection)))
		{
			return 0;
		}
		if (!(await HasAllBaseTablesAsync(connection)))
		{
            await _logger.LogWarningAsync("Database has no schema version and base tables are incomplete; rebuilding as v0.");
			return 0;
		}
		int inferredVersion = await InferLegacySchemaVersionAsync(connection);
		await SetSchemaVersionAsync(connection, inferredVersion);
        await _logger.LogWarningAsync($"Database has no schema version; inferred current schema as v{inferredVersion}.");
		return inferredVersion;
	}

	private static async Task<int> GetPragmaUserVersionAsync(SqliteConnection connection)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "PRAGMA user_version;";
		return Convert.ToInt32(await sqliteCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
	}

	private static async Task SetSchemaVersionAsync(SqliteConnection connection, int version)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = $"PRAGMA user_version = {version};";
		await sqliteCommand.ExecuteNonQueryAsync();
	}

	private static async Task<bool> HasAnyBusinessTablesAsync(SqliteConnection connection)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT COUNT(*)\nFROM sqlite_master\nWHERE type = 'table'\n    AND name IN (\n        'categories',\n        'customers',\n        'customer_category_prices',\n        'orders',\n        'inbound_records',\n        'outbound_records',\n        'stock_adjustments',\n        'stock_audits'\n    );";
		return Convert.ToInt32(await sqliteCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
	}

	private static async Task<bool> HasAllBaseTablesAsync(SqliteConnection connection)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT COUNT(*)\nFROM sqlite_master\nWHERE type = 'table'\n    AND name IN (\n        'categories',\n        'customers',\n        'customer_category_prices',\n        'orders',\n        'inbound_records',\n        'outbound_records',\n        'stock_adjustments',\n        'stock_audits'\n    );";
		return Convert.ToInt32(await sqliteCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 8;
	}

	private static async Task<int> InferLegacySchemaVersionAsync(SqliteConnection connection)
	{
		if (await ColumnExistsAsync(connection, null, "categories", "is_archived"))
		{
			return 2;
		}
		return 1;
	}

	private static async Task ApplyMigrationAsync(SqliteConnection connection, int targetVersion)
	{
		await using SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync());
		switch (targetVersion)
		{
		case 1:
			await ApplyBaseSchemaMigrationAsync(connection, transaction);
			break;
		case 2:
			await ApplyCategoryArchiveMigrationAsync(connection, transaction);
			break;
		case 3:
			await ApplyPerformanceIndexesMigrationAsync(connection, transaction);
			break;
		default:
			throw new InvalidOperationException($"鏈畾涔夌殑鏁版嵁搴撹縼绉荤増鏈細v{targetVersion}");
		}
		await transaction.CommitAsync();
	}

	private static async Task ApplyBaseSchemaMigrationAsync(SqliteConnection connection, SqliteTransaction transaction)
	{
		await ExecuteNonQueryAsync(connection, transaction, "CREATE TABLE IF NOT EXISTS categories (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    name TEXT NOT NULL,\n    buy_price REAL NOT NULL,\n    sell_price REAL NOT NULL,\n    stock REAL NOT NULL DEFAULT 0,\n    stock_in_jin REAL NOT NULL DEFAULT 0,\n    stock_in_pieces INTEGER NOT NULL DEFAULT 0,\n    unit_type TEXT NOT NULL,\n    created_at INTEGER NOT NULL,\n    updated_at INTEGER NOT NULL\n);\n\nCREATE TABLE IF NOT EXISTS customers (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    name TEXT NOT NULL,\n    phone TEXT NOT NULL DEFAULT '',\n    email TEXT NOT NULL DEFAULT '',\n    address TEXT NOT NULL DEFAULT '',\n    note TEXT NOT NULL DEFAULT '',\n    has_inbound_orders INTEGER NOT NULL DEFAULT 0,\n    has_outbound_orders INTEGER NOT NULL DEFAULT 0,\n    created_at INTEGER NOT NULL,\n    updated_at INTEGER NOT NULL\n);\n\nCREATE TABLE IF NOT EXISTS customer_category_prices (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    customer_id INTEGER NOT NULL,\n    category_id INTEGER NOT NULL,\n    price REAL NOT NULL,\n    updated_at INTEGER NOT NULL\n);\n\nCREATE TABLE IF NOT EXISTS orders (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    order_number TEXT NOT NULL,\n    type TEXT NOT NULL,\n    total_weight REAL NOT NULL,\n    total_amount REAL NOT NULL,\n    category_count INTEGER NOT NULL,\n    item_count INTEGER NOT NULL,\n    customer_id INTEGER NULL,\n    note TEXT NOT NULL DEFAULT '',\n    timestamp INTEGER NOT NULL\n);\n\nCREATE TABLE IF NOT EXISTS inbound_records (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    order_id INTEGER NULL,\n    category_id INTEGER NOT NULL,\n    weight REAL NOT NULL,\n    unit_price REAL NOT NULL,\n    total_cost REAL NOT NULL,\n    unit_type TEXT NOT NULL,\n    timestamp INTEGER NOT NULL\n);\n\nCREATE TABLE IF NOT EXISTS outbound_records (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    order_id INTEGER NULL,\n    category_id INTEGER NOT NULL,\n    weight REAL NOT NULL,\n    unit_price REAL NOT NULL,\n    total_revenue REAL NOT NULL,\n    unit_type TEXT NOT NULL,\n    timestamp INTEGER NOT NULL\n);\n\nCREATE TABLE IF NOT EXISTS stock_adjustments (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    category_id INTEGER NOT NULL,\n    previous_quantity REAL NOT NULL,\n    adjusted_quantity REAL NOT NULL,\n    delta_quantity REAL NOT NULL,\n    unit_type TEXT NOT NULL,\n    reason TEXT NOT NULL DEFAULT '',\n    timestamp INTEGER NOT NULL\n);\n\nCREATE TABLE IF NOT EXISTS stock_audits (\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\n    category_id INTEGER NOT NULL,\n    system_quantity REAL NOT NULL,\n    actual_quantity REAL NOT NULL,\n    delta_quantity REAL NOT NULL,\n    unit_type TEXT NOT NULL,\n    note TEXT NOT NULL DEFAULT '',\n    timestamp INTEGER NOT NULL\n);");
	}

	private static async Task ApplyCategoryArchiveMigrationAsync(SqliteConnection connection, SqliteTransaction transaction)
	{
		if (!(await ColumnExistsAsync(connection, transaction, "categories", "is_archived")))
		{
			await ExecuteNonQueryAsync(connection, transaction, "ALTER TABLE categories ADD COLUMN is_archived INTEGER NOT NULL DEFAULT 0;");
		}
	}

	private static async Task ApplyPerformanceIndexesMigrationAsync(SqliteConnection connection, SqliteTransaction transaction)
	{
		await ExecuteNonQueryAsync(connection, transaction, "CREATE INDEX IF NOT EXISTS idx_categories_name ON categories(name COLLATE NOCASE);\nCREATE INDEX IF NOT EXISTS idx_customers_name ON customers(name COLLATE NOCASE);\nCREATE INDEX IF NOT EXISTS idx_customer_category_prices_lookup ON customer_category_prices(customer_id, category_id);\nCREATE INDEX IF NOT EXISTS idx_orders_timestamp ON orders(timestamp DESC);\nCREATE INDEX IF NOT EXISTS idx_orders_customer_id ON orders(customer_id);\nCREATE INDEX IF NOT EXISTS idx_inbound_records_order_id ON inbound_records(order_id);\nCREATE INDEX IF NOT EXISTS idx_inbound_records_category_id ON inbound_records(category_id);\nCREATE INDEX IF NOT EXISTS idx_outbound_records_order_id ON outbound_records(order_id);\nCREATE INDEX IF NOT EXISTS idx_outbound_records_category_id ON outbound_records(category_id);\nCREATE INDEX IF NOT EXISTS idx_stock_adjustments_category_id ON stock_adjustments(category_id);\nCREATE INDEX IF NOT EXISTS idx_stock_adjustments_timestamp ON stock_adjustments(timestamp DESC);\nCREATE INDEX IF NOT EXISTS idx_stock_audits_category_id ON stock_audits(category_id);\nCREATE INDEX IF NOT EXISTS idx_stock_audits_timestamp ON stock_audits(timestamp DESC);");
	}

	private static async Task ExecuteNonQueryAsync(SqliteConnection connection, SqliteTransaction transaction, string commandText)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = commandText;
		await sqliteCommand.ExecuteNonQueryAsync();
	}

	private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, SqliteTransaction? transaction, string tableName, string columnName)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT COUNT(*)\nFROM pragma_table_info('" + tableName + "')\nWHERE name = $columnName;";
		sqliteCommand.Parameters.AddWithValue("$columnName", columnName);
		return Convert.ToInt32(await sqliteCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
	}

	private static string GetMigrationName(int version)
	{
		return version switch
		{
            1 => "Create base tables",
			2 => "琛ュ厖鍒嗙骇褰掓。瀛楁", 
			3 => "鍒涘缓鏌ヨ鎬ц兘绱㈠紩", 
			_ => "鏈煡杩佺Щ", 
		};
	}

	private static async Task<StockMutationSnapshot> ApplyStockMutationAsync(SqliteConnection connection, SqliteTransaction transaction, CategoryModel category, double adjustedQuantity, string? reason, long timestamp, bool allowSameQuantity)
	{
		double normalizedQuantity = NormalizeQuantity(adjustedQuantity, category.UnitType);
		if (normalizedQuantity < 0.0)
		{
            throw new InvalidOperationException("Adjusted stock cannot be negative.");
		}
		double previousQuantity = GetAvailableStock(category.Stock, category.StockInJin, category.StockInPieces, category.UnitType);
		double deltaQuantity = Math.Round(normalizedQuantity - previousQuantity, 2);
		if (!allowSameQuantity && Math.Abs(deltaQuantity) < 0.0001)
		{
            throw new InvalidOperationException("Adjusted stock matches current stock.");
		}
		double stock = category.Stock;
		double stockInJin = category.StockInJin;
		int stockInPieces = category.StockInPieces;
		ApplyStockQuantity(normalizedQuantity, category.UnitType, ref stock, ref stockInJin, ref stockInPieces);
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "UPDATE categories\nSET\n    stock = $stock,\n    stock_in_jin = $stockInJin,\n    stock_in_pieces = $stockInPieces,\n    updated_at = $updatedAt\nWHERE id = $id;";
		sqliteCommand.Parameters.AddWithValue("$id", category.Id);
		sqliteCommand.Parameters.AddWithValue("$stock", stock);
		sqliteCommand.Parameters.AddWithValue("$stockInJin", stockInJin);
		sqliteCommand.Parameters.AddWithValue("$stockInPieces", stockInPieces);
		sqliteCommand.Parameters.AddWithValue("$updatedAt", timestamp);
		await sqliteCommand.ExecuteNonQueryAsync();
		if (Math.Abs(deltaQuantity) >= 0.0001)
		{
			SqliteCommand sqliteCommand2 = connection.CreateCommand();
			sqliteCommand2.Transaction = transaction;
			sqliteCommand2.CommandText = "INSERT INTO stock_adjustments (\n    category_id,\n    previous_quantity,\n    adjusted_quantity,\n    delta_quantity,\n    unit_type,\n    reason,\n    timestamp\n)\nVALUES (\n    $categoryId,\n    $previousQuantity,\n    $adjustedQuantity,\n    $deltaQuantity,\n    $unitType,\n    $reason,\n    $timestamp\n);";
			sqliteCommand2.Parameters.AddWithValue("$categoryId", category.Id);
			sqliteCommand2.Parameters.AddWithValue("$previousQuantity", previousQuantity);
			sqliteCommand2.Parameters.AddWithValue("$adjustedQuantity", normalizedQuantity);
			sqliteCommand2.Parameters.AddWithValue("$deltaQuantity", deltaQuantity);
			sqliteCommand2.Parameters.AddWithValue("$unitType", category.UnitType.ToString());
			sqliteCommand2.Parameters.AddWithValue("$reason", reason?.Trim() ?? string.Empty);
			sqliteCommand2.Parameters.AddWithValue("$timestamp", timestamp);
			await sqliteCommand2.ExecuteNonQueryAsync();
		}
		return new StockMutationSnapshot(previousQuantity, normalizedQuantity, deltaQuantity);
	}

	private static double NormalizeQuantity(double quantity, WeightUnit unitType)
	{
		double num = Math.Max(0.0, quantity);
		if (unitType != WeightUnit.Piece)
		{
			return Math.Round(num, 2);
		}
		return Math.Round(num);
	}

	private static double GetAvailableStock(double stock, double stockInJin, int stockInPieces, WeightUnit unitType)
	{
		return unitType switch
		{
			WeightUnit.Kilogram => stock, 
			WeightUnit.Jin => stockInJin, 
			WeightUnit.Piece => stockInPieces, 
			_ => stock, 
		};
	}

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
		List<CustomerModel> customers = new List<CustomerModel>();
		IReadOnlyList<CustomerModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT\n    id,\n    name,\n    phone,\n    email,\n    address,\n    note,\n    has_inbound_orders,\n    has_outbound_orders,\n    created_at,\n    updated_at\nFROM customers\nWHERE " + usageColumn + " = 1\nORDER BY name COLLATE NOCASE;";
			IReadOnlyList<CustomerModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
						HasInboundOrders = (reader.GetInt64(6) == 1),
						HasOutboundOrders = (reader.GetInt64(7) == 1),
						CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
						UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9))
					});
				}
				readOnlyList = customers;
			}
			result = readOnlyList;
		}
		return result;
	}

	private static async Task FillDailyAnalyticsAsync(SqliteConnection connection, string table, string amountColumn, int days, IDictionary<DateOnly, DailyAnalyticsItemModel> items, bool isInbound)
	{
		long num = new DateTimeOffset(DateTimeOffset.Now.Date.AddDays(-(days - 1)), DateTimeOffset.Now.Offset).ToUnixTimeMilliseconds();
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = $"SELECT\n    strftime('%Y-%m-%d', datetime(timestamp / 1000, 'unixepoch', 'localtime')) AS day_key,\n    IFNULL(SUM({amountColumn}), 0) AS amount\nFROM {table}\nWHERE timestamp >= $start\nGROUP BY day_key\nORDER BY day_key ASC;";
		sqliteCommand.Parameters.AddWithValue("$start", num);
		await using SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			if (DateOnly.TryParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) && items.TryGetValue(result, out DailyAnalyticsItemModel value))
			{
				double num2 = reader.GetDouble(1);
				items[result] = new DailyAnalyticsItemModel
				{
					Date = value.Date,
					InboundAmount = (isInbound ? num2 : value.InboundAmount),
					OutboundAmount = (isInbound ? value.OutboundAmount : num2)
				};
			}
		}
	}

	private async Task<IReadOnlyList<CategoryAnalyticsItemModel>> GetTopCategoryAnalyticsAsync(string table, string amountColumn, int count)
	{
		List<CategoryAnalyticsItemModel> categories = new List<CategoryAnalyticsItemModel>();
		IReadOnlyList<CategoryAnalyticsItemModel> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = $"SELECT\n    c.name,\n    IFNULL(SUM(r.weight), 0) AS quantity,\n    r.unit_type,\n    IFNULL(SUM(r.{amountColumn}), 0) AS amount\nFROM {table} r\nINNER JOIN categories c ON c.id = r.category_id\nGROUP BY r.category_id, c.name, r.unit_type\nORDER BY amount DESC, c.name COLLATE NOCASE\nLIMIT $count;";
			sqliteCommand.Parameters.AddWithValue("$count", count);
			IReadOnlyList<CategoryAnalyticsItemModel> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
			{
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
				readOnlyList = categories;
			}
			result = readOnlyList;
		}
		return result;
	}

	private static async Task EnsureCustomerNameAvailableAsync(SqliteConnection connection, SqliteTransaction transaction, long customerId, string customerName)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT id\nFROM customers\nWHERE LOWER(name) = LOWER($name)\n    AND id != $id\nLIMIT 1;";
		sqliteCommand.Parameters.AddWithValue("$name", customerName);
		sqliteCommand.Parameters.AddWithValue("$id", customerId);
		if (await sqliteCommand.ExecuteScalarAsync() != null)
		{
            throw new InvalidOperationException("A customer with the same name already exists.");
		}
	}

	private static async Task UpdateCustomerUsageFlagsAsync(SqliteConnection connection, SqliteTransaction transaction, long customerId)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT\n    SUM(CASE WHEN type = 'inbound' THEN 1 ELSE 0 END) AS inbound_count,\n    SUM(CASE WHEN type = 'outbound' THEN 1 ELSE 0 END) AS outbound_count\nFROM orders\nWHERE customer_id = $customerId;";
		sqliteCommand.Parameters.AddWithValue("$customerId", customerId);
		bool hasInboundOrders = false;
		bool hasOutboundOrders = false;
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			if (await reader.ReadAsync())
			{
				hasInboundOrders = !reader.IsDBNull(0) && reader.GetInt64(0) > 0;
				hasOutboundOrders = !reader.IsDBNull(1) && reader.GetInt64(1) > 0;
			}
		}
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.Transaction = transaction;
		sqliteCommand2.CommandText = "UPDATE customers\nSET\n    has_inbound_orders = $hasInboundOrders,\n    has_outbound_orders = $hasOutboundOrders,\n    updated_at = $updatedAt\nWHERE id = $customerId;";
		sqliteCommand2.Parameters.AddWithValue("$customerId", customerId);
		sqliteCommand2.Parameters.AddWithValue("$hasInboundOrders", hasInboundOrders ? 1 : 0);
		sqliteCommand2.Parameters.AddWithValue("$hasOutboundOrders", hasOutboundOrders ? 1 : 0);
		sqliteCommand2.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToUnixTimeMilliseconds());
		await sqliteCommand2.ExecuteNonQueryAsync();
	}

	private static async Task RebuildCustomerPriceMemoriesAsync(SqliteConnection connection, SqliteTransaction transaction, long customerId)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "DELETE FROM customer_category_prices WHERE customer_id = $customerId;";
		sqliteCommand.Parameters.AddWithValue("$customerId", customerId);
		await sqliteCommand.ExecuteNonQueryAsync();
		Dictionary<long, (double Price, long UpdatedAt)> latestPrices = new Dictionary<long, (double, long)>();
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.Transaction = transaction;
		sqliteCommand2.CommandText = "SELECT category_id, price, updated_at\nFROM (\n    SELECT ir.category_id, ir.unit_price AS price, ir.timestamp AS updated_at, ir.id AS record_id\n    FROM inbound_records ir\n    INNER JOIN orders o ON o.id = ir.order_id\n    WHERE o.customer_id = $customerId\n\n    UNION ALL\n\n    SELECT orr.category_id, orr.unit_price AS price, orr.timestamp AS updated_at, orr.id AS record_id\n    FROM outbound_records orr\n    INNER JOIN orders o ON o.id = orr.order_id\n    WHERE o.customer_id = $customerId\n)\nORDER BY updated_at DESC, record_id DESC;";
		sqliteCommand2.Parameters.AddWithValue("$customerId", customerId);
		await using (SqliteDataReader reader = await sqliteCommand2.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				long @int = reader.GetInt64(0);
				if (!latestPrices.ContainsKey(@int))
				{
					latestPrices[@int] = (reader.GetDouble(1), reader.GetInt64(2));
				}
			}
		}
		foreach (KeyValuePair<long, (double, long)> item in latestPrices)
		{
			SqliteCommand sqliteCommand3 = connection.CreateCommand();
			sqliteCommand3.Transaction = transaction;
			sqliteCommand3.CommandText = "INSERT INTO customer_category_prices (\n    customer_id, category_id, price, updated_at\n)\nVALUES (\n    $customerId, $categoryId, $price, $updatedAt\n);";
			sqliteCommand3.Parameters.AddWithValue("$customerId", customerId);
			sqliteCommand3.Parameters.AddWithValue("$categoryId", item.Key);
			sqliteCommand3.Parameters.AddWithValue("$price", item.Value.Item1);
			sqliteCommand3.Parameters.AddWithValue("$updatedAt", item.Value.Item2);
			await sqliteCommand3.ExecuteNonQueryAsync();
		}
	}

	private static async Task RebuildAllCategoryDataAsync(SqliteConnection connection, SqliteTransaction transaction)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT\n    c.id,\n    c.buy_price,\n    c.sell_price,\n    IFNULL((SELECT SUM(weight) FROM inbound_records WHERE category_id = c.id AND unit_type = 'Kilogram'), 0),\n    IFNULL((SELECT SUM(weight) FROM inbound_records WHERE category_id = c.id AND unit_type = 'Jin'), 0),\n    IFNULL((SELECT SUM(weight) FROM inbound_records WHERE category_id = c.id AND unit_type = 'Piece'), 0),\n    IFNULL((SELECT SUM(weight) FROM outbound_records WHERE category_id = c.id AND unit_type = 'Kilogram'), 0),\n    IFNULL((SELECT SUM(weight) FROM outbound_records WHERE category_id = c.id AND unit_type = 'Jin'), 0),\n    IFNULL((SELECT SUM(weight) FROM outbound_records WHERE category_id = c.id AND unit_type = 'Piece'), 0),\n    (SELECT unit_price FROM inbound_records WHERE category_id = c.id ORDER BY timestamp DESC, id DESC LIMIT 1),\n    (SELECT unit_price FROM outbound_records WHERE category_id = c.id ORDER BY timestamp DESC, id DESC LIMIT 1)\nFROM categories c;";
		List<(long CategoryId, double BuyPrice, double SellPrice, double Stock, double StockInJin, int StockInPieces)> snapshots = new List<(long, double, double, double, double, int)>();
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				double num = reader.GetDouble(3);
				double num2 = reader.GetDouble(4);
				double num3 = reader.GetDouble(5);
				double num4 = reader.GetDouble(6);
				double num5 = reader.GetDouble(7);
				double num6 = reader.GetDouble(8);
				snapshots.Add((reader.GetInt64(0), reader.IsDBNull(9) ? reader.GetDouble(1) : reader.GetDouble(9), reader.IsDBNull(10) ? reader.GetDouble(2) : reader.GetDouble(10), Math.Max(0.0, Math.Round(num - num4, 2)), Math.Max(0.0, Math.Round(num2 - num5, 2)), Math.Max(0, (int)Math.Round(num3 - num6))));
			}
		}
		long updatedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		foreach (var item in snapshots)
		{
			SqliteCommand sqliteCommand2 = connection.CreateCommand();
			sqliteCommand2.Transaction = transaction;
			sqliteCommand2.CommandText = "UPDATE categories\nSET\n    buy_price = $buyPrice,\n    sell_price = $sellPrice,\n    stock = $stock,\n    stock_in_jin = $stockInJin,\n    stock_in_pieces = $stockInPieces,\n    updated_at = $updatedAt\nWHERE id = $id;";
			sqliteCommand2.Parameters.AddWithValue("$id", item.CategoryId);
			sqliteCommand2.Parameters.AddWithValue("$buyPrice", item.BuyPrice);
			sqliteCommand2.Parameters.AddWithValue("$sellPrice", item.SellPrice);
			sqliteCommand2.Parameters.AddWithValue("$stock", item.Stock);
			sqliteCommand2.Parameters.AddWithValue("$stockInJin", item.StockInJin);
			sqliteCommand2.Parameters.AddWithValue("$stockInPieces", item.StockInPieces);
			sqliteCommand2.Parameters.AddWithValue("$updatedAt", updatedAt);
			await sqliteCommand2.ExecuteNonQueryAsync();
		}
	}

	private static async Task<CategoryModel> GetCategoryAsync(SqliteConnection connection, SqliteTransaction transaction, long categoryId)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT id, name, buy_price, sell_price, stock, stock_in_jin, stock_in_pieces, unit_type, is_archived, created_at, updated_at\nFROM categories\nWHERE id = $id\nLIMIT 1;";
		sqliteCommand.Parameters.AddWithValue("$id", categoryId);
		CategoryModel result;
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			if (!(await reader.ReadAsync()))
			{
                throw new InvalidOperationException("Category was not found.");
			}
			result = new CategoryModel
			{
				Id = reader.GetInt64(0),
				Name = reader.GetString(1),
				BuyPrice = reader.GetDouble(2),
				SellPrice = reader.GetDouble(3),
				Stock = reader.GetDouble(4),
				StockInJin = reader.GetDouble(5),
				StockInPieces = reader.GetInt32(6),
				UnitType = ParseUnit(reader.GetString(7)),
				IsArchived = (reader.GetInt64(8) == 1),
				CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
				UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))
			};
		}
		return result;
	}

	private static async Task<OrderEditModel> GetOrderEditModelAsync(SqliteConnection connection, SqliteTransaction transaction, long orderId)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT\n    o.id,\n    o.order_number,\n    o.type,\n    o.customer_id,\n    IFNULL(c.name, ''),\n    d.category_id,\n    d.quantity,\n    d.unit_price,\n    d.unit_type,\n    o.timestamp\nFROM orders o\nLEFT JOIN customers c ON c.id = o.customer_id\nINNER JOIN (\n    SELECT\n        ir.order_id,\n        ir.category_id,\n        ir.weight AS quantity,\n        ir.unit_price,\n        ir.unit_type\n    FROM inbound_records ir\n\n    UNION ALL\n\n    SELECT\n        orr.order_id,\n        orr.category_id,\n        orr.weight AS quantity,\n        orr.unit_price,\n        orr.unit_type\n    FROM outbound_records orr\n) d ON d.order_id = o.id\nWHERE o.id = $orderId;";
		sqliteCommand.Parameters.AddWithValue("$orderId", orderId);
		List<OrderEditModel> records = new List<OrderEditModel>();
		OrderEditModel result;
		await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				records.Add(new OrderEditModel
				{
					OrderId = reader.GetInt64(0),
					OrderNumber = reader.GetString(1),
					Type = reader.GetString(2),
					CustomerId = (reader.IsDBNull(3) ? ((long?)null) : new long?(reader.GetInt64(3))),
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
                throw new InvalidOperationException("Order to edit was not found.");
			}
			if (records.Count > 1)
			{
                throw new InvalidOperationException("Only single-detail orders can be edited.");
			}
			result = records[0];
		}
		return result;
	}

	private static async Task<long> GetOrCreateCustomerAsync(SqliteConnection connection, SqliteTransaction transaction, string customerName, long now, bool markInbound, bool markOutbound)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT id\nFROM customers\nWHERE LOWER(name) = LOWER($name)\nLIMIT 1;";
		sqliteCommand.Parameters.AddWithValue("$name", customerName);
		object existingCustomerId = await sqliteCommand.ExecuteScalarAsync();
		if (existingCustomerId != null && existingCustomerId != DBNull.Value)
		{
			SqliteCommand sqliteCommand2 = connection.CreateCommand();
			sqliteCommand2.Transaction = transaction;
			sqliteCommand2.CommandText = "UPDATE customers\nSET\n    has_inbound_orders = CASE WHEN $markInbound = 1 THEN 1 ELSE has_inbound_orders END,\n    has_outbound_orders = CASE WHEN $markOutbound = 1 THEN 1 ELSE has_outbound_orders END,\n    updated_at = $updatedAt\nWHERE id = $id;";
			sqliteCommand2.Parameters.AddWithValue("$id", Convert.ToInt64(existingCustomerId, CultureInfo.InvariantCulture));
			sqliteCommand2.Parameters.AddWithValue("$markInbound", markInbound ? 1 : 0);
			sqliteCommand2.Parameters.AddWithValue("$markOutbound", markOutbound ? 1 : 0);
			sqliteCommand2.Parameters.AddWithValue("$updatedAt", now);
			await sqliteCommand2.ExecuteNonQueryAsync();
			return Convert.ToInt64(existingCustomerId, CultureInfo.InvariantCulture);
		}
		SqliteCommand sqliteCommand3 = connection.CreateCommand();
		sqliteCommand3.Transaction = transaction;
		sqliteCommand3.CommandText = "INSERT INTO customers (\n    name, phone, email, address, note, has_inbound_orders, has_outbound_orders, created_at, updated_at\n)\nVALUES (\n    $name, '', '', '', '', $hasInboundOrders, $hasOutboundOrders, $createdAt, $updatedAt\n);";
		sqliteCommand3.Parameters.AddWithValue("$name", customerName);
		sqliteCommand3.Parameters.AddWithValue("$hasInboundOrders", markInbound ? 1 : 0);
		sqliteCommand3.Parameters.AddWithValue("$hasOutboundOrders", markOutbound ? 1 : 0);
		sqliteCommand3.Parameters.AddWithValue("$createdAt", now);
		sqliteCommand3.Parameters.AddWithValue("$updatedAt", now);
		await sqliteCommand3.ExecuteNonQueryAsync();
		return await GetLastInsertRowIdAsync(connection, transaction);
	}

	private static async Task UpsertCustomerCategoryPriceAsync(SqliteConnection connection, SqliteTransaction transaction, long customerId, long categoryId, double price, long now)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT id\nFROM customer_category_prices\nWHERE customer_id = $customerId AND category_id = $categoryId\nORDER BY updated_at DESC\nLIMIT 1;";
		sqliteCommand.Parameters.AddWithValue("$customerId", customerId);
		sqliteCommand.Parameters.AddWithValue("$categoryId", categoryId);
		object obj = await sqliteCommand.ExecuteScalarAsync();
		if (obj != null && obj != DBNull.Value)
		{
			SqliteCommand sqliteCommand2 = connection.CreateCommand();
			sqliteCommand2.Transaction = transaction;
			sqliteCommand2.CommandText = "UPDATE customer_category_prices\nSET\n    price = $price,\n    updated_at = $updatedAt\nWHERE id = $id;";
			sqliteCommand2.Parameters.AddWithValue("$id", Convert.ToInt64(obj, CultureInfo.InvariantCulture));
			sqliteCommand2.Parameters.AddWithValue("$price", price);
			sqliteCommand2.Parameters.AddWithValue("$updatedAt", now);
			await sqliteCommand2.ExecuteNonQueryAsync();
		}
		else
		{
			SqliteCommand sqliteCommand3 = connection.CreateCommand();
			sqliteCommand3.Transaction = transaction;
			sqliteCommand3.CommandText = "INSERT INTO customer_category_prices (\n    customer_id, category_id, price, updated_at\n)\nVALUES (\n    $customerId, $categoryId, $price, $updatedAt\n);";
			sqliteCommand3.Parameters.AddWithValue("$customerId", customerId);
			sqliteCommand3.Parameters.AddWithValue("$categoryId", categoryId);
			sqliteCommand3.Parameters.AddWithValue("$price", price);
			sqliteCommand3.Parameters.AddWithValue("$updatedAt", now);
			await sqliteCommand3.ExecuteNonQueryAsync();
		}
	}

	private static async Task<long> InsertOrderAsync(SqliteConnection connection, SqliteTransaction transaction, string orderNumber, string type, long? customerId, double quantity, double totalAmount, int categoryCount, int itemCount, long now)
	{
		SqliteCommand command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText =
			"""
			INSERT INTO orders (
			    order_number, type, total_weight, total_amount, category_count, item_count, customer_id, note, timestamp
			)
			VALUES (
			    $orderNumber, $type, $totalWeight, $totalAmount, $categoryCount, $itemCount, $customerId, '', $timestamp
			);
			""";
		command.Parameters.AddWithValue("$orderNumber", orderNumber);
		command.Parameters.AddWithValue("$type", type);
		command.Parameters.AddWithValue("$totalWeight", quantity);
		command.Parameters.AddWithValue("$totalAmount", totalAmount);
		command.Parameters.AddWithValue("$categoryCount", categoryCount);
		command.Parameters.AddWithValue("$itemCount", itemCount);
		command.Parameters.AddWithValue("$timestamp", now);
		SqliteParameter customerParameter = command.CreateParameter();
		customerParameter.ParameterName = "$customerId";
		customerParameter.Value = customerId.HasValue ? customerId.Value : DBNull.Value;
		command.Parameters.Add(customerParameter);
		await command.ExecuteNonQueryAsync();
		return await GetLastInsertRowIdAsync(connection, transaction);
	}

	private static string CreateOrderNumber(string type, long now, string? customerName)
	{
		string orderPrefix = string.Equals(type, "outbound", StringComparison.OrdinalIgnoreCase) ? "出库" : "入库";
		string timestamp = DateTimeOffset
			.FromUnixTimeMilliseconds(now)
			.ToLocalTime()
			.ToString("yyyy_MM_dd_HH_mm", CultureInfo.InvariantCulture);
		string customerSegment = NormalizeOrderCustomerName(customerName);
		return $"{orderPrefix}_{timestamp}_{customerSegment}";
	}

	private static string NormalizeOrderCustomerName(string? customerName)
	{
		if (string.IsNullOrWhiteSpace(customerName))
		{
			return "匿名客户";
		}

		string value = customerName
			.Trim()
			.Replace("\r", " ", StringComparison.Ordinal)
			.Replace("\n", " ", StringComparison.Ordinal);

		while (value.Contains("  ", StringComparison.Ordinal))
		{
			value = value.Replace("  ", " ", StringComparison.Ordinal);
		}

		return string.IsNullOrWhiteSpace(value) ? "匿名客户" : value;
	}

	private static int CalculateOrderItemCount(IEnumerable<OrderDetailItemModel> detailItems)
	{
		return detailItems.Sum((OrderDetailItemModel item) => item.UnitType == WeightUnit.Piece ? (int)Math.Round(item.Quantity) : 1);
	}

	private static async Task<long> GetLastInsertRowIdAsync(SqliteConnection connection, SqliteTransaction transaction)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "SELECT last_insert_rowid();";
		return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
	}

	private static async Task ExportQueryToCsvAsync(SqliteConnection connection, string filePath, string query)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = query;
		SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync();
		try
		{
			await using FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
			await using StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			IEnumerable<string> values = from index in Enumerable.Range(0, reader.FieldCount)
				select EscapeCsv(reader.GetName(index));
			await writer.WriteLineAsync(string.Join(",", values));
			while (await reader.ReadAsync())
			{
				string[] array = new string[reader.FieldCount];
				for (int num = 0; num < reader.FieldCount; num++)
				{
					array[num] = EscapeCsv(reader.IsDBNull(num) ? string.Empty : (Convert.ToString(reader.GetValue(num), CultureInfo.InvariantCulture) ?? string.Empty));
				}
				await writer.WriteLineAsync(string.Join(",", array));
			}
		}
		finally
		{
			if (reader != null)
			{
				await reader.DisposeAsync();
			}
		}
	}

	private static string EscapeCsv(string value)
	{
		string text = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
		if (text.Contains(',', StringComparison.Ordinal) || text.Contains('"', StringComparison.Ordinal) || text.Contains('\n', StringComparison.Ordinal))
		{
			return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
		}
		return text;
	}

	private async Task<(ImportPreviewModel Preview, List<CategoryImportPlan> Plans)> BuildCategoryImportPreviewAsync(string sourcePath)
	{
		ValidateCsvSourcePath(sourcePath);
		IReadOnlyList<CsvRowData> rows = await ReadCsvRowsAsync(sourcePath);
		Dictionary<string, (long Id, WeightUnit UnitType)> existingCategories = new Dictionary<string, (long, WeightUnit)>(StringComparer.OrdinalIgnoreCase);
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT id, name, unit_type FROM categories;";
			await using SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				existingCategories[reader.GetString(1).Trim()] = (reader.GetInt64(0), ParseUnit(reader.GetString(2)));
			}
		}
		List<ImportPreviewIssueModel> list = new List<ImportPreviewIssueModel>();
		List<ImportPreviewRowModel> list2 = new List<ImportPreviewRowModel>();
		List<CategoryImportPlan> list3 = new List<CategoryImportPlan>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CsvRowData item in rows)
		{
			int lineNumber = item.LineNumber;
			try
			{
				string text = GetCsvValue(item.Values, "category_name", "name", "鍒嗙被鍚嶇О", "鍒嗙被")?.Trim() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(text))
				{
					list.Add(new ImportPreviewIssueModel
					{
						LineNumber = lineNumber,
                        Message = "Missing category name.",
					});
					continue;
				}
				if (!hashSet.Add(text))
				{
					list.Add(new ImportPreviewIssueModel
					{
						LineNumber = lineNumber,
						Message = $"分类“{text}”在同一个 CSV 文件中重复出现。"
					});
					continue;
				}
				WeightUnit weightUnit = ParseCsvUnit(GetCsvValue(item.Values, "unit_type", "unit", "鍗曚綅绫诲瀷", "鍗曚綅"));
                double num = ParseCsvDouble(GetCsvValue(item.Values, "buy_price", "purchase_price", "buyPrice"), "buy_price");
				double num2 = ParseCsvDouble(GetCsvValue(item.Values, "sell_price", "鍗栦环", "閿€鍞环"), "鍗栦环");
                bool flag = ParseCsvBool(GetCsvValue(item.Values, "is_archived", "archived", "isArchived"), defaultValue: false);
				if (num <= 0.0)
				{
					list.Add(new ImportPreviewIssueModel
					{
						LineNumber = lineNumber,
                        Message = "Buy price must be greater than 0.",
					});
					continue;
				}
				if (num2 <= 0.0)
				{
					list.Add(new ImportPreviewIssueModel
					{
						LineNumber = lineNumber,
                        Message = "Sell price must be greater than 0.",
					});
					continue;
				}
				(long, WeightUnit) value;
				bool flag2 = existingCategories.TryGetValue(text, out value);
				if (flag2 && value.Item2 != weightUnit)
				{
					list.Add(new ImportPreviewIssueModel
					{
						LineNumber = lineNumber,
						Message = $"分类“{text}”的单位与现有数据不一致，无法自动合并。"
					});
					continue;
				}
				list2.Add(new ImportPreviewRowModel
				{
					LineNumber = lineNumber,
					Name = text,
					ActionText = (flag2 ? "鏇存柊" : "鏂板"),
                    DetailText = $"Unit: {FormatUnitLabel(weightUnit)}, buy price: ?{num:0.##}, sell price: ?{num2:0.##}{(flag ? ", status: archived" : string.Empty)}"
				});
				list3.Add(new CategoryImportPlan(flag2 ? new long?(value.Item1) : ((long?)null), text, num, num2, weightUnit, flag, flag2));
			}
			catch (Exception ex)
			{
				list.Add(new ImportPreviewIssueModel
				{
					LineNumber = lineNumber,
					Message = ex.Message
				});
			}
		}
		return (Preview: new ImportPreviewModel
		{
			EntityDisplayName = "鍒嗙被",
			SourcePath = sourcePath,
			TotalRowCount = rows.Count,
			ReadyToImportCount = list3.Count,
			InsertCount = list3.Count((CategoryImportPlan plan) => !plan.IsUpdate),
			UpdateCount = list3.Count((CategoryImportPlan plan) => plan.IsUpdate),
			SkippedCount = 0,
			Rows = list2,
			Issues = list
		}, Plans: list3);
	}

	private async Task<(ImportPreviewModel Preview, List<CustomerImportPlan> Plans)> BuildCustomerImportPreviewAsync(string sourcePath)
	{
		ValidateCsvSourcePath(sourcePath);
		IReadOnlyList<CsvRowData> rows = await ReadCsvRowsAsync(sourcePath);
		Dictionary<string, (long Id, bool HasInboundOrders, bool HasOutboundOrders)> existingCustomers = new Dictionary<string, (long, bool, bool)>(StringComparer.OrdinalIgnoreCase);
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT id, name, has_inbound_orders, has_outbound_orders FROM customers;";
			await using SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				existingCustomers[reader.GetString(1).Trim()] = (reader.GetInt64(0), reader.GetInt64(2) == 1, reader.GetInt64(3) == 1);
			}
		}
		List<ImportPreviewIssueModel> list = new List<ImportPreviewIssueModel>();
		List<ImportPreviewRowModel> list2 = new List<ImportPreviewRowModel>();
		List<CustomerImportPlan> list3 = new List<CustomerImportPlan>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CsvRowData item in rows)
		{
			int lineNumber = item.LineNumber;
			try
			{
				string text = GetCsvValue(item.Values, "customer_name", "name", "瀹㈡埛鍚嶇О", "瀹㈡埛")?.Trim() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(text))
				{
					list.Add(new ImportPreviewIssueModel
					{
						LineNumber = lineNumber,
                        Message = "Missing customer name.",
					});
					continue;
				}
				if (!hashSet.Add(text))
				{
					list.Add(new ImportPreviewIssueModel
					{
						LineNumber = lineNumber,
						Message = $"客户“{text}”在同一个 CSV 文件中重复出现。"
					});
					continue;
				}
                string text2 = GetCsvValue(item.Values, "phone", "mobile", "telephone")?.Trim() ?? string.Empty;
				string text3 = GetCsvValue(item.Values, "email", "閭")?.Trim() ?? string.Empty;
				string address = GetCsvValue(item.Values, "address", "鍦板潃")?.Trim() ?? string.Empty;
				string note = GetCsvValue(item.Values, "note", "澶囨敞")?.Trim() ?? string.Empty;
				bool flag = ParseCsvBool(GetCsvValue(item.Values, "has_inbound_orders", "鍏ュ簱瀹㈡埛"), defaultValue: false);
				bool flag2 = ParseCsvBool(GetCsvValue(item.Values, "has_outbound_orders", "鍑哄簱瀹㈡埛"), defaultValue: false);
				(long, bool, bool) value;
				bool flag3 = existingCustomers.TryGetValue(text, out value);
				bool hasInboundOrders = (flag3 ? (value.Item2 || flag) : flag);
				bool hasOutboundOrders = (flag3 ? (value.Item3 || flag2) : flag2);
				list2.Add(new ImportPreviewRowModel
				{
					LineNumber = lineNumber,
					Name = text,
					ActionText = (flag3 ? "鏇存柊" : "鏂板"),
					DetailText = $"电话：{DisplayOrPlaceholder(text2)}，邮箱：{DisplayOrPlaceholder(text3)}，标记：{BuildCustomerUsageText(hasInboundOrders, hasOutboundOrders)}"
				});
				list3.Add(new CustomerImportPlan(flag3 ? new long?(value.Item1) : ((long?)null), text, text2, text3, address, note, hasInboundOrders, hasOutboundOrders, flag3));
			}
			catch (Exception ex)
			{
				list.Add(new ImportPreviewIssueModel
				{
					LineNumber = lineNumber,
					Message = ex.Message
				});
			}
		}
		return (Preview: new ImportPreviewModel
		{
			EntityDisplayName = "瀹㈡埛",
			SourcePath = sourcePath,
			TotalRowCount = rows.Count,
			ReadyToImportCount = list3.Count,
			InsertCount = list3.Count((CustomerImportPlan plan) => !plan.IsUpdate),
			UpdateCount = list3.Count((CustomerImportPlan plan) => plan.IsUpdate),
			SkippedCount = 0,
			Rows = list2,
			Issues = list
		}, Plans: list3);
	}

	private static InvalidOperationException CreateImportPreviewException(ImportPreviewModel preview)
	{
		if (preview.IssueCount == 0)
		{
            return new InvalidOperationException("There are no valid records to import.");
		}
		string text = string.Join(Environment.NewLine, from issue in preview.Issues.Take(3)
                select issue.LineText + ": " + issue.Message);
		if (preview.IssueCount > 3)
		{
            text += $"{Environment.NewLine}And {preview.IssueCount - 3} more issue(s).";
		}
		return new InvalidOperationException("瀵煎叆棰勮鏈€氳繃锛岃鍏堜慨姝ｄ互涓嬮棶棰橈細" + Environment.NewLine + text);
	}

	private static void ValidateCsvSourcePath(string sourcePath)
	{
		if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
		{
            throw new InvalidOperationException("CSV file to import was not found.");
		}
		if (!string.Equals(Path.GetExtension(sourcePath), ".csv", StringComparison.OrdinalIgnoreCase))
		{
            throw new InvalidOperationException("Only CSV files are supported for import.");
		}
	}

	private static async Task<IReadOnlyList<CsvRowData>> ReadCsvRowsAsync(string sourcePath)
	{
		List<CsvRawRow> list = ParseCsvContent(await File.ReadAllTextAsync(sourcePath, Encoding.UTF8));
		if (list.Count <= 1)
		{
			return Array.Empty<CsvRowData>();
		}
		List<string> list2 = list[0].Values.Select((string value) => NormalizeCsvHeader(value)).ToList();
		List<CsvRowData> list3 = new List<CsvRowData>();
		foreach (CsvRawRow item in list.Skip(1))
		{
			if (item.Values.All((string value) => string.IsNullOrWhiteSpace(value)))
			{
				continue;
			}
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			for (int num = 0; num < list2.Count; num++)
			{
				if (!string.IsNullOrWhiteSpace(list2[num]))
				{
					dictionary[list2[num]] = ((num < item.Values.Count) ? item.Values[num].Trim() : string.Empty);
				}
			}
			list3.Add(new CsvRowData(item.LineNumber, dictionary));
		}
		return list3;
	}

	private static List<CsvRawRow> ParseCsvContent(string content)
	{
		List<CsvRawRow> list = new List<CsvRawRow>();
		List<string> list2 = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		int num = 1;
		for (int i = 0; i < content.Length; i++)
		{
			char c = content[i];
			if (flag)
			{
				if (c == '"')
				{
					if (i + 1 < content.Length && content[i + 1] == '"')
					{
						stringBuilder.Append('"');
						i++;
					}
					else
					{
						flag = false;
					}
				}
				else
				{
					stringBuilder.Append(c);
				}
				continue;
			}
			switch (c)
			{
			case '"':
				flag = true;
				break;
			case ',':
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
				break;
			case '\r':
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
				list.Add(new CsvRawRow(num, list2));
				list2 = new List<string>();
				if (i + 1 < content.Length && content[i + 1] == '\n')
				{
					i++;
				}
				num++;
				break;
			case '\n':
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
				list.Add(new CsvRawRow(num, list2));
				list2 = new List<string>();
				num++;
				break;
			default:
				stringBuilder.Append(c);
				break;
			}
		}
		list2.Add(stringBuilder.ToString());
		list.Add(new CsvRawRow(num, list2));
		while (list.Count > 0)
		{
			if (!list[list.Count - 1].Values.All((string value) => string.IsNullOrWhiteSpace(value)))
			{
				break;
			}
			list.RemoveAt(list.Count - 1);
		}
		return list;
	}

	private static string NormalizeCsvHeader(string header)
	{
		return header.Trim().TrimStart('\ufeff').Replace("_", string.Empty, StringComparison.Ordinal)
			.Replace("-", string.Empty, StringComparison.Ordinal)
			.Replace(" ", string.Empty, StringComparison.Ordinal)
			.ToLowerInvariant();
	}

	private static string? GetCsvValue(IReadOnlyDictionary<string, string> row, params string[] candidates)
	{
		for (int i = 0; i < candidates.Length; i++)
		{
			string key = NormalizeCsvHeader(candidates[i]);
			if (row.TryGetValue(key, out string value))
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
		if (double.TryParse(value, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var result2))
		{
			return result2;
		}
		throw new InvalidOperationException($"“{fieldName}”包含无法识别的数字：{value}");
	}

	private static bool ParseCsvBool(string? value, bool defaultValue)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return defaultValue;
		}
		string text = value.Trim().ToLowerInvariant();
		return text switch
		{
			"1" or "true" or "yes" or "y" or "是" or "有" => true,
			"0" or "false" or "no" or "n" or "否" or "无" => false,
			_ => throw new InvalidOperationException("无法识别的布尔值：" + value)
		};
	}

	private static WeightUnit ParseCsvUnit(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return WeightUnit.Kilogram;
		}
		switch (value.Trim().ToLowerInvariant())
		{
		case "kg":
		case "鍏枻":
		case "kilogram":
			return WeightUnit.Kilogram;
		case "斤":
		case "jin":
			return WeightUnit.Jin;
		case "件":
		case "piece":
		case "pieces":
			return WeightUnit.Piece;
		default:
			return ParseUnit(value);
		}
	}

	private static string FormatUnitLabel(WeightUnit unitType)
	{
		return unitType switch
		{
			WeightUnit.Kilogram => "kg",
			WeightUnit.Jin => "斤",
			WeightUnit.Piece => "件",
			_ => "kg",
		};
	}

	private static string DisplayOrPlaceholder(string value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
        return "Not filled";
	}

	private static string BuildCustomerUsageText(bool hasInboundOrders, bool hasOutboundOrders)
	{
		if (hasInboundOrders && hasOutboundOrders)
		{
			return "鍏ュ簱 / 鍑哄簱";
		}
		if (hasInboundOrders)
		{
			return "鍏ュ簱";
		}
		if (hasOutboundOrders)
		{
			return "鍑哄簱";
		}
        return "Not set";
	}

	private static async Task<double> GetAmountSumAsync(SqliteConnection connection, string table, string columnName, long start, long end)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = $"SELECT IFNULL(SUM({columnName}), 0)\nFROM {table}\nWHERE timestamp >= $start AND timestamp < $end;";
		sqliteCommand.Parameters.AddWithValue("$start", start);
		sqliteCommand.Parameters.AddWithValue("$end", end);
		return Convert.ToDouble(await sqliteCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
	}

	private static (long Start, long End) GetTodayRange()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		DateTimeOffset dateTimeOffset = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
		DateTimeOffset dateTimeOffset2 = dateTimeOffset.AddDays(1.0);
		return (Start: dateTimeOffset.ToUnixTimeMilliseconds(), End: dateTimeOffset2.ToUnixTimeMilliseconds());
	}

	private static WeightUnit ParseUnit(string value)
	{
		if (!Enum.TryParse<WeightUnit>(value, ignoreCase: true, out var result))
		{
			return WeightUnit.Kilogram;
		}
		return result;
	}
}

