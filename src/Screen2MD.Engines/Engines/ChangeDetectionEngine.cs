using Screen2MD.Engines.Interfaces;
using Screen2MD.Kernel.Interfaces;
using System.Diagnostics;

namespace Screen2MD.Engines.Engines;

/// <summary>
/// 变化检测引擎 - 支持多种算法，目标 <100ms
/// </summary>
public sealed class ChangeDetectionEngine : IChangeDetectionEngine, IDisposable
{
    private byte[]? _referenceFrame;
    private readonly object _lock = new();
    private bool _disposed;

    public string Name => nameof(ChangeDetectionEngine);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;
    public ChangeDetectionAlgorithm CurrentAlgorithm { get; private set; } = ChangeDetectionAlgorithm.PerceptualHash;
    public int Sensitivity { get; set; } = 50; // 默认50%

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

    public async Task<ChangeDetectionResult> DetectAsync(byte[] currentScreen, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        lock (_lock)
        {
            if (_referenceFrame == null)
            {
                _referenceFrame = currentScreen;
                return new ChangeDetectionResult
                {
                    HasChanged = true,
                    ChangeScore = 100,
                    Timestamp = DateTimeOffset.UtcNow,
                    DetectionTime = sw.Elapsed
                };
            }

            // 快速大小检查
            if (currentScreen.Length != _referenceFrame.Length)
            {
                return new ChangeDetectionResult
                {
                    HasChanged = true,
                    ChangeScore = 100,
                    Timestamp = DateTimeOffset.UtcNow,
                    DetectionTime = sw.Elapsed
                };
            }

            var score = CurrentAlgorithm switch
            {
                ChangeDetectionAlgorithm.PixelDiff => CalculatePixelDiff(currentScreen),
                ChangeDetectionAlgorithm.PerceptualHash => CalculatePerceptualHash(currentScreen),
                ChangeDetectionAlgorithm.FeatureMatch => CalculateFeatureMatch(currentScreen),
                _ => CalculatePerceptualHash(currentScreen)
            };

            var threshold = 100 - Sensitivity; // 敏感度转换为阈值
            var hasChanged = score > threshold;

            sw.Stop();

            return new ChangeDetectionResult
            {
                HasChanged = hasChanged,
                ChangeScore = score,
                Timestamp = DateTimeOffset.UtcNow,
                DetectionTime = sw.Elapsed
            };
        }
    }

    public Task UpdateReferenceAsync(byte[] screen, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _referenceFrame = screen.ToArray(); // 复制避免引用问题
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 像素级差分 - 最快，适合实时监控
    /// </summary>
    private double CalculatePixelDiff(byte[] current)
    {
        if (_referenceFrame == null || current.Length != _referenceFrame.Length)
            return 100;

        // 采样检查（每100个像素检查1个，提升速度）
        long diffSum = 0;
        var sampleStep = 100;
        var samples = 0;

        for (int i = 0; i < current.Length; i += sampleStep)
        {
            diffSum += Math.Abs(current[i] - _referenceFrame[i]);
            samples++;
        }

        var avgDiff = diffSum / (double)samples;
        return Math.Min(avgDiff / 2.55, 100); // 归一化到0-100
    }

    /// <summary>
    /// 感知哈希 - 平衡速度和准确度
    /// </summary>
    private double CalculatePerceptualHash(byte[] current)
    {
        // 简化版：计算整体亮度变化的哈希
        var refHash = CalculateSimpleHash(_referenceFrame!);
        var currHash = CalculateSimpleHash(current);
        
        // 汉明距离
        var diff = 0;
        for (int i = 0; i < refHash.Length; i++)
        {
            if (refHash[i] != currHash[i])
                diff++;
        }

        return (diff / (double)refHash.Length) * 100;
    }

    private byte[] CalculateSimpleHash(byte[] data)
    {
        // 分块平均哈希
        const int blocks = 16;
        var blockSize = data.Length / blocks;
        var hash = new byte[blocks];

        for (int i = 0; i < blocks; i++)
        {
            long sum = 0;
            var start = i * blockSize;
            var end = Math.Min(start + blockSize, data.Length);
            
            for (int j = start; j < end; j++)
            {
                sum += data[j];
            }
            
            hash[i] = (byte)(sum / (end - start));
        }

        return hash;
    }

    /// <summary>
    /// 特征匹配 - 最准，但较慢
    /// </summary>
    private double CalculateFeatureMatch(byte[] current)
    {
        // Phase 1 简化实现，使用感知哈希代替
        return CalculatePerceptualHash(current);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _referenceFrame = null;
            _disposed = true;
        }
    }
}
