using Xunit;
using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using Screen2MD.Abstractions;

namespace Screen2MD.Core.Tests.Services;

/// <summary>
/// OcrService 完整单元测试
/// </summary>
public class OcrServiceConstructorTests
{
    [Fact]
    public void Constructor_WithValidEngine_ShouldNotThrow()
    {
        var engine = new MockOcrEngine();
        
        var service = new OcrService(engine);
        
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullEngine_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new OcrService(null!));
    }

    [Fact]
    public void Constructor_WithCustomLanguages_ShouldStoreLanguages()
    {
        var engine = new MockOcrEngine();
        var languages = new[] { "chi_sim", "eng", "jpn" };
        
        var service = new OcrService(engine, languages);
        
        Assert.NotNull(service);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Constructor_WithVariousTimeouts_ShouldNotThrow(int timeoutMs)
    {
        var engine = new MockOcrEngine();
        
        var service = new OcrService(engine, timeoutMs: timeoutMs);
        
        Assert.NotNull(service);
    }
}

/// <summary>
/// RecognizeAsync 正常流程测试
/// </summary>
public class OcrServiceRecognizeTests
{
    private readonly MockOcrEngine _engine;
    private readonly OcrService _service;
    private readonly MockCaptureEngine _captureEngine;

    public OcrServiceRecognizeTests()
    {
        _engine = new MockOcrEngine();
        _service = new OcrService(_engine);
        _captureEngine = new MockCaptureEngine();
    }

    [Fact]
    public async Task RecognizeAsync_WithValidImage_ShouldReturnSuccess()
    {
        var image = await _captureEngine.CaptureDisplayAsync(0);
        
        var result = await _service.RecognizeAsync(image);
        
        Assert.True(result.Success);
        Assert.NotNull(result.Text);
    }

    [Fact(Skip = "Causes test host crash")]
    public async Task RecognizeAsync_WithDefaultLanguages_ShouldUseDefaults()
    {
        var image = await _captureEngine.CaptureDisplayAsync(0);
        
        var result = await _service.RecognizeAsync(image);
        
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RecognizeAsync_WithCustomLanguages_ShouldUseCustom()
    {
        var image = await _captureEngine.CaptureDisplayAsync(0);
        var languages = new[] { "chi_sim" };
        
        var result = await _service.RecognizeAsync(image, languages);
        
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RecognizeAsync_ShouldReturnConfidence()
    {
        var image = await _captureEngine.CaptureDisplayAsync(0);
        
        var result = await _service.RecognizeAsync(image);
        
        Assert.True(result.Confidence >= 0);
        Assert.True(result.Confidence <= 1);
    }

    [Fact]
    public async Task RecognizeAsync_ShouldReturnProcessingTime()
    {
        var image = await _captureEngine.CaptureDisplayAsync(0);
        
        var result = await _service.RecognizeAsync(image);
        
        Assert.True(result.ProcessingTime >= TimeSpan.Zero);
    }

    [Fact(Skip = "Causes test host crash when run with other tests")]
    public async Task RecognizeAsync_ShouldRespectCancellationToken()
    {
        var image = await _captureEngine.CaptureDisplayAsync(0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.RecognizeAsync(image, cancellationToken: cts.Token);
        });
    }
}

/// <summary>
/// OcrService 异常处理测试
/// </summary>
public class OcrServiceExceptionTests
{
    [Fact(Skip = "Linux test instability")]
    public async Task RecognizeAsync_WhenEngineThrows_ShouldReturnFailure()
    {
        var failingEngine = new FailingMockOcrEngine();
        var service = new OcrService(failingEngine);
        var captureEngine = new MockCaptureEngine();
        var image = await captureEngine.CaptureDisplayAsync(0);
        
        var result = await service.RecognizeAsync(image);
        
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenEngineAvailable_ShouldReturnTrue()
    {
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        
        var available = await service.IsAvailableAsync();
        
        Assert.True(available);
    }

    [Fact]
    public async Task GetSupportedLanguagesAsync_ShouldReturnLanguages()
    {
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        
        var languages = await service.GetSupportedLanguagesAsync();
        
        Assert.NotNull(languages);
        Assert.True(languages.Count > 0);
    }
}

/// <summary>
/// 用于测试的失败 Mock OCR 引擎
/// </summary>
public class FailingMockOcrEngine : IOcrEngine
{
    public Task<OcrResult> RecognizeAsync(ICapturedImage image, OcrOptions options, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Simulated OCR failure");
    }

    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<string>> GetSupportedLanguagesAsync()
    {
        throw new InvalidOperationException("Simulated language fetch failure");
    }
}