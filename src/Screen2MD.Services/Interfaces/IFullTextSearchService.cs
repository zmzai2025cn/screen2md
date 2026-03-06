using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Interfaces;

/// <summary>
/// 全文搜索服务接口
/// </summary>
public interface IFullTextSearchService : IKernelComponent
{
    /// <summary>
    /// 索引捕获内容
    /// </summary>
    Task IndexCaptureAsync(CaptureDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜索捕获内容
    /// </summary>
    Task<SearchResult> SearchAsync(string query, SearchOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除索引
    /// </summary>
    Task DeleteIndexAsync(string captureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有索引
    /// </summary>
    Task ClearAllIndexesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取索引统计信息
    /// </summary>
    Task<IndexStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 优化索引
    /// </summary>
    Task OptimizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据时间范围删除旧索引
    /// </summary>
    Task DeleteOldIndexesAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}

/// <summary>
/// 捕获文档
/// </summary>
public class CaptureDocument
{
    public string Id { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public int DisplayIndex { get; set; }
}

/// <summary>
/// 搜索结果
/// </summary>
public class SearchResult
{
    public int TotalCount { get; set; }
    public List<SearchResultItem> Items { get; set; } = new();
    public string Query { get; set; } = "";
    public TimeSpan SearchTime { get; set; }
}

/// <summary>
/// 搜索结果项
/// </summary>
public class SearchResultItem
{
    public string CaptureId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public double Rank { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 搜索选项
/// </summary>
public class SearchOptions
{
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
    public string? ProcessName { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string[]? Tags { get; set; }
    public bool HighlightMatches { get; set; } = true;
}

/// <summary>
/// 索引统计信息
/// </summary>
public class IndexStatistics
{
    public long TotalDocuments { get; set; }
    public long TotalTerms { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public DateTimeOffset LastOptimized { get; set; }
}