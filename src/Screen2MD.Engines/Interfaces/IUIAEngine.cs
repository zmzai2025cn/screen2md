using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Engines.Interfaces;

/// <summary>
/// UI元素信息
/// </summary>
public record UIElement
{
    public string AutomationId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ControlType { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public Rectangle BoundingRectangle { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsVisible { get; init; }
    public string? Text { get; init; }
    public List<UIElement> Children { get; init; } = new();
}

/// <summary>
/// 软件信息
/// </summary>
public record SoftwareInfo
{
    public string ProcessName { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;
    public string SoftwareType { get; init; } = string.Empty; // IDE, Browser, IM, etc.
    public string? DocumentTitle { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// UIA引擎接口 - Windows UI自动化
/// </summary>
public interface IUIAEngine : IKernelComponent
{
    /// <summary>
    /// 获取当前活动窗口的信息
    /// </summary>
    Task<SoftwareInfo?> GetActiveWindowInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取当前窗口的UI元素树
    /// </summary>
    Task<UIElement?> GetUIElementTreeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据类型查找UI元素
    /// </summary>
    Task<List<UIElement>> FindElementsByTypeAsync(string controlType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取文本内容（如代码编辑器中的文本）
    /// </summary>
    Task<string?> GetTextContentAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 识别软件类型
    /// </summary>
    string ClassifySoftware(SoftwareInfo info);
}
