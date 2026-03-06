using Screen2MD.Engines.Interfaces;
using Screen2MD.Kernel.Interfaces;
using System.Diagnostics;

namespace Screen2MD.Engines.Engines;

/// <summary>
/// 屏幕捕获引擎 - WinAPI封装，目标无闪烁、<100ms
/// </summary>
public sealed class ScreenCaptureEngine : IScreenCaptureEngine, IDisposable
{
    private string _outputFormat = "bmp";
    private int _quality = 80;
    private bool _disposed;

    public string Name => nameof(ScreenCaptureEngine);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

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

    public Task<ScreenCaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            // Linux环境下使用模拟实现
            var (width, height) = GetScreenResolution();
            
            // 创建模拟屏幕数据 (在实际Windows上会使用WinAPI)
            var imageData = CreateMockScreenData(width, height);
            
            sw.Stop();

            return Task.FromResult(new ScreenCaptureResult
            {
                Success = true,
                ImageData = imageData,
                Width = width,
                Height = height,
                Format = _outputFormat,
                Timestamp = DateTimeOffset.UtcNow,
                CaptureTime = sw.Elapsed
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Task.FromResult(new ScreenCaptureResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTimeOffset.UtcNow,
                CaptureTime = sw.Elapsed
            });
        }
    }

    public Task<ScreenCaptureResult> CaptureRegionAsync(Rectangle region, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            // 创建模拟区域数据
            var imageData = CreateMockScreenData(region.Width, region.Height);
            
            sw.Stop();

            return Task.FromResult(new ScreenCaptureResult
            {
                Success = true,
                ImageData = imageData,
                Width = region.Width,
                Height = region.Height,
                Format = _outputFormat,
                Timestamp = DateTimeOffset.UtcNow,
                CaptureTime = sw.Elapsed
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Task.FromResult(new ScreenCaptureResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTimeOffset.UtcNow,
                CaptureTime = sw.Elapsed
            });
        }
    }

    public Task<ScreenCaptureResult> CaptureWindowAsync(IntPtr windowHandle, CancellationToken cancellationToken = default)
    {
        // 简化实现：返回固定大小的窗口图像
        return CaptureRegionAsync(new Rectangle(0, 0, 800, 600), cancellationToken);
    }

    public (int Width, int Height) GetScreenResolution()
    {
        // Linux环境下返回默认值
        // Windows环境下使用: User32.GetSystemMetrics(SM_CXSCREEN/SM_CYSCREEN)
        return (1920, 1080);
    }

    public void SetOutputFormat(string format, int quality = 80)
    {
        _outputFormat = format.ToLowerInvariant();
        _quality = Math.Clamp(quality, 1, 100);
    }

    /// <summary>
    /// 创建模拟屏幕数据（用于Linux测试）
    /// 实际Windows上会使用BitBlt等WinAPI
    /// </summary>
    private byte[] CreateMockScreenData(int width, int height)
    {
        // 创建简单的BMP格式数据
        var pixelDataSize = width * height * 4; // RGBA
        var fileSize = 54 + pixelDataSize; // Header + data
        var data = new byte[fileSize];

        // BMP Header
        data[0] = (byte)'B';
        data[1] = (byte)'M';
        BitConverter.GetBytes(fileSize).CopyTo(data, 2);
        data[10] = 54; // Data offset

        // DIB Header (BITMAPINFOHEADER)
        BitConverter.GetBytes(40).CopyTo(data, 14); // Header size
        BitConverter.GetBytes(width).CopyTo(data, 18);
        BitConverter.GetBytes(height).CopyTo(data, 22);
        data[26] = 1; // Color planes
        data[28] = 32; // Bits per pixel (RGBA)
        BitConverter.GetBytes(pixelDataSize).CopyTo(data, 34);

        // Fill with random data (simulated screen content)
        var random = new Random();
        for (int i = 54; i < fileSize; i++)
        {
            data[i] = (byte)random.Next(256);
        }

        return data;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
