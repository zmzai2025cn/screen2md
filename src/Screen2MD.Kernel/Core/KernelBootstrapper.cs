using Screen2MD.Kernel.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Screen2MD.Kernel.Core;

/// <summary>
/// 内核引导器 - 零崩溃设计的核心
/// </summary>
public sealed class KernelBootstrapper : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IKernelComponent> _components = [];
    private readonly IKernelLogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isInitialized;
    private bool _isStarted;

    public KernelBootstrapper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = serviceProvider.GetService(typeof(ILogManager)) is ILogManager logMgr
            ? logMgr.GetLogger(nameof(KernelBootstrapper))
            : new FallbackLogger();
    }

    /// <summary>
    /// 内核启动入口 - 确保零崩溃
    /// </summary>
    public async Task<KernelStartupResult> StartAsync(CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token);
        var linkedToken = linkedCts.Token;

        try
        {
            _logger.LogInformation("Starting kernel bootstrap sequence...");

            // 1. 设置全局异常处理（最先执行）
            SetupGlobalExceptionHandling();

            // 2. 验证系统环境
            await ValidateSystemEnvironmentAsync(linkedToken);

            // 3. 按优先级排序组件
            var components = GetComponentsInPriorityOrder();

            // 4. 初始化组件
            await InitializeComponentsAsync(components, linkedToken);

            // 5. 启动组件
            await StartComponentsAsync(components, linkedToken);

            _isStarted = true;
            _logger.LogInformation("Kernel started successfully. Zero-crash protection active.");

            return KernelStartupResult.CreateSuccess();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Kernel startup cancelled by request.");
            return KernelStartupResult.CreateCancelled();
        }
        catch (Exception ex)
        {
            // 内核启动失败是致命错误，但我们仍然优雅处理
            await HandleFatalStartupErrorAsync(ex);
            return KernelStartupResult.CreateFailed(ex);
        }
    }

    /// <summary>
    /// 设置全局异常处理 - 零崩溃保障第一层
    /// </summary>
    private void SetupGlobalExceptionHandling()
    {
        // 捕获未处理异常
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            _logger.LogFatal($"Unhandled domain exception: {ex?.Message}", ex);
            
            // 记录崩溃报告
            CrashReporter.GenerateReport(ex, "AppDomain.UnhandledException");
            
            // 尝试恢复
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogInformation("Attempting emergency recovery...");
                _ = AttemptEmergencyRecoveryAsync();
            }
            
            // 如果不可恢复，优雅退出
            if (e.IsTerminating)
            {
                _logger.LogFatal("Process is terminating due to unhandled exception.");
                // 确保日志写入
                ( _logger as IDisposable)?.Dispose();
            }
        };

        // 捕获未观察到的任务异常
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            _logger.LogError($"Unobserved task exception: {e.Exception.Message}", e.Exception);
            e.SetObserved(); // 防止进程终止
            
            // 记录但不崩溃
            CrashReporter.GenerateReport(e.Exception, "TaskScheduler.UnobservedTaskException");
        };

        // 捕获FirstChance异常（用于诊断）
        AppDomain.CurrentDomain.FirstChanceException += (sender, e) =>
        {
            // 仅在调试模式下记录
            if (Debugger.IsAttached)
            {
                Debug.WriteLine($"FirstChanceException: {e.Exception.Message}");
            }
        };

        _logger.LogInformation("Global exception handling configured.");
    }

    /// <summary>
    /// 验证系统环境
    /// </summary>
    private async Task ValidateSystemEnvironmentAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating system environment...");

        // 检查操作系统
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Kernel is designed for Windows. Running on non-Windows platform may cause compatibility issues.");
        }

        // 检查.NET版本
        var dotnetVersion = Environment.Version;
        if (dotnetVersion.Major < 8)
        {
            throw new InvalidOperationException($"Requires .NET 8.0 or higher. Current: {dotnetVersion}");
        }

        // 检查可用内存
        var availableMemory = GC.GetTotalMemory(false);
        if (availableMemory < 50 * 1024 * 1024) // 50MB
        {
            _logger.LogWarning("Low memory detected. Performance may be degraded.");
        }

        // 检查磁盘空间
        var driveInfo = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
        if (driveInfo.AvailableFreeSpace < 100 * 1024 * 1024) // 100MB
        {
            _logger.LogWarning("Low disk space detected. Log rotation may be affected.");
        }

        await Task.CompletedTask;
        _logger.LogInformation("System environment validation completed.");
    }

    /// <summary>
    /// 获取按优先级排序的组件
    /// </summary>
    private IEnumerable<IKernelComponent> GetComponentsInPriorityOrder()
    {
        // 从DI容器获取所有内核组件
        var components = _serviceProvider.GetServices<IKernelComponent>()
            ?? Enumerable.Empty<IKernelComponent>();

        // 按依赖优先级排序
        var priorityMap = new Dictionary<Type, int>
        {
            { typeof(ILogManager), 1 },           // 日志最先
            { typeof(IConfigurationManager), 2 }, // 配置次之
            { typeof(IEventBus), 3 },             // 事件总线
            { typeof(IResourceMonitor), 4 },      // 资源监控
            { typeof(IPluginHost), 5 },           // 插件系统最后
        };

        return components.OrderBy(c =>
        {
            var type = c.GetType();
            foreach (var (iface, priority) in priorityMap)
            {
                if (iface.IsAssignableFrom(type))
                    return priority;
            }
            return 100; // 默认优先级
        });
    }

    /// <summary>
    /// 初始化组件
    /// </summary>
    private async Task InitializeComponentsAsync(
        IEnumerable<IKernelComponent> components, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing kernel components...");

        foreach (var component in components)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation($"Initializing {component.Name}...");
                await component.InitializeAsync(cancellationToken);
                _components.Add(component);
                _logger.LogInformation($"{component.Name} initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize {component.Name}: {ex.Message}", ex);
                
                // 根据组件重要性决定是否继续
                if (IsCriticalComponent(component))
                {
                    throw new KernelInitializationException(
                        $"Critical component {component.Name} failed to initialize", ex);
                }
                
                _logger.LogWarning($"Non-critical component {component.Name} initialization failed. Continuing...");
            }
        }

        _isInitialized = true;
        _logger.LogInformation($"Initialized {_components.Count} components.");
    }

    /// <summary>
    /// 启动组件
    /// </summary>
    private async Task StartComponentsAsync(
        IEnumerable<IKernelComponent> components, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting kernel components...");

        foreach (var component in _components)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation($"Starting {component.Name}...");
                await component.StartAsync(cancellationToken);
                _logger.LogInformation($"{component.Name} started successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to start {component.Name}: {ex.Message}", ex);
                
                if (IsCriticalComponent(component))
                {
                    throw new KernelStartupException(
                        $"Critical component {component.Name} failed to start", ex);
                }
            }
        }

        _logger.LogInformation("All components started.");
    }

    /// <summary>
    /// 判断是否关键组件
    /// </summary>
    private static bool IsCriticalComponent(IKernelComponent component)
    {
        // 日志和配置是关键组件
        return component is ILogManager or IConfigurationManager;
    }

    /// <summary>
    /// 处理致命启动错误
    /// </summary>
    private async Task HandleFatalStartupErrorAsync(Exception ex)
    {
        _logger.LogFatal($"Kernel startup failed: {ex.Message}", ex);
        
        // 生成崩溃报告
        CrashReporter.GenerateReport(ex, "KernelStartup");
        
        // 尝试优雅关闭已初始化的组件
        await ShutdownAsync();
        
        // 通知外部（如果有配置）
        // 这里可以集成告警系统
    }

    /// <summary>
    /// 尝试紧急恢复
    /// </summary>
    private async Task AttemptEmergencyRecoveryAsync()
    {
        try
        {
            _logger.LogInformation("Emergency recovery initiated...");
            
            // 1. 重启关键组件
            foreach (var component in _components.Where(IsCriticalComponent))
            {
                try
                {
                    await component.StopAsync(CancellationToken.None);
                    await component.StartAsync(CancellationToken.None);
                    _logger.LogInformation($"Recovered {component.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to recover {component.Name}: {ex.Message}");
                }
            }
            
            _logger.LogInformation("Emergency recovery completed.");
        }
        catch (Exception ex)
        {
            _logger.LogFatal($"Emergency recovery failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 优雅关闭内核
    /// </summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (!_isStarted && !_isInitialized)
            return;

        _logger.LogInformation("Shutting down kernel...");
        _cancellationTokenSource.Cancel();

        // 反向停止组件
        foreach (var component in _components.AsEnumerable().Reverse())
        {
            try
            {
                _logger.LogInformation($"Stopping {component.Name}...");
                await component.StopAsync(cancellationToken);
                component.Dispose();
                _logger.LogInformation($"{component.Name} stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping {component.Name}: {ex.Message}", ex);
            }
        }

        _logger.LogInformation("Kernel shutdown completed.");
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        
        foreach (var component in _components)
        {
            try
            {
                component?.Dispose();
            }
            catch
            {
                // 忽略处置错误
            }
        }
    }
}

/// <summary>
/// 内核启动结果
/// </summary>
public record KernelStartupResult
{
    public bool Success { get; init; }
    public bool Cancelled { get; init; }
    public Exception? Error { get; init; }
    public string? ErrorMessage { get; init; }

    public static KernelStartupResult CreateSuccess() => new() { Success = true };
    public static KernelStartupResult CreateCancelled() => new() { Cancelled = true };
    public static KernelStartupResult CreateFailed(Exception error) => 
        new() { Success = false, Error = error, ErrorMessage = error.Message };
}

/// <summary>
/// 内核初始化异常
/// </summary>
public class KernelInitializationException : Exception
{
    public KernelInitializationException(string message) : base(message) { }
    public KernelInitializationException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// 内核启动异常
/// </summary>
public class KernelStartupException : Exception
{
    public KernelStartupException(string message) : base(message) { }
    public KernelStartupException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// 崩溃报告生成器
/// </summary>
internal static class CrashReporter
{
    public static void GenerateReport(Exception? ex, string source)
    {
        try
        {
            var reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Screen2MD",
                "CrashReports",
                $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.json");

            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

            var report = new
            {
                Timestamp = DateTime.UtcNow,
                Source = source,
                Exception = ex?.ToString(),
                ExceptionType = ex?.GetType().FullName,
                StackTrace = ex?.StackTrace,
                Environment = new
                {
                    OS = Environment.OSVersion.ToString(),
                    DotNetVersion = Environment.Version.ToString(),
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet
                }
            };

            File.WriteAllText(reportPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            }));
        }
        catch
        {
            // 崩溃报告生成失败，静默处理
        }
    }
}

/// <summary>
/// 备用日志记录器（当正式日志系统未就绪时使用）
/// </summary>
internal class FallbackLogger : IKernelLogger, IDisposable
{
    public void LogTrace(string message, Exception? exception = null) => 
        Console.WriteLine($"[TRACE] {message}");
    
    public void LogDebug(string message, Exception? exception = null) => 
        Console.WriteLine($"[DEBUG] {message}");
    
    public void LogInformation(string message, Exception? exception = null) => 
        Console.WriteLine($"[INFO] {message}");
    
    public void LogWarning(string message, Exception? exception = null) => 
        Console.WriteLine($"[WARN] {message}");
    
    public void LogError(string message, Exception? exception = null) => 
        Console.WriteLine($"[ERROR] {message}");
    
    public void LogFatal(string message, Exception? exception = null) => 
        Console.WriteLine($"[FATAL] {message}");
    
    public void Dispose() { }
}