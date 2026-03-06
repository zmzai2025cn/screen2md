using Screen2MD.Engines.Interfaces;
using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Engines.Engines;

/// <summary>
/// UIA引擎 - Windows UI自动化实现
/// 注意：Linux环境下使用模拟实现，实际功能在Windows/Wine中运行
/// </summary>
public sealed class UIAEngine : IUIAEngine, IDisposable
{
    private bool _disposed;

    public string Name => nameof(UIAEngine);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    // 软件类型映射
    private readonly Dictionary<string, string> _softwarePatterns = new()
    {
        ["devenv.exe"] = "IDE",
        ["code.exe"] = "IDE",
        ["idea64.exe"] = "IDE",
        ["chrome.exe"] = "Browser",
        ["firefox.exe"] = "Browser",
        ["msedge.exe"] = "Browser",
        ["wechat.exe"] = "IM",
        ["dingtalk.exe"] = "IM",
        ["slack.exe"] = "IM",
        ["teams.exe"] = "IM",
        ["outlook.exe"] = "Email",
        ["foxmail.exe"] = "Email",
        ["word.exe"] = "Document",
        ["excel.exe"] = "Document",
        ["powerpnt.exe"] = "Document",
        ["notepad.exe"] = "Editor",
        ["sublime_text.exe"] = "Editor",
        ["xshell.exe"] = "Terminal",
        ["putty.exe"] = "Terminal",
        ["terminal.exe"] = "Terminal"
    };

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<SoftwareInfo?> GetActiveWindowInfoAsync(CancellationToken cancellationToken = default)
    {
        // Linux环境下返回模拟数据
        // Windows环境下使用: User32.GetForegroundWindow() + UIAutomation
        var mockInfo = new SoftwareInfo
        {
            ProcessName = "code.exe",
            WindowTitle = "Screen2MD - Visual Studio Code",
            SoftwareType = "IDE",
            DocumentTitle = "ChangeDetectionEngine.cs",
            DetectedAt = DateTimeOffset.UtcNow
        };
        
        return Task.FromResult<SoftwareInfo?>(mockInfo);
    }

    public Task<UIElement?> GetUIElementTreeAsync(CancellationToken cancellationToken = default)
    {
        // 模拟UI元素树
        var root = new UIElement
        {
            AutomationId = "MainWindow",
            Name = "Visual Studio Code",
            ControlType = "Window",
            ClassName = "Chrome_WidgetWin_1",
            BoundingRectangle = new Rectangle(0, 0, 1920, 1080),
            IsEnabled = true,
            IsVisible = true,
            Children = new List<UIElement>
            {
                new()
                {
                    AutomationId = "Editor",
                    Name = "Editor",
                    ControlType = "Document",
                    ClassName = "MonacoEditor",
                    BoundingRectangle = new Rectangle(300, 100, 1600, 900),
                    IsEnabled = true,
                    IsVisible = true,
                    Text = "public class Example { }"
                },
                new()
                {
                    AutomationId = "Sidebar",
                    Name = "Explorer",
                    ControlType = "Pane",
                    BoundingRectangle = new Rectangle(0, 100, 300, 900),
                    IsEnabled = true,
                    IsVisible = true
                }
            }
        };

        return Task.FromResult<UIElement?>(root);
    }

    public Task<List<UIElement>> FindElementsByTypeAsync(string controlType, CancellationToken cancellationToken = default)
    {
        var elements = new List<UIElement>
        {
            new()
            {
                AutomationId = "Editor1",
                Name = "Code Editor",
                ControlType = controlType,
                BoundingRectangle = new Rectangle(300, 100, 1600, 900),
                IsEnabled = true,
                IsVisible = true
            }
        };

        return Task.FromResult(elements);
    }

    public Task<string?> GetTextContentAsync(CancellationToken cancellationToken = default)
    {
        // 模拟从代码编辑器获取文本
        var mockCode = @"using System;

public class Example
{
    public void Main()
    {
        Console.WriteLine(""Hello, World!"");
    }
}";
        return Task.FromResult<string?>(mockCode);
    }

    public string ClassifySoftware(SoftwareInfo info)
    {
        var processName = info.ProcessName.ToLowerInvariant();
        
        // 直接匹配
        if (_softwarePatterns.TryGetValue(processName, out var type))
        {
            return type;
        }

        // 窗口标题关键词匹配
        var title = info.WindowTitle.ToLowerInvariant();
        if (title.Contains("visual studio") || title.Contains("intellij") || title.Contains("eclipse"))
            return "IDE";
        if (title.Contains("chrome") || title.Contains("edge") || title.Contains("firefox"))
            return "Browser";
        if (title.Contains("wechat") || title.Contains("微信") || title.Contains("qq"))
            return "IM";
        if (title.Contains("outlook") || title.Contains("mail") || title.Contains("邮件"))
            return "Email";
        if (title.Contains("word") || title.Contains("excel") || title.Contains("powerpoint"))
            return "Document";
        if (title.Contains("terminal") || title.Contains("cmd") || title.Contains("powershell"))
            return "Terminal";

        return "Unknown";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
