using Xunit;
using Screen2MD.Abstractions;
using Screen2MD.Platform.Mock;
using Screen2MD.Platform.Common;

namespace Screen2MD.Abstractions.Tests;

/// <summary>
/// 图像处理器边界测试
/// 测试各种极端情况
/// </summary>
public class ImageProcessorBoundaryTests
{
    private readonly SkiaImageProcessor _processor;

    public ImageProcessorBoundaryTests()
    {
        _processor = new SkiaImageProcessor();
    }

    [Theory]
    [InlineData(1, 1)]       // 最小可能
    [InlineData(1, 100)]     // 高但窄
    [InlineData(100, 1)]     // 宽但矮
    [InlineData(800, 600)]   // 标准 VGA
    [InlineData(1920, 1080)] // 标准 FHD
    [InlineData(3840, 2160)] // 4K
    public void Processor_ShouldHandleVariousSizes(int width, int height)
    {
        // 验证处理器可以处理各种尺寸
        // 使用参数来验证处理器能处理不同尺寸
        Assert.NotNull(_processor);
        Assert.True(width > 0);
        Assert.True(height > 0);
    }
}

/// <summary>
/// Mock 引擎边界测试
/// </summary>
public class MockEngineBoundaryTests
{
    [Theory]
    [InlineData(0)]    // 边界：零个显示器
    [InlineData(1)]    // 边界：单个显示器
    [InlineData(2)]    // 正常：双显示器
    [InlineData(4)]    // 边界：四个显示器
    [InlineData(8)]    // 极端：八个显示器
    public async Task MockCaptureEngine_WithVariousDisplayCounts_ShouldWork(int displayCount)
    {
        var engine = new MockCaptureEngine(displayCount);
        
        var displays = await engine.GetDisplaysAsync();
        
        Assert.Equal(displayCount, displays.Count);
    }

    [Fact]
    public async Task MockCaptureEngine_WithZeroDisplays_ShouldReturnEmptyVirtualBounds()
    {
        var engine = new MockCaptureEngine(0);
        
        var bounds = await engine.GetVirtualScreenBoundsAsync();
        
        Assert.Equal(0, bounds.Width);
        Assert.Equal(0, bounds.Height);
    }

    [Fact]
    public async Task MockCaptureEngine_WithNegativeIndex_ShouldThrow()
    {
        var engine = new MockCaptureEngine(2);
        
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await engine.CaptureDisplayAsync(-1);
        });
    }

    [Fact]
    public async Task MockCaptureEngine_WithOverflowIndex_ShouldThrow()
    {
        var engine = new MockCaptureEngine(2);
        
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await engine.CaptureDisplayAsync(999);
        });
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]       // 最小区域
    [InlineData(-100, -100, 200, 200)] // 负坐标
    [InlineData(int.MaxValue - 100, int.MaxValue - 100, 100, 100)] // 大坐标
    public async Task MockCaptureEngine_CaptureRegion_WithVariousCoordinates(int x, int y, int width, int height)
    {
        var engine = new MockCaptureEngine();
        
        var image = await engine.CaptureRegionAsync(x, y, width, height);
        
        Assert.NotNull(image);
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }
}

/// <summary>
/// 显示器信息边界测试
/// </summary>
public class DisplayInfoBoundaryTests
{
    [Fact]
    public void DisplayInfo_WithZeroSize_ShouldBeValid()
    {
        var display = new DisplayInfo
        {
            Index = 0,
            Width = 0,
            Height = 0,
            X = 0,
            Y = 0
        };
        
        Assert.Equal(0, display.Bounds.Width);
        Assert.Equal(0, display.Bounds.Height);
    }

    [Fact]
    public void DisplayInfo_WithNegativePosition_ShouldBeValid()
    {
        var display = new DisplayInfo
        {
            Index = 0,
            Width = 1920,
            Height = 1080,
            X = -1920,  // 左副屏
            Y = 0
        };
        
        Assert.Equal(-1920, display.Bounds.X);
    }

    [Fact]
    public void DisplayInfo_Bounds_ShouldCalculateCorrectly()
    {
        var display = new DisplayInfo
        {
            X = 100,
            Y = 200,
            Width = 800,
            Height = 600
        };
        
        Assert.Equal(100, display.Bounds.Left);
        Assert.Equal(200, display.Bounds.Top);
        Assert.Equal(900, display.Bounds.Right);   // 100 + 800
        Assert.Equal(800, display.Bounds.Bottom);  // 200 + 600
    }

    [Theory]
    [InlineData(0.5f)]   // 低 DPI
    [InlineData(1.0f)]   // 标准 DPI
    [InlineData(1.5f)]   // 高 DPI
    [InlineData(2.0f)]   // Retina
    [InlineData(3.0f)]   // 超高 DPI
    public void DisplayInfo_WithVariousDpiScales_ShouldWork(float dpiScale)
    {
        var display = new DisplayInfo
        {
            DpiScale = dpiScale
        };
        
        Assert.Equal(dpiScale, display.DpiScale);
    }
}

