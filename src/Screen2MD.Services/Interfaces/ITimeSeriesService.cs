using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Interfaces;

/// <summary>
/// 时序数据点
/// </summary>
public record TimeSeriesData
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Measurement { get; init; } = string.Empty; // cpu, memory, captures
    public Dictionary<string, object> Fields { get; init; } = new();
    public Dictionary<string, string> Tags { get; init; } = new();
}

/// <summary>
/// 资源使用指标
/// </summary>
public record ResourceMetrics
{
    public DateTimeOffset Timestamp { get; init; }
    public double CpuPercent { get; init; }
    public double MemoryMB { get; init; }
    public int CaptureCount { get; init; }
    public long DiskUsageMB { get; init; }
}

/// <summary>
/// 时序数据服务接口 - InfluxDB集成
/// 目标: 批量写入, 实时监控
/// </summary>
public interface ITimeSeriesService : IKernelComponent
{
    /// <summary>
    /// 写入单个数据点
    /// </summary>
    Task WriteAsync(TimeSeriesData data, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 批量写入数据点
    /// </summary>
    Task WriteBatchAsync(IEnumerable<TimeSeriesData> dataPoints, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 记录资源指标
    /// </summary>
    Task RecordResourceMetricsAsync(ResourceMetrics metrics, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 记录采集事件
    /// </summary>
    Task RecordCaptureEventAsync(string softwareType, bool isCode, long processingTimeMs, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 查询资源历史
    /// </summary>
    Task<IReadOnlyList<ResourceMetrics>> QueryResourceHistoryAsync(
        DateTimeOffset from, 
        DateTimeOffset to, 
        TimeSpan? interval = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取平均资源使用
    /// </summary>
    Task<ResourceMetrics> GetAverageResourceUsageAsync(
        DateTimeOffset from, 
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取软件使用统计
    /// </summary>
    Task<Dictionary<string, int>> GetSoftwareUsageStatsAsync(
        DateTimeOffset from, 
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 刷新缓冲区（确保数据写入）
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
