namespace Screen2MD.Abstractions;

/// <summary>
/// 截图引擎抽象接口
/// 平台无关的截图能力定义
/// </summary>
public interface IScreenCaptureEngine : IDisposable
{
    /// <summary>
    /// 获取所有显示器信息
    /// </summary>
    Task<IReadOnlyList<IDisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 捕获指定显示器的屏幕
    /// </summary>
    Task<ICapturedImage> CaptureDisplayAsync(int displayIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// 捕获指定区域的屏幕
    /// </summary>
    Task<ICapturedImage> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取虚拟屏幕边界（多显示器并集）
    /// </summary>
    Task<Rectangle> GetVirtualScreenBoundsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 显示器信息接口
/// </summary>
public interface IDisplayInfo
{
    int Index { get; }
    string DeviceName { get; }
    bool IsPrimary { get; }
    int Width { get; }
    int Height { get; }
    int X { get; }
    int Y { get; }
    float DpiScale { get; }
}

/// <summary>
/// 捕获的图像接口
/// 使用 SkiaSharp 的 SKBitmap 作为统一格式
/// </summary>
public interface ICapturedImage : IDisposable
{
    /// <summary>
    /// 图像宽度
    /// </summary>
    int Width { get; }

    /// <summary>
    /// 图像高度
    /// </summary>
    int Height { get; }

    /// <summary>
    /// 捕获时间戳
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// 来源显示器索引
    /// </summary>
    int DisplayIndex { get; }

    /// <summary>
    /// 获取 SkiaSharp 位图（跨平台）
    /// </summary>
    SkiaSharp.SKBitmap ToSKBitmap();

    /// <summary>
    /// 保存为文件
    /// </summary>
    Task SaveAsync(string filePath, ImageFormat format = ImageFormat.Png, int quality = 95);

    /// <summary>
    /// 转换为字节数组
    /// </summary>
    byte[] ToByteArray(ImageFormat format = ImageFormat.Png, int quality = 95);
}

/// <summary>
/// 图像格式
/// </summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    Webp,
    Bmp
}

/// <summary>
/// 图像处理器接口
/// 跨平台的图像操作
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// 计算图像哈希（用于变化检测）
    /// </summary>
    Task<string> ComputeHashAsync(ICapturedImage image);

    /// <summary>
    /// 计算两张图像的相似度（0-1）
    /// </summary>
    Task<double> ComputeSimilarityAsync(ICapturedImage image1, ICapturedImage image2);

    /// <summary>
    /// 对区域进行模糊处理（隐私保护）
    /// </summary>
    Task<ICapturedImage> BlurRegionAsync(ICapturedImage image, Rectangle region, int radius);

    /// <summary>
    /// 调整图像大小
    /// </summary>
    Task<ICapturedImage> ResizeAsync(ICapturedImage image, int width, int height);

    /// <summary>
    /// 计算图像熵值（内容复杂度）
    /// </summary>
    Task<double> ComputeEntropyAsync(ICapturedImage image);
}

/// <summary>
/// 窗口信息接口
/// </summary>
public interface IWindowInfo
{
    IntPtr Handle { get; }
    string Title { get; }
    string ProcessName { get; }
    int ProcessId { get; }
    Rectangle Bounds { get; }
    bool IsVisible { get; }
}

/// <summary>
/// 窗口管理器接口
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// 获取当前活动窗口
    /// </summary>
    Task<IWindowInfo?> GetActiveWindowAsync();

    /// <summary>
    /// 获取所有可见窗口
    /// </summary>
    Task<IReadOnlyList<IWindowInfo>> GetVisibleWindowsAsync();

    /// <summary>
    /// 获取指定点所在的窗口
    /// </summary>
    Task<IWindowInfo?> GetWindowAtPointAsync(int x, int y);
}

/// <summary>
/// OCR 引擎接口
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// 识别图像中的文字
    /// </summary>
    Task<OcrResult> RecognizeAsync(ICapturedImage image, OcrOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查 OCR 引擎是否可用
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// 获取支持的语言列表
    /// </summary>
    Task<IReadOnlyList<string>> GetSupportedLanguagesAsync();
}

/// <summary>
/// OCR 选项
/// </summary>
public class OcrOptions
{
    public IReadOnlyList<string> Languages { get; set; } = new[] { "eng" };
    public int TimeoutMs { get; set; } = 30000;
    public bool PreserveFormatting { get; set; } = true;
}

/// <summary>
/// OCR 结果
/// </summary>
public class OcrResult
{
    public string Text { get; set; } = "";
    public double Confidence { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<OcrRegion> Regions { get; set; } = Array.Empty<OcrRegion>();
}

/// <summary>
/// OCR 区域
/// </summary>
public class OcrRegion
{
    public string Text { get; set; } = "";
    public Rectangle Bounds { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// 平台服务工厂
/// 抽象工厂模式，用于创建平台特定实现
/// </summary>
public interface IPlatformServiceFactory
{
    IScreenCaptureEngine CreateCaptureEngine();
    IImageProcessor CreateImageProcessor();
    IWindowManager CreateWindowManager();
    IOcrEngine CreateOcrEngine();
}

/// <summary>
/// 矩形结构（跨平台）
/// </summary>
public readonly record struct Rectangle(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}