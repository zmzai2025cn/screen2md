using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Engines.Interfaces;

/// <summary>
/// 图像特征
/// </summary>
public record ImageFeatures
{
    public byte[]? FeatureVector { get; init; }
    public List<KeyPoint> KeyPoints { get; init; } = new();
    public Rectangle[] Regions { get; init; } = Array.Empty<Rectangle>();
    public TimeSpan ExtractionTime { get; init; }
}

/// <summary>
/// 关键点
/// </summary>
public record KeyPoint(int X, int Y, float Size, float Angle);

/// <summary>
/// 图像分割结果
/// </summary>
public record SegmentationResult
{
    public Rectangle[] Regions { get; init; } = Array.Empty<Rectangle>();
    public double[] ConfidenceScores { get; init; } = Array.Empty<double>();
    public TimeSpan ProcessingTime { get; init; }
}

/// <summary>
/// OpenCV引擎接口 - 计算机视觉处理
/// </summary>
public interface IOpenCVEngine : IKernelComponent
{
    /// <summary>
    /// 提取图像特征
    /// </summary>
    Task<ImageFeatures> ExtractFeaturesAsync(byte[] imageData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 图像分割（识别不同UI区域）
    /// </summary>
    Task<SegmentationResult> SegmentImageAsync(byte[] imageData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 匹配特征（对比两张图像）
    /// </summary>
    Task<double> MatchFeaturesAsync(byte[] image1, byte[] image2, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检测边缘
    /// </summary>
    Task<byte[]> DetectEdgesAsync(byte[] imageData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 识别UI组件类型（按钮、输入框等）
    /// </summary>
    Task<string[]> RecognizeUIComponentsAsync(byte[] imageData, CancellationToken cancellationToken = default);
}
