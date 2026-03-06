using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Kernel.Core;

/// <summary>
/// 内核事件基类
/// </summary>
public abstract record KernelEventBase : IKernelEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public abstract string EventType { get; }
    public string Source { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
}

/// <summary>
/// 应用程序启动事件
/// </summary>
public record ApplicationStartedEvent : KernelEventBase
{
    public override string EventType => nameof(ApplicationStartedEvent);
    public DateTimeOffset StartTime { get; init; }
    public string Version { get; init; } = string.Empty;
}

/// <summary>
/// 应用程序停止事件
/// </summary>
public record ApplicationStoppedEvent : KernelEventBase
{
    public override string EventType => nameof(ApplicationStoppedEvent);
    public DateTimeOffset StopTime { get; init; }
    public TimeSpan Uptime { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// 配置变更事件
/// </summary>
public record ConfigurationChangedEvent : KernelEventBase
{
    public override string EventType => nameof(ConfigurationChangedEvent);
    public string Key { get; init; } = string.Empty;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
}

/// <summary>
/// 组件健康状态变更事件
/// </summary>
public record ComponentHealthChangedEvent : KernelEventBase
{
    public override string EventType => nameof(ComponentHealthChangedEvent);
    public string ComponentName { get; init; } = string.Empty;
    public HealthStatus OldStatus { get; init; }
    public HealthStatus NewStatus { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// 资源告警事件
/// </summary>
public record ResourceAlertEvent : KernelEventBase
{
    public override string EventType => nameof(ResourceAlertEvent);
    public ResourceType ResourceType { get; init; }
    public double CurrentValue { get; init; }
    public double Threshold { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// 错误事件
/// </summary>
public record ErrorEvent : KernelEventBase
{
    public override string EventType => nameof(ErrorEvent);
    public string ErrorType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? StackTrace { get; init; }
    public bool IsFatal { get; init; }
}