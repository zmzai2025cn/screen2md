using Screen2MD.Kernel.Interfaces;
using Screen2MD.Services.Configuration;
using Screen2MD.Services.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Screen2MD 服务注册扩展
/// </summary>
public static class Screen2MDServiceExtensions
{
    /// <summary>
    /// 添加 Screen2MD 配置管理器
    /// </summary>
    public static IServiceCollection AddScreen2MDConfiguration(
        this IServiceCollection services, string? configPath = null)
    {
        services.AddSingleton<IConfigurationManager>(sp =>
        {
            var config = new JsonConfigurationManager(configPath, logger: null);
            config.InitializeAsync().Wait();
            return config;
        });

        return services;
    }

    /// <summary>
    /// 添加全文搜索服务（使用 Lucene.NET，跨平台，零外部依赖）
    /// </summary>
    public static IServiceCollection AddFullTextSearch(
        this IServiceCollection services, string? indexPath = null)
    {
        services.AddSingleton<IFullTextSearchService>(sp =>
        {
            // 使用 Lucene.NET 替代 SQLite FTS5
            // 优势：纯.NET、跨平台、无需外部扩展、性能更好
            var search = new LuceneSearchService(indexPath, logger: null);
            search.InitializeAsync().Wait();
            return search;
        });

        return services;
    }

    /// <summary>
    /// 添加截图索引服务（自动索引截图到全文搜索）
    /// </summary>
    public static IServiceCollection AddCaptureIndexing(
        this IServiceCollection services)
    {
        services.AddSingleton<CaptureIndexingService>(sp =>
        {
            var searchService = sp.GetRequiredService<IFullTextSearchService>();
            var eventBus = sp.GetRequiredService<IEventBus>();
            
            var indexing = new CaptureIndexingService(searchService, eventBus, logger: null);
            indexing.InitializeAsync().Wait();
            return indexing;
        });

        return services;
    }

    /// <summary>
    /// 添加日志管理器
    /// </summary>
    public static IServiceCollection AddLogManager(
        this IServiceCollection services, string? logDirectory = null)
    {
        services.AddSingleton<ILogManager>(sp =>
        {
            var logManager = new LogManager(logDirectory);
            logManager.InitializeAsync().Wait();
            return logManager;
        });

        return services;
    }

    /// <summary>
    /// 添加自动清理服务
    /// </summary>
    public static IServiceCollection AddAutoCleanup(
        this IServiceCollection services)
    {
        services.AddSingleton<AutoCleanupService>(sp =>
        {
            var configManager = sp.GetRequiredService<IConfigurationManager>();
            var searchService = sp.GetService<IFullTextSearchService>();
            
            var cleanup = new AutoCleanupService(configManager, searchService, logger: null);
            cleanup.InitializeAsync().Wait();
            return cleanup;
        });

        return services;
    }

    /// <summary>
    /// 添加统计服务
    /// </summary>
    public static IServiceCollection AddStatistics(
        this IServiceCollection services, string? dbPath = null)
    {
        services.AddSingleton<StatisticsService>(sp =>
        {
            var configManager = sp.GetRequiredService<IConfigurationManager>();
            var stats = new StatisticsService(configManager, dbPath, logger: null);
            stats.InitializeAsync().Wait();
            return stats;
        });

        return services;
    }

    /// <summary>
    /// 获取类型安全的配置
    /// </summary>
    public static Screen2MDConfig GetScreen2MDConfig(this IServiceProvider provider)
    {
        var configManager = provider.GetRequiredService<IConfigurationManager>();
        
        return new Screen2MDConfig
        {
            Capture = new CaptureConfig
            {
                IntervalSeconds = configManager.GetValue("capture.intervalSeconds", 30),
                OutputDirectory = configManager.GetValue("capture.outputDirectory", ""),
                Formats = configManager.GetValue("capture.formats", new[] { "BMP" }),
                EnableOCR = configManager.GetValue("capture.enableOcr", true),
                SimilarityThreshold = configManager.GetValue("capture.similarityThreshold", 0.9),
                ManualMode = configManager.GetValue("capture.manualMode", false)
            },
            OCR = new OCRConfig
            {
                TesseractPath = configManager.GetValue("ocr.tesseractPath", ""),
                Languages = configManager.GetValue("ocr.languages", new[] { "chi_sim", "eng" }),
                TimeoutSeconds = configManager.GetValue("ocr.timeoutSeconds", 30),
                Parallelism = configManager.GetValue("ocr.parallelism", 2),
                DPI = configManager.GetValue("ocr.dpi", 300),
                PSM = configManager.GetValue("ocr.psm", 3)
            },
            Storage = new StorageConfig
            {
                MaxStorageGB = configManager.GetValue("storage.maxStorageGB", 10),
                AutoCleanupDays = configManager.GetValue("storage.autoCleanupDays", 30),
                CleanupEnabled = configManager.GetValue("storage.cleanupEnabled", true),
                CleanupStrategy = configManager.GetValue("storage.cleanupStrategy", "Days"),
                KeepCount = configManager.GetValue("storage.keepCount", 1000)
            },
            Display = new DisplayConfig
            {
                CaptureAllDisplays = configManager.GetValue("display.captureAllDisplays", true),
                PrimaryDisplayOnly = configManager.GetValue("display.primaryDisplayOnly", false)
            },
            Privacy = new PrivacyConfig
            {
                EnablePrivacyFilter = configManager.GetValue("privacy.enablePrivacyFilter", false),
                BlurPasswordFields = configManager.GetValue("privacy.blurPasswordFields", false)
            },
            Log = new LogConfig
            {
                Level = configManager.GetValue("log.level", "Information"),
                MaxFileSizeMB = configManager.GetValue("log.maxFileSizeMB", 100),
                MaxFiles = configManager.GetValue("log.maxFiles", 10),
                OutputToConsole = configManager.GetValue("log.outputToConsole", true),
                OutputToFile = configManager.GetValue("log.outputToFile", true)
            }
        };
    }
}