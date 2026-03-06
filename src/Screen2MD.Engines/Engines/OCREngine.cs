using Screen2MD.Engines.Interfaces;
using Screen2MD.Kernel.Interfaces;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Screen2MD.Engines.Engines;

/// <summary>
/// OCR引擎 - 文字识别
/// 注意：Linux环境下使用模拟实现，实际功能使用PaddleOCR
/// </summary>
public sealed class OCREngine : IOCREngine, IDisposable
{
    private bool _disposed;
    private readonly List<string> _supportedLanguages = new() { "en", "zh", "ja", "ko" };

    public string Name => nameof(OCREngine);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;
    public IReadOnlyList<string> SupportedLanguages => _supportedLanguages;

    // 代码检测模式
    private static readonly Regex[] CodePatterns = new[]
    {
        new Regex(@"\b(public|private|protected|class|interface|struct|enum|void|int|string|bool)\b"),
        new Regex(@"[{;}]\s*$", RegexOptions.Multiline),
        new Regex(@"\b(function|var|let|const|if|else|for|while|return)\b"),
        new Regex(@"#include|#define|import|from|using\s+\w+"),
        new Regex(@"^\s*[\{\}\[\]\(\);\/\\]"),
    };

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

    public Task<OCRResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 模拟OCR结果（实际应调用PaddleOCR）
        var mockText = @"using System;

namespace Screen2MD
{
    public class Example
    {
        public void Main()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";

        var blocks = new List<TextBlock>
        {
            new()
            {
                Text = "using System;",
                BoundingBox = new Rectangle(10, 10, 200, 30),
                Confidence = 0.98f,
                Language = "en"
            },
            new()
            {
                Text = "public class Example",
                BoundingBox = new Rectangle(10, 50, 250, 30),
                Confidence = 0.95f,
                Language = "en"
            },
            new()
            {
                Text = "Console.WriteLine",
                BoundingBox = new Rectangle(30, 150, 200, 30),
                Confidence = 0.97f,
                Language = "en"
            }
        };

        sw.Stop();

        var result = new OCRResult
        {
            FullText = mockText,
            TextBlocks = blocks,
            AverageConfidence = blocks.Average(b => b.Confidence),
            ProcessingTime = sw.Elapsed,
            IsStructuredContent = IsCodeContent(mockText)
        };

        return Task.FromResult(result);
    }

    public Task<OCRResult> RecognizeRegionAsync(byte[] imageData, Rectangle region, CancellationToken cancellationToken = default)
    {
        // 简化实现：裁剪区域后识别（实际应裁剪图像）
        return RecognizeAsync(imageData, cancellationToken);
    }

    public bool IsCodeContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var codeIndicators = 0;
        foreach (var pattern in CodePatterns)
        {
            if (pattern.IsMatch(text))
            {
                codeIndicators++;
            }
        }

        // 单行代码只需要1个特征，多行代码需要至少2个
        var threshold = text.Split('\n').Length > 1 ? 2 : 1;
        return codeIndicators >= threshold;
    }

    public Task<List<TextBlock>> ExtractCodeBlocksAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        // 模拟从图像中提取代码块
        var codeBlocks = new List<TextBlock>
        {
            new()
            {
                Text = "public void Main() { }",
                BoundingBox = new Rectangle(50, 100, 400, 200),
                Confidence = 0.96f,
                Language = "en"
            },
            new()
            {
                Text = "var x = 10;",
                BoundingBox = new Rectangle(50, 320, 200, 30),
                Confidence = 0.94f,
                Language = "en"
            }
        };

        return Task.FromResult(codeBlocks);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
