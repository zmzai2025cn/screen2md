using Screen2MD.Engines.Interfaces;
using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Interfaces;

/// <summary>
/// 隐私规则
/// </summary>
public record PrivacyRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public PrivacyRuleType Type { get; init; }
    public string Pattern { get; init; } = string.Empty; // 正则或关键词
    public bool IsEnabled { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 隐私规则类型
/// </summary>
public enum PrivacyRuleType
{
    WindowTitle,    // 窗口标题匹配
    ProcessName,    // 进程名匹配
    TextContent,    // 文本内容匹配
    UrlPattern,     // URL匹配
    CustomRegex     // 自定义正则
}

/// <summary>
/// 隐私过滤结果
/// </summary>
public record PrivacyFilterResult
{
    public bool IsBlocked { get; init; }
    public string? BlockedReason { get; init; }
    public string? MatchedRuleId { get; init; }
    public bool ShouldBlur { get; init; }
    public Rectangle? BlurRegion { get; init; }
}

/// <summary>
/// 隐私过滤服务接口 - 100%敏感信息拦截
/// 目标: 零隐私泄露
/// </summary>
public interface IPrivacyFilterService : IKernelComponent
{
    /// <summary>
    /// 检查是否应该捕获
    /// </summary>
    Task<PrivacyFilterResult> ShouldCaptureAsync(
        string processName, 
        string windowTitle, 
        string? textContent = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 过滤敏感文本
    /// </summary>
    string FilterSensitiveText(string text);
    
    /// <summary>
    /// 添加隐私规则
    /// </summary>
    Task<PrivacyRule> AddRuleAsync(PrivacyRule rule, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除隐私规则
    /// </summary>
    Task RemoveRuleAsync(string ruleId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取所有规则
    /// </summary>
    Task<IReadOnlyList<PrivacyRule>> GetAllRulesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 启用/禁用规则
    /// </summary>
    Task ToggleRuleAsync(string ruleId, bool isEnabled, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 加载默认规则
    /// 包括: 银行、支付、密码输入框等
    /// </summary>
    Task LoadDefaultRulesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查文本是否包含敏感信息
    /// </summary>
    bool ContainsSensitiveInfo(string text);
    
    /// <summary>
    /// 获取隐私报告（被拦截的内容统计）
    /// </summary>
    Task<PrivacyReport> GetPrivacyReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>
/// 隐私报告
/// </summary>
public record PrivacyReport
{
    public int TotalBlocked { get; init; }
    public Dictionary<string, int> BlockedByRuleType { get; init; } = new();
    public List<string> TopBlockedApplications { get; init; } = new();
}
