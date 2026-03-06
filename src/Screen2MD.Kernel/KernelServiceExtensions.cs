using Screen2MD.Kernel.Core;
using Screen2MD.Kernel.Interfaces;
using Screen2MD.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Screen2MD.Kernel;

/// <summary>
/// 内核服务注册扩展
/// </summary>
public static class KernelServiceExtensions
{
    /// <summary>
    /// 添加Screen2MD内核服务
    /// </summary>
    public static IServiceCollection AddScreen2MDKernel(
        this IServiceCollection services,
        KernelOptions? options = null)
    {
        options ??= new KernelOptions();

        // 配置管理器（单例，最早初始化）
        services.AddSingleton<IConfigurationManager>(sp =>
        {
            var configPath = options.ConfigurationPath ?? 
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Screen2MD",
                    "config.json");
            return new ConfigurationManager(configPath);
        });

        // 日志管理器（单例）
        services.AddSingleton<ILogManager>(sp =>
        {
            var config = sp.GetRequiredService<IConfigurationManager>();
            var logConfig = new LogConfiguration
            {
                LogDirectory = options.LogDirectory,
                MinimumLevel = options.MinimumLogLevel,
                EnableConsoleLogging = options.EnableConsoleLogging,
                EnableFileLogging = true,
                MaxFileSizeMB = options.MaxLogFileSizeMB,
                MaxFileCount = options.MaxLogFileCount,
                MemoryBufferSize = options.LogMemoryBufferSize
            };
            return new LogManager(logConfig);
        });

        // 事件总线（单例）
        services.AddSingleton<EventBus>(sp =>
        {
            var logManager = sp.GetService<ILogManager>();
            return new EventBus(logManager);
        });
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBus>());

        // 资源监控器（单例）
        services.AddSingleton<IResourceMonitor>(sp =>
        {
            var config = sp.GetRequiredService<IConfigurationManager>();
            return new ResourceMonitor(config);
        });

        // 插件主机（单例，延迟初始化）
        services.AddSingleton<IPluginHost>(sp =>
        {
            var logManager = sp.GetRequiredService<ILogManager>();
            var eventBus = sp.GetRequiredService<IEventBus>();
            var config = sp.GetRequiredService<IConfigurationManager>();
            return new PluginHost(sp, eventBus, logManager, config);
        });

        // 内核引导器（作用域）
        services.AddScoped<KernelBootstrapper>();

        // 注册所有内核组件为IKernelComponent
        services.AddSingleton<IKernelComponent>(sp => sp.GetRequiredService<IConfigurationManager>());
        services.AddSingleton<IKernelComponent>(sp => sp.GetRequiredService<ILogManager>());
        services.AddSingleton<IKernelComponent>(sp => sp.GetRequiredService<EventBus>());
        services.AddSingleton<IKernelComponent>(sp => sp.GetRequiredService<IResourceMonitor>());
        services.AddSingleton<IKernelComponent>(sp => sp.GetRequiredService<IPluginHost>());

        return services;
    }

    /// <summary>
    /// 使用Screen2MD内核
    /// </summary>
    public static async Task<KernelStartupResult> UseScreen2MDKernelAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
        return await bootstrapper.StartAsync(cancellationToken);
    }
}

/// <summary>
/// 内核选项
/// </summary>
public class KernelOptions
{
    /// <summary>
    /// 配置文件路径
    /// </summary>
    public string? ConfigurationPath { get; set; }

    /// <summary>
    /// 日志目录
    /// </summary>
    public string? LogDirectory { get; set; }

    /// <summary>
    /// 最小日志级别
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 启用控制台日志
    /// </summary>
    public bool EnableConsoleLogging { get; set; }

    /// <summary>
    /// 最大日志文件大小(MB)
    /// </summary>
    public int MaxLogFileSizeMB { get; set; } = 10;

    /// <summary>
    /// 最大日志文件数量
    /// </summary>
    public int MaxLogFileCount { get; set; } = 10;

    /// <summary>
    /// 日志内存缓冲区大小
    /// </summary>
    public int LogMemoryBufferSize { get; set; } = 10000;
}