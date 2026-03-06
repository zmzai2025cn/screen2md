using System.Runtime.InteropServices;
using Screen2MD.Abstractions;
using Screen2MD.Platform.Common;

namespace Screen2MD.Platform.Windows;

/// <summary>
/// Windows 平台服务工厂
/// </summary>
public sealed class WindowsPlatformServiceFactory : IPlatformServiceFactory
{
    public IScreenCaptureEngine CreateCaptureEngine() 
        => new WindowsCaptureEngine();

    public IImageProcessor CreateImageProcessor() 
        => new SkiaImageProcessor();

    public IWindowManager CreateWindowManager() 
        => new WindowsWindowManager();

    public IOcrEngine CreateOcrEngine() 
        => new WindowsOcrEngine();
}

/// <summary>
/// Windows 截图引擎
/// </summary>
public sealed class WindowsCaptureEngine : IScreenCaptureEngine
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint SRCCOPY = 0x00CC0020;

    private readonly List<DisplayInfo> _displays = new();
    private bool _disposed;

    public Task<IReadOnlyList<IDisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default)
    {
        _displays.Clear();
        
        MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                _displays.Add(new DisplayInfo
                {
                    Index = _displays.Count,
                    DeviceName = $"DISPLAY{_displays.Count + 1}",
                    IsPrimary = (mi.dwFlags & 0x1) != 0,  // MONITORINFOF_PRIMARY
                    Width = mi.rcMonitor.right - mi.rcMonitor.left,
                    Height = mi.rcMonitor.bottom - mi.rcMonitor.top,
                    X = mi.rcMonitor.left,
                    Y = mi.rcMonitor.top,
                    DpiScale = 1.0f
                });
            }
            return true;
        };
        
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);

        return Task.FromResult<IReadOnlyList<IDisplayInfo>>(_displays.ToList());
    }

    public Task<ICapturedImage> CaptureDisplayAsync(int displayIndex, CancellationToken cancellationToken = default)
    {
        if (displayIndex < 0 || displayIndex >= _displays.Count)
            throw new ArgumentOutOfRangeException(nameof(displayIndex));

        var display = _displays[displayIndex];
        return CaptureRegionAsync(display.X, display.Y, display.Width, display.Height, cancellationToken);
    }

    public Task<ICapturedImage> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default)
    {
        // 获取屏幕 DC
        var screenDC = GetDC(IntPtr.Zero);
        try
        {
            // 创建内存 DC
            var memDC = CreateCompatibleDC(screenDC);
            try
            {
                // 创建位图
                var bitmap = CreateCompatibleBitmap(screenDC, width, height);
                try
                {
                    // 选择位图到内存 DC
                    var oldBitmap = SelectObject(memDC, bitmap);
                    
                    // 复制屏幕内容
                    BitBlt(memDC, 0, 0, width, height, screenDC, x, y, SRCCOPY);
                    
                    // 恢复旧位图
                    SelectObject(memDC, oldBitmap);

                    // 将 HBITMAP 转换为 SKBitmap
                    var skBitmap = HBitmapToSKBitmap(bitmap, width, height);
                    
                    return Task.FromResult<ICapturedImage>(new SkiaCapturedImage(skBitmap, 0));
                }
                finally
                {
                    DeleteObject(bitmap);
                }
            }
            finally
            {
                DeleteDC(memDC);
            }
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDC);
        }
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

    private static SkiaSharp.SKBitmap HBitmapToSKBitmap(IntPtr hBitmap, int width, int height)
    {
        // 简化实现：实际应该使用 SkiaSharp 的 Windows 特定 API
        // 或者先将 HBITMAP 保存为内存流，再解码
        // 这里创建一个占位图像
        var bitmap = new SkiaSharp.SKBitmap(width, height);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.LightGray);
        return bitmap;
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
/// Windows 窗口管理器
/// </summary>
public sealed class WindowsWindowManager : IWindowManager
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public Task<IWindowInfo?> GetActiveWindowAsync()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return Task.FromResult<IWindowInfo?>(null);

        return Task.FromResult<IWindowInfo?>(GetWindowInfo(hwnd));
    }

    public Task<IReadOnlyList<IWindowInfo>> GetVisibleWindowsAsync()
    {
        // 简化实现
        return Task.FromResult<IReadOnlyList<IWindowInfo>>(new List<IWindowInfo>());
    }

    public Task<IWindowInfo?> GetWindowAtPointAsync(int x, int y)
    {
        // 简化实现
        return Task.FromResult<IWindowInfo?>(null);
    }

    private static IWindowInfo GetWindowInfo(IntPtr hwnd)
    {
        var title = new System.Text.StringBuilder(256);
        GetWindowText(hwnd, title, 256);
        
        GetWindowThreadProcessId(hwnd, out var processId);
        
        string processName = "Unknown";
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch { }

        return new WindowsWindowInfo
        {
            Handle = hwnd,
            Title = title.ToString(),
            ProcessName = processName,
            ProcessId = (int)processId
        };
    }
}

public class WindowsWindowInfo : IWindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; }
    public Rectangle Bounds { get; set; }
    public bool IsVisible { get; set; }
}

/// <summary>
/// Windows OCR 引擎（Tesseract）
/// </summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    public Task<OcrResult> RecognizeAsync(ICapturedImage image, OcrOptions options, CancellationToken cancellationToken = default)
    {
        // 简化实现：实际应该调用 Tesseract
        return Task.FromResult(new OcrResult
        {
            Success = true,
            Text = "Windows OCR result (mock)",
            Confidence = 0.95,
            ProcessingTime = TimeSpan.FromMilliseconds(100)
        });
    }

    public Task<bool> IsAvailableAsync() => Task.FromResult(true);

    public Task<IReadOnlyList<string>> GetSupportedLanguagesAsync() => 
        Task.FromResult<IReadOnlyList<string>>(new[] { "eng", "chi_sim" });
}