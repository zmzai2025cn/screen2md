using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Interfaces;

/// <summary>
/// 采集调度配置
/// </summary>
public record SchedulerConfig
{
    public int MinIntervalMs { get; init; } = 1000;      // 最小采集间隔
    public int MaxIntervalMs { get; init; } = 60000;    // 最大采集间隔
    public int AdaptiveThreshold { get; init; } = 3;    // 自适应阈值（连续变化次数）
    public double CpuThreshold { get; init; } = 1.0;    // CPU阈值，超过则降频
    public double MemoryThresholdMB { get; init; } = 50; // 内存阈值
    public bool EnableAdaptiveSampling { get; init; } = true; // 启用自适应采样
}

/// <summary>
/// 调度状态
/// </summary>
public record SchedulerStatus
{
    public bool IsRunning { get; init; }
    public int CurrentIntervalMs { get; init; }
    public DateTimeOffset LastCaptureAt { get; init; }
    public int CapturesInLastMinute { get; init; }
    public double AverageCpuPercent { get; init; }
    public double AverageMemoryMB { get; init; }
}

/// <summary>
/// 采集调度服务接口 - 自适应采样，CPU<1%
/// 目标: 智能调度，资源最优
/// </summary>
public interface ICaptureScheduler : IKernelComponent
{
    /// <summary>
    /// 配置
    /// </summary>
    SchedulerConfig Config { get; set; }
    
    /// <summary>
    /// 当前状态
    /// </summary>
    SchedulerStatus Status { get; }
    
    /// <summary>
    /// 启动调度器
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止调度器
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 触发一次采集（手动）
    /// </summary>
    Task TriggerCaptureAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 暂停调度
    /// </summary>
    Task PauseAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 恢复调度
    /// </summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新配置
    /// </summary>
    Task UpdateConfigAsync(SchedulerConfig config, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 设置采集回调
    /// </summary>
    void SetCaptureCallback(Func<CancellationToken, Task> callback);
    
    /// <summary>
    /// 获取下次采集时间
    /// </summary>
    DateTimeOffset? GetNextScheduledTime();
    
    /// <summary>
    /// 获取调度历史（最近N次）
    /// </summary>
    IReadOnlyList<DateTimeOffset> GetScheduleHistory(int count);
}
