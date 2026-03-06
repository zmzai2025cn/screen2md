using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Engines.Interfaces;

/// <summary>
/// 变化检测结果
/// </summary>
public record ChangeDetectionResult
{
    public bool HasChanged { get; init; }
    public double ChangeScore { get; init; } // 0-100
    public Rectangle? ChangedRegion { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan DetectionTime { get; init; }
}

/// <summary>
/// 变化检测引擎接口
/// </summary>
public interface IChangeDetectionEngine : IKernelComponent
{
    /// <summary>
    /// 检测屏幕是否发生变化
    /// </summary>
    Task<ChangeDetectionResult> DetectAsync(byte[] currentScreen, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新参考帧（当确认需要采集后）
    /// </summary>
    Task UpdateReferenceAsync(byte[] screen, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取当前检测算法
    /// </summary>
    ChangeDetectionAlgorithm CurrentAlgorithm { get; }
    
    /// <summary>
    /// 设置敏感度 (0-100)
    /// </summary>
    int Sensitivity { get; set; }
}

/// <summary>
/// 变化检测算法类型
/// </summary>
public enum ChangeDetectionAlgorithm
{
    PixelDiff,      // 像素级差分（最快）
    PerceptualHash, // 感知哈希（平衡）
    FeatureMatch    // 特征匹配（最准）
}

/// <summary>
/// 屏幕区域
/// </summary>
public record Rectangle(int X, int Y, int Width, int Height);
