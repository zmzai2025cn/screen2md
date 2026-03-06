using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Engines.Interfaces;

/// <summary>
/// OCR文本块
/// </summary>
public record TextBlock
{
    public string Text { get; init; } = string.Empty;
    public Rectangle BoundingBox { get; init; }
    public float Confidence { get; init; }
    public string Language { get; init; } = "unknown";
}

/// <summary>
/// OCR识别结果
/// </summary>
public record OCRResult
{
    public string FullText { get; init; } = string.Empty;
    public List<TextBlock> TextBlocks { get; init; } = new();
    public float AverageConfidence { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public bool IsStructuredContent { get; init; } // 是否为代码等结构化内容
}

/// <summary>
/// OCR引擎接口 - 文字识别
/// </summary>
public interface IOCREngine : IKernelComponent
{
    /// <summary>
    /// 识别图像中的文字
    /// </summary>
    Task<OCRResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 识别指定区域
    /// </summary>
    Task<OCRResult> RecognizeRegionAsync(byte[] imageData, Rectangle region, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检测是否为代码内容
    /// </summary>
    bool IsCodeContent(string text);
    
    /// <summary>
    /// 提取代码块（从混合内容中）
    /// </summary>
    Task<List<TextBlock>> ExtractCodeBlocksAsync(byte[] imageData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取支持的语言列表
    /// </summary>
    IReadOnlyList<string> SupportedLanguages { get; }
}
