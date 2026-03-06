using Screen2MD.Kernel.Interfaces;
using Screen2MD.Services.Interfaces;
using System.Diagnostics;
using System.Threading;

namespace Screen2MD.Services.Services;

/// <summary>
/// 采集调度器 - 自适应采样，智能降频
/// </summary>
public sealed class CaptureScheduler : ICaptureScheduler, IDisposable
{
    private readonly IResourceMonitor? _resourceMonitor;
    private readonly IKernelLogger? _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<DateTimeOffset> _scheduleHistory = new();
    private readonly object _historyLock = new();
    
    private Task? _schedulerTask;
    private Func<CancellationToken, Task>? _captureCallback;
    private bool _isPaused;
    private int _consecutiveChanges;
    private DateTimeOffset _lastCaptureAt;
    private bool _disposed;

    public string Name => nameof(CaptureScheduler);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;
    public SchedulerConfig Config { get; set; } = new();
    
    public SchedulerStatus Status { get; private set; } = new();

    public CaptureScheduler(IResourceMonitor? resourceMonitor = null, IKernelLogger? logger = null)
    {
        _resourceMonitor = resourceMonitor;
        _logger = logger;
        Config = new SchedulerConfig();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        _logger?.LogInformation("Capture scheduler initialized");
        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_schedulerTask != null)
        {
            _logger?.LogWarning("Scheduler already running");
            return;
        }

        if (_captureCallback == null)
        {
            throw new InvalidOperationException("Capture callback not set");
        }

        _logger?.LogInformation($"Starting scheduler with interval {Config.MinIntervalMs}ms");
        
        _schedulerTask = SchedulerLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts.Cancel();
        
        if (_schedulerTask != null)
        {
            try
            {
                await _schedulerTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        _schedulerTask = null;
        _logger?.LogInformation("Scheduler stopped");
    }

    private async Task SchedulerLoopAsync(CancellationToken cancellationToken)
    {
        var currentInterval = Config.MinIntervalMs;
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_isPaused)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                // 检查资源使用情况，动态调整间隔
                if (Config.EnableAdaptiveSampling && _resourceMonitor != null)
                {
                    var cpu = _resourceMonitor.CpuUsagePercent;
                    var memory = _resourceMonitor.MemoryUsageMB;

                    // CPU或内存过高时增加间隔（降频）
                    if (cpu > Config.CpuThreshold || memory > Config.MemoryThresholdMB)
                    {
                        currentInterval = Math.Min(currentInterval * 2, Config.MaxIntervalMs);
                        _logger?.LogDebug($"Throttling: CPU={cpu:F1}%, Mem={memory:F1}MB, Interval={currentInterval}ms");
                    }
                    else if (_consecutiveChanges >= Config.AdaptiveThreshold)
                    {
                        // 频繁变化时缩短间隔
                        currentInterval = Math.Max(currentInterval / 2, Config.MinIntervalMs);
                    }
                }

                Status = new SchedulerStatus
                {
                    IsRunning = true,
                    CurrentIntervalMs = currentInterval,
                    LastCaptureAt = _lastCaptureAt,
                    AverageCpuPercent = _resourceMonitor?.CpuUsagePercent ?? 0,
                    AverageMemoryMB = _resourceMonitor?.MemoryUsageMB ?? 0
                };

                // 等待下一次采集
                await Task.Delay(currentInterval, cancellationToken);

                if (!_isPaused && _captureCallback != null)
                {
                    try
                    {
                        await _captureCallback(cancellationToken);
                        
                        lock (_historyLock)
                        {
                            _scheduleHistory.Add(DateTimeOffset.UtcNow);
                            // 保留最近1000条
                            if (_scheduleHistory.Count > 1000)
                            {
                                _scheduleHistory.RemoveAt(0);
                            }
                        }
                        
                        _lastCaptureAt = DateTimeOffset.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Capture failed: {ex.Message}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Scheduler loop error: {ex.Message}");
            HealthStatus = HealthStatus.Unhealthy;
        }
    }

    public Task TriggerCaptureAsync(CancellationToken cancellationToken = default)
    {
        if (_captureCallback == null)
        {
            throw new InvalidOperationException("Capture callback not set");
        }

        return _captureCallback(cancellationToken);
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        _isPaused = true;
        _logger?.LogInformation("Scheduler paused");
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        _isPaused = false;
        _logger?.LogInformation("Scheduler resumed");
        return Task.CompletedTask;
    }

    public Task UpdateConfigAsync(SchedulerConfig config, CancellationToken cancellationToken = default)
    {
        Config = config;
        _logger?.LogInformation($"Config updated: MinInterval={config.MinIntervalMs}ms, MaxInterval={config.MaxIntervalMs}ms");
        return Task.CompletedTask;
    }

    public void SetCaptureCallback(Func<CancellationToken, Task> callback)
    {
        _captureCallback = callback;
    }

    public DateTimeOffset? GetNextScheduledTime()
    {
        if (_schedulerTask == null || _isPaused)
            return null;

        return DateTimeOffset.UtcNow.AddMilliseconds(Config.MinIntervalMs);
    }

    public IReadOnlyList<DateTimeOffset> GetScheduleHistory(int count)
    {
        lock (_historyLock)
        {
            return _scheduleHistory.TakeLast(count).ToList();
        }
    }

    // 通知检测到变化（用于自适应采样）
    public void NotifyChangeDetected()
    {
        Interlocked.Increment(ref _consecutiveChanges);
    }

    // 通知无变化（重置计数器）
    public void NotifyNoChange()
    {
        _consecutiveChanges = 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _logger?.LogInformation("Scheduler disposed");
        }
    }
}