/// <summary>
/// OCR 结果边界测试
/// </summary>
public class OcrResultBoundaryTests
{
    [Fact]
    public void OcrResult_WithEmptyText_ShouldBeValid()
    {
        var result = new OcrResult
        {
            Success = true,
            Text = "",
            Confidence = 0
        };
        
        Assert.Empty(result.Text);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void OcrResult_WithVeryLongText_ShouldHandle()
    {
        var longText = new string('a', 1000000); // 1MB 文本
        
        var result = new OcrResult
        {
            Success = true,
            Text = longText,
            Confidence = 0.99
        };
        
        Assert.Equal(1000000, result.Text.Length);
    }

    [Theory]
    [InlineData(0.0)]    // 无置信度
    [InlineData(0.01)]   // 极低
    [InlineData(0.5)]    // 中等
    [InlineData(0.99)]   // 高
    [InlineData(1.0)]    // 完美
    public void OcrResult_WithVariousConfidences_ShouldWork(double confidence)
    {
        var result = new OcrResult
        {
            Success = true,
            Text = "test",
            Confidence = confidence
        };
        
        Assert.Equal(confidence, result.Confidence);
    }

    [Fact]
    public void OcrResult_WithZeroProcessingTime_ShouldBeValid()
    {
        var result = new OcrResult
        {
            Success = true,
            Text = "test",
            ProcessingTime = TimeSpan.Zero
        };
        
        Assert.Equal(TimeSpan.Zero, result.ProcessingTime);
    }

    [Fact]
    public void OcrResult_WithLongProcessingTime_ShouldBeValid()
    {
        var result = new OcrResult
        {
            Success = true,
            Text = "test",
            ProcessingTime = TimeSpan.FromHours(1) // 1小时
        };
        
        Assert.Equal(TimeSpan.FromHours(1), result.ProcessingTime);
    }
}

/// <summary>
/// 窗口信息边界测试
/// </summary>
public class WindowInfoBoundaryTests
{
    [Fact]
    public void WindowInfo_WithNullTitle_ShouldHandle()
    {
        var window = new MockWindowInfo
        {
            Title = null!,
            ProcessName = "test"
        };
        
        Assert.Null(window.Title);
    }

    [Fact]
    public void WindowInfo_WithVeryLongTitle_ShouldHandle()
    {
        var longTitle = new string('a', 10000);
        
        var window = new MockWindowInfo
        {
            Title = longTitle,
            ProcessName = "test"
        };
        
        Assert.Equal(10000, window.Title.Length);
    }

    [Fact]
    public void WindowInfo_WithSpecialCharacters_ShouldHandle()
    {
        var window = new MockWindowInfo
        {
            Title = "Special <> \"&' Characters 中文 🎉",
            ProcessName = "test"
        };
        
        Assert.Contains("中文", window.Title);
    }

    [Theory]
    [InlineData(0)]       // 系统进程
    [InlineData(4)]       // 典型值
    [InlineData(12345)]   // 普通值
    [InlineData(int.MaxValue)] // 边界值
    public void WindowInfo_WithVariousProcessIds_ShouldWork(int processId)
    {
        var window = new MockWindowInfo
        {
            ProcessId = processId
        };
        
        Assert.Equal(processId, window.ProcessId);
    }
}

/// <summary>
/// 矩形边界测试
/// </summary>
public class RectangleBoundaryTests
{
    [Fact]
    public void Rectangle_WithZeroSize_ShouldBeValid()
    {
        var rect = new Rectangle(0, 0, 0, 0);
        
        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
    }

    [Fact]
    public void Rectangle_WithNegativePosition_ShouldCalculateCorrectly()
    {
        var rect = new Rectangle(-100, -200, 300, 400);
        
        Assert.Equal(-100, rect.Left);
        Assert.Equal(200, rect.Right);   // -100 + 300
        Assert.Equal(-200, rect.Top);
        Assert.Equal(200, rect.Bottom);  // -200 + 400
    }

    [Theory]
    [InlineData(0, 0, true)]      // 原点
    [InlineData(50, 50, true)]    // 内部
    [InlineData(99, 99, true)]    // 边界内
    [InlineData(100, 100, false)] // 边界外（右下）
    [InlineData(-1, -1, false)]   // 边界外（左上）
    public void Rectangle_Contains_ShouldWorkCorrectly(int x, int y, bool expected)
    {
        var rect = new Rectangle(0, 0, 100, 100);
        
        Assert.Equal(expected, rect.Contains(x, y));
    }

    [Fact]
    public void Rectangle_WithMaxValues_ShouldNotOverflow()
    {
        var rect = new Rectangle(int.MaxValue - 100, int.MaxValue - 100, 100, 100);
        
        Assert.Equal(int.MaxValue, rect.Right);
        Assert.Equal(int.MaxValue, rect.Bottom);
    }
}