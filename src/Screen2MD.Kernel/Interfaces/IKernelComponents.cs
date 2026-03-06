namespace Screen2MD.Kernel.Interfaces;

/// <summary>
/// 内核组件生命周期接口
/// </summary>
public interface IKernelComponent : IDisposable
{
    /// <summary>
    /// 组件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 初始化组件
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化任务</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动组件
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>启动任务</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止组件
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停止任务</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 组件健康状态
    /// </summary>
    HealthStatus HealthStatus { get; }
}

/// <summary>
/// 组件健康状态
/// </summary>
public enum HealthStatus
{
    Unknown,
    Initializing,
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// 内核事件接口
/// </summary>
public interface IKernelEvent
{
    /// <summary>
    /// 事件ID
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// 事件时间戳
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// 事件类型
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// 事件来源组件
    /// </summary>
    string Source { get; }

    /// <summary>
    /// 关联ID（用于追踪）
    /// </summary>
    string? CorrelationId { get; }
}

/// <summary>
/// 事件处理器接口
/// </summary>
/// <typedef name="TEvent">事件类型</typedef>
public interface IEventHandler<in TEvent> where TEvent : IKernelEvent
{
    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="event">事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理任务</returns>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// 事件总线接口
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发布任务</returns>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IKernelEvent;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">处理器</param>
    /// <returns>订阅对象（用于取消订阅）</returns>
    IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IKernelEvent;
}

/// <summary>
/// 配置管理器接口
/// </summary>
public interface IConfigurationManager : IKernelComponent
{
    /// <summary>
    /// 获取配置值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>配置值</returns>
    T? GetValue<T>(string key, T? defaultValue = default);

    /// <summary>
    /// 设置配置值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">配置键</param>
    /// <param name="value">值</param>
    void SetValue<T>(string key, T value);

    /// <summary>
    /// 配置变更事件
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// 热重载配置
    /// </summary>
    /// <returns>重载任务</returns>
    Task ReloadAsync();
}

/// <summary>
/// 配置变更事件参数
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    public string Key { get; init; } = string.Empty;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// 日志管理器接口
/// </summary>
public interface ILogManager : IKernelComponent
{
    /// <summary>
    /// 获取日志记录器
    /// </summary>
    /// <param name="category">类别</param>
    /// <returns>日志记录器</returns>
    IKernelLogger GetLogger(string category);

    /// <summary>
    /// 获取最近的日志条目
    /// </summary>
    /// <param name="count">数量</param>
    /// <returns>日志条目</returns>
    IEnumerable<LogEntry> GetRecentLogs(int count = 100);

    /// <summary>
    /// 按级别获取日志
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="since">起始时间</param>
    /// <returns>日志条目</returns>
    IEnumerable<LogEntry> GetLogsByLevel(LogLevel level, DateTimeOffset? since = null);
}

/// <summary>
/// 内核日志记录器接口
/// </summary>
public interface IKernelLogger
{
    void LogTrace(string message, Exception? exception = null);
    void LogDebug(string message, Exception? exception = null);
    void LogInformation(string message, Exception? exception = null);
    void LogWarning(string message, Exception? exception = null);
    void LogError(string message, Exception? exception = null);
    void LogFatal(string message, Exception? exception = null);
}

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// 日志条目
/// </summary>
public record LogEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public LogLevel Level { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// 资源监控器接口
/// </summary>
public interface IResourceMonitor : IKernelComponent
{
    /// <summary>
    /// 当前CPU使用率 (%)
    /// </summary>
    double CpuUsagePercent { get; }

    /// <summary>
    /// 当前内存使用 (MB)
    /// </summary>
    double MemoryUsageMB { get; }

    /// <summary>
    /// 资源使用报告
    /// </summary>
    ResourceUsageReport GetReport(TimeSpan? period = null);

    /// <summary>
    /// 资源告警事件
    /// </summary>
    event EventHandler<ResourceAlertEventArgs>? ResourceAlert;
}

/// <summary>
/// 资源使用报告
/// </summary>
public record ResourceUsageReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public TimeSpan Period { get; init; }
    public double AvgCpuPercent { get; init; }
    public double MaxCpuPercent { get; init; }
    public double AvgMemoryMB { get; init; }
    public double MaxMemoryMB { get; init; }
}

/// <summary>
/// 资源告警事件参数
/// </summary>
public class ResourceAlertEventArgs : EventArgs
{
    public ResourceType ResourceType { get; init; }
    public double CurrentValue { get; init; }
    public double Threshold { get; init; }
    public DateTimeOffset AlertAt { get; init; }
}

/// <summary>
/// 资源类型
/// </summary>
public enum ResourceType
{
    Cpu,
    Memory,
    Disk,
    Network
}

/// <summary>
/// 插件接口
/// </summary>
public interface IPlugin : IDisposable
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// 初始化插件
    /// </summary>
    /// <param name="context">插件上下文</param>
    void Initialize(IPluginContext context);
}

/// <summary>
/// 插件上下文
/// </summary>
public interface IPluginContext
{
    IServiceProvider Services { get; }
    IConfigurationManager Configuration { get; }
    IEventBus EventBus { get; }
    IKernelLogger Logger { get; }
}

/// <summary>
/// 插件主机接口
/// </summary>
public interface IPluginHost : IKernelComponent
{
    /// <summary>
    /// 加载插件
    /// </summary>
    /// <param name="pluginPath">插件路径</param>
    /// <returns>加载的插件</returns>
    IPlugin LoadPlugin(string pluginPath);

    /// <summary>
    /// 卸载插件
    /// </summary>
    /// <param name="pluginName">插件名称</param>
    void UnloadPlugin(string pluginName);

    /// <summary>
    /// 获取已加载的插件
    /// </summary>
    IEnumerable<IPlugin> LoadedPlugins { get; }
}