using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Interfaces;

/// <summary>
/// 采集记录实体 - 顶级团队标准设计
/// </summary>
public record CaptureRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
    public string SoftwareType { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;
    public string? DocumentTitle { get; init; }
    public byte[]? ThumbnailData { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public bool IsUploaded { get; init; }
    public DateTimeOffset? UploadedAt { get; init; }
    public string? TextContent { get; init; }
    public bool IsCodeContent { get; init; }
    public string? Language { get; init; }
}

/// <summary>
/// 查询条件
/// </summary>
public record CaptureQuery
{
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public string? SoftwareType { get; init; }
    public string? ProcessName { get; init; }
    public bool? IsCodeContent { get; init; }
    public bool? IsUploaded { get; init; }
    public int PageSize { get; init; } = 50;
    public int PageNumber { get; init; } = 1;
}

/// <summary>
/// 查询结果
/// </summary>
public record QueryResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}

/// <summary>
/// 存储服务接口 - 企业级SQLite存储，目标1000 TPS
/// </summary>
public interface IStorageService : IKernelComponent
{
    /// <summary>
    /// 保存采集记录
    /// </summary>
    Task<string> SaveCaptureAsync(CaptureRecord record, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ID查询
    /// </summary>
    Task<CaptureRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 条件查询
    /// </summary>
    Task<QueryResult<CaptureRecord>> QueryAsync(CaptureQuery query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新上传状态
    /// </summary>
    Task UpdateUploadStatusAsync(string id, bool isUploaded, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除记录
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取统计信息
    /// </summary>
    Task<StorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 清理旧数据
    /// </summary>
    Task<int> CleanupOldDataAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}

/// <summary>
/// 存储统计
/// </summary>
public record StorageStatistics
{
    public int TotalRecords { get; init; }
    public long TotalSize { get; init; }
    public int UploadedCount { get; init; }
    public int PendingCount { get; init; }
    public Dictionary<string, int> CountBySoftwareType { get; init; } = new();
}
