using Microsoft.Data.Sqlite;
using Screen2MD.Kernel.Interfaces;
using Screen2MD.Services.Interfaces;
using Screen2MD.Services;
using System.Text;

namespace Screen2MD.Services.Services;

/// <summary>
/// 全文搜索服务 - 基于 SQLite FTS5（已废弃）
/// 使用 LuceneSearchService 替代，提供更好的跨平台支持
/// </summary>
[Obsolete("SQLite FTS5 has cross-platform compatibility issues. Use LuceneSearchService instead.", error: false)]
public sealed class FullTextSearchService : IFullTextSearchService, IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly IKernelLogger? _logger;
    private bool _disposed;

    /// <summary>
    /// 服务名称
    /// </summary>
    public string Name => nameof(FullTextSearchService);

    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    /// <summary>
    /// 构造函数
    /// </summary>
    public FullTextSearchService(string? dbPath = null, IKernelLogger? logger = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "search.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _connectionString = $"Data Source={_dbPath};";
        _logger = logger;
    }

    /// <summary>
    /// 初始化全文搜索服务
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 创建 FTS5 虚拟表
            var createFtsSql = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS capture_fts USING fts5(
                    capture_id,
                    title,
                    content,
                    process_name,
                    window_title,
                    tags,
                    tokenize = 'porter unicode61'
                )";

            using (var command = new SqliteCommand(createFtsSql, connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // 创建元数据表
            var createMetaSql = @"
                CREATE TABLE IF NOT EXISTS capture_meta (
                    capture_id TEXT PRIMARY KEY,
                    timestamp INTEGER,
                    file_path TEXT,
                    display_title TEXT
                )";

            using (var command = new SqliteCommand(createMetaSql, connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // 创建索引
            using (var command = new SqliteCommand("CREATE INDEX IF NOT EXISTS idx_meta_time ON capture_meta(timestamp)", connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            using (var command = new SqliteCommand("CREATE INDEX IF NOT EXISTS idx_meta_process ON capture_meta(process_name)", connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            HealthStatus = HealthStatus.Healthy;
            _logger?.LogInformation("FullTextSearchService initialized");
        }
        catch (Exception ex)
        {
            HealthStatus = HealthStatus.Unhealthy;
            _logger?.LogError($"Failed to initialize FullTextSearchService: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 启动服务
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 索引捕获内容
    /// </summary>
    public async Task IndexCaptureAsync(CaptureDocument document, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FullTextSearchService));

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();

            try
            {
                // 插入元数据
                var metaSql = @"
                    INSERT OR REPLACE INTO capture_meta (capture_id, timestamp, file_path, display_title)
                    VALUES (@captureId, @timestamp, @filePath, @displayTitle)";

                using (var metaCmd = new SqliteCommand(metaSql, connection, transaction))
                {
                    metaCmd.Parameters.AddWithValue("@captureId", document.Id);
                    metaCmd.Parameters.AddWithValue("@timestamp", document.Timestamp.ToUnixTimeSeconds());
                    metaCmd.Parameters.AddWithValue("@filePath", document.FilePath);
                    metaCmd.Parameters.AddWithValue("@displayTitle", document.Title);
                    await metaCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // 插入FTS索引
                var ftsSql = @"
                    INSERT OR REPLACE INTO capture_fts (capture_id, title, content, process_name, window_title, tags)
                    VALUES (@captureId, @title, @content, @processName, @windowTitle, @tags)";

                using (var ftsCmd = new SqliteCommand(ftsSql, connection, transaction))
                {
                    ftsCmd.Parameters.AddWithValue("@captureId", document.Id);
                    ftsCmd.Parameters.AddWithValue("@title", document.Title ?? "");
                    ftsCmd.Parameters.AddWithValue("@content", document.Content ?? "");
                    ftsCmd.Parameters.AddWithValue("@processName", document.ProcessName ?? "");
                    ftsCmd.Parameters.AddWithValue("@windowTitle", document.WindowTitle ?? "");
                    ftsCmd.Parameters.AddWithValue("@tags", string.Join(",", document.Tags ?? Array.Empty<string>()));
                    await ftsCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                transaction.Commit();
                _logger?.LogDebug($"Indexed capture: {document.Id}");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to index capture: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 搜索
    /// </summary>
    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FullTextSearchService));

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var whereClause = new StringBuilder();
            var parameters = new List<SqliteParameter>();

            // 关键词搜索
            if (!string.IsNullOrWhiteSpace(query.Keywords))
            {
                // 支持多种搜索模式
                var searchTerm = query.Keywords.Trim();
                
                if (query.ExactMatch)
                {
                    // 精确匹配
                    whereClause.Append("(title = @keywords OR content = @keywords)");
                    parameters.Add(new SqliteParameter("@keywords", searchTerm));
                }
                else if (query.PrefixMatch)
                {
                    // 前缀匹配
                    whereClause.Append("(capture_fts MATCH @keywords)");
                    parameters.Add(new SqliteParameter("@keywords", $"{searchTerm}*"));
                }
                else
                {
                    // 全文匹配
                    whereClause.Append("(capture_fts MATCH @keywords)");
                    parameters.Add(new SqliteParameter("@keywords", searchTerm));
                }
            }

            // 应用筛选
            if (!string.IsNullOrWhiteSpace(query.ProcessName))
            {
                if (whereClause.Length > 0) whereClause.Append(" AND ");
                whereClause.Append("f.process_name = @processName");
                parameters.Add(new SqliteParameter("@processName", query.ProcessName));
            }

            // 时间范围筛选
            if (query.StartTime.HasValue)
            {
                if (whereClause.Length > 0) whereClause.Append(" AND ");
                whereClause.Append("c.timestamp >= @startTime");
                parameters.Add(new SqliteParameter("@startTime", query.StartTime.Value.ToUnixTimeSeconds()));
            }

            if (query.EndTime.HasValue)
            {
                if (whereClause.Length > 0) whereClause.Append(" AND ");
                whereClause.Append("c.timestamp <= @endTime");
                parameters.Add(new SqliteParameter("@endTime", query.EndTime.Value.ToUnixTimeSeconds()));
            }

            // 构建查询
            var sql = $@"
                SELECT 
                    c.capture_id,
                    c.timestamp,
                    c.file_path,
                    c.display_title,
                    f.title,
                    f.process_name,
                    f.window_title,
                    rank
                FROM capture_fts f
                JOIN capture_meta c ON f.capture_id = c.capture_id
                {(whereClause.Length > 0 ? "WHERE " + whereClause : "")}
                ORDER BY rank
                LIMIT @limit OFFSET @offset";

            var countSql = $@"
                SELECT COUNT(*)
                FROM capture_fts f
                JOIN capture_meta c ON f.capture_id = c.capture_id
                {(whereClause.Length > 0 ? "WHERE " + whereClause : "")}";

            var results = new List<SearchResultItem>();
            int totalCount = 0;

            // 执行计数查询
            using (var countCmd = new SqliteCommand(countSql, connection))
            {
                countCmd.Parameters.AddRange(parameters.ToArray());
                var countResult = await countCmd.ExecuteScalarAsync(cancellationToken);
                totalCount = Convert.ToInt32(countResult);
            }

            // 执行数据查询
            using (var cmd = new SqliteCommand(sql, connection))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
                cmd.Parameters.AddWithValue("@limit", query.PageSize);
                cmd.Parameters.AddWithValue("@offset", (query.PageNumber - 1) * query.PageSize);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new SearchResultItem
                    {
                        CaptureId = reader.GetString(0),
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
                        FilePath = reader.GetString(2),
                        DisplayTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Title = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ProcessName = reader.IsDBNull(5) ? null : reader.GetString(5),
                        WindowTitle = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Rank = reader.IsDBNull(7) ? 0 : reader.GetDouble(7)
                    });
                }
            }

            return new SearchResult
            {
                Items = results,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Query = query.Keywords ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Search failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 删除索引
    /// </summary>
    public async Task DeleteIndexAsync(string captureId, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FullTextSearchService));

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();

            var ftsSql = "DELETE FROM capture_fts WHERE capture_id = @captureId";
            var metaSql = "DELETE FROM capture_meta WHERE capture_id = @captureId";

            using (var ftsCmd = new SqliteCommand(ftsSql, connection, transaction))
            {
                ftsCmd.Parameters.AddWithValue("@captureId", captureId);
                await ftsCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            using (var metaCmd = new SqliteCommand(metaSql, connection, transaction))
            {
                metaCmd.Parameters.AddWithValue("@captureId", captureId);
                await metaCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to delete index: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 优化索引
    /// </summary>
    public async Task OptimizeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var cmd = new SqliteCommand("INSERT INTO capture_fts(capture_fts) VALUES('optimize')", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger?.LogInformation("Full-text search index optimized");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to optimize index: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public async Task<SearchStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT 
                    (SELECT COUNT(*) FROM capture_fts),
                    (SELECT COUNT(DISTINCT process_name) FROM capture_fts),
                    (SELECT MIN(timestamp) FROM capture_meta),
                    (SELECT MAX(timestamp) FROM capture_meta)";

            using var cmd = new SqliteCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                return new SearchStatistics
                {
                    TotalDocuments = reader.GetInt32(0),
                    UniqueProcesses = reader.GetInt32(1),
                    OldestDocument = reader.IsDBNull(2) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)),
                    NewestDocument = reader.IsDBNull(3) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3))
                };
            }

            return new SearchStatistics();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to get statistics: {ex.Message}");
            return new SearchStatistics();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// 全文搜索服务接口
/// </summary>
public interface IFullTextSearchService : IKernelComponent
{
    /// <summary>
    /// 索引捕获内容
    /// </summary>
    Task IndexCaptureAsync(CaptureDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜索
    /// </summary>
    Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除索引
    /// </summary>
    Task DeleteIndexAsync(string captureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 优化索引
    /// </summary>
    Task OptimizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    Task<SearchStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 捕获文档
/// </summary>
public class CaptureDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }
    public string[]? Tags { get; set; }
    public string? FilePath { get; set; }
    public int DisplayIndex { get; set; }
}

/// <summary>
/// 搜索查询
/// </summary>
public class SearchQuery
{
    public string? Keywords { get; set; }
    public string? ProcessName { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public bool ExactMatch { get; set; }
    public bool PrefixMatch { get; set; }
    public int PageSize { get; set; } = 20;
    public int PageNumber { get; set; } = 1;
}

/// <summary>
/// 搜索结果
/// </summary>
public class SearchResult
{
    public IReadOnlyList<SearchResultItem> Items { get; set; } = new List<SearchResultItem>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public string Query { get; set; } = "";
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}

/// <summary>
/// 搜索结果项
/// </summary>
public class SearchResultItem
{
    public string CaptureId { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public string? FilePath { get; set; }
    public string? DisplayTitle { get; set; }
    public string? Title { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }
    public double Rank { get; set; }
}

/// <summary>
/// 搜索统计
/// </summary>
public class SearchStatistics
{
    public int TotalDocuments { get; set; }
    public int UniqueProcesses { get; set; }
    public DateTimeOffset? OldestDocument { get; set; }
    public DateTimeOffset? NewestDocument { get; set; }
}
