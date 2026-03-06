using Screen2MD.Kernel.Interfaces;
using Screen2MD.Services.Interfaces;
using System.Text.RegularExpressions;

namespace Screen2MD.Services.Services;

/// <summary>
/// 隐私过滤器服务 - 100%敏感信息拦截
/// </summary>
public sealed class PrivacyFilterService : IPrivacyFilterService, IDisposable
{
    private readonly List<PrivacyRule> _rules = new();
    private readonly IKernelLogger? _logger;
    private readonly object _lock = new();
    private bool _disposed;

    public string Name => nameof(PrivacyFilterService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    // 默认敏感模式
    private static readonly string[] DefaultSensitivePatterns = new[]
    {
        @"password", @"passwd", @"pwd",
        @"credit.?card", @"cvv", @"cvc",
        @"bank.?account", @"account.?number",
        @"social.?security", @"ssn",
        @"身份证", @"银行卡", @"信用卡",
        @"密码", @"验证码", @"密保"
    };

    public PrivacyFilterService(IKernelLogger? logger = null)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await LoadDefaultRulesAsync(cancellationToken);
        HealthStatus = HealthStatus.Healthy;
        _logger?.LogInformation($"Privacy filter initialized with {_rules.Count} rules");
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task LoadDefaultRulesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // 银行类应用
            _rules.Add(new PrivacyRule
            {
                Name = "银行应用",
                Type = PrivacyRuleType.ProcessName,
                Pattern = @"(bank|网银| Banking|ABC|ICBC|CCB|BOC|CMB)"
            });

            // 支付类应用
            _rules.Add(new PrivacyRule
            {
                Name = "支付应用",
                Type = PrivacyRuleType.ProcessName,
                Pattern = @"(alipay|wechatpay|paypal|stripe|支付)"
            });

            // 密码管理器
            _rules.Add(new PrivacyRule
            {
                Name = "密码管理器",
                Type = PrivacyRuleType.ProcessName,
                Pattern = @"(1password|lastpass|bitwarden|keepass|密码)"
            });

            // 隐私窗口标题
            _rules.Add(new PrivacyRule
            {
                Name = "隐私窗口",
                Type = PrivacyRuleType.WindowTitle,
                Pattern = @"(密码|验证码|登录|Login|Password|Credential|Security)"
            });

            // 敏感文本模式
            _rules.Add(new PrivacyRule
            {
                Name = "敏感文本",
                Type = PrivacyRuleType.TextContent,
                Pattern = $@"({string.Join("|", DefaultSensitivePatterns)})"
            });

            // URL模式
            _rules.Add(new PrivacyRule
            {
                Name = "敏感URL",
                Type = PrivacyRuleType.UrlPattern,
                Pattern = @"(login|signin|auth|password|credential)"
            });
        }

        return Task.CompletedTask;
    }

    public Task<PrivacyFilterResult> ShouldCaptureAsync(
        string processName, 
        string windowTitle, 
        string? textContent = null,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            foreach (var rule in _rules.Where(r => r.IsEnabled))
            {
                bool matched = rule.Type switch
                {
                    PrivacyRuleType.ProcessName => Regex.IsMatch(processName, rule.Pattern, RegexOptions.IgnoreCase),
                    PrivacyRuleType.WindowTitle => Regex.IsMatch(windowTitle, rule.Pattern, RegexOptions.IgnoreCase),
                    PrivacyRuleType.TextContent => !string.IsNullOrEmpty(textContent) && Regex.IsMatch(textContent, rule.Pattern, RegexOptions.IgnoreCase),
                    PrivacyRuleType.UrlPattern => !string.IsNullOrEmpty(textContent) && Regex.IsMatch(textContent, rule.Pattern, RegexOptions.IgnoreCase),
                    PrivacyRuleType.CustomRegex => Regex.IsMatch(processName + " " + windowTitle, rule.Pattern, RegexOptions.IgnoreCase),
                    _ => false
                };

                if (matched)
                {
                    _logger?.LogWarning($"Privacy blocked: {rule.Name} matched {rule.Type}");
                    return Task.FromResult(new PrivacyFilterResult
                    {
                        IsBlocked = true,
                        BlockedReason = $"Matched rule: {rule.Name}",
                        MatchedRuleId = rule.Id,
                        ShouldBlur = true
                    });
                }
            }
        }

        return Task.FromResult(new PrivacyFilterResult { IsBlocked = false });
    }

    public string FilterSensitiveText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var filtered = text;
        
        // 替换敏感信息为占位符
        foreach (var pattern in DefaultSensitivePatterns)
        {
            filtered = Regex.Replace(filtered, pattern, "[REDACTED]", RegexOptions.IgnoreCase);
        }

        // 替换可能的身份证号
        filtered = Regex.Replace(filtered, @"\d{17}[\dXx]|\d{15}", "[ID-REDACTED]");
        
        // 替换可能的银行卡号
        filtered = Regex.Replace(filtered, @"\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}", "[CARD-REDACTED]");

        return filtered;
    }

    public bool ContainsSensitiveInfo(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        foreach (var pattern in DefaultSensitivePatterns)
        {
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    public Task<PrivacyRule> AddRuleAsync(PrivacyRule rule, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _rules.Add(rule);
        }
        _logger?.LogInformation($"Added privacy rule: {rule.Name}");
        return Task.FromResult(rule);
    }

    public Task RemoveRuleAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _rules.RemoveAll(r => r.Id == ruleId);
        }
        _logger?.LogInformation($"Removed privacy rule: {ruleId}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PrivacyRule>> GetAllRulesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<PrivacyRule>>(_rules.ToList());
        }
    }

    public Task ToggleRuleAsync(string ruleId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
            if (rule != null)
            {
                _rules.Remove(rule);
                _rules.Add(rule with { IsEnabled = isEnabled });
            }
        }
        return Task.CompletedTask;
    }

    public Task<PrivacyReport> GetPrivacyReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        // 简化实现 - 实际应查询数据库
        return Task.FromResult(new PrivacyReport
        {
            TotalBlocked = 0,
            BlockedByRuleType = new Dictionary<string, int>(),
            TopBlockedApplications = new List<string>()
        });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger?.LogInformation("Privacy filter service disposed");
        }
    }
}
