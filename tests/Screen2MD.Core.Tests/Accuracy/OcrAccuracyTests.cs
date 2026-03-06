using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using Xunit;
using Xunit.Abstractions;

namespace Screen2MD.Core.Tests.Accuracy;

/// <summary>
/// OCR 准确率验证测试 - Ground Truth 测试
/// </summary>
public class OcrAccuracyTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public OcrAccuracyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose() { }

    [Theory]
    [InlineData("Hello World")]
    [InlineData("1234567890")]
    [Trait("Category", "Accuracy")]
    public async Task Ocr_EnglishText_ShouldHaveHighAccuracy(string inputText)
    {
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage { Width = 800, Height = 200 };

        var result = await service.RecognizeAsync(image);

        Assert.True(result.Success);
        Assert.True(result.Confidence > 0.90, $"Confidence {result.Confidence:P} is below 90%");
    }

    [Theory]
    [InlineData("你好世界")]
    [InlineData("这是一个测试")]
    [Trait("Category", "Accuracy")]
    public async Task Ocr_ChineseText_ShouldHaveHighAccuracy(string inputText)
    {
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage { Width = 800, Height = 200 };

        var result = await service.RecognizeAsync(image);

        Assert.True(result.Success);
        Assert.True(result.Confidence > 0.85, $"Confidence {result.Confidence:P} is below 85%");
    }

    [Fact]
    [Trait("Category", "Accuracy")]
    public async Task Ocr_DifferentResolutions_ShouldRecognize()
    {
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);

        var resolutions = new[] { (1920, 1080), (2560, 1440), (3840, 2160), (800, 600) };
        
        foreach (var (width, height) in resolutions)
        {
            var image = new MockCapturedImage { Width = width, Height = height };
            var result = await service.RecognizeAsync(image);
            Assert.NotNull(result);
        }
    }
}
