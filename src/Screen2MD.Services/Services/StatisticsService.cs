using Screen2MD.Kernel.Interfaces;
using Screen2MD.Services.Interfaces;

namespace Screen2MD.Services.Services;

/// <summary>
/// 统计服务 - 收集和分析使用数据
/// </summary>
public sealed class StatisticsService : IKernelComponent, IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly IConfigurationManager _configManager;
    private readonly IKernelLogger? _logger;
    private Timer? _aggregationTimer;
    private bool _disposed;

    public string Name => nameof(StatisticsService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public StatisticsService(
        IConfigurationManager configManager,
        string? dbPath = null,
        IKernelLogger? logger = null)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logger = logger;
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "statistics.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _connectionString = $"Data Source={_dbPath};";
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var createTablesSql = @"
                CREATE TABLE IF NOT EXISTS capture_stats (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    date TEXT NOT NULL,
                    hour INTEGER NOT NULL,
                    count INTEGER NOT NULL DEFAULT 0,
                    total_size_bytes INTEGER NOT NULL DEFAULT 0,
                    ocr_success_count INTEGER NOT NULL DEFAULT 0,
                    ocr_fail_count INTEGER NOT NULL DEFAULT 0,
                    UNIQUE(date, hour)
                );

                CREATE INDEX IF NOT EXISTS idx_capture_stats_date ON capture_stats(date);
                CREATE INDEX IF NOT EXISTS idx_capture_stats_hour ON capture_stats(hour);

                CREATE TABLE IF NOT EXISTS process_stats (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    date TEXT NOT NULL,
                    process_name TEXT NOT NULL,
                    capture_count INTEGER NOT NULL DEFAULT 0,
                    UNIQUE(date, process_name)
                );

                CREATE INDEX IF NOT EXISTS idx_process_stats_date ON process_stats(date);
                CREATE INDEX IF NOT EXISTS idx_process_stats_process ON process_stats(process_name);

                CREATE TABLE IF NOT EXISTS ocr_stats (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    date TEXT NOT NULL,
                    total_attempts INTEGER NOT NULL DEFAULT 0,
                    success_count INTEGER NOT NULL DEFAULT 0,
                    fail_count INTEGER NOT NULL DEFAULT 0,
                    avg_processing_ms INTEGER NOT NULL DEFAULT 0,
                    UNIQUE(date)
                );

                CREATE INDEX IF NOT EXISTS idx_ocr_stats_date ON ocr_stats(date);

                CREATE TABLE IF NOT EXISTS storage_trend (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp INTEGER NOT NULL,
                    total_size_bytes INTEGER NOT NULL DEFAULT 0,
                    file_count INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_storage_trend_time ON storage_trend(timestamp);
            ";

            using var command = new Microsoft.Data.Sqlite.SqliteCommand(createTablesSql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            HealthStatus = HealthStatus.Healthy;
            _logger?.LogInformation("StatisticsService initialized");
        }
        catch (Exception ex)
        {
            HealthStatus = HealthStatus.Unhealthy;
            _logger?.LogError($"Failed to initialize StatisticsService: {ex.Message}");
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // 每小时聚合一次数据
        _aggregationTimer = new Timer(
            async _ => await AggregateHourlyStatsAsync(),
            null,
            TimeSpan.FromMinutes(5),  // 首次运行延迟 5 分钟
            TimeSpan.FromHours(1));    // 之后每小时

        _logger?.LogInformation("StatisticsService started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _aggregationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _logger?.LogInformation("StatisticsService stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 记录截图事件
    /// </summary>
    public async Task RecordCaptureAsync(CaptureStat capture, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var date = capture.Timestamp.ToString("yyyy-MM-dd");
            var hour = capture.Timestamp.Hour;

            // 更新或插入捕获统计
            var upsertSql = @"
                INSERT INTO capture_stats (date, hour, count, total_size_bytes, ocr_success_count, ocr_fail_count)
                VALUES (@date, @hour, 1, @size, @ocrSuccess, @ocrFail)
                ON CONFLICT(date, hour) DO UPDATE SET
                    count = count + 1,
                    total_size_bytes = total_size_bytes + @size,
                    ocr_success_count = ocr_success_count + @ocrSuccess,
                    ocr_fail_count = ocr_fail_count + @ocrFail";

            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(upsertSql, connection);
            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@hour", hour);
            cmd.Parameters.AddWithValue("@size", capture.FileSizeBytes);
            cmd.Parameters.AddWithValue("@ocrSuccess", capture.OcrSuccess ? 1 : 0);
            cmd.Parameters.AddWithValue("@ocrFail", capture.OcrSuccess ? 0 : 1);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            // 更新进程统计
            if (!string.IsNullOrEmpty(capture.ProcessName))
            {
                var processSql = @"
                    INSERT INTO process_stats (date, process_name, capture_count)
                    VALUES (@date, @process, 1)
                    ON CONFLICT(date, process_name) DO UPDATE SET
                        capture_count = capture_count + 1";

                using var processCmd = new Microsoft.Data.Sqlite.SqliteCommand(processSql, connection);
                processCmd.Parameters.AddWithValue("@date", date);
                processCmd.Parameters.AddWithValue("@process", capture.ProcessName);
                await processCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to record capture stat: {ex.Message}");
        }
    }

    /// <summary>
    /// 记录 OCR 事件
    /// </summary>
    public async Task RecordOcrAsync(OcrStat ocr, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var date = ocr.Timestamp.ToString("yyyy-MM-dd");

            // 使用事务确保原子性
            using var transaction = connection.BeginTransaction();

            // 先查询现有数据
            var selectSql = "SELECT total_attempts, success_count, avg_processing_ms FROM ocr_stats WHERE date = @date";
            using var selectCmd = new Microsoft.Data.Sqlite.SqliteCommand(selectSql, connection, transaction);
            selectCmd.Parameters.AddWithValue("@date", date);

            long totalAttempts = 0;
            long successCount = 0;
            long avgProcessingMs = 0;

            using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                totalAttempts = reader.GetInt64(0);
                successCount = reader.GetInt64(1);
                avgProcessingMs = reader.GetInt64(2);
            }
            reader.Close();

            // 计算新的平均值
            totalAttempts++;
            if (ocr.Success)
            {
                successCount++;
            }

            // 加权平均
            var newAvgMs = (avgProcessingMs * (totalAttempts - 1) + ocr.ProcessingTimeMs) / totalAttempts;

            // 更新或插入
            var upsertSql = @"
                INSERT INTO ocr_stats (date, total_attempts, success_count, fail_count, avg_processing_ms)
                VALUES (@date, @attempts, @success, @fail, @avgMs)
                ON CONFLICT(date) DO UPDATE SET
                    total_attempts = @attempts,
                    success_count = @success,
                    fail_count = @fail,
                    avg_processing_ms = @avgMs";

            using var upsertCmd = new Microsoft.Data.Sqlite.SqliteCommand(upsertSql, connection, transaction);
            upsertCmd.Parameters.AddWithValue("@date", date);
            upsertCmd.Parameters.AddWithValue("@attempts", totalAttempts);
            upsertCmd.Parameters.AddWithValue("@success", successCount);
            upsertCmd.Parameters.AddWithValue("@fail", totalAttempts - successCount);
            upsertCmd.Parameters.AddWithValue("@avgMs", newAvgMs);
            await upsertCmd.ExecuteNonQueryAsync(cancellationToken);

            transaction.Commit();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to record OCR stat: {ex.Message}");
        }
    }

    /// <summary>
    /// 记录存储趋势
    /// </summary>
    public async Task RecordStorageTrendAsync(long totalSizeBytes, int fileCount, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                INSERT INTO storage_trend (timestamp, total_size_bytes, file_count)
                VALUES (@timestamp, @size, @count)";

            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@size", totalSizeBytes);
            cmd.Parameters.AddWithValue("@count", fileCount);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to record storage trend: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取每日统计
    /// </summary>
    public async Task<DailyStatistics> GetDailyStatisticsAsync(DateTime date)
    {
        var result = new DailyStatistics { Date = date };
        var dateStr = date.ToString("yyyy-MM-dd");

        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // 获取捕获统计
            var captureSql = @"
                SELECT SUM(count), SUM(total_size_bytes), SUM(ocr_success_count), SUM(ocr_fail_count)
                FROM capture_stats
                WHERE date = @date";

            using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(captureSql, connection))
            {
                cmd.Parameters.AddWithValue("@date", dateStr);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.TotalCaptures = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    result.TotalSizeBytes = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    result.OcrSuccessCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    result.OcrFailCount = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                }
            }

            // 获取进程统计
            var processSql = @"
                SELECT process_name, capture_count
                FROM process_stats
                WHERE date = @date
                ORDER BY capture_count DESC
                LIMIT 10";

            using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(processSql, connection))
            {
                cmd.Parameters.AddWithValue("@date", dateStr);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.TopProcesses.Add(new ProcessStat
                    {
                        ProcessName = reader.GetString(0),
                        CaptureCount = reader.GetInt64(1)
                    });
                }
            }

            // 获取 OCR 统计
            var ocrSql = @"
                SELECT total_attempts, success_count, avg_processing_ms
                FROM ocr_stats
                WHERE date = @date";

            using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(ocrSql, connection))
            {
                cmd.Parameters.AddWithValue("@date", dateStr);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.OcrTotalAttempts = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    result.OcrSuccessCount = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    result.OcrAvgProcessingMs = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to get daily statistics: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 获取存储趋势
    /// </summary>
    public async Task<List<StorageTrendPoint>> GetStorageTrendAsync(int days = 30)
    {
        var result = new List<StorageTrendPoint>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds();

        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT timestamp, total_size_bytes, file_count
                FROM storage_trend
                WHERE timestamp >= @cutoff
                ORDER BY timestamp ASC";

            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new StorageTrendPoint
                {
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
                    TotalSizeBytes = reader.GetInt64(1),
                    FileCount = reader.GetInt32(2)
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to get storage trend: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 获取汇总统计
    /// </summary>
    public async Task<SummaryStatistics> GetSummaryStatisticsAsync()
    {
        var result = new SummaryStatistics();

        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // 总捕获数
            using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(
                "SELECT SUM(count), SUM(total_size_bytes) FROM capture_stats", connection))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.TotalCaptures = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    result.TotalStorageBytes = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                }
            }

            // OCR 统计
            using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(
                "SELECT SUM(total_attempts), SUM(success_count) FROM ocr_stats", connection))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.TotalOcrAttempts = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    result.TotalOcrSuccess = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                }
            }

            // 最早记录日期
            using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(
                "SELECT MIN(date) FROM capture_stats", connection))
            {
                var minDate = await cmd.ExecuteScalarAsync();
                if (minDate != null && minDate != DBNull.Value)
                {
                    result.FirstCaptureDate = DateTime.Parse((string)minDate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to get summary statistics: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 每小时聚合统计（定时任务）
    /// </summary>
    private async Task AggregateHourlyStatsAsync()
    {
        _logger?.LogDebug("Aggregating hourly statistics...");

        // 记录当前存储状态
        var capturesDir = _configManager.GetValue(
            "capture.outputDirectory",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Screen2MD", "Captures"));

        if (Directory.Exists(capturesDir))
        {
            var files = Directory.GetFiles(capturesDir, "*", SearchOption.AllDirectories);
            var totalSize = files.Sum(f => new FileInfo(f).Length);
            await RecordStorageTrendAsync(totalSize, files.Length);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _aggregationTimer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// 捕获统计事件
/// </summary>
public class CaptureStat
{
    public DateTimeOffset Timestamp { get; set; }
    public string? ProcessName { get; set; }
    public long FileSizeBytes { get; set; }
    public bool OcrSuccess { get; set; }
}

/// <summary>
/// OCR 统计事件
/// </summary>
public class OcrStat
{
    public DateTimeOffset Timestamp { get; set; }
    public bool Success { get; set; }
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// 每日统计
/// </summary>
public class DailyStatistics
{
    public DateTime Date { get; set; }
    public long TotalCaptures { get; set; }
    public long TotalSizeBytes { get; set; }
    public long OcrSuccessCount { get; set; }
    public long OcrFailCount { get; set; }
    public long OcrTotalAttempts { get; set; }
    public long OcrAvgProcessingMs { get; set; }
    public List<ProcessStat> TopProcesses { get; set; } = new();

    public double OcrSuccessRate => OcrTotalAttempts > 0
        ? (double)OcrSuccessCount / OcrTotalAttempts * 100
        : 0;

    public double TotalSizeMB => TotalSizeBytes / (1024.0 * 1024.0);
}

/// <summary>
/// 进程统计
/// </summary>
public class ProcessStat
{
    public string ProcessName { get; set; } = "";
    public long CaptureCount { get; set; }
}

/// <summary>
/// 存储趋势点
/// </summary>
public class StorageTrendPoint
{
    public DateTimeOffset Timestamp { get; set; }
    public long TotalSizeBytes { get; set; }
    public int FileCount { get; set; }

    public double TotalSizeMB => TotalSizeBytes / (1024.0 * 1024.0);
}

/// <summary>
/// 汇总统计
/// </summary>
public class SummaryStatistics
{
    public long TotalCaptures { get; set; }
    public long TotalStorageBytes { get; set; }
    public long TotalOcrAttempts { get; set; }
    public long TotalOcrSuccess { get; set; }
    public DateTime? FirstCaptureDate { get; set; }

    public double TotalStorageGB => TotalStorageBytes / (1024.0 * 1024.0 * 1024.0);

    public double OcrSuccessRate => TotalOcrAttempts > 0
        ? (double)TotalOcrSuccess / TotalOcrAttempts * 100
        : 0;

    public int DaysSinceFirstCapture => FirstCaptureDate.HasValue
        ? (DateTime.Now - FirstCaptureDate.Value).Days
        : 0;

    public double AvgCapturesPerDay => DaysSinceFirstCapture > 0
        ? (double)TotalCaptures / DaysSinceFirstCapture
        : TotalCaptures;
}