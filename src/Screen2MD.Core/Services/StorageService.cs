using Microsoft.Extensions.Logging;

namespace Screen2MD.Core.Services;

/// <summary>
/// 存储服务 - 管理截图文件
/// </summary>
public sealed class StorageService
{
    private readonly string _baseDirectory;
    private readonly long _maxStorageBytes;
    private readonly int _cleanupDays;
    private readonly bool _autoCleanup;
    private readonly ILogger<StorageService>? _logger;

    public StorageService(
        string? baseDirectory = null,
        long maxStorageGB = 10,
        int cleanupDays = 30,
        bool autoCleanup = true,
        ILogger<StorageService>? logger = null)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "Captures");
        _maxStorageBytes = maxStorageGB * 1024 * 1024 * 1024;
        _cleanupDays = cleanupDays;
        _autoCleanup = autoCleanup;
        _logger = logger;
        
        Directory.CreateDirectory(_baseDirectory);
    }

    /// <summary>
    /// 获取存储统计
    /// </summary>
    public StorageStats GetStats()
    {
        if (!Directory.Exists(_baseDirectory))
            return new StorageStats();

        var files = Directory.GetFiles(_baseDirectory, "*", SearchOption.AllDirectories);
        var totalSize = files.Sum(f => new FileInfo(f).Length);

        return new StorageStats
        {
            TotalFiles = files.Length,
            TotalSizeBytes = totalSize,
            TotalSizeGB = totalSize / (1024.0 * 1024 * 1024),
            DirectoryPath = _baseDirectory
        };
    }

    /// <summary>
    /// 清理旧文件
    /// </summary>
    public async Task<CleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var result = new CleanupResult();
        
        if (!Directory.Exists(_baseDirectory))
            return result;

        var cutoffDate = DateTime.Now.AddDays(-_cleanupDays);
        var directories = Directory.GetDirectories(_baseDirectory);

        foreach (var dir in directories)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var dirInfo = new DirectoryInfo(dir);
            if (dirInfo.LastWriteTime < cutoffDate)
            {
                try
                {
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    result.DeletedFiles += files.Length;
                    result.FreedBytes += files.Sum(f => new FileInfo(f).Length);
                    
                    Directory.Delete(dir, recursive: true);
                    _logger?.LogInformation($"Cleaned up directory: {dir}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Failed to cleanup directory: {dir}");
                }
            }
        }

        // 检查存储上限
        var stats = GetStats();
        if (stats.TotalSizeBytes > _maxStorageBytes)
        {
            _logger?.LogWarning($"Storage exceeds limit: {stats.TotalSizeGB:F2}GB > {_maxStorageBytes / (1024.0 * 1024 * 1024):F2}GB");
        }

        return result;
    }

    /// <summary>
    /// 获取最近的文件
    /// </summary>
    public List<FileInfo> GetRecentFiles(int count = 100)
    {
        if (!Directory.Exists(_baseDirectory))
            return new List<FileInfo>();

        return Directory.GetFiles(_baseDirectory, "*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Take(count)
            .ToList();
    }
}

/// <summary>
/// 存储统计
/// </summary>
public class StorageStats
{
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public double TotalSizeGB { get; set; }
    public string DirectoryPath { get; set; } = "";
}

/// <summary>
/// 清理结果
/// </summary>
public class CleanupResult
{
    public int DeletedFiles { get; set; }
    public long FreedBytes { get; set; }
    public double FreedGB => FreedBytes / (1024.0 * 1024 * 1024);
}