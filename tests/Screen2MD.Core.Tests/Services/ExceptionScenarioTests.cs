using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using Screen2MD.Platform.Common;
using Screen2MD.Abstractions;
using Xunit;

namespace Screen2MD.Core.Tests.Services;

/// <summary>
/// 异常场景测试 - 验证系统在各种异常情况下的表现
/// </summary>
public class ExceptionScenarioTests
{
    #region CaptureService 异常场景

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P0")]
    public async Task CaptureAsync_WhenDisplayDisconnected_ShouldHandleGracefully()
    {
        // 模拟显示器在截图过程中断开
        var engine = new DisconnectingMockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor);

        var result = await service.CaptureAsync();

        // 应该返回失败而非崩溃
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P0")]
    public async Task CaptureAsync_WhenOutOfMemory_ShouldHandleGracefully()
    {
        // 模拟内存不足场景
        var engine = new MemoryHungryMockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor);

        var result = await service.CaptureAsync();

        Assert.False(result.Success);
        Assert.Contains("memory", result.ErrorMessage?.ToLower() ?? "");
    }

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P0")]
    public async Task CaptureAsync_ConcurrentCalls_ShouldNotCorruptState()
    {
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor);

        // 并发发起 10 个截图请求
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.CaptureAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // 所有请求都应该完成（成功或失败），不应有未处理异常
        Assert.All(results, r => Assert.NotNull(r));
        Assert.True(results.Count(r => r.Success) >= 1); // 至少一个成功
    }

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P0")]
    public async Task CaptureAsync_WhenDiskFull_ShouldHandleGracefully()
    {
        // 模拟磁盘满
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: "/dev/full");

        var result = await service.CaptureAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region OcrService 异常场景

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P0")]
    public async Task RecognizeAsync_WithCorruptedImage_ShouldReturnFailure()
    {
        var engine = new FailingOcrEngine();
        var service = new OcrService(engine);

        // 创建一个模拟图像
        var mockImage = new MockCapturedImage 
        { 
            Width = 100, 
            Height = 100,
            RawData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 } // 损坏的 JPEG 头
        };

        var result = await service.RecognizeAsync(mockImage);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P0")]
    public async Task RecognizeAsync_WithExtremelyLargeImage_ShouldHandle()
    {
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);

        // 模拟超大图像（16K 分辨率）
        var mockImage = new MockCapturedImage 
        { 
            Width = 15360, 
            Height = 8640 
        };

        // 不应该抛出异常
        var result = await service.RecognizeAsync(mockImage);
        
        Assert.NotNull(result);
    }

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P1")]
    public async Task RecognizeAsync_WhenOcrEngineTimesOut_ShouldHandle()
    {
        var engine = new SlowMockOcrEngine(timeoutMs: 100);
        var service = new OcrService(engine, timeoutMs: 50); // 50ms 超时

        var mockImage = new MockCapturedImage { Width = 100, Height = 100 };
        var result = await service.RecognizeAsync(mockImage);

        Assert.False(result.Success);
    }

    #endregion

    #region ConfigurationService 异常场景

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P1")]
    public void ConfigurationService_WithCorruptedJson_ShouldHandle()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid()}.json");
        try
        {
            // 写入损坏的 JSON
            File.WriteAllText(tempFile, "{ \"key\": \"value\"");
            
            var service = new ConfigurationService(configPath: tempFile);
            
            // 应该能创建服务，使用默认值
            Assert.NotNull(service);
            
            // 读取损坏的配置应返回默认值
            var value = service.Get("any.key", "default");
            Assert.Equal("default", value);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P1")]
    public void ConfigurationService_WithPermissionError_ShouldHandle()
    {
        // 使用系统路径（通常无写入权限）
        var service = new ConfigurationService(configPath: "/etc/screen2md_test_config.json");
        
        // 尝试写入（应该会失败但不崩溃）
        service.Set("test", "value");
        
        // 能运行到这里说明异常被处理了
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P1")]
    public void ConfigurationService_ConcurrentReadWrite_ShouldBeThreadSafe()
    {
        var service = new ConfigurationService();
        var tasks = new List<Task>();

        // 并发写入
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => service.Set($"key{index}", $"value{index}")));
        }

        // 并发读取
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => service.Get("any", "default")));
        }

        // 不应抛出异常
        Task.WaitAll(tasks.ToArray());
        Assert.True(true);
    }

    #endregion

    #region StorageService 异常场景

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P1")]
    public async Task CleanupAsync_WhenFileLocked_ShouldContinue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"locked_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // 创建一个被锁定的文件
            var lockedFile = Path.Combine(tempDir, "locked.txt");
            File.WriteAllText(lockedFile, "content");
            
            using (var stream = File.Open(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var service = new StorageService(baseDirectory: tempDir);
                
                // 应该继续清理其他文件，不因一个锁定文件而失败
                var result = await service.CleanupAsync();
                
                Assert.NotNull(result);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Exception")]
    [Trait("Priority", "P1")]
    public void GetStats_WithPathTooLong_ShouldHandle()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"longpath_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // 创建超长路径
            var longPath = Path.Combine(tempDir, new string('a', 200));
            Directory.CreateDirectory(longPath);
            File.WriteAllText(Path.Combine(longPath, "file.txt"), "content");

            var service = new StorageService(baseDirectory: tempDir);
            
            // 不应抛出异常
            var stats = service.GetStats();
            Assert.NotNull(stats);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    #region Mock 辅助类

    private class DisconnectingMockCaptureEngine : IScreenCaptureEngine
    {
        public void Dispose() { }
        public Task<ICapturedImage> CaptureDisplayAsync(int displayIndex, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Display disconnected");
        }

        public Task<ICapturedImage> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Display disconnected");
        }

        public Task<IReadOnlyList<IDisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IDisplayInfo>>(new List<IDisplayInfo>());
        }

        public Task<Rectangle> GetVirtualScreenBoundsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Rectangle(0, 0, 0, 0));
        }
    }

    private class MemoryHungryMockCaptureEngine : IScreenCaptureEngine
    {
        public void Dispose() { }
        public Task<ICapturedImage> CaptureDisplayAsync(int displayIndex, CancellationToken cancellationToken = default)
        {
            // 模拟内存不足
            throw new OutOfMemoryException("Not enough memory to capture screen");
        }

        public Task<ICapturedImage> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default)
        {
            throw new OutOfMemoryException("Not enough memory to capture screen");
        }

        public Task<IReadOnlyList<IDisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IDisplayInfo>>(new List<IDisplayInfo> { new MockDisplayInfo() });
        }

        public Task<Rectangle> GetVirtualScreenBoundsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Rectangle(0, 0, 1920, 1080));
        }
    }

    private class FailingOcrEngine : IOcrEngine
    {
        public Task<OcrResult> RecognizeAsync(ICapturedImage image, OcrOptions options, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OcrResult
            {
                Success = false,
                ErrorMessage = "Image format not supported or corrupted"
            });
        }

        public Task<bool> IsAvailableAsync() => Task.FromResult(true);
        public Task<IReadOnlyList<string>> GetSupportedLanguagesAsync() => 
            Task.FromResult<IReadOnlyList<string>>(new[] { "eng" });
    }

    private class SlowMockOcrEngine : IOcrEngine
    {
        private readonly int _delayMs;
        public SlowMockOcrEngine(int timeoutMs) => _delayMs = timeoutMs;

        public async Task<OcrResult> RecognizeAsync(ICapturedImage image, OcrOptions options, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delayMs, cancellationToken);
            return new OcrResult { Success = true, Text = "test" };
        }

        public Task<bool> IsAvailableAsync() => Task.FromResult(true);
        public Task<IReadOnlyList<string>> GetSupportedLanguagesAsync() => 
            Task.FromResult<IReadOnlyList<string>>(new[] { "eng" });
    }

    private class MockCapturedImage : ICapturedImage
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] RawData { get; set; } = Array.Empty<byte>();
        public int DisplayIndex { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public string Format { get; set; } = "jpeg";

        public void Dispose() { }

        public SkiaSharp.SKBitmap ToSKBitmap()
        {
            return new SkiaSharp.SKBitmap(Width, Height);
        }

        public Task SaveAsync(string filePath, ImageFormat format = ImageFormat.Png, int quality = 95)
        {
            return Task.CompletedTask;
        }

        public byte[] ToByteArray(ImageFormat format = ImageFormat.Png, int quality = 95)
        {
            return RawData;
        }
    }

    private class MockDisplayInfo : IDisplayInfo
    {
        public int Index { get; set; }
        public string DeviceName { get; set; } = "Display 1";
        public string Name => DeviceName;
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public bool IsPrimary { get; set; } = true;
        public float ScaleFactor { get; set; } = 1.0f;
        public float DpiScale { get; set; } = 1.0f;
    }

    #endregion
}
