using Screen2MD.Kernel.Interfaces;
using IKernelLogger = Screen2MD.Services.IKernelLogger;

namespace Screen2MD.Services.Services;

/// <summary>
/// 自动清理服务 - 管理截图文件和索引的存储空间
/// </summary>
public sealed class AutoCleanupService : IKernelComponent, IDisposable
{
    private readonly string _capturesDirectory;
    private readonly string _searchDbPath;
    private readonly IConfigurationManager _configManager;
    private readonly IFullTextSearchService? _searchService;
    private readonly IKernelLogger? _logger;
    private Timer? _cleanupTimer;
    private bool _disposed;

    public string Name => nameof(AutoCleanupService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public AutoCleanupService(
        IConfigurationManager configManager,
        IFullTextSearchService? searchService = null,
        IKernelLogger? logger = null)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _searchService = searchService;
        _logger = logger;

        _capturesDirectory = _configManager.GetValue(
            "capture.outputDirectory",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Screen2MD", "Captures"));

        _searchDbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "search.db");
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            HealthStatus = HealthStatus.Healthy;
            _logger?.LogInformation("AutoCleanupService initialized");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            HealthStatus = HealthStatus.Unhealthy;
            _logger?.LogError($"Failed to initialize AutoCleanupService: {ex.Message}");
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // 每小时检查一次
        _cleanupTimer = new Timer(
            async _ => await CleanupAsync(),
            null,
            TimeSpan.FromMinutes(1),  // 首次运行延迟 1 分钟
            TimeSpan.FromHours(1));    // 之后每小时

        _logger?.LogInformation("AutoCleanupService started (check interval: 1 hour)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cleanupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _logger?.LogInformation("AutoCleanupService stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 执行清理
    /// </summary>
    public async Task<CleanupResult> CleanupAsync()
    {
        var result = new CleanupResult();

        try
        {
            _logger?.LogInformation("Starting auto cleanup...");

            // 获取配置
            var maxStorageGB = _configManager.GetValue("storage.maxStorageGB", 10);
            var cleanupDays = _configManager.GetValue("storage.autoCleanupDays", 30);
            var cleanupEnabled = _configManager.GetValue("storage.cleanupEnabled", true);
            var cleanupStrategy = _configManager.GetValue("storage.cleanupStrategy", "Days");
            var keepCount = _configManager.GetValue("storage.keepCount", 1000);

            if (!cleanupEnabled)
            {
                _logger?.LogDebug("Auto cleanup is disabled");
                return result;
            }

            // 检查存储空间
            var storageInfo = GetStorageInfo();
            result.StorageSizeBeforeMB = storageInfo.TotalSizeMB;

            if (storageInfo.TotalSizeMB > maxStorageGB * 1024)
            {
                _logger?.LogWarning($"Storage exceeds limit: {storageInfo.TotalSizeMB}MB > {maxStorageGB * 1024}MB");
            }

            // 根据策略执行清理
            if (cleanupStrategy == "Days")
            {
                result += await CleanupByDaysAsync(cleanupDays);
            }
            else if (cleanupStrategy == "Count")
            {
                result += await CleanupByCountAsync(keepCount);
            }

            // 清理后再次检查存储空间
            storageInfo = GetStorageInfo();
            result.StorageSizeAfterMB = storageInfo.TotalSizeMB;
            result.FreedSpaceMB = result.StorageSizeBeforeMB - result.StorageSizeAfterMB;

            _logger?.LogInformation($"Cleanup completed. Freed: {result.FreedSpaceMB}MB, " +
                $"Deleted files: {result.DeletedFiles}, Deleted indices: {result.DeletedIndices}");

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Cleanup failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 按时间清理
    /// </summary>
    private async Task<CleanupResult> CleanupByDaysAsync(int days)
    {
        var result = new CleanupResult();
        var cutoffDate = DateTime.Now.AddDays(-days);

        // 清理截图文件
        if (Directory.Exists(_capturesDirectory))
        {
            var directories = Directory.GetDirectories(_capturesDirectory);
            foreach (var dir in directories)
            {
                var dirInfo = new DirectoryInfo(dir);
                if (dirInfo.LastWriteTime < cutoffDate)
                {
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    result.DeletedFiles += files.Length;
                    result.FreedSpaceMB += files.Sum(f => new FileInfo(f).Length) / (1024.0 * 1024.0);

                    Directory.Delete(dir, recursive: true);
                    _logger?.LogDebug($"Deleted old directory: {dir}");
                }
            }
        }

        // 清理索引
        if (_searchService != null)
        {
            // TODO: 实现索引清理
            // await _searchService.DeleteOldIndexesAsync(cutoffTimestamp);
            result.DeletedIndices = 0; // 占位
        }

        return result;
    }

    /// <summary>
    /// 按数量清理（保留最新的 N 个）
    /// </summary>
    private async Task<CleanupResult> CleanupByCountAsync(int keepCount)
    {
        var result = new CleanupResult();

        // 清理截图文件
        if (Directory.Exists(_capturesDirectory))
        {
            var allFiles = Directory.GetFiles(_capturesDirectory, "*.*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (allFiles.Count > keepCount)
            {
                var filesToDelete = allFiles.Skip(keepCount).ToList();
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        file.Delete();
                        result.DeletedFiles++;
                        result.FreedSpaceMB += file.Length / (1024.0 * 1024.0);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"Failed to delete file {file.FullName}: {ex.Message}");
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 获取存储信息
    /// </summary>
    public StorageInfo GetStorageInfo()
    {
        var info = new StorageInfo();

        if (Directory.Exists(_capturesDirectory))
        {
            var files = Directory.GetFiles(_capturesDirectory, "*.*", SearchOption.AllDirectories);
            info.TotalFiles = files.Length;
            info.TotalSizeMB = files.Sum(f => new FileInfo(f).Length) / (1024.0 * 1024.0);
        }

        if (File.Exists(_searchDbPath))
        {
            var dbInfo = new FileInfo(_searchDbPath);
            info.DatabaseSizeMB = dbInfo.Length / (1024.0 * 1024.0);
        }

        info.LastCleanupTime = _configManager.GetValue<DateTime?>("storage.lastCleanupTime", null);

        return info;
    }

    /// <summary>
    /// 获取旧索引数量（占位实现）
    /// </summary>
    private async Task<long> GetOldIndexCountAsync(DateTimeOffset cutoff)
    {
        // 实际实现需要查询数据库
        return 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// 清理结果
/// </summary>
public class CleanupResult
{
    public double StorageSizeBeforeMB { get; set; }
    public double StorageSizeAfterMB { get; set; }
    public double FreedSpaceMB { get; set; }
    public int DeletedFiles { get; set; }
    public int DeletedIndices { get; set; }

    public static CleanupResult operator +(CleanupResult a, CleanupResult b)
    {
        return new CleanupResult
        {
            StorageSizeBeforeMB = a.StorageSizeBeforeMB + b.StorageSizeBeforeMB,
            StorageSizeAfterMB = a.StorageSizeAfterMB + b.StorageSizeAfterMB,
            FreedSpaceMB = a.FreedSpaceMB + b.FreedSpaceMB,
            DeletedFiles = a.DeletedFiles + b.DeletedFiles,
            DeletedIndices = a.DeletedIndices + b.DeletedIndices
        };
    }
}

/// <summary>
/// 存储信息
/// </summary>
public class StorageInfo
{
    public int TotalFiles { get; set; }
    public double TotalSizeMB { get; set; }
    public double DatabaseSizeMB { get; set; }
    public DateTime? LastCleanupTime { get; set; }

    public double TotalStorageMB => TotalSizeMB + DatabaseSizeMB;
}