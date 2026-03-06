using Screen2MD.Kernel.Interfaces;
using System.Diagnostics;

namespace Screen2MD.Web.Services;

/// <summary>
/// 下载服务 - 版本管理和文件分发
/// </summary>
public class DownloadService : IKernelComponent
{
    private readonly string _downloadDirectory;
    private readonly ILogger? _logger;

    public string Name => nameof(DownloadService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    // 版本信息
    public static readonly List<VersionInfo> Versions = new()
    {
        new VersionInfo
        {
            Version = "v2.0.0",
            ReleaseDate = new DateTime(2026, 3, 6),
            IsLatest = true,
            IsStable = true,
            Description = "正式发布版本，完整功能",
            DownloadUrl = "/downloads/Screen2MD-v2.0.0.exe",
            FileSize = 15_000_000, // 15MB
            Changelog = new[]
            {
                "✅ 完整采集功能",
                "✅ Web 管理控制台",
                "✅ 系统托盘集成",
                "✅ 隐私过滤",
                "✅ 智能调度"
            }
        },
        new VersionInfo
        {
            Version = "v1.5.0",
            ReleaseDate = new DateTime(2026, 2, 20),
            IsLatest = false,
            IsStable = true,
            Description = "界面优化版本",
            DownloadUrl = "/downloads/Screen2MD-v1.5.0.exe",
            FileSize = 12_000_000,
            Changelog = new[]
            {
                "界面优化",
                "性能提升"
            }
        },
        new VersionInfo
        {
            Version = "v1.0.0",
            ReleaseDate = new DateTime(2026, 2, 1),
            IsLatest = false,
            IsStable = true,
            Description = "首个稳定版本",
            DownloadUrl = "/downloads/Screen2MD-v1.0.0.exe",
            FileSize = 10_000_000,
            Changelog = new[]
            {
                "基础功能",
                "内核稳定"
            }
        }
    };

    public DownloadService(ILogger? logger = null)
    {
        _logger = logger;
        _downloadDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "wwwroot", "downloads");
        
        Directory.CreateDirectory(_downloadDirectory);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // 无需要释放的资源
    }

    /// <summary>
    /// 获取所有版本
    /// </summary>
    public IReadOnlyList<VersionInfo> GetAllVersions()
    {
        return Versions.OrderByDescending(v => v.ReleaseDate).ToList();
    }

    /// <summary>
    /// 获取最新版本
    /// </summary>
    public VersionInfo? GetLatestVersion()
    {
        return Versions.FirstOrDefault(v => v.IsLatest);
    }

    /// <summary>
    /// 获取文件路径
    /// </summary>
    public string? GetFilePath(string version)
    {
        var fileName = $"Screen2MD-{version}.exe";
        var filePath = Path.Combine(_downloadDirectory, fileName);
        
        return File.Exists(filePath) ? filePath : null;
    }

    /// <summary>
    /// 创建模拟下载文件（用于演示）
    /// </summary>
    public async Task CreateDummyDownloadsAsync()
    {
        foreach (var version in Versions)
        {
            var fileName = Path.GetFileName(version.DownloadUrl);
            var filePath = Path.Combine(_downloadDirectory, fileName);
            
            if (!File.Exists(filePath))
            {
                // 创建模拟文件（实际项目中这是真实安装包）
                var content = $"Screen2MD {version.Version} Installer\nRelease Date: {version.ReleaseDate:yyyy-MM-dd}\n";
                await File.WriteAllTextAsync(filePath, content);
            }
        }
    }
}

/// <summary>
/// 版本信息
/// </summary>
public record VersionInfo
{
    public string Version { get; init; } = string.Empty;
    public DateTime ReleaseDate { get; init; }
    public bool IsLatest { get; init; }
    public bool IsStable { get; init; }
    public string Description { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string[] Changelog { get; init; } = Array.Empty<string>();
}
