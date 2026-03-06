using SkiaSharp;
using Screen2MD.Abstractions;
using Screen2MD.Platform.Common;

namespace Screen2MD.Platform.Mock;

/// <summary>
/// Mock 截图引擎 - 用于 Linux 测试
/// 返回预定义的测试图像
/// </summary>
public sealed class MockCaptureEngine : IScreenCaptureEngine
{
    private readonly List<DisplayInfo> _displays;
    private bool _disposed;

    public MockCaptureEngine(int displayCount = 2)
    {
        _displays = new List<DisplayInfo>();
        
        // 创建模拟显示器
        for (int i = 0; i < displayCount; i++)
        {
            _displays.Add(new DisplayInfo
            {
                Index = i,
                DeviceName = $"DISPLAY{i + 1}",
                IsPrimary = i == 0,
                Width = 1920,
                Height = 1080,
                X = i * 1920,
                Y = 0,
                DpiScale = 1.0f
            });
        }
    }

    public Task<IReadOnlyList<IDisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<IDisplayInfo>>(_displays);
    }

    public Task<ICapturedImage> CaptureDisplayAsync(int displayIndex, CancellationToken cancellationToken = default)
    {
        if (displayIndex < 0 || displayIndex >= _displays.Count)
            throw new ArgumentOutOfRangeException(nameof(displayIndex));

        // 生成测试图像（带颜色标记）
        var display = _displays[displayIndex];
        var bitmap = new SKBitmap(display.Width, display.Height);
        
        using var canvas = new SKCanvas(bitmap);
        
        // 每个显示器不同颜色
        var colors = new[] { SKColors.LightBlue, SKColors.LightGreen, SKColors.LightYellow, SKColors.LightCoral };
        canvas.Clear(colors[displayIndex % colors.Length]);
        
        // 添加显示器信息文本
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 48,
            IsAntialias = true
        };
        
        canvas.DrawText($"Display {displayIndex}", 50, 100, paint);
        canvas.DrawText($"Resolution: {display.Width}x{display.Height}", 50, 160, paint);
        canvas.DrawText($"Position: ({display.X}, {display.Y})", 50, 220, paint);
        
        // 添加一些模拟内容（用于 OCR 测试）
        paint.TextSize = 24;
        canvas.DrawText("This is a test screenshot for Screen2MD", 50, 300, paint);
        canvas.DrawText("Mock data generated for testing", 50, 340, paint);
        canvas.DrawText($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", 50, 380, paint);

        return Task.FromResult<ICapturedImage>(new SkiaCapturedImage(bitmap, displayIndex));
    }

    public Task<ICapturedImage> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        
        // 创建渐变效果
        for (int row = 0; row < height; row++)
        {
            var color = new SKColor(
                (byte)((row * 255) / height),
                (byte)(255 - (row * 255) / height),
                128);
            
            using var paint = new SKPaint { Color = color };
            canvas.DrawRect(0, row, width, 1, paint);
        }

        return Task.FromResult<ICapturedImage>(new SkiaCapturedImage(bitmap, 0));
    }

    public Task<Rectangle> GetVirtualScreenBoundsAsync(CancellationToken cancellationToken = default)
    {
        if (_displays.Count == 0)
            return Task.FromResult(new Rectangle(0, 0, 0, 0));

        int left = _displays.Min(d => d.X);
        int top = _displays.Min(d => d.Y);
        int right = _displays.Max(d => d.X + d.Width);
        int bottom = _displays.Max(d => d.Y + d.Height);

        return Task.FromResult(new Rectangle(left, top, right - left, bottom - top));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Mock 窗口管理器
/// </summary>
public sealed class MockWindowManager : IWindowManager
{
    private readonly List<MockWindowInfo> _windows = new();

    public MockWindowManager()
    {
        // 创建模拟窗口
        _windows.Add(new MockWindowInfo 
        { 
            Title = "Visual Studio Code", 
            ProcessName = "Code",
            Bounds = new Rectangle(100, 100, 1200, 800)
        });
        _windows.Add(new MockWindowInfo 
        { 
            Title = "Google Chrome", 
            ProcessName = "chrome",
            Bounds = new Rectangle(1300, 100, 1200, 800)
        });
    }

    public Task<IWindowInfo?> GetActiveWindowAsync()
    {
        return Task.FromResult<IWindowInfo?>(_windows.FirstOrDefault());
    }

    public Task<IReadOnlyList<IWindowInfo>> GetVisibleWindowsAsync()
    {
        return Task.FromResult<IReadOnlyList<IWindowInfo>>(_windows.ToList());
    }

    public Task<IWindowInfo?> GetWindowAtPointAsync(int x, int y)
    {
        var window = _windows.FirstOrDefault(w => w.Bounds.Contains(x, y));
        return Task.FromResult<IWindowInfo?>(window);
    }
}

public class MockWindowInfo : IWindowInfo
{
    public IntPtr Handle { get; set; } = new IntPtr(12345);
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; } = 1234;
    public Rectangle Bounds { get; set; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Mock OCR 引擎
/// </summary>
public sealed class MockOcrEngine : IOcrEngine
{
    public async Task<OcrResult> RecognizeAsync(ICapturedImage image, OcrOptions options, CancellationToken cancellationToken = default)
    {
        // 模拟 OCR 处理时间，并检查取消令牌
        try
        {
            await Task.Delay(100, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw new OperationCanceledException();
        }

        return new OcrResult
        {
            Success = true,
            Text = $"This is mock OCR text for display {image.DisplayIndex}\n" +
                   $"Image size: {image.Width}x{image.Height}\n" +
                   $"Timestamp: {image.Timestamp:yyyy-MM-dd HH:mm:ss}",
            Confidence = 0.95,
            ProcessingTime = TimeSpan.FromMilliseconds(100),
            Regions = new List<OcrRegion>
            {
                new OcrRegion 
                { 
                    Text = "Mock OCR", 
                    Bounds = new Rectangle(50, 300, 200, 30),
                    Confidence = 0.95 
                }
            }
        };
    }

    public Task<bool> IsAvailableAsync() => Task.FromResult(true);

    public Task<IReadOnlyList<string>> GetSupportedLanguagesAsync() => 
        Task.FromResult<IReadOnlyList<string>>(new[] { "eng", "chi_sim" });
}

/// <summary>
/// Mock 平台服务工厂
/// 用于测试时创建 Mock 实现
/// </summary>
public sealed class MockPlatformServiceFactory : IPlatformServiceFactory
{
    public IScreenCaptureEngine CreateCaptureEngine() => new MockCaptureEngine();
    public IImageProcessor CreateImageProcessor() => new SkiaImageProcessor();
    public IWindowManager CreateWindowManager() => new MockWindowManager();
    public IOcrEngine CreateOcrEngine() => new MockOcrEngine();
}