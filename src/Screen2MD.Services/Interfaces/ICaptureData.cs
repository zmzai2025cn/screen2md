using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Interfaces;

/// <summary>
/// 截图数据模型
/// </summary>
public class CaptureData
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// 采集时间
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// 窗口标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 进程名称
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;
    
    /// <summary>
    /// 标签
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// OCR文本（如果有）
    /// </summary>
    public string? OcrText { get; set; }
    
    /// <summary>
    /// 窗口句柄
    /// </summary>
    public IntPtr? WindowHandle { get; set; }
    
    /// <summary>
    /// 屏幕区域
    /// </summary>
    public Rectangle? CaptureRegion { get; set; }
}

/// <summary>
/// 矩形区域
/// </summary>
public struct Rectangle
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
