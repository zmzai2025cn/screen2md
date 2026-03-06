using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Engines.Interfaces;

/// <summary>
/// 屏幕捕获结果
/// </summary>
public record ScreenCaptureResult
{
    public bool Success { get; init; }
    public byte[]? ImageData { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string Format { get; init; } = "bmp"; // bmp, png, jpeg
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan CaptureTime { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 屏幕捕获引擎接口
/// </summary>
public interface IScreenCaptureEngine : IKernelComponent
{
    /// <summary>
    /// 捕获整个屏幕
    /// </summary>
    Task<ScreenCaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 捕获指定区域
    /// </summary>
    Task<ScreenCaptureResult> CaptureRegionAsync(Rectangle region, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 捕获指定窗口
    /// </summary>
    Task<ScreenCaptureResult> CaptureWindowAsync(IntPtr windowHandle, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取屏幕分辨率
    /// </summary>
    (int Width, int Height) GetScreenResolution();
    
    /// <summary>
    /// 设置输出格式和质量
    /// </summary>
    void SetOutputFormat(string format, int quality = 80);
}
