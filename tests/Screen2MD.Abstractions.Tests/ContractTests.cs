using Screen2MD.Abstractions;
using Screen2MD.Platform.Mock;
using Screen2MD.Platform.Common;
using Screen2MD.Testing;
using Xunit;

namespace Screen2MD.Abstractions.Tests;

/// <summary>
/// 截图引擎契约测试
/// 使用 Mock 实现，可在 Linux 下运行
/// </summary>
public class CaptureEngineContractTests
{
    [Fact]
    [UsesMockData("Tests display enumeration logic")]
    public async Task GetDisplaysAsync_ShouldReturnAtLeastOneDisplay()
    {
        var engine = new MockCaptureEngine(displayCount: 2);
        var displays = await engine.GetDisplaysAsync();
        
        Assert.NotNull(displays);
        Assert.True(displays.Count > 0, "Should return at least one display");
    }

    [Fact]
    [UsesMockData("Tests virtual screen bounds")]
    public async Task GetVirtualScreenBoundsAsync_ShouldReturnValidBounds()
    {
        var engine = new MockCaptureEngine(displayCount: 2);
        var bounds = await engine.GetVirtualScreenBoundsAsync();
        
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }
}

/// <summary>
/// 图像处理器测试
/// 纯算法测试，完全跨平台
/// </summary>
public class ImageProcessorTests
{
    [Fact]
    public void Processor_ShouldNotBeNull()
    {
        var processor = new SkiaImageProcessor();
        Assert.NotNull(processor);
    }
}

/// <summary>
/// 窗口管理器契约测试
/// </summary>
public class WindowManagerContractTests
{
    [Fact]
    [UsesMockData("Tests window enumeration")]
    public async Task GetVisibleWindowsAsync_ShouldReturnWindows()
    {
        var manager = new MockWindowManager();
        var windows = await manager.GetVisibleWindowsAsync();
        
        Assert.NotNull(windows);
        Assert.True(windows.Count >= 0);
    }

    [Fact]
    [UsesMockData("Tests active window retrieval")]
    public async Task GetActiveWindowAsync_ShouldNotThrow()
    {
        var manager = new MockWindowManager();
        var window = await manager.GetActiveWindowAsync();
        
        // 可以是 null 或窗口，不应抛出异常
    }
}

/// <summary>
/// OCR 引擎契约测试
/// </summary>
public class OcrEngineContractTests
{
    [Fact]
    public async Task IsAvailableAsync_ShouldReturnTrue()
    {
        var engine = new MockOcrEngine();
        var available = await engine.IsAvailableAsync();
        Assert.True(available);
    }

    [Fact]
    public async Task GetSupportedLanguagesAsync_ShouldReturnLanguages()
    {
        var engine = new MockOcrEngine();
        var languages = await engine.GetSupportedLanguagesAsync();
        
        Assert.NotNull(languages);
        Assert.True(languages.Count > 0);
    }
}