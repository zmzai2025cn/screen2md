using Microsoft.Data.Sqlite;
using Dapper;
using Screen2MD.Kernel.Interfaces;
using Screen2MD.Kernel.Services;
using Screen2MD.Services.Interfaces;
using System.Diagnostics;
using System.Data;

namespace Screen2MD.Services.Services;

/// <summary>
/// SQLite存储服务 - 企业级实现，目标1000 TPS
/// </summary>
public sealed class SqliteStorageService : IStorageService, IDisposable
{
    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly IKernelLogger? _logger;
    private bool _disposed;

    public string Name => nameof(SqliteStorageService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    static SqliteStorageService()
    {
        // 注册 DateTimeOffset 类型处理器（只需执行一次）
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
    }

    public SqliteStorageService(string? dbPath = null, IKernelLogger? logger = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "data.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _connectionString = $"Data Source={_dbPath};Cache=Shared;Mode=ReadWriteCreate;";
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await InitializeDatabaseAsync();
        HealthStatus = HealthStatus.Healthy;
        _logger?.LogInformation($"Storage initialized at: {_dbPath}");
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private async Task InitializeDatabaseAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS CaptureRecords (
                Id TEXT PRIMARY KEY,
                CapturedAt TEXT NOT NULL,
                SoftwareType TEXT NOT NULL,
                ProcessName TEXT NOT NULL,
                WindowTitle TEXT NOT NULL,
                DocumentTitle TEXT,
                ThumbnailData BLOB,
                StoragePath TEXT NOT NULL,
                FileSize INTEGER DEFAULT 0,
                IsUploaded INTEGER DEFAULT 0,
                UploadedAt TEXT,
                TextContent TEXT,
                IsCodeContent INTEGER DEFAULT 0,
                Language TEXT
            )");

        await connection.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS IX_CapturedAt ON CaptureRecords(CapturedAt)");
        await connection.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS IX_IsUploaded ON CaptureRecords(IsUploaded)");
        await connection.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS IX_SoftwareType ON CaptureRecords(SoftwareType)");
    }

    public async Task<string> SaveCaptureAsync(CaptureRecord record, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            INSERT INTO CaptureRecords 
            (Id, CapturedAt, SoftwareType, ProcessName, WindowTitle, DocumentTitle, 
             ThumbnailData, StoragePath, FileSize, IsUploaded, UploadedAt, TextContent, IsCodeContent, Language)
            VALUES 
            (@Id, @CapturedAt, @SoftwareType, @ProcessName, @WindowTitle, @DocumentTitle,
             @ThumbnailData, @StoragePath, @FileSize, @IsUploaded, @UploadedAt, @TextContent, @IsCodeContent, @Language)";

        await connection.ExecuteAsync(sql, record);
        
        sw.Stop();
        _logger?.LogDebug($"Saved capture {record.Id} in {sw.ElapsedMilliseconds}ms");

        return record.Id;
    }

    public async Task<CaptureRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<CaptureRecord>(
            "SELECT * FROM CaptureRecords WHERE Id = @id",
            new { id });
    }

    public async Task<QueryResult<CaptureRecord>> QueryAsync(CaptureQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var whereConditions = new List<string>();
        var parameters = new DynamicParameters();

        if (query.StartTime.HasValue)
        {
            whereConditions.Add("CapturedAt >= @StartTime");
            parameters.Add("StartTime", query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            whereConditions.Add("CapturedAt <= @EndTime");
            parameters.Add("EndTime", query.EndTime.Value);
        }

        if (!string.IsNullOrEmpty(query.SoftwareType))
        {
            whereConditions.Add("SoftwareType = @SoftwareType");
            parameters.Add("SoftwareType", query.SoftwareType);
        }

        if (!string.IsNullOrEmpty(query.ProcessName))
        {
            whereConditions.Add("ProcessName LIKE @ProcessName");
            parameters.Add("ProcessName", $"%{query.ProcessName}%");
        }

        if (query.IsCodeContent.HasValue)
        {
            whereConditions.Add("IsCodeContent = @IsCodeContent");
            parameters.Add("IsCodeContent", query.IsCodeContent.Value ? 1 : 0);
        }

        if (query.IsUploaded.HasValue)
        {
            whereConditions.Add("IsUploaded = @IsUploaded");
            parameters.Add("IsUploaded", query.IsUploaded.Value ? 1 : 0);
        }

        var whereClause = whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";

        var countSql = $"SELECT COUNT(*) FROM CaptureRecords {whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var dataSql = $@"
            SELECT * FROM CaptureRecords {whereClause}
            ORDER BY CapturedAt DESC
            LIMIT @PageSize OFFSET @Offset";
        
        parameters.Add("PageSize", query.PageSize);
        parameters.Add("Offset", (query.PageNumber - 1) * query.PageSize);

        var items = await connection.QueryAsync<CaptureRecord>(dataSql, parameters);

        return new QueryResult<CaptureRecord>
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task UpdateUploadStatusAsync(string id, bool isUploaded, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"UPDATE CaptureRecords 
                     SET IsUploaded = @isUploaded, 
                         UploadedAt = CASE WHEN @isUploaded = 1 THEN @uploadedAt ELSE NULL END
                     WHERE Id = @id";

        await connection.ExecuteAsync(sql, new 
        { 
            id, 
            isUploaded = isUploaded ? 1 : 0, 
            uploadedAt = DateTimeOffset.UtcNow 
        });
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM CaptureRecords WHERE Id = @id", new { id });
    }

    public async Task<StorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var totalRecords = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CaptureRecords");
        var totalSize = await connection.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(FileSize), 0) FROM CaptureRecords");
        var uploadedCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CaptureRecords WHERE IsUploaded = 1");
        var pendingCount = totalRecords - uploadedCount;

        var typeCounts = await connection.QueryAsync<KeyValuePair<string, int>>(
            "SELECT SoftwareType as Key, COUNT(*) as Value FROM CaptureRecords GROUP BY SoftwareType");

        return new StorageStatistics
        {
            TotalRecords = totalRecords,
            TotalSize = totalSize,
            UploadedCount = uploadedCount,
            PendingCount = pendingCount,
            CountBySoftwareType = typeCounts.ToDictionary(x => x.Key, x => x.Value)
        };
    }

    public async Task<int> CleanupOldDataAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var cutoff = DateTimeOffset.UtcNow - retentionPeriod;
        var sql = "DELETE FROM CaptureRecords WHERE CapturedAt < @cutoff AND IsUploaded = 1";
        
        var deleted = await connection.ExecuteAsync(sql, new { cutoff });
        _logger?.LogInformation($"Cleaned up {deleted} old records");
        
        return deleted;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger?.LogInformation("Storage service disposed");
        }
    }
}

/// <summary>
/// Dapper DateTimeOffset 类型处理器
/// </summary>
public class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.Value = value.ToString("O");
    }

    public override DateTimeOffset Parse(object value)
    {
        if (value is string str)
        {
            return DateTimeOffset.Parse(str);
        }
        return (DateTimeOffset)value;
    }
}
